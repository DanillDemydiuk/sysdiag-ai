using System.Globalization;
using SysDiag.Core.Abstractions;
using SysDiag.Core.Diff;
using SysDiag.Core.Formatting;
using SysDiag.Core.Models;

namespace SysDiag.Storage;

/// <summary>
/// Compares two snapshots and produces a flat list of differences.
/// </summary>
/// <remarks>
/// Disks and network adapters are matched by their identifier, never by their
/// position in the list: a machine can report its volumes in a different order
/// on the next boot, and a position-based comparison would then invent changes
/// that never happened.
/// </remarks>
public sealed class SnapshotComparer : ISnapshotComparer
{
    public SnapshotDiff Compare(SystemSnapshot left, SystemSnapshot right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        var entries = new List<DiffEntry>();

        CompareOs(entries, left.Os, right.Os);
        CompareCpu(entries, left.Cpu, right.Cpu);
        CompareMemory(entries, left.Memory, right.Memory);
        CompareDisks(entries, left.Disks, right.Disks);
        CompareAdapters(entries, left.NetworkAdapters, right.NetworkAdapters);

        return new SnapshotDiff
        {
            LeftSnapshotId = left.Id,
            RightSnapshotId = right.Id,
            LeftCreatedAtUtc = left.CreatedAtUtc,
            RightCreatedAtUtc = right.CreatedAtUtc,
            Entries = entries,
        };
    }

    private static void CompareOs(List<DiffEntry> entries, OsInfo left, OsInfo right)
    {
        const string category = "Operating system";

        AddIfDifferent(entries, category, "Platform", left.Platform, right.Platform);
        AddIfDifferent(entries, category, "Name", left.Caption, right.Caption);
        AddIfDifferent(entries, category, "Version", left.Version, right.Version);
        AddIfDifferent(entries, category, "Architecture", left.Architecture, right.Architecture);
    }

    private static void CompareCpu(List<DiffEntry> entries, CpuInfo left, CpuInfo right)
    {
        const string category = "CPU";

        AddIfDifferent(entries, category, "Model", left.Name, right.Name);
        AddIfDifferent(entries, category, "Physical cores", FormatNumber(left.PhysicalCores), FormatNumber(right.PhysicalCores));
        AddIfDifferent(entries, category, "Logical cores", FormatNumber(left.LogicalCores), FormatNumber(right.LogicalCores));
        AddIfDifferent(entries, category, "Max clock", FormatClock(left.MaxClockMhz), FormatClock(right.MaxClockMhz));
        AddIfDifferent(entries, category, "Architecture", left.Architecture, right.Architecture);
    }

    private static void CompareMemory(List<DiffEntry> entries, MemoryInfo left, MemoryInfo right)
    {
        const string category = "Memory";

        AddIfDifferent(entries, category, "Total", ByteSize.Format(left.TotalBytes), ByteSize.Format(right.TotalBytes));

        // Available memory changes every second. It is reported, but flagged, so
        // that it never turns "nothing changed" into "something changed".
        AddIfDifferent(
            entries,
            category,
            "Available",
            ByteSize.Format(left.AvailableBytes),
            ByteSize.Format(right.AvailableBytes),
            isVolatile: true);
    }

    private static void CompareDisks(
        List<DiffEntry> entries,
        IReadOnlyList<DiskInfo> left,
        IReadOnlyList<DiskInfo> right)
    {
        Dictionary<string, DiskInfo> oldDisks = Index(left, disk => disk.Identifier);
        Dictionary<string, DiskInfo> newDisks = Index(right, disk => disk.Identifier);

        foreach (string identifier in AllKeys(oldDisks, newDisks))
        {
            string category = $"Disk {identifier}";
            bool existedBefore = oldDisks.TryGetValue(identifier, out DiskInfo? oldDisk);
            bool existsNow = newDisks.TryGetValue(identifier, out DiskInfo? newDisk);

            if (!existedBefore)
            {
                entries.Add(Added(category, "Volume", DescribeDisk(newDisk!)));
                continue;
            }

            if (!existsNow)
            {
                entries.Add(Removed(category, "Volume", DescribeDisk(oldDisk!)));
                continue;
            }

            AddIfDifferent(entries, category, "File system", oldDisk!.FileSystem, newDisk!.FileSystem);
            AddIfDifferent(entries, category, "Label", oldDisk.Label, newDisk.Label);
            AddIfDifferent(entries, category, "Capacity", ByteSize.Format(oldDisk.TotalBytes), ByteSize.Format(newDisk.TotalBytes));
            AddIfDifferent(
                entries,
                category,
                "Free space",
                ByteSize.Format(oldDisk.FreeBytes),
                ByteSize.Format(newDisk.FreeBytes),
                isVolatile: true);
        }
    }

