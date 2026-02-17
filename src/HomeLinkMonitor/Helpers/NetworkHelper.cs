using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace HomeLinkMonitor.Helpers;

public static class NetworkHelper
{
    public static string? GetDefaultGateway()
    {
        try
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up
                         && n.NetworkInterfaceType != NetworkInterfaceType.Loopback
                         && n.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                .SelectMany(n => n.GetIPProperties().GatewayAddresses)
                .Where(g => g.Address.AddressFamily == AddressFamily.InterNetwork
                         && g.Address.ToString() != "0.0.0.0")
                .Select(g => g.Address.ToString())
                .FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    public static NetworkInterface? GetActiveWifiAdapter()
    {
        return NetworkInterface.GetAllNetworkInterfaces()
            .FirstOrDefault(n => n.OperationalStatus == OperationalStatus.Up
                              && n.NetworkInterfaceType == NetworkInterfaceType.Wireless80211);
    }

    public static int FrequencyToChannel(int frequencyKHz)
    {
        int freqMHz = frequencyKHz / 1000;
        if (freqMHz >= 2412 && freqMHz <= 2484)
        {
            if (freqMHz == 2484) return 14;
            return (freqMHz - 2412) / 5 + 1;
        }
        if (freqMHz >= 5180 && freqMHz <= 5825)
            return (freqMHz - 5000) / 5;
        if (freqMHz >= 5955 && freqMHz <= 7115)
            return (freqMHz - 5950) / 5;
        return 0;
    }

    public static string FrequencyToBand(int frequencyKHz)
    {
        int freqMHz = frequencyKHz / 1000;
        if (freqMHz >= 2400 && freqMHz <= 2500) return "2.4 GHz";
        if (freqMHz >= 5100 && freqMHz <= 5900) return "5 GHz";
        if (freqMHz >= 5925 && freqMHz <= 7125) return "6 GHz";
        return "Unknown";
    }

    public static int SignalQualityToRssi(int quality)
    {
        // Windows signal quality is 0-100, roughly maps to -100 to -50 dBm
        return quality / 2 - 100;
    }

    public static ConnectionQuality ClassifySignal(int quality)
    {
        return quality switch
        {
            >= 80 => ConnectionQuality.Excellent,
            >= 60 => ConnectionQuality.Good,
            >= 40 => ConnectionQuality.Fair,
            >= 20 => ConnectionQuality.Poor,
            _ => ConnectionQuality.VeryPoor
        };
    }

    public enum ConnectionQuality
    {
        Excellent,
        Good,
        Fair,
        Poor,
        VeryPoor
    }
}
