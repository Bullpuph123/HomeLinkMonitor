using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using Application = System.Windows.Application;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using HomeLinkMonitor.Models;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace HomeLinkMonitor.ViewModels;

public partial class MiniViewModel : ObservableObject, IRecipient<MonitoringUpdateMessage>
{
    private readonly Dispatcher _dispatcher;
    private const int MaxMiniPoints = 60; // 5 min at 5s intervals

    private readonly ObservableCollection<DateTimePoint> _miniLatencyPoints = [];

    [ObservableProperty] private ConnectionStatus _overallStatus = ConnectionStatus.Unknown;
    [ObservableProperty] private string _ssid = "--";
    [ObservableProperty] private int _signalQuality;
    [ObservableProperty] private double _gatewayLatency;
    [ObservableProperty] private double _dnsLatency;
    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private string _statusText = "...";

    public ISeries[] MiniLatencySeries { get; }

    public Axis[] MiniXAxes { get; } =
    [
        new DateTimeAxis(TimeSpan.FromSeconds(30), _ => string.Empty)
        {
            LabelsPaint = null,
            SeparatorsPaint = null,
            ShowSeparatorLines = false,
            IsVisible = false
        }
    ];

    public Axis[] MiniYAxes { get; } =
    [
        new Axis
        {
            LabelsPaint = null,
            NamePaint = null,
            SeparatorsPaint = null,
            ShowSeparatorLines = false,
            IsVisible = false,
            MinLimit = 0
        }
    ];

    public MiniViewModel()
    {
        _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

        MiniLatencySeries =
        [
            new LineSeries<DateTimePoint>
            {
                Values = _miniLatencyPoints,
                Stroke = new SolidColorPaint(SKColors.LimeGreen, 1.5f),
                Fill = null,
                GeometrySize = 0,
                LineSmoothness = 0.3
            }
        ];

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
            AddMiniPoint(DateTime.Now, gw?.LatencyMs ?? 0);

            var dns = s.PingResults.FirstOrDefault(p => p.TargetLabel == "DNS1");
            DnsLatency = dns?.LatencyMs ?? 0;
        });
    }

    private void AddMiniPoint(DateTime time, double value)
    {
        _miniLatencyPoints.Add(new DateTimePoint(time, value));
        while (_miniLatencyPoints.Count > MaxMiniPoints)
            _miniLatencyPoints.RemoveAt(0);
    }

    [RelayCommand]
    private void SwitchToMainMode()
    {
        WeakReferenceMessenger.Default.Send(new SwitchWindowModeMessage(false));
    }
}
