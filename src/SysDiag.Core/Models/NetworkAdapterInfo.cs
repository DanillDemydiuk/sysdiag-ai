namespace SysDiag.Core.Models;

/// <summary>
/// A network interface of the machine, physical or virtual.
/// </summary>
public sealed record NetworkAdapterInfo
{
    /// <summary>Interface name as reported by the operating system ("Ethernet", "wlan0").</summary>
    public required string Name { get; init; }

    /// <summary>Longer description of the adapter hardware, or <c>null</c> if unavailable.</summary>
    public string? Description { get; init; }

    /// <summary>MAC address in "AA:BB:CC:DD:EE:FF" form, or <c>null</c> for interfaces without one.</summary>
    public string? MacAddress { get; init; }

    /// <summary>IPv4 and IPv6 addresses currently assigned to the interface.</summary>
    public IReadOnlyList<string> IpAddresses { get; init; } = [];

    /// <summary>Link speed in Mbit/s, or <c>null</c> if the interface does not report it.</summary>
    public long? SpeedMbps { get; init; }

    /// <summary>True if the interface is operational (link up).</summary>
    public required bool IsUp { get; init; }
}
