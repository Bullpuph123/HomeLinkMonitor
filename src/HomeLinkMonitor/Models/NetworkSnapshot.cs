namespace HomeLinkMonitor.Models;

public class NetworkSnapshot
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string LocalIpAddress { get; set; } = string.Empty;
    public string SubnetMask { get; set; } = string.Empty;
    public string GatewayAddress { get; set; } = string.Empty;
    public string DnsServers { get; set; } = string.Empty; // comma-separated
    public string MacAddress { get; set; } = string.Empty;
    public long BytesSent { get; set; }
    public long BytesReceived { get; set; }
    public string AdapterName { get; set; } = string.Empty;
    public string AdapterDescription { get; set; } = string.Empty;
    public bool IsConnected { get; set; }
}
