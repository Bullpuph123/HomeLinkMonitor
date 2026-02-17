using System.Windows;
using System.Windows.Input;
using HomeLinkMonitor.Models;
using HomeLinkMonitor.ViewModels;
using HomeLinkMonitor.Views;
using Microsoft.Extensions.DependencyInjection;

namespace HomeLinkMonitor;

public partial class MainWindow : Window
{
    private readonly IServiceProvider _services;
    private readonly AppConfig _config;

    public MainWindow(MainViewModel viewModel, IServiceProvider services, AppConfig config)
    {
        InitializeComponent();
        DataContext = viewModel;
        _services = services;
        _config = config;

        SettingsButton.Click += SettingsButton_Click;

        // Restore window position
        if (!double.IsNaN(config.MainWindowLeft) && !double.IsNaN(config.MainWindowTop))
        {
            Left = config.MainWindowLeft;
            Top = config.MainWindowTop;
            WindowStartupLocation = WindowStartupLocation.Manual;
        }
        Width = config.MainWindowWidth;
        Height = config.MainWindowHeight;

        // Keyboard shortcuts
        InputBindings.Add(new KeyBinding(viewModel.SwitchToMiniModeCommand, Key.M, ModifierKeys.Control));

        Loaded += (_, _) =>
        {
            // Focus the window
            Focus();
        };
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var settingsWindow = _services.GetRequiredService<SettingsWindow>();
        settingsWindow.Owner = this;
        settingsWindow.ShowDialog();
    }

    protected override void OnLocationChanged(EventArgs e)
    {
        base.OnLocationChanged(e);
        if (WindowState == WindowState.Normal)
        {
            _config.MainWindowLeft = Left;
            _config.MainWindowTop = Top;
        }
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        if (WindowState == WindowState.Normal)
        {
            _config.MainWindowWidth = ActualWidth;
            _config.MainWindowHeight = ActualHeight;
        }
    }
}
