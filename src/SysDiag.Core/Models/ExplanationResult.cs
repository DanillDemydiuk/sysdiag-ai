namespace SysDiag.Core.Models;

/// <summary>
/// Outcome of an explanation request. The LLM lives in a container that may not
/// be running, so "no answer" has to be a value the caller can inspect rather
/// than an exception the caller has to catch.
/// </summary>
public sealed record ExplanationResult
{
    private ExplanationResult()
    {
    }

    /// <summary>True if the model returned text.</summary>
    public bool IsSuccess { get; private init; }

    /// <summary>The explanation, or <c>null</c> if the request failed.</summary>
    public string? Text { get; private init; }

    /// <summary>
    /// Human-readable reason why no explanation is available, or <c>null</c> on
    /// success. Written for the end user, not for a log file.
    /// </summary>
    public string? FailureReason { get; private init; }

    public static ExplanationResult Success(string text) => new()
    {
        IsSuccess = true,
        Text = text,
    };

    public static ExplanationResult Unavailable(string reason) => new()
    {
        IsSuccess = false,
        FailureReason = reason,
    };
}
