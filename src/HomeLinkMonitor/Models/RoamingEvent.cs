namespace HomeLinkMonitor.Models;

public class RoamingEvent
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string PreviousBssid { get; set; } = string.Empty;
    public string NewBssid { get; set; } = string.Empty;
    public string Ssid { get; set; } = string.Empty;
    public int PreviousSignalQuality { get; set; }
    public int NewSignalQuality { get; set; }
    public int PreviousChannel { get; set; }
    public int NewChannel { get; set; }
}
