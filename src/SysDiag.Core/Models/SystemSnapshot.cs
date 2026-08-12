namespace SysDiag.Core.Models;

/// <summary>
/// The complete state of one machine at one point in time. This is the central
/// type of the application: collectors produce it, the repository stores it,
/// the diff engine compares two of them and the LLM layer describes it.
/// </summary>
public sealed record SystemSnapshot
{
    /// <summary>Id of a snapshot that has not been written to the database yet.</summary>
    public const long NotPersisted = 0;

    /// <summary>
    /// Database id, assigned by the repository on insert. A freshly collected
    /// snapshot carries <see cref="NotPersisted"/> until it is stored.
    /// </summary>
    public long Id { get; init; } = NotPersisted;

    /// <summary>Creation time in UTC, so snapshots stay comparable across time zones.</summary>
    public required DateTimeOffset CreatedAtUtc { get; init; }

    /// <summary>Host name of the scanned machine.</summary>
    public required string MachineName { get; init; }

    /// <summary>
    /// Name of the collector that produced this snapshot ("windows-wmi",
    /// "linux-procfs", "demo"). It documents where the data came from, which
    /// matters when a snapshot is read back months later.
    /// </summary>
    public required string CollectorName { get; init; }

    public required OsInfo Os { get; init; }

    public required CpuInfo Cpu { get; init; }

    public required MemoryInfo Memory { get; init; }

    public IReadOnlyList<DiskInfo> Disks { get; init; } = [];

    public IReadOnlyList<NetworkAdapterInfo> NetworkAdapters { get; init; } = [];

    /// <summary>True once the snapshot has an id from the database.</summary>
    public bool IsPersisted => Id != NotPersisted;
}
