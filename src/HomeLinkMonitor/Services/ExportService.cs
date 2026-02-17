using System.IO;
using System.Text;
using System.Text.Json;
using HomeLinkMonitor.Data;
using HomeLinkMonitor.Models;
using Microsoft.Extensions.Logging;

namespace HomeLinkMonitor.Services;

public interface IExportService
{
    Task<string> ExportPingDataCsvAsync(DateTime from, DateTime to, CancellationToken ct = default);
    Task<string> ExportWifiDataCsvAsync(DateTime from, DateTime to, CancellationToken ct = default);
    Task<string> ExportAlertsCsvAsync(DateTime from, DateTime to, CancellationToken ct = default);
    Task<string> ExportAllJsonAsync(DateTime from, DateTime to, CancellationToken ct = default);
}

public class ExportService : IExportService
{
    private readonly DataRepository _repository;
    private readonly ILogger<ExportService> _logger;

    private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public ExportService(DataRepository repository, ILogger<ExportService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<string> ExportPingDataCsvAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        var data = await _repository.GetPingResultsAsync(from, to, ct: ct);
        var sb = new StringBuilder();
        sb.AppendLine("Timestamp,Target,TargetLabel,LatencyMs,IsSuccess,Status,Ttl");
        foreach (var r in data)
        {
            sb.AppendLine($"{r.Timestamp:O},{Escape(r.Target)},{Escape(r.TargetLabel)},{r.LatencyMs},{r.IsSuccess},{Escape(r.Status)},{r.Ttl}");
        }
        return await SaveExportAsync("ping_data", ".csv", sb.ToString(), ct);
    }

    public async Task<string> ExportWifiDataCsvAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        var data = await _repository.GetWifiSnapshotsAsync(from, to, ct);
        var sb = new StringBuilder();
        sb.AppendLine("Timestamp,SSID,BSSID,SignalQuality,RssiDbm,LinkSpeedMbps,Channel,FrequencyGHz,Band,PhyType");
        foreach (var w in data)
        {
            sb.AppendLine($"{w.Timestamp:O},{Escape(w.Ssid)},{Escape(w.Bssid)},{w.SignalQuality},{w.RssiDbm},{w.LinkSpeedMbps},{w.Channel},{w.FrequencyGHz:F3},{Escape(w.Band)},{Escape(w.PhyType)}");
        }
        return await SaveExportAsync("wifi_data", ".csv", sb.ToString(), ct);
    }

    public async Task<string> ExportAlertsCsvAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        var data = await _repository.GetAlertsAsync(from, to, ct);
        var sb = new StringBuilder();
        sb.AppendLine("Timestamp,AlertType,Severity,Message,Details");
        foreach (var a in data)
        {
            sb.AppendLine($"{a.Timestamp:O},{Escape(a.AlertType)},{Escape(a.Severity)},{Escape(a.Message)},{Escape(a.Details)}");
        }
        return await SaveExportAsync("alerts", ".csv", sb.ToString(), ct);
    }

    public async Task<string> ExportAllJsonAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        var pingData = await _repository.GetPingResultsAsync(from, to, ct: ct);
        var wifiData = await _repository.GetWifiSnapshotsAsync(from, to, ct);
        var alertData = await _repository.GetAlertsAsync(from, to, ct);
        var dnsData = await _repository.GetDnsResultsAsync(from, to, ct);

        var export = new
        {
            ExportedAt = DateTime.UtcNow,
            Period = new { From = from, To = to },
            WifiSnapshots = wifiData,
            PingResults = pingData,
            DnsResults = dnsData,
            Alerts = alertData
        };

        var json = JsonSerializer.Serialize(export, _jsonOptions);
        return await SaveExportAsync("homelink_export", ".json", json, ct);
    }

    private static async Task<string> SaveExportAsync(string prefix, string extension, string content, CancellationToken ct)
    {
        var exportDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "HomeLinkMonitor", "Exports");
        Directory.CreateDirectory(exportDir);

        var fileName = $"{prefix}_{DateTime.Now:yyyyMMdd_HHmmss}{extension}";
        var filePath = Path.Combine(exportDir, fileName);
        await File.WriteAllTextAsync(filePath, content, ct);
        return filePath;
    }

    private static string Escape(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
