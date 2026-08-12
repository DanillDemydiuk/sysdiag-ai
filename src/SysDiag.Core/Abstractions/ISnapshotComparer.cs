using SysDiag.Core.Diff;
using SysDiag.Core.Models;

namespace SysDiag.Core.Abstractions;

/// <summary>
/// Compares two snapshots field by field. Kept as an interface so the comparison
/// rules can be replaced or mocked without touching the CLI.
/// </summary>
public interface ISnapshotComparer
{
    /// <summary>
    /// Compares <paramref name="left"/> (the older snapshot) against
    /// <paramref name="right"/> (the newer one). The result is always a value,
    /// never an exception: two snapshots without differences produce an empty
    /// list of entries.
    /// </summary>
    SnapshotDiff Compare(SystemSnapshot left, SystemSnapshot right);
}
