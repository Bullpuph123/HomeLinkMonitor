using CommunityToolkit.Mvvm.Messaging;
using HomeLinkMonitor.Data;
using HomeLinkMonitor.Models;
using Microsoft.Extensions.Logging;

namespace HomeLinkMonitor.Services;

public interface IRoamingDetector
{
    Task CheckForRoamingAsync(WifiSnapshot? current, CancellationToken ct = default);
}

public class RoamingDetector : IRoamingDetector
{
    private readonly DataRepository _repository;
    private readonly IMessenger _messenger;
    private readonly ILogger<RoamingDetector> _logger;
    private string _lastBssid = string.Empty;
    private int _lastSignal;
    private int _lastChannel;

    public RoamingDetector(
        DataRepository repository,
        IMessenger messenger,
        ILogger<RoamingDetector> logger)
    {
        _repository = repository;
        _messenger = messenger;
        _logger = logger;
    }

    public async Task CheckForRoamingAsync(WifiSnapshot? current, CancellationToken ct = default)
    {
        if (current == null || !current.IsConnected || string.IsNullOrEmpty(current.Bssid))
            return;

        if (!string.IsNullOrEmpty(_lastBssid)
            && _lastBssid != current.Bssid)
        {
            var roaming = new RoamingEvent
            {
                PreviousBssid = _lastBssid,
                NewBssid = current.Bssid,
                Ssid = current.Ssid,
                PreviousSignalQuality = _lastSignal,
                NewSignalQuality = current.SignalQuality,
                PreviousChannel = _lastChannel,
                NewChannel = current.Channel
            };

            _logger.LogInformation(
                "Roaming detected: {OldBssid} -> {NewBssid} (signal: {OldSig}% -> {NewSig}%)",
                _lastBssid, current.Bssid, _lastSignal, current.SignalQuality);

            await _repository.SaveRoamingEventAsync(roaming, ct);

            var alert = new AlertEvent
            {
                AlertType = "Roaming",
                Severity = "Info",
                Message = $"Roamed from {_lastBssid} to {current.Bssid}",
                Details = $"Ch {_lastChannel} -> Ch {current.Channel}, Signal {_lastSignal}% -> {current.SignalQuality}%"
            };
            await _repository.SaveAlertAsync(alert, ct);
            _messenger.Send(new AlertFiredMessage(alert));
        }

        _lastBssid = current.Bssid;
        _lastSignal = current.SignalQuality;
        _lastChannel = current.Channel;
    }
}
