using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HomeLinkMonitor.Models;
using HomeLinkMonitor.Services;
using HomeLinkMonitor.Views;

namespace HomeLinkMonitor.ViewModels;

public partial class TracerouteViewModel : ObservableObject
{
    private readonly ITracerouteService _tracerouteService;
    private readonly IGeoIpService _geoIpService;
    private readonly AppConfig _config;
    private readonly Dispatcher _dispatcher;
    private CancellationTokenSource? _cts;

    [ObservableProperty] private string _target = "8.8.8.8";
    [ObservableProperty] private string _statusText = "Ready";
    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private bool _hasHops;

    public ObservableCollection<TracerouteHop> Hops { get; } = new();

    public TracerouteViewModel(ITracerouteService tracerouteService, IGeoIpService geoIpService, AppConfig config)
    {
        _tracerouteService = tracerouteService;
        _geoIpService = geoIpService;
        _config = config;
        _dispatcher = Dispatcher.CurrentDispatcher;
    }

    partial void OnIsRunningChanged(bool value) => ShowMapCommand.NotifyCanExecuteChanged();
    partial void OnHasHopsChanged(bool value) => ShowMapCommand.NotifyCanExecuteChanged();

    [RelayCommand]
    private async Task RunAsync()
    {
        if (string.IsNullOrWhiteSpace(Target)) return;

        Hops.Clear();
        HasHops = false;
        IsRunning = true;
        StatusText = $"Tracing route to {Target}...";
        _cts = new CancellationTokenSource();

        try
        {
            await foreach (var hop in _tracerouteService.RunAsync(Target, ct: _cts.Token))
            {
                _dispatcher.Invoke(() => Hops.Add(hop));
                StatusText = $"Hop {hop.Hop}: {hop.Address}";
            }
            StatusText = $"Trace complete - {Hops.Count} hops";
        }
        catch (OperationCanceledException)
        {
            StatusText = $"Cancelled after {Hops.Count} hops";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            HasHops = Hops.Count > 0;
            IsRunning = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    [RelayCommand(CanExecute = nameof(CanShowMap))]
    private void ShowMap()
    {
        var hopsSnapshot = Hops.ToList().AsReadOnly();
        var mapWindow = new TracerouteMapWindow(hopsSnapshot, Target, _geoIpService, _config);
        mapWindow.Owner = System.Windows.Application.Current.Windows
            .OfType<TracerouteWindow>()
            .FirstOrDefault();
        mapWindow.ShowDialog();
    }

    private bool CanShowMap() => HasHops && !IsRunning;

    [RelayCommand]
    private void Cancel()
    {
        _cts?.Cancel();
    }
}
