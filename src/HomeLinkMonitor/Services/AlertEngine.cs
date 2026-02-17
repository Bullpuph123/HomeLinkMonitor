using CommunityToolkit.Mvvm.Messaging;
using HomeLinkMonitor.Data;
using HomeLinkMonitor.Models;
using Microsoft.Extensions.Logging;

namespace HomeLinkMonitor.Services;

public interface IAlertEngine
{
    Task EvaluateAsync(MonitoringSnapshot snapshot, CancellationToken ct = default);
}

public class AlertEngine : IAlertEngine
{
    private readonly AppConfig _config;
    private readonly DataRepository _repository;
    private readonly INotificationService _notificationService;
    private readonly IMessenger _messenger;
    private readonly ILogger<AlertEngine> _logger;
    private readonly Dictionary<string, DateTime> _lastAlertTimes = new();

    public AlertEngine(
        AppConfig config,
        DataRepository repository,
        INotificationService notificationService,
        IMessenger messenger,
        ILogger<AlertEngine> logger)
    {
        _config = config;
        _repository = repository;
        _notificationService = notificationService;
        _messenger = messenger;
        _logger = logger;
    }

    public async Task EvaluateAsync(MonitoringSnapshot snapshot, CancellationToken ct = default)
    {
        // Check signal quality
        if (snapshot.Wifi is { IsConnected: true, SignalQuality: > 0 }
            && snapshot.Wifi.SignalQuality < _config.AlertSignalLowThreshold)
        {
            await FireAlertAsync("SignalLow", "Warning",
                $"Wi-Fi signal is low: {snapshot.Wifi.SignalQuality}%",
                $"SSID: {snapshot.Wifi.Ssid}, RSSI: {snapshot.Wifi.RssiDbm} dBm", ct);
        }

        // Check disconnection
        if (snapshot.Wifi is { IsConnected: false })
        {
            await FireAlertAsync("Disconnected", "Critical",
                "Wi-Fi disconnected",
                "No Wi-Fi connection detected", ct);
        }

        // Check gateway latency
        var gatewayPing = snapshot.PingResults.FirstOrDefault(p => p.TargetLabel == "Gateway");
        if (gatewayPing != null)
        {
            if (!gatewayPing.IsSuccess)
            {
                await FireAlertAsync("GatewayUnreachable", "Critical",
                    "Gateway is unreachable",
                    $"Target: {gatewayPing.Target}", ct);
            }
            else if (gatewayPing.LatencyMs.HasValue && gatewayPing.LatencyMs.Value > _config.AlertLatencyHighMs)
            {
                await FireAlertAsync("HighLatency", "Warning",
                    $"High gateway latency: {gatewayPing.LatencyMs.Value:F0}ms",
                    $"Threshold: {_config.AlertLatencyHighMs}ms", ct);
            }
        }

        // Check internet connectivity
        if (snapshot.HttpProbe is { IsSuccess: false } && snapshot.Wifi is { IsConnected: true })
        {
            await FireAlertAsync("NoInternet", "Warning",
                "Internet connectivity lost",
                snapshot.HttpProbe.Error, ct);
        }

        // Check captive portal
        if (snapshot.HttpProbe is { IsCaptivePortal: true })
        {
            await FireAlertAsync("CaptivePortal", "Info",
                "Captive portal detected",
                "You may need to authenticate with the network", ct);
        }
    }

    private async Task FireAlertAsync(string alertType, string severity, string message, string details, CancellationToken ct)
    {
        // Cooldown check
        if (_lastAlertTimes.TryGetValue(alertType, out var lastTime)
            && (DateTime.UtcNow - lastTime).TotalSeconds < _config.AlertCooldownSeconds)
        {
            return;
        }

        _lastAlertTimes[alertType] = DateTime.UtcNow;

        var alert = new AlertEvent
        {
            AlertType = alertType,
            Severity = severity,
            Message = message,
            Details = details
        };

        _logger.LogInformation("Alert: [{Severity}] {Message}", severity, message);

        await _repository.SaveAlertAsync(alert, ct);
        _messenger.Send(new AlertFiredMessage(alert));

        if (_config.ShowNotifications)
        {
            _notificationService.ShowNotification(severity, message);
        }
    }
}
