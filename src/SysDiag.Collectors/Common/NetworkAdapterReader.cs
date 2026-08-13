using System.Net.NetworkInformation;
using SysDiag.Core.Models;

namespace SysDiag.Collectors.Common;

/// <summary>
/// Reads network interfaces through <see cref="NetworkInterface"/>. This part of
/// the BCL works on Windows and on Linux alike, so both platform collectors share
/// it instead of each implementing the same thing twice via WMI and /sys.
/// </summary>
internal static class NetworkAdapterReader
{
    public static IReadOnlyList<NetworkAdapterInfo> Read()
    {
        NetworkInterface[] interfaces;

        try
        {
            interfaces = NetworkInterface.GetAllNetworkInterfaces();
        }
        catch (NetworkInformationException)
        {
            // Container images without a network stack can fail here.
            return [];
        }

        return interfaces
            .Where(nic => nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .Select(Map)
            .OrderBy(adapter => adapter.Name, StringComparer.Ordinal)
            .ToList();
    }

    private static NetworkAdapterInfo Map(NetworkInterface nic) => new()
    {
        Name = nic.Name,
        Description = string.IsNullOrWhiteSpace(nic.Description) ? null : nic.Description,
        MacAddress = FormatMacAddress(nic),
        IpAddresses = ReadIpAddresses(nic),
        // Speed is reported in bits per second and is -1 when unknown.
        SpeedMbps = nic.Speed > 0 ? nic.Speed / 1_000_000 : null,
        IsUp = nic.OperationalStatus == OperationalStatus.Up,
    };

    private static string? FormatMacAddress(NetworkInterface nic)
    {
        byte[] address = nic.GetPhysicalAddress().GetAddressBytes();
        return address.Length == 0
            ? null
            : string.Join(":", address.Select(octet => octet.ToString("X2")));
    }

    private static IReadOnlyList<string> ReadIpAddresses(NetworkInterface nic)
    {
        try
        {
            return nic.GetIPProperties()
                .UnicastAddresses
                .Select(address => address.Address.ToString())
                .ToList();
        }
        catch (PlatformNotSupportedException)
        {
            // Not every interface exposes IP properties on every platform.
            return [];
        }
    }
}
