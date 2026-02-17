namespace HomeLinkMonitor.Models;

public class PingResult
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Target { get; set; } = string.Empty;
    public string TargetLabel { get; set; } = string.Empty; // "Gateway", "DNS1", "DNS2", "Custom"
    public double? LatencyMs { get; set; }
    public bool IsSuccess { get; set; }
    public string Status { get; set; } = string.Empty; // "Success", "TimedOut", "Unreachable", etc.
    public int Ttl { get; set; }
}
