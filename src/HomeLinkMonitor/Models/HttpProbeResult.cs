namespace HomeLinkMonitor.Models;

public class HttpProbeResult
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Url { get; set; } = string.Empty;
    public double? LatencyMs { get; set; }
    public int StatusCode { get; set; }
    public bool IsSuccess { get; set; }
    public bool IsCaptivePortal { get; set; }
    public string Error { get; set; } = string.Empty;
}
