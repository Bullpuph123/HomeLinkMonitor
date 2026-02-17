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

public partial class MainViewModel : ObservableObject, IRecipient<MonitoringUpdateMessage>, IRecipient<AlertFiredMessage>
{
    private readonly Dispatcher _dispatcher;
    private const int MaxChartPoints = 180; // 15 min at 5s intervals

    // Observable collections for chart data
    private readonly ObservableCollection<DateTimePoint> _gatewayLatencyPoints = [];
    private readonly ObservableCollection<DateTimePoint> _dns1LatencyPoints = [];
    private readonly ObservableCollection<DateTimePoint> _dns2LatencyPoints = [];
    private readonly ObservableCollection<DateTimePoint> _signalPoints = [];

    // --- Connection Status ---
    [ObservableProperty] private ConnectionStatus _overallStatus = ConnectionStatus.Unknown;
    [ObservableProperty] private string _statusText = "Initializing...";

    // --- Wi-Fi Info ---
    [ObservableProperty] private string _ssid = "--";
    [ObservableProperty] private string _bssid = "--";
    [ObservableProperty] private int _signalQuality;
    [ObservableProperty] private int _rssiDbm;
    [ObservableProperty] private string _band = "--";
    [ObservableProperty] private int _channel;
    [ObservableProperty] private string _phyType = "--";
    [ObservableProperty] private int _linkSpeedMbps;
    [ObservableProperty] private bool _isWifiConnected;

    // --- Network Info ---
    [ObservableProperty] private string _localIp = "--";
    [ObservableProperty] private string _gatewayIp = "--";
    [ObservableProperty] private string _dnsServers = "--";
    [ObservableProperty] private string _macAddress = "--";
    [ObservableProperty] private string _adapterName = "--";

    // --- Latency ---
    [ObservableProperty] private double _gatewayLatency;
    [ObservableProperty] private double _dns1Latency;
    [ObservableProperty] private double _dns2Latency;
    [ObservableProperty] private bool _gatewayReachable;
    [ObservableProperty] private bool _dns1Reachable;
    [ObservableProperty] private bool _dns2Reachable;

    // --- HTTP Probe ---
    [ObservableProperty] private bool _internetReachable;
    [ObservableProperty] private double _httpLatency;
    [ObservableProperty] private bool _isCaptivePortal;

    // --- DNS Probe ---
    [ObservableProperty] private double _dnsQueryLatency1;
    [ObservableProperty] private double _dnsQueryLatency2;

    // --- Stats ---
    [ObservableProperty] private int _totalPings;
    [ObservableProperty] private int _failedPings;
    [ObservableProperty] private double _packetLossPercent;
    [ObservableProperty] private DateTime _lastUpdate = DateTime.UtcNow;
    [ObservableProperty] private string _uptimeText = "0:00:00";

    private DateTime _monitorStartTime = DateTime.UtcNow;
    private int _totalPingCount;
    private int _failedPingCount;

    // Alert events log
    public ObservableCollection<AlertEvent> RecentAlerts { get; } = [];
    private const int MaxAlerts = 100;

    public ISeries[] LatencySeries { get; }
    public ISeries[] SignalSeries { get; }

    public Axis[] LatencyXAxes { get; }
    public Axis[] LatencyYAxes { get; }
    public Axis[] SignalXAxes { get; }
    public Axis[] SignalYAxes { get; }

