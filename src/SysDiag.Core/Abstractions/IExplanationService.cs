using SysDiag.Core.Diff;
using SysDiag.Core.Models;

namespace SysDiag.Core.Abstractions;

/// <summary>
/// Turns a snapshot into plain-language text with the help of a local LLM.
/// </summary>
public interface IExplanationService
{
    /// <summary>
    /// Asks the model to describe the snapshot. An unreachable model is a normal
    /// outcome, not an error: the method returns an unsuccessful
    /// <see cref="ExplanationResult"/> instead of throwing, so the CLI can print
    /// a notice and continue.
    /// </summary>
    Task<ExplanationResult> ExplainAsync(SystemSnapshot snapshot, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asks the model to judge what changed between two snapshots: which entries
    /// are harmless and which deserve attention. Fails the same way as
    /// <see cref="ExplainAsync"/> - with a result, never with an exception.
    /// </summary>
    Task<ExplanationResult> ExplainDiffAsync(SnapshotDiff diff, CancellationToken cancellationToken = default);
}
