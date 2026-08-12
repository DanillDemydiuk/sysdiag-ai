namespace SysDiag.Core.Models;

/// <summary>
/// Operating system of the machine.
/// </summary>
public sealed record OsInfo
{
    /// <summary>Platform family: "Windows", "Linux" or "Unknown".</summary>
    public required string Platform { get; init; }

    /// <summary>Product name, for example "Windows 11 Pro" or "Ubuntu 24.04 LTS".</summary>
    public required string Caption { get; init; }

    /// <summary>Version or build string as reported by the platform.</summary>
    public required string Version { get; init; }

    /// <summary>OS architecture, for example "X64".</summary>
    public required string Architecture { get; init; }
}
