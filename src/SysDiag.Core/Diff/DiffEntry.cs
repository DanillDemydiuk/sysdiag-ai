namespace SysDiag.Core.Diff;

/// <summary>
/// One difference between two snapshots, already formatted for display.
/// Values are strings on purpose: the diff is consumed by the console output and
/// by the LLM prompt, and both need text, not numbers.
/// </summary>
public sealed record DiffEntry
{
    /// <summary>Section the change belongs to, for example "CPU", "Memory" or "Disk C:".</summary>
    public required string Category { get; init; }

    /// <summary>Name of the changed property, for example "Total" or "MAC address".</summary>
    public required string Property { get; init; }

    /// <summary>Value in the older snapshot, or <c>null</c> when the item was added.</summary>
    public string? OldValue { get; init; }

    /// <summary>Value in the newer snapshot, or <c>null</c> when the item was removed.</summary>
    public string? NewValue { get; init; }

    public required ChangeKind Kind { get; init; }

    /// <summary>
    /// True for values that change on their own between two scans, such as free
    /// memory or free disk space. They are still reported, but they must not make
    /// two otherwise identical snapshots look different.
    /// </summary>
    public bool IsVolatile { get; init; }
}
