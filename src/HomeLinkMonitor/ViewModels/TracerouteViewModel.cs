using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HomeLinkMonitor.Services;

namespace HomeLinkMonitor.ViewModels;

public partial class TracerouteViewModel : ObservableObject
{
    private readonly ITracerouteService _tracerouteService;
    private readonly Dispatcher _dispatcher;
    private CancellationTokenSource? _cts;

    [ObservableProperty] private string _target = "8.8.8.8";
    [ObservableProperty] private string _statusText = "Ready";
    [ObservableProperty] private bool _isRunning;

    public ObservableCollection<TracerouteHop> Hops { get; } = new();

    public TracerouteViewModel(ITracerouteService tracerouteService)
    {
        _tracerouteService = tracerouteService;
        _dispatcher = Dispatcher.CurrentDispatcher;
    }

    [RelayCommand]
    private async Task RunAsync()
    {
        if (string.IsNullOrWhiteSpace(Target)) return;

        Hops.Clear();
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
            IsRunning = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        _cts?.Cancel();
    }
}
