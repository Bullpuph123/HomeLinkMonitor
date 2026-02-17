using CommunityToolkit.Mvvm.Messaging.Messages;

namespace HomeLinkMonitor.Models;

/// <summary>
/// Published by MonitoringOrchestrator each cycle.
/// </summary>
public class MonitoringUpdateMessage(MonitoringSnapshot snapshot)
    : ValueChangedMessage<MonitoringSnapshot>(snapshot);

/// <summary>
/// Request to switch between main and mini window.
/// </summary>
public class SwitchWindowModeMessage(bool isMiniMode)
    : ValueChangedMessage<bool>(isMiniMode);

/// <summary>
/// Published when an alert fires.
/// </summary>
public class AlertFiredMessage(AlertEvent alert)
    : ValueChangedMessage<AlertEvent>(alert);
