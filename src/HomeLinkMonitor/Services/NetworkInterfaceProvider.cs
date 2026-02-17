using System.Net.NetworkInformation;
using System.Net.Sockets;
using HomeLinkMonitor.Models;
using Microsoft.Extensions.Logging;

namespace HomeLinkMonitor.Services;

public interface INetworkInterfaceProvider
{
    NetworkSnapshot? GetCurrentSnapshot();
}

public class NetworkInterfaceProvider : INetworkInterfaceProvider
{
    private readonly ILogger<NetworkInterfaceProvider> _logger;

    public NetworkInterfaceProvider(ILogger<NetworkInterfaceProvider> logger)
    {
        _logger = logger;
    }

    public NetworkSnapshot? GetCurrentSnapshot()
    {
        try
        {
            var adapter = NetworkInterface.GetAllNetworkInterfaces()
                .FirstOrDefault(n => n.OperationalStatus == OperationalStatus.Up
                    && n.NetworkInterfaceType == NetworkInterfaceType.Wireless80211);

            if (adapter == null)
            {
                _logger.LogDebug("No active Wi-Fi adapter found");
                return new NetworkSnapshot { IsConnected = false };
            }

            var ipProps = adapter.GetIPProperties();
            var stats = adapter.GetIPv4Statistics();

            var ipv4 = ipProps.UnicastAddresses
                .FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork);

            var gateway = ipProps.GatewayAddresses
                .FirstOrDefault(g => g.Address.AddressFamily == AddressFamily.InterNetwork
                    && g.Address.ToString() != "0.0.0.0");

            var dnsServers = ipProps.DnsAddresses
                .Where(d => d.AddressFamily == AddressFamily.InterNetwork)
                .Select(d => d.ToString());

            return new NetworkSnapshot
            {
                IsConnected = true,
                LocalIpAddress = ipv4?.Address.ToString() ?? string.Empty,
                SubnetMask = ipv4?.IPv4Mask?.ToString() ?? string.Empty,
                GatewayAddress = gateway?.Address.ToString() ?? string.Empty,
                DnsServers = string.Join(", ", dnsServers),
                MacAddress = FormatMac(adapter.GetPhysicalAddress()),
                BytesSent = stats.BytesSent,
                BytesReceived = stats.BytesReceived,
                AdapterName = adapter.Name,
                AdapterDescription = adapter.Description
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get network interface info");
            return null;
        }
    }

    private static string FormatMac(PhysicalAddress mac)
    {
        var bytes = mac.GetAddressBytes();
        return string.Join(":", bytes.Select(b => b.ToString("X2")));
    }
}
