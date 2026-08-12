using SysDiag.Core.Abstractions;
using SysDiag.Core.Models;

namespace SysDiag.Collectors;

/// <summary>
/// Collector that returns a fixed, fictional machine instead of reading the real
/// hardware. It exists for three reasons: the application can be tried out
/// without Docker and without administrator rights, screenshots stay identical
/// on every machine, and CI has a collector that behaves the same on every agent.
/// </summary>
public sealed class DemoCollector : ISystemCollector
{
    /// <summary>Value stored in <see cref="SystemSnapshot.CollectorName"/> for demo snapshots.</summary>
    public const string CollectorName = "demo";

    private const long Mib = 1024L * 1024;
    private const long Gib = 1024L * 1024 * 1024;

    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// <paramref name="timeProvider"/> is injected so tests can pin the clock and
    /// get a byte-for-byte reproducible snapshot.
    /// </summary>
    public DemoCollector(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public string Name => CollectorName;

    /// <summary>Demo data needs no platform support, so this collector runs anywhere.</summary>
    public bool IsSupported => true;

    public Task<SystemSnapshot> CollectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(BuildSnapshot(_timeProvider.GetUtcNow()));
    }

    /// <summary>
    /// Builds the fictional snapshot. The hardware is always identical, while
    /// free memory and free disk space move with the clock: that way two demo
    /// scans differ exactly in the volatile values, which is what the diff engine
    /// is supposed to recognise and mark as irrelevant.
    /// </summary>
    private static SystemSnapshot BuildSnapshot(DateTimeOffset timestamp)
    {
        long jitter = timestamp.ToUnixTimeSeconds() % 16 * 64 * Mib;

        return new SystemSnapshot
        {
            CreatedAtUtc = timestamp,
            MachineName = "DEMO-WORKSTATION",
            CollectorName = CollectorName,
            Os = new OsInfo
            {
                Platform = "Windows",
                Caption = "Windows 11 Pro",
                Version = "10.0.26100",
                Architecture = "X64",
            },
            Cpu = new CpuInfo
            {
                Name = "AMD Ryzen 5 5600X 6-Core Processor",
                PhysicalCores = 6,
                LogicalCores = 12,
                MaxClockMhz = 4650,
                Architecture = "X64",
            },
            Memory = new MemoryInfo
            {
                TotalBytes = 32 * Gib,
                AvailableBytes = 18 * Gib - jitter,
            },
            Disks =
            [
                new DiskInfo
                {
                    Identifier = "C:",
                    Label = "System",
                    FileSystem = "NTFS",
                    TotalBytes = 1024 * Gib,
                    FreeBytes = 412 * Gib - jitter,
                },
                new DiskInfo
                {
                    Identifier = "D:",
                    Label = "Data",
                    FileSystem = "NTFS",
                    TotalBytes = 2048 * Gib,
                    FreeBytes = 1740 * Gib,
                },
            ],
            NetworkAdapters =
            [
                new NetworkAdapterInfo
                {
                    Name = "Ethernet",
                    Description = "Realtek PCIe GbE Family Controller",
                    MacAddress = "00:1A:2B:3C:4D:5E",
                    IpAddresses = ["192.168.1.42", "fe80::1c2d:3e4f:5a6b:7c8d"],
                    SpeedMbps = 1000,
                    IsUp = true,
                },
                new NetworkAdapterInfo
                {
                    Name = "Wi-Fi",
                    Description = "Intel(R) Wi-Fi 6 AX200 160MHz",
                    MacAddress = "00:1A:2B:3C:4D:5F",
                    IpAddresses = [],
                    SpeedMbps = null,
                    IsUp = false,
                },
            ],
        };
    }
}
