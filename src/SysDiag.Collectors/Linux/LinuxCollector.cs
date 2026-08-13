using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using SysDiag.Collectors.Common;
using SysDiag.Core.Abstractions;
using SysDiag.Core.Models;

namespace SysDiag.Collectors.Linux;

/// <summary>
/// Reads the machine configuration from the kernel's virtual file systems.
/// Linux exposes hardware information as plain text files, so no extra library
/// is needed here: reading a file and parsing it is the whole mechanism.
/// </summary>
[SupportedOSPlatform("linux")]
public sealed class LinuxCollector : ISystemCollector
{
    /// <summary>Value stored in <see cref="SystemSnapshot.CollectorName"/> for procfs snapshots.</summary>
    public const string CollectorName = "linux-procfs";

    private const string CpuInfoPath = "/proc/cpuinfo";
    private const string MemInfoPath = "/proc/meminfo";
    private const string OsReleasePath = "/etc/os-release";
    private const string KernelReleasePath = "/proc/sys/kernel/osrelease";
    private const string MaxClockPath = "/sys/devices/system/cpu/cpu0/cpufreq/cpuinfo_max_freq";

    /// <summary>
    /// Virtual file systems that appear in the mount table but describe no real
    /// storage. Listing them as disks would be noise in every diff.
    /// </summary>
    private static readonly HashSet<string> PseudoFileSystems = new(StringComparer.OrdinalIgnoreCase)
    {
        "tmpfs", "devtmpfs", "ramfs", "squashfs", "overlay", "proc", "sysfs", "devpts", "cgroup2fs", "fuse",
    };

    private readonly TimeProvider _timeProvider;

    public LinuxCollector(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public string Name => CollectorName;

    public bool IsSupported => OperatingSystem.IsLinux();

    public Task<SystemSnapshot> CollectAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() => Collect(cancellationToken), cancellationToken);

    private SystemSnapshot Collect(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string cpuInfo = TryReadFile(CpuInfoPath) ?? string.Empty;
        string memInfo = TryReadFile(MemInfoPath) ?? string.Empty;
        string osRelease = TryReadFile(OsReleasePath) ?? string.Empty;

        return new SystemSnapshot
        {
            CreatedAtUtc = _timeProvider.GetUtcNow(),
            MachineName = Environment.MachineName,
            CollectorName = CollectorName,
            Os = BuildOs(osRelease),
            Cpu = BuildCpu(cpuInfo),
            Memory = BuildMemory(memInfo),
            Disks = ReadDisks(),
            NetworkAdapters = NetworkAdapterReader.Read(),
        };
    }

    private static OsInfo BuildOs(string osRelease)
    {
        IReadOnlyDictionary<string, string> values = ProcParser.ParseOsRelease(osRelease);

        // The kernel release ("6.8.0-45-generic") is the more useful version here:
        // it changes on every kernel update, which is exactly what a diff should show.
        string? kernelRelease = TryReadFile(KernelReleasePath)?.Trim();

        return new OsInfo
        {
            Platform = "Linux",
            Caption = values.GetValueOrDefault("PRETTY_NAME")
                ?? values.GetValueOrDefault("NAME")
                ?? RuntimeInformation.OSDescription,
            Version = string.IsNullOrEmpty(kernelRelease)
                ? Environment.OSVersion.Version.ToString()
                : kernelRelease,
            Architecture = RuntimeInformation.OSArchitecture.ToString(),
        };
    }

    private static CpuInfo BuildCpu(string cpuInfo)
    {
        int logicalCores = ProcParser.ParseLogicalCoreCount(cpuInfo);
        string? maxClock = TryReadFile(MaxClockPath);

        return new CpuInfo
        {
            Name = ProcParser.ParseCpuModelName(cpuInfo) ?? "Unknown processor",
            PhysicalCores = ProcParser.ParsePhysicalCoreCount(cpuInfo),
            LogicalCores = logicalCores > 0 ? logicalCores : Environment.ProcessorCount,
            MaxClockMhz = maxClock is null ? null : ProcParser.ParseMaxClockMhz(maxClock),
            Architecture = RuntimeInformation.OSArchitecture.ToString(),
        };
    }

    private static MemoryInfo BuildMemory(string memInfo) => new()
    {
        TotalBytes = ProcParser.ParseMemoryBytes(memInfo, "MemTotal") ?? 0,
        // MemAvailable is the kernel's own estimate of what a new process could
        // get; it is more honest than MemFree, which ignores reclaimable caches.
        AvailableBytes = ProcParser.ParseMemoryBytes(memInfo, "MemAvailable"),
    };

    private static IReadOnlyList<DiskInfo> ReadDisks()
    {
        var disks = new List<DiskInfo>();

        foreach (DriveInfo drive in DriveInfo.GetDrives())
        {
            try
            {
                if (!drive.IsReady || PseudoFileSystems.Contains(drive.DriveFormat) || drive.TotalSize <= 0)
                {
                    continue;
                }

                disks.Add(new DiskInfo
                {
                    // On Linux the mount point is the stable identifier: "/", "/home".
                    Identifier = drive.Name,
                    // DriveInfo.VolumeLabel is a Windows-only API, so Linux snapshots
                    // carry no label. The model allows that.
                    Label = null,
                    FileSystem = drive.DriveFormat,
                    TotalBytes = drive.TotalSize,
                    FreeBytes = drive.AvailableFreeSpace,
                });
            }
            catch (IOException)
            {
                // A mount can disappear between enumeration and query.
            }
            catch (UnauthorizedAccessException)
            {
                // Mount points the current user may not inspect.
            }
        }

        return disks.OrderBy(disk => disk.Identifier, StringComparer.Ordinal).ToList();
    }

    private static string? TryReadFile(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
}
