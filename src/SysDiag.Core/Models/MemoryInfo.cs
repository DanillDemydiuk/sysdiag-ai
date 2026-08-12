namespace SysDiag.Core.Models;

/// <summary>
/// Main memory of a machine. All values are raw bytes; formatting for humans
/// happens in the presentation layer, never in the model.
/// </summary>
public sealed record MemoryInfo
{
    /// <summary>Total installed physical memory in bytes.</summary>
    public required long TotalBytes { get; init; }

    /// <summary>
    /// Currently available physical memory in bytes, or <c>null</c> if unknown.
    /// This value is volatile: it differs between two scans even on an idle
    /// machine, so the diff engine reports it as informational only.
    /// </summary>
    public long? AvailableBytes { get; init; }
}
