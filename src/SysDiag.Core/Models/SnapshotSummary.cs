namespace SysDiag.Core.Models;

/// <summary>
/// One row of the snapshot list: enough information to identify a snapshot,
/// without loading its disks and network adapters.
/// </summary>
public sealed record SnapshotSummary
{
    public required long Id { get; init; }

    public required DateTimeOffset CreatedAtUtc { get; init; }

    public required string MachineName { get; init; }

    public required string CollectorName { get; init; }

    /// <summary>Product name of the operating system, shown as context in the list.</summary>
    public required string OsCaption { get; init; }
}
