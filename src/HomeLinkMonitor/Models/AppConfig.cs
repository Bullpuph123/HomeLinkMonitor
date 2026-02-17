using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HomeLinkMonitor.Models;

public class AppConfig
{
    public int PollingIntervalSeconds { get; set; } = 5;
    public string Theme { get; set; } = "Dark";
    public bool StartMinimized { get; set; }
    public bool MinimizeToTray { get; set; } = true;
    public bool StartWithWindows { get; set; }
    public bool ShowNotifications { get; set; } = true;

    // Ping targets
    public List<string> CustomPingTargets { get; set; } = [];
    public string PrimaryDns { get; set; } = "8.8.8.8";
    public string SecondaryDns { get; set; } = "1.1.1.1";
    public int PingTimeoutMs { get; set; } = 2000;

    // DNS probe
    public string DnsQueryName { get; set; } = "google.com";

    // HTTP probe
    public string HttpProbeUrl { get; set; } = "http://www.msftconnecttest.com/connecttest.txt";
    public int HttpTimeoutMs { get; set; } = 5000;

    // Alerts
    public int AlertSignalLowThreshold { get; set; } = 30;
    public int AlertLatencyHighMs { get; set; } = 100;
    public int AlertPacketLossPercent { get; set; } = 10;
    public int AlertCooldownSeconds { get; set; } = 60;

    // Data retention
    public int RawDataRetentionDays { get; set; } = 7;
    public int AggregatedRetentionDays { get; set; } = 90;
    public int AlertRetentionDays { get; set; } = 365;

    // Window state
    public double MainWindowLeft { get; set; } = double.NaN;
    public double MainWindowTop { get; set; } = double.NaN;
    public double MainWindowWidth { get; set; } = 1100;
    public double MainWindowHeight { get; set; } = 700;
    public double MiniWindowLeft { get; set; } = double.NaN;
    public double MiniWindowTop { get; set; } = double.NaN;

    // Chart
    public string DefaultChartTimeRange { get; set; } = "15m";

    private static string ConfigDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HomeLinkMonitor");

    private static string ConfigPath => Path.Combine(ConfigDir, "config.json");

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault
    };

    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                return JsonSerializer.Deserialize<AppConfig>(json, _jsonOptions) ?? new AppConfig();
            }
        }
        catch
        {
            // Fall through to default
        }
        return new AppConfig();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(ConfigDir);
            var json = JsonSerializer.Serialize(this, _jsonOptions);
            File.WriteAllText(ConfigPath, json);
        }
        catch
        {
            // Silently fail - not critical
        }
    }
}
