namespace HomeLinkMonitor.Models;

public class DnsResult
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string DnsServer { get; set; } = string.Empty;
    public string QueryName { get; set; } = string.Empty;
    public double? LatencyMs { get; set; }
    public bool IsSuccess { get; set; }
    public string Error { get; set; } = string.Empty;
}
