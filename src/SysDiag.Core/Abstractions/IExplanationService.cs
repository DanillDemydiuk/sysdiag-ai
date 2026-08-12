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
}
