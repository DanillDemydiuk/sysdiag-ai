namespace SysDiag.Core.Models;

/// <summary>
/// Processor description of a machine.
/// </summary>
public sealed record CpuInfo
{
    /// <summary>Marketing name, for example "AMD Ryzen 5 5600X 6-Core Processor".</summary>
    public required string Name { get; init; }

    /// <summary>Number of physical cores, or <c>null</c> if the platform does not report it.</summary>
    public int? PhysicalCores { get; init; }

    /// <summary>Number of logical processors (cores including hardware threads).</summary>
    public required int LogicalCores { get; init; }

    /// <summary>Maximum clock speed in MHz, or <c>null</c> if unknown.</summary>
    public int? MaxClockMhz { get; init; }

    /// <summary>Process architecture, for example "X64" or "Arm64".</summary>
    public required string Architecture { get; init; }
}
