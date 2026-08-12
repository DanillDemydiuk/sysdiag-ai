using SysDiag.Core.Abstractions;

namespace SysDiag.Collectors;

/// <summary>
/// The collector chosen for this run, together with an optional message for the
/// user. The message exists so that a fallback is never silent: if the program
/// cannot read the real hardware, it has to say so instead of quietly showing
/// invented numbers.
/// </summary>
public sealed record CollectorSelection
{
    public required ISystemCollector Collector { get; init; }

    /// <summary>
    /// Short notice to print before the results, or <c>null</c> when the expected
    /// collector for this platform was used.
    /// </summary>
    public string? Notice { get; init; }
}
