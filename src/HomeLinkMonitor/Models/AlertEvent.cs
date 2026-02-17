namespace HomeLinkMonitor.Models;

public class AlertEvent
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string AlertType { get; set; } = string.Empty; // "SignalLow", "HighLatency", "PacketLoss", "Disconnected", etc.
    public string Severity { get; set; } = string.Empty; // "Info", "Warning", "Critical"
    public string Message { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public bool IsAcknowledged { get; set; }
}
