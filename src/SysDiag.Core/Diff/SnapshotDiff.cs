namespace SysDiag.Core.Diff;

/// <summary>
/// Result of comparing two snapshots: the entries plus enough metadata to label
/// the two sides in the output.
/// </summary>
public sealed record SnapshotDiff
{
    public required long LeftSnapshotId { get; init; }

    public required long RightSnapshotId { get; init; }

    public required DateTimeOffset LeftCreatedAtUtc { get; init; }

    public required DateTimeOffset RightCreatedAtUtc { get; init; }

    /// <summary>All differences, volatile ones included, in a stable order.</summary>
    public IReadOnlyList<DiffEntry> Entries { get; init; } = [];

    /// <summary>
    /// True if the hardware or software configuration really changed. Volatile
    /// entries are ignored here, so "nothing changed" stays truthful even though
    /// free memory is never identical between two scans.
    /// </summary>
    public bool HasRelevantChanges => Entries.Any(entry => !entry.IsVolatile);
}
