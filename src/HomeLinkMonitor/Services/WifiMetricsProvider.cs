using HomeLinkMonitor.Helpers;
using HomeLinkMonitor.Models;
using ManagedNativeWifi;
using Microsoft.Extensions.Logging;

namespace HomeLinkMonitor.Services;

public interface IWifiMetricsProvider
{
    WifiSnapshot? GetCurrentSnapshot();
}

public class WifiMetricsProvider : IWifiMetricsProvider
{
    private readonly ILogger<WifiMetricsProvider> _logger;

    public WifiMetricsProvider(ILogger<WifiMetricsProvider> logger)
    {
        _logger = logger;
    }

    public WifiSnapshot? GetCurrentSnapshot()
    {
        try
        {
            var connected = NativeWifi.EnumerateConnectedNetworkSsids().FirstOrDefault();
            if (connected == null)
            {
                _logger.LogDebug("No connected Wi-Fi network found");
                return new WifiSnapshot { IsConnected = false };
            }

            var interfaces = NativeWifi.EnumerateInterfaces().ToList();
            var connectedInterface = interfaces.FirstOrDefault(i =>
                i.State == InterfaceState.Connected);

            var networks = NativeWifi.EnumerateBssNetworks().ToList();

            // Get connection info from the connected interface
            var snapshot = new WifiSnapshot
            {
                IsConnected = true,
                Ssid = connected.ToString(),
                InterfaceId = connectedInterface?.Id.ToString() ?? string.Empty
            };

            // Try to get detailed info from available networks
            var currentBss = networks
                .Where(n => n.Ssid.ToString() == snapshot.Ssid)
                .OrderByDescending(n => n.SignalStrength)
                .FirstOrDefault();

            if (currentBss != null)
            {
                snapshot.Bssid = currentBss.Bssid.ToString();
                snapshot.SignalQuality = currentBss.SignalStrength;
                snapshot.RssiDbm = NetworkHelper.SignalQualityToRssi(currentBss.SignalStrength);
                snapshot.LinkSpeedMbps = (int)(currentBss.LinkQuality);
                snapshot.Channel = NetworkHelper.FrequencyToChannel(currentBss.Frequency);
                snapshot.FrequencyGHz = currentBss.Frequency / 1_000_000.0;
                snapshot.Band = NetworkHelper.FrequencyToBand(currentBss.Frequency);
                snapshot.PhyType = currentBss.BssType.ToString();
            }
            else
            {
                // Fallback: get signal from available networks
                var availableNetworks = NativeWifi.EnumerateAvailableNetworks().ToList();
                var connNet = availableNetworks.FirstOrDefault(n =>
                    n.Ssid.ToString() == snapshot.Ssid);
                if (connNet != null)
                {
                    snapshot.SignalQuality = connNet.SignalQuality;
                    snapshot.RssiDbm = NetworkHelper.SignalQualityToRssi(connNet.SignalQuality);
                }
            }

            // Get profile name
            var profiles = NativeWifi.EnumerateProfiles().ToList();
            var matchingProfile = profiles.FirstOrDefault(p =>
                p.Name == snapshot.Ssid);
            if (matchingProfile != null)
            {
                snapshot.ProfileName = matchingProfile.Name;
            }

            return snapshot;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get Wi-Fi metrics");
            return null;
        }
    }
}
