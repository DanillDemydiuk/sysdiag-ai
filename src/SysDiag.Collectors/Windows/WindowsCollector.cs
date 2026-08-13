using System.Management;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using SysDiag.Collectors.Common;
using SysDiag.Core.Abstractions;
using SysDiag.Core.Models;

namespace SysDiag.Collectors.Windows;

/// <summary>
/// Reads the machine configuration through WMI (Windows Management
/// Instrumentation), the standard inventory interface of Windows.
/// </summary>
/// <remarks>
/// The <see cref="SupportedOSPlatformAttribute"/> makes the platform requirement
/// part of the type: the compiler now reports every call that is not guarded by
/// <see cref="OperatingSystem.IsWindows"/>, instead of the program failing at
/// runtime on a Linux machine.
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class WindowsCollector : ISystemCollector
{
    /// <summary>Value stored in <see cref="SystemSnapshot.CollectorName"/> for WMI snapshots.</summary>
    public const string CollectorName = "windows-wmi";

    /// <summary>Win32_LogicalDisk.DriveType 3 means "local disk"; network shares and CD drives are skipped.</summary>
    private const int LocalDiskDriveType = 3;

    private readonly TimeProvider _timeProvider;

    public WindowsCollector(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public string Name => CollectorName;

    public bool IsSupported => OperatingSystem.IsWindows();

    /// <summary>
    /// WMI queries are blocking calls into a COM service, so they run on a thread
    /// pool thread instead of freezing the console while the data is collected.
    /// </summary>
    public Task<SystemSnapshot> CollectAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() => Collect(cancellationToken), cancellationToken);

    private SystemSnapshot Collect(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return new SystemSnapshot
        {
            CreatedAtUtc = _timeProvider.GetUtcNow(),
            MachineName = Environment.MachineName,
            CollectorName = CollectorName,
            Os = ReadOs(),
            Cpu = ReadCpu(),
            Memory = ReadMemory(),
            Disks = ReadDisks(),
            NetworkAdapters = NetworkAdapterReader.Read(),
        };
    }

    private static OsInfo ReadOs()
    {
        ManagementBaseObject? os = QueryFirst("SELECT Caption, Version, OSArchitecture FROM Win32_OperatingSystem");

        return new OsInfo
        {
            Platform = "Windows",
            // Every value falls back to a BCL equivalent: a locked-down machine may
            // refuse the WMI query, and a partial snapshot beats no snapshot at all.
            Caption = ReadString(os, "Caption") ?? RuntimeInformation.OSDescription,
            Version = ReadString(os, "Version") ?? Environment.OSVersion.Version.ToString(),
            Architecture = ReadString(os, "OSArchitecture") ?? RuntimeInformation.OSArchitecture.ToString(),
        };
    }

    private static CpuInfo ReadCpu()
    {
        ManagementBaseObject? cpu = QueryFirst(
            "SELECT Name, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed FROM Win32_Processor");

        return new CpuInfo
        {
            Name = ReadString(cpu, "Name") ?? "Unknown processor",
            PhysicalCores = (int?)ReadUInt32(cpu, "NumberOfCores"),
            LogicalCores = (int?)ReadUInt32(cpu, "NumberOfLogicalProcessors") ?? Environment.ProcessorCount,
            MaxClockMhz = (int?)ReadUInt32(cpu, "MaxClockSpeed"),
            Architecture = RuntimeInformation.OSArchitecture.ToString(),
        };
    }

    private static MemoryInfo ReadMemory()
    {
        ManagementBaseObject? computerSystem = QueryFirst("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");
        ManagementBaseObject? os = QueryFirst("SELECT FreePhysicalMemory FROM Win32_OperatingSystem");

        // Win32_OperatingSystem reports free memory in kibibytes, not in bytes.
        ulong? freeKibibytes = ReadUInt64(os, "FreePhysicalMemory");

        return new MemoryInfo
        {
            TotalBytes = (long?)ReadUInt64(computerSystem, "TotalPhysicalMemory") ?? 0,
            AvailableBytes = freeKibibytes is null ? null : (long)(freeKibibytes.Value * 1024),
        };
    }

    private static IReadOnlyList<DiskInfo> ReadDisks()
    {
        var disks = new List<DiskInfo>();

        foreach (ManagementBaseObject volume in Query(
            $"SELECT DeviceID, VolumeName, FileSystem, Size, FreeSpace FROM Win32_LogicalDisk WHERE DriveType = {LocalDiskDriveType}"))
        {
            using (volume)
            {
                string? identifier = ReadString(volume, "DeviceID");
                if (identifier is null)
                {
                    continue;
                }

                disks.Add(new DiskInfo
                {
                    Identifier = identifier,
                    Label = ReadString(volume, "VolumeName"),
                    FileSystem = ReadString(volume, "FileSystem"),
                    TotalBytes = (long?)ReadUInt64(volume, "Size") ?? 0,
                    FreeBytes = (long?)ReadUInt64(volume, "FreeSpace"),
                });
            }
        }

        return disks.OrderBy(disk => disk.Identifier, StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// Runs a WQL query and returns all rows. A failing query yields an empty
    /// result: WMI can be disabled or damaged on a machine, and that must not
    /// take the whole scan down.
    /// </summary>
    private static List<ManagementBaseObject> Query(string wql)
    {
        var rows = new List<ManagementBaseObject>();

        try
        {
            using var searcher = new ManagementObjectSearcher(wql);
            using ManagementObjectCollection results = searcher.Get();

            foreach (ManagementBaseObject row in results)
            {
                rows.Add(row);
            }
        }
        catch (ManagementException)
        {
            // The WMI service refused the query (repository corrupt, class missing).
        }
        catch (UnauthorizedAccessException)
        {
            // The current user is not allowed to read this class.
        }
        catch (COMException)
        {
            // The WMI service is not running at all.
        }

        return rows;
    }

    private static ManagementBaseObject? QueryFirst(string wql)
    {
        List<ManagementBaseObject> rows = Query(wql);

        for (int index = 1; index < rows.Count; index++)
        {
            rows[index].Dispose();
        }

        return rows.Count > 0 ? rows[0] : null;
    }

    private static string? ReadString(ManagementBaseObject? row, string property)
    {
        object? value = ReadValue(row, property);
        string? text = value?.ToString()?.Trim();
        return string.IsNullOrEmpty(text) ? null : text;
    }

    private static uint? ReadUInt32(ManagementBaseObject? row, string property) =>
        ReadValue(row, property) as uint?;

    private static ulong? ReadUInt64(ManagementBaseObject? row, string property) =>
        ReadValue(row, property) as ulong?;

    /// <summary>
    /// Reads a property value. WMI throws when a class does not expose the
    /// requested property, which happens on older Windows versions.
    /// </summary>
    private static object? ReadValue(ManagementBaseObject? row, string property)
    {
        if (row is null)
        {
            return null;
        }

        try
        {
            return row[property];
        }
        catch (ManagementException)
        {
            return null;
        }
    }
}