    public MainViewModel()
    {
        _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

        // Configure chart series
        LatencySeries =
        [
            new LineSeries<DateTimePoint>
            {
                Name = "Gateway",
                Values = _gatewayLatencyPoints,
                Stroke = new SolidColorPaint(SKColors.LimeGreen, 2),
                Fill = null,
                GeometrySize = 0,
                LineSmoothness = 0.3
            },
            new LineSeries<DateTimePoint>
            {
                Name = "DNS 1",
                Values = _dns1LatencyPoints,
                Stroke = new SolidColorPaint(new SKColor(0x4F, 0xC3, 0xF7), 2),
                Fill = null,
                GeometrySize = 0,
                LineSmoothness = 0.3
            },
            new LineSeries<DateTimePoint>
            {
                Name = "DNS 2",
                Values = _dns2LatencyPoints,
                Stroke = new SolidColorPaint(new SKColor(0xFF, 0xB7, 0x4D), 2),
                Fill = null,
                GeometrySize = 0,
                LineSmoothness = 0.3
            }
        ];

        SignalSeries =
        [
            new LineSeries<DateTimePoint>
            {
                Name = "Signal",
                Values = _signalPoints,
                Stroke = new SolidColorPaint(new SKColor(0x4F, 0xC3, 0xF7), 2),
                Fill = new SolidColorPaint(new SKColor(0x4F, 0xC3, 0xF7, 40)),
                GeometrySize = 0,
                LineSmoothness = 0.3
            }
        ];

        var labelColor = new SKColor(0xA0, 0xA0, 0xB0);

        LatencyXAxes =
        [
            new DateTimeAxis(TimeSpan.FromSeconds(30), d => d.ToString("HH:mm:ss"))
            {
                LabelsPaint = new SolidColorPaint(labelColor) { SKTypeface = SKTypeface.Default },
                SeparatorsPaint = new SolidColorPaint(new SKColor(0x40, 0x40, 0x60, 80)),
                TextSize = 10
            }
        ];

        LatencyYAxes =
        [
            new Axis
            {
                Name = "ms",
                NamePaint = new SolidColorPaint(labelColor),
                LabelsPaint = new SolidColorPaint(labelColor) { SKTypeface = SKTypeface.Default },
                SeparatorsPaint = new SolidColorPaint(new SKColor(0x40, 0x40, 0x60, 80)),
                TextSize = 10,
                MinLimit = 0
            }
        ];

        SignalXAxes =
        [
            new DateTimeAxis(TimeSpan.FromSeconds(30), d => d.ToString("HH:mm:ss"))
            {
                LabelsPaint = new SolidColorPaint(labelColor) { SKTypeface = SKTypeface.Default },
                SeparatorsPaint = new SolidColorPaint(new SKColor(0x40, 0x40, 0x60, 80)),
                TextSize = 10
            }
        ];

        SignalYAxes =
        [
            new Axis
            {
                Name = "%",
                NamePaint = new SolidColorPaint(labelColor),
                LabelsPaint = new SolidColorPaint(labelColor) { SKTypeface = SKTypeface.Default },
                SeparatorsPaint = new SolidColorPaint(new SKColor(0x40, 0x40, 0x60, 80)),
                TextSize = 10,
                MinLimit = 0,
                MaxLimit = 100
            }
        ];

        // Register for messages
        WeakReferenceMessenger.Default.RegisterAll(this);

        // Start uptime timer
        var uptimeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        uptimeTimer.Tick += (_, _) =>
        {
            var elapsed = DateTime.UtcNow - _monitorStartTime;
            UptimeText = elapsed.ToString(@"h\:mm\:ss");
        };
        uptimeTimer.Start();
    }

    public void Receive(MonitoringUpdateMessage message)
    {
        _dispatcher.BeginInvoke(() => UpdateFromSnapshot(message.Value));
    }

