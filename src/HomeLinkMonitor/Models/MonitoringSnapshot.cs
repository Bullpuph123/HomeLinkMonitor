namespace HomeLinkMonitor.Models;

/// <summary>
/// Aggregated result from a single monitoring cycle.
/// Published via IMessenger to ViewModels.
/// </summary>
public class MonitoringSnapshot
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public WifiSnapshot? Wifi { get; set; }
    public NetworkSnapshot? Network { get; set; }
    public List<PingResult> PingResults { get; set; } = [];
    public List<DnsResult> DnsResults { get; set; } = [];
    public HttpProbeResult? HttpProbe { get; set; }
    public ConnectionStatus OverallStatus { get; set; } = ConnectionStatus.Unknown;
}

public enum ConnectionStatus
{
    Unknown,
    Excellent,
    Good,
    Fair,
    Poor,
    Disconnected,
    NoInternet
}
