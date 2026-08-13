using SysDiag.Core.Models;

namespace SysDiag.Tests;

/// <summary>
/// Builders for snapshots used across the test suite. Tests state only what they
/// care about and inherit the rest, which keeps each test readable and makes the
/// interesting difference obvious at a glance.
/// </summary>
internal static class TestData
{
    public const long Gib = 1024L * 1024 * 1024;

    public static readonly DateTimeOffset Morning = new(2026, 8, 1, 9, 0, 0, TimeSpan.Zero);
    public static readonly DateTimeOffset Evening = new(2026, 8, 1, 21, 0, 0, TimeSpan.Zero);

    public static SystemSnapshot Snapshot(long id = 1, DateTimeOffset? createdAt = null) => new()
    {
        Id = id,
        CreatedAtUtc = createdAt ?? Morning,
        MachineName = "TEST-PC",
        CollectorName = "demo",
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
            AvailableBytes = 18 * Gib,
        },
        Disks = [Disk("C:")],
        NetworkAdapters = [Adapter("Ethernet")],
    };

    public static DiskInfo Disk(string identifier, long totalBytes = 1024 * Gib, long? freeBytes = 412 * Gib) => new()
    {
        Identifier = identifier,
        Label = "System",
        FileSystem = "NTFS",
        TotalBytes = totalBytes,
        FreeBytes = freeBytes,
    };

    public static NetworkAdapterInfo Adapter(string name, bool isUp = true) => new()
    {
        Name = name,
        Description = "Test adapter",
        MacAddress = "00:1A:2B:3C:4D:5E",
        IpAddresses = ["192.168.1.42"],
        SpeedMbps = 1000,
        IsUp = isUp,
    };
}