    private void UpdateFromSnapshot(MonitoringSnapshot snapshot)
    {
        OverallStatus = snapshot.OverallStatus;
        StatusText = snapshot.OverallStatus switch
        {
            ConnectionStatus.Excellent => "Excellent",
            ConnectionStatus.Good => "Good",
            ConnectionStatus.Fair => "Fair",
            ConnectionStatus.Poor => "Poor",
            ConnectionStatus.Disconnected => "Disconnected",
            ConnectionStatus.NoInternet => "No Internet",
            _ => "Unknown"
        };

        LastUpdate = snapshot.Timestamp;

        // Wi-Fi
        if (snapshot.Wifi != null)
        {
            IsWifiConnected = snapshot.Wifi.IsConnected;
            Ssid = snapshot.Wifi.IsConnected ? snapshot.Wifi.Ssid : "Not Connected";
            Bssid = snapshot.Wifi.Bssid;
            SignalQuality = snapshot.Wifi.SignalQuality;
            RssiDbm = snapshot.Wifi.RssiDbm;
            Band = snapshot.Wifi.Band;
            Channel = snapshot.Wifi.Channel;
            PhyType = snapshot.Wifi.PhyType;
            LinkSpeedMbps = snapshot.Wifi.LinkSpeedMbps;

            // Add signal to chart
            AddChartPoint(_signalPoints, snapshot.Timestamp, snapshot.Wifi.SignalQuality);
        }

        // Network
        if (snapshot.Network != null)
        {
            LocalIp = snapshot.Network.LocalIpAddress;
            GatewayIp = snapshot.Network.GatewayAddress;
            DnsServers = snapshot.Network.DnsServers;
            MacAddress = snapshot.Network.MacAddress;
            AdapterName = snapshot.Network.AdapterName;
        }

        // Ping results
        foreach (var ping in snapshot.PingResults)
        {
            _totalPingCount++;
            if (!ping.IsSuccess)
                _failedPingCount++;

            switch (ping.TargetLabel)
            {
                case "Gateway":
                    GatewayLatency = ping.LatencyMs ?? 0;
                    GatewayReachable = ping.IsSuccess;
                    AddChartPoint(_gatewayLatencyPoints, snapshot.Timestamp, ping.LatencyMs ?? 0);
                    break;
                case "DNS1":
                    Dns1Latency = ping.LatencyMs ?? 0;
                    Dns1Reachable = ping.IsSuccess;
                    AddChartPoint(_dns1LatencyPoints, snapshot.Timestamp, ping.LatencyMs ?? 0);
                    break;
                case "DNS2":
                    Dns2Latency = ping.LatencyMs ?? 0;
                    Dns2Reachable = ping.IsSuccess;
                    AddChartPoint(_dns2LatencyPoints, snapshot.Timestamp, ping.LatencyMs ?? 0);
                    break;
            }
        }

        TotalPings = _totalPingCount;
        FailedPings = _failedPingCount;
        PacketLossPercent = _totalPingCount > 0
            ? Math.Round(100.0 * _failedPingCount / _totalPingCount, 1)
            : 0;

        // DNS probe
        if (snapshot.DnsResults.Count > 0)
        {
            var dns1 = snapshot.DnsResults.FirstOrDefault(d => d.DnsServer.Contains("8.8"));
            var dns2 = snapshot.DnsResults.FirstOrDefault(d => d.DnsServer.Contains("1.1"));
            DnsQueryLatency1 = dns1?.LatencyMs ?? 0;
            DnsQueryLatency2 = dns2?.LatencyMs ?? 0;
        }

        // HTTP probe
        if (snapshot.HttpProbe != null)
        {
            InternetReachable = snapshot.HttpProbe.IsSuccess;
            HttpLatency = snapshot.HttpProbe.LatencyMs ?? 0;
            IsCaptivePortal = snapshot.HttpProbe.IsCaptivePortal;
        }
    }

    private void AddChartPoint(ObservableCollection<DateTimePoint> collection, DateTime time, double value)
    {
        collection.Add(new DateTimePoint(time.ToLocalTime(), value));
        while (collection.Count > MaxChartPoints)
            collection.RemoveAt(0);
    }

    public void Receive(AlertFiredMessage message)
    {
        _dispatcher.BeginInvoke(() =>
        {
            RecentAlerts.Insert(0, message.Value);
            while (RecentAlerts.Count > MaxAlerts)
                RecentAlerts.RemoveAt(RecentAlerts.Count - 1);
        });
    }

    [RelayCommand]
    private void SwitchToMiniMode()
    {
        WeakReferenceMessenger.Default.Send(new SwitchWindowModeMessage(true));
    }
}
