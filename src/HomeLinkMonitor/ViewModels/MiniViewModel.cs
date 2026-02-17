using System.Windows;
using System.Windows.Threading;
using Application = System.Windows.Application;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using HomeLinkMonitor.Models;

namespace HomeLinkMonitor.ViewModels;

public partial class MiniViewModel : ObservableObject, IRecipient<MonitoringUpdateMessage>
{
    private readonly Dispatcher _dispatcher;

    [ObservableProperty] private ConnectionStatus _overallStatus = ConnectionStatus.Unknown;
    [ObservableProperty] private string _ssid = "--";
    [ObservableProperty] private int _signalQuality;
    [ObservableProperty] private double _gatewayLatency;
    [ObservableProperty] private double _dnsLatency;
    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private string _statusText = "...";

    public MiniViewModel()
    {
        _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        WeakReferenceMessenger.Default.Register(this);
    }

    public void Receive(MonitoringUpdateMessage message)
    {
        _dispatcher.BeginInvoke(() =>
        {
            var s = message.Value;
            OverallStatus = s.OverallStatus;
            StatusText = s.OverallStatus switch
            {
                ConnectionStatus.Excellent => "Excellent",
                ConnectionStatus.Good => "Good",
                ConnectionStatus.Fair => "Fair",
                ConnectionStatus.Poor => "Poor",
                ConnectionStatus.Disconnected => "Disconnected",
                ConnectionStatus.NoInternet => "No Internet",
                _ => "..."
            };

            if (s.Wifi != null)
            {
                Ssid = s.Wifi.IsConnected ? s.Wifi.Ssid : "N/A";
                SignalQuality = s.Wifi.SignalQuality;
                IsConnected = s.Wifi.IsConnected;
            }

            var gw = s.PingResults.FirstOrDefault(p => p.TargetLabel == "Gateway");
            GatewayLatency = gw?.LatencyMs ?? 0;

            var dns = s.PingResults.FirstOrDefault(p => p.TargetLabel == "DNS1");
            DnsLatency = dns?.LatencyMs ?? 0;
        });
    }

    [RelayCommand]
    private void SwitchToMainMode()
    {
        WeakReferenceMessenger.Default.Send(new SwitchWindowModeMessage(false));
    }
}
