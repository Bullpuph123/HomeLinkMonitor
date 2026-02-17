namespace HomeLinkMonitor.Models;

public class WifiSnapshot
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Ssid { get; set; } = string.Empty;
    public string Bssid { get; set; } = string.Empty;
    public int SignalQuality { get; set; }
    public int RssiDbm { get; set; }
    public int LinkSpeedMbps { get; set; }
    public int Channel { get; set; }
    public double FrequencyGHz { get; set; }
    public string Band { get; set; } = string.Empty; // "2.4GHz", "5GHz", "6GHz"
    public string PhyType { get; set; } = string.Empty; // "802.11ac", "802.11ax", etc.
    public string AuthAlgorithm { get; set; } = string.Empty;
    public bool IsConnected { get; set; }
    public string ProfileName { get; set; } = string.Empty;
    public string InterfaceId { get; set; } = string.Empty;
}
