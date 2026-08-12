namespace SysDiag.Core.Diff;

/// <summary>
/// How a single piece of the configuration changed between two snapshots.
/// </summary>
public enum ChangeKind
{
    /// <summary>Present in the newer snapshot only, for example a disk that was plugged in.</summary>
    Added,

    /// <summary>Present in the older snapshot only, for example an adapter that was disabled.</summary>
    Removed,

    /// <summary>Present in both snapshots, but with a different value.</summary>
    Modified,
}
