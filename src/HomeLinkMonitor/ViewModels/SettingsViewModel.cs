using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HomeLinkMonitor.Helpers;
using HomeLinkMonitor.Models;
using Application = System.Windows.Application;

namespace HomeLinkMonitor.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly AppConfig _config;

    // General
    [ObservableProperty] private int _pollingIntervalSeconds;
    [ObservableProperty] private string _theme = "Dark";
    [ObservableProperty] private bool _startMinimized;
    [ObservableProperty] private bool _minimizeToTray;
    [ObservableProperty] private bool _startWithWindows;
    [ObservableProperty] private bool _showNotifications;

    // Monitoring
    [ObservableProperty] private string _primaryDns = "8.8.8.8";
    [ObservableProperty] private string _secondaryDns = "1.1.1.1";
    [ObservableProperty] private int _pingTimeoutMs;
    [ObservableProperty] private string _dnsQueryName = "google.com";
    [ObservableProperty] private string _httpProbeUrl = "";
    [ObservableProperty] private int _httpTimeoutMs;
    [ObservableProperty] private string _customPingTargets = "";

    // Alerts
    [ObservableProperty] private int _alertSignalLowThreshold;
    [ObservableProperty] private int _alertLatencyHighMs;
    [ObservableProperty] private int _alertPacketLossPercent;
    [ObservableProperty] private int _alertCooldownSeconds;

    // Data
    [ObservableProperty] private int _rawDataRetentionDays;
    [ObservableProperty] private int _aggregatedRetentionDays;
    [ObservableProperty] private int _alertRetentionDays;

    public SettingsViewModel(AppConfig config)
    {
        _config = config;
        LoadFromConfig();
    }

    private void LoadFromConfig()
    {
        PollingIntervalSeconds = _config.PollingIntervalSeconds;
        Theme = _config.Theme;
        StartMinimized = _config.StartMinimized;
        MinimizeToTray = _config.MinimizeToTray;
        StartWithWindows = _config.StartWithWindows;
        ShowNotifications = _config.ShowNotifications;

        PrimaryDns = _config.PrimaryDns;
        SecondaryDns = _config.SecondaryDns;
        PingTimeoutMs = _config.PingTimeoutMs;
        DnsQueryName = _config.DnsQueryName;
        HttpProbeUrl = _config.HttpProbeUrl;
        HttpTimeoutMs = _config.HttpTimeoutMs;
        CustomPingTargets = string.Join(", ", _config.CustomPingTargets);

        AlertSignalLowThreshold = _config.AlertSignalLowThreshold;
        AlertLatencyHighMs = _config.AlertLatencyHighMs;
        AlertPacketLossPercent = _config.AlertPacketLossPercent;
        AlertCooldownSeconds = _config.AlertCooldownSeconds;

        RawDataRetentionDays = _config.RawDataRetentionDays;
        AggregatedRetentionDays = _config.AggregatedRetentionDays;
        AlertRetentionDays = _config.AlertRetentionDays;
    }

    [RelayCommand]
    private void Save()
    {
        _config.PollingIntervalSeconds = Math.Max(1, PollingIntervalSeconds);
        _config.Theme = Theme;
        _config.StartMinimized = StartMinimized;
        _config.MinimizeToTray = MinimizeToTray;
        _config.StartWithWindows = StartWithWindows;
        _config.ShowNotifications = ShowNotifications;

        _config.PrimaryDns = PrimaryDns;
        _config.SecondaryDns = SecondaryDns;
        _config.PingTimeoutMs = PingTimeoutMs;
        _config.DnsQueryName = DnsQueryName;
        _config.HttpProbeUrl = HttpProbeUrl;
        _config.HttpTimeoutMs = HttpTimeoutMs;
        _config.CustomPingTargets = CustomPingTargets
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        _config.AlertSignalLowThreshold = AlertSignalLowThreshold;
        _config.AlertLatencyHighMs = AlertLatencyHighMs;
        _config.AlertPacketLossPercent = AlertPacketLossPercent;
        _config.AlertCooldownSeconds = AlertCooldownSeconds;

        _config.RawDataRetentionDays = RawDataRetentionDays;
        _config.AggregatedRetentionDays = AggregatedRetentionDays;
        _config.AlertRetentionDays = AlertRetentionDays;

        _config.Save();

        // Apply auto-start
        AutoStartHelper.SetAutoStart(_config.StartWithWindows);

        // Apply theme change
        ApplyTheme(Theme);
    }

    [RelayCommand]
    private void ResetDefaults()
    {
        var defaults = new AppConfig();
        _config.PollingIntervalSeconds = defaults.PollingIntervalSeconds;
        _config.PrimaryDns = defaults.PrimaryDns;
        _config.SecondaryDns = defaults.SecondaryDns;
        _config.AlertSignalLowThreshold = defaults.AlertSignalLowThreshold;
        _config.AlertLatencyHighMs = defaults.AlertLatencyHighMs;
        _config.AlertCooldownSeconds = defaults.AlertCooldownSeconds;
        LoadFromConfig();
    }

    public static void ApplyTheme(string theme)
    {
        var app = Application.Current;
        if (app == null) return;

        var themePath = theme == "Light"
            ? "Themes/LightTheme.xaml"
            : "Themes/DarkTheme.xaml";

        app.Resources.MergedDictionaries.Clear();
        app.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri(themePath, UriKind.Relative)
        });
    }
}
