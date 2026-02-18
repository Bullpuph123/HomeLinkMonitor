using CommunityToolkit.Mvvm.Messaging;
using HomeLinkMonitor.Data;
using HomeLinkMonitor.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HomeLinkMonitor.Services;

public class MonitoringOrchestrator : BackgroundService
{
    private readonly IWifiMetricsProvider _wifiProvider;
    private readonly INetworkInterfaceProvider _networkProvider;
    private readonly IPingProbe _pingProbe;
    private readonly IDnsProbe _dnsProbe;
    private readonly IHttpProbe _httpProbe;
    private readonly DataRepository _repository;
    private readonly IMessenger _messenger;
    private readonly IAlertEngine _alertEngine;
    private readonly IRoamingDetector _roamingDetector;
    private readonly ILogger<MonitoringOrchestrator> _logger;
    private readonly AppConfig _config;

    public MonitoringOrchestrator(
        IWifiMetricsProvider wifiProvider,
        INetworkInterfaceProvider networkProvider,
        IPingProbe pingProbe,
        IDnsProbe dnsProbe,
        IHttpProbe httpProbe,
        DataRepository repository,
        IAlertEngine alertEngine,
        IRoamingDetector roamingDetector,
        IMessenger messenger,
        ILogger<MonitoringOrchestrator> logger,
        AppConfig config)
    {
        _wifiProvider = wifiProvider;
        _networkProvider = networkProvider;
        _pingProbe = pingProbe;
        _dnsProbe = dnsProbe;
        _httpProbe = httpProbe;
        _repository = repository;
        _alertEngine = alertEngine;
        _roamingDetector = roamingDetector;
        _messenger = messenger;
        _logger = logger;
        _config = config;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Monitoring orchestrator started (interval: {Interval}s)",
            _config.PollingIntervalSeconds);

        // Initial delay to let the UI load
        await Task.Delay(500, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var snapshot = await CollectSnapshotAsync(stoppingToken);

                // Publish to UI first so dashboard always updates
                _messenger.Send(new MonitoringUpdateMessage(snapshot));

                // Persist to database
                await _repository.SaveSnapshotAsync(snapshot, stoppingToken);

                // Evaluate alerts and roaming (non-critical for UI updates)
                try
                {
                    await _alertEngine.EvaluateAsync(snapshot, stoppingToken);
                    await _roamingDetector.CheckForRoamingAsync(snapshot.Wifi, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Alert/roaming evaluation failed");
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in monitoring cycle");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_config.PollingIntervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("Monitoring orchestrator stopped");
    }

    private async Task<MonitoringSnapshot> CollectSnapshotAsync(CancellationToken ct)
    {
        var snapshot = new MonitoringSnapshot();

        // Collect Wi-Fi and network info synchronously (fast local calls)
        snapshot.Wifi = _wifiProvider.GetCurrentSnapshot();
        snapshot.Network = _networkProvider.GetCurrentSnapshot();

        // Run probes in parallel
        var pingTask = _pingProbe.PingAllTargetsAsync(_config, ct);
        var dnsTask = _dnsProbe.QueryAllAsync(_config, ct);
        var httpTask = _httpProbe.CheckAsync(_config, ct);

        await Task.WhenAll(pingTask, dnsTask, httpTask);

        snapshot.PingResults = await pingTask;
        snapshot.DnsResults = await dnsTask;
        snapshot.HttpProbe = await httpTask;

        // Determine overall status
        snapshot.OverallStatus = DetermineStatus(snapshot);

        return snapshot;
    }

    private static ConnectionStatus DetermineStatus(MonitoringSnapshot snapshot)
    {
        if (snapshot.Wifi == null || !snapshot.Wifi.IsConnected)
            return ConnectionStatus.Disconnected;

        var gatewayPing = snapshot.PingResults.FirstOrDefault(p => p.TargetLabel == "Gateway");
        var dnsPings = snapshot.PingResults.Where(p => p.TargetLabel.StartsWith("DNS")).ToList();
        var anyDnsSuccess = dnsPings.Any(p => p.IsSuccess);

        if (gatewayPing != null && !gatewayPing.IsSuccess)
            return ConnectionStatus.Disconnected;

        if (!anyDnsSuccess && snapshot.HttpProbe is { IsSuccess: false })
            return ConnectionStatus.NoInternet;

        // Rate based on signal + latency
        var signal = snapshot.Wifi.SignalQuality;
        var avgLatency = snapshot.PingResults
            .Where(p => p.IsSuccess && p.LatencyMs.HasValue)
            .Select(p => p.LatencyMs!.Value)
            .DefaultIfEmpty(0)
            .Average();

        if (signal >= 70 && avgLatency < 30)
            return ConnectionStatus.Excellent;
        if (signal >= 50 && avgLatency < 60)
            return ConnectionStatus.Good;
        if (signal >= 30 && avgLatency < 100)
            return ConnectionStatus.Fair;

        return ConnectionStatus.Poor;
    }
}