    private static void CompareAdapters(
        List<DiffEntry> entries,
        IReadOnlyList<NetworkAdapterInfo> left,
        IReadOnlyList<NetworkAdapterInfo> right)
    {
        Dictionary<string, NetworkAdapterInfo> oldAdapters = Index(left, adapter => adapter.Name);
        Dictionary<string, NetworkAdapterInfo> newAdapters = Index(right, adapter => adapter.Name);

        foreach (string name in AllKeys(oldAdapters, newAdapters))
        {
            string category = $"Network {name}";
            bool existedBefore = oldAdapters.TryGetValue(name, out NetworkAdapterInfo? oldAdapter);
            bool existsNow = newAdapters.TryGetValue(name, out NetworkAdapterInfo? newAdapter);

            if (!existedBefore)
            {
                entries.Add(Added(category, "Adapter", DescribeAdapter(newAdapter!)));
                continue;
            }

            if (!existsNow)
            {
                entries.Add(Removed(category, "Adapter", DescribeAdapter(oldAdapter!)));
                continue;
            }

            AddIfDifferent(entries, category, "Status", FormatStatus(oldAdapter!.IsUp), FormatStatus(newAdapter!.IsUp));
            AddIfDifferent(entries, category, "MAC address", oldAdapter.MacAddress, newAdapter.MacAddress);
            AddIfDifferent(entries, category, "Link speed", FormatSpeed(oldAdapter.SpeedMbps), FormatSpeed(newAdapter.SpeedMbps));

            // IP addresses are compared as a sorted set: the order in which the OS
            // reports them carries no meaning.
            AddIfDifferent(
                entries,
                category,
                "IP addresses",
                FormatAddresses(oldAdapter.IpAddresses),
                FormatAddresses(newAdapter.IpAddresses));
        }
    }

    /// <summary>
    /// Builds a lookup keyed by identifier. Duplicate keys are possible on broken
    /// systems (two mounts with the same name), so the first entry wins instead of
    /// the whole comparison failing with an exception.
    /// </summary>
    private static Dictionary<string, T> Index<T>(IReadOnlyList<T> items, Func<T, string> keySelector) =>
        items
            .GroupBy(keySelector, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// All identifiers from both sides, in a stable alphabetical order, so the
    /// output of two runs on the same data is identical.
    /// </summary>
    private static IEnumerable<string> AllKeys<T>(Dictionary<string, T> left, Dictionary<string, T> right) =>
        left.Keys
            .Union(right.Keys, StringComparer.OrdinalIgnoreCase)
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase);

    private static void AddIfDifferent(
        List<DiffEntry> entries,
        string category,
        string property,
        string? oldValue,
        string? newValue,
        bool isVolatile = false)
    {
        if (string.Equals(oldValue, newValue, StringComparison.Ordinal))
        {
            return;
        }

        entries.Add(new DiffEntry
        {
            Category = category,
            Property = property,
            OldValue = oldValue ?? ByteSize.Unknown,
            NewValue = newValue ?? ByteSize.Unknown,
            Kind = ChangeKind.Modified,
            IsVolatile = isVolatile,
        });
    }

    private static DiffEntry Added(string category, string property, string value) => new()
    {
        Category = category,
        Property = property,
        OldValue = null,
        NewValue = value,
        Kind = ChangeKind.Added,
    };

    private static DiffEntry Removed(string category, string property, string value) => new()
    {
        Category = category,
        Property = property,
        OldValue = value,
        NewValue = null,
        Kind = ChangeKind.Removed,
    };

    private static string DescribeDisk(DiskInfo disk) =>
        $"{ByteSize.Format(disk.TotalBytes)} ({disk.FileSystem ?? ByteSize.Unknown})";

    private static string DescribeAdapter(NetworkAdapterInfo adapter) =>
        $"{adapter.Description ?? adapter.Name} ({FormatStatus(adapter.IsUp)})";

    private static string FormatStatus(bool isUp) => isUp ? "up" : "down";

    private static string FormatNumber(int? value) =>
        value?.ToString(CultureInfo.InvariantCulture) ?? ByteSize.Unknown;

    private static string FormatClock(int? megahertz) =>
        megahertz is null ? ByteSize.Unknown : $"{megahertz.Value.ToString(CultureInfo.InvariantCulture)} MHz";

    private static string FormatSpeed(long? megabitsPerSecond) =>
        megabitsPerSecond is null ? ByteSize.Unknown : $"{megabitsPerSecond.Value.ToString(CultureInfo.InvariantCulture)} Mbit/s";

    private static string FormatAddresses(IReadOnlyList<string> addresses) =>
        addresses.Count == 0
            ? ByteSize.Unknown
            : string.Join(", ", addresses.OrderBy(address => address, StringComparer.OrdinalIgnoreCase));
}
