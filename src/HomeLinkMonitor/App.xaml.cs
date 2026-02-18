using System.IO;
using System.Net.Http;
using System.Windows;
using Application = System.Windows.Application;
using CommunityToolkit.Mvvm.Messaging;
using HomeLinkMonitor.Data;
using HomeLinkMonitor.Models;
using HomeLinkMonitor.Services;
using HomeLinkMonitor.ViewModels;
using HomeLinkMonitor.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HomeLinkMonitor;

public partial class App : Application, IRecipient<SwitchWindowModeMessage>
{
    private IHost? _host;
    private MainWindow? _mainWindow;
    private MiniWindow? _miniWindow;
    private H.NotifyIcon.TaskbarIcon? _trayIcon;
    private AppConfig _config = new();

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Prevent auto-shutdown so tray can keep app alive
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _config = AppConfig.Load();

        _host = Host.CreateDefaultBuilder()
            .ConfigureLogging(logging =>
            {
                logging.SetMinimumLevel(LogLevel.Information);
                logging.AddDebug();
            })
            .ConfigureServices(services =>
            {
                // Config
                services.AddSingleton(_config);

                // Messenger
                services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);

                // Database
                var appData = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "HomeLinkMonitor");
                Directory.CreateDirectory(appData);
                var dbPath = Path.Combine(appData, "homelink.db");

                services.AddDbContextFactory<AppDbContext>(options =>
                    options.UseSqlite($"Data Source={dbPath}"));

                services.AddSingleton<DataRepository>();

                // Services
                services.AddSingleton<IWifiMetricsProvider, WifiMetricsProvider>();
                services.AddSingleton<INetworkInterfaceProvider, NetworkInterfaceProvider>();
                services.AddSingleton<IPingProbe, PingProbe>();
                services.AddSingleton<IDnsProbe, DnsProbe>();
                services.AddSingleton<IHttpProbe, HttpProbe>();
                services.AddSingleton<HttpClient>();
                services.AddSingleton<IAlertEngine, AlertEngine>();
                services.AddSingleton<IRoamingDetector, RoamingDetector>();
                services.AddSingleton<ITracerouteService, TracerouteService>();
                services.AddSingleton<IGeoIpService, GeoIpService>();
                services.AddSingleton<IExportService, ExportService>();
                services.AddSingleton<INotificationService, NotificationService>();

                // Background services
                services.AddHostedService<MonitoringOrchestrator>();
                services.AddHostedService<DataRetentionService>();

                // ViewModels
                services.AddSingleton<MainViewModel>();
                services.AddSingleton<MiniViewModel>();
                services.AddSingleton<TrayViewModel>();
                services.AddTransient<SettingsViewModel>();
                services.AddTransient<TracerouteViewModel>();

                // Windows
                services.AddTransient<MainWindow>();
                services.AddTransient<MiniWindow>();
                services.AddTransient<SettingsWindow>();
                services.AddTransient<TracerouteWindow>();
            })
            .Build();

        // Ensure database is created with WAL mode
        using (var scope = _host.Services.CreateScope())
        {
            var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
            await using var db = await contextFactory.CreateDbContextAsync();
            await db.Database.EnsureCreatedAsync();
            await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
        }

        // Apply saved theme (default App.xaml loads Dark, switch if config says Light)
        if (_config.Theme == "Light")
        {
            SettingsViewModel.ApplyTheme("Light");
        }

        // Register for window mode switch messages
        WeakReferenceMessenger.Default.Register(this);

        // Eagerly resolve singleton ViewModels so they start listening for messages immediately
        _ = _host.Services.GetRequiredService<MiniViewModel>();

        // Create tray icon
        SetupTrayIcon();

        // Start the host (background services)
        await _host.StartAsync();

        // Show main window
        ShowMainWindow();
    }

    private void SetupTrayIcon()
    {
        var iconStream = GetResourceStream(new Uri("pack://application:,,,/Assets/app.ico"))?.Stream;
        var icon = iconStream != null
            ? new System.Drawing.Icon(iconStream)
            : System.Drawing.SystemIcons.Application;

        _trayIcon = new H.NotifyIcon.TaskbarIcon
        {
            ToolTipText = "HomeLink Monitor",
            Icon = icon,
            ContextMenu = CreateTrayContextMenu()
        };

        _trayIcon.TrayMouseDoubleClick += (_, _) => ShowMainWindow();
        _trayIcon.ForceCreate();
    }

    private System.Windows.Controls.ContextMenu CreateTrayContextMenu()
    {
        var menu = new System.Windows.Controls.ContextMenu();

        var showMain = new System.Windows.Controls.MenuItem { Header = "Show Dashboard" };
        showMain.Click += (_, _) => ShowMainWindow();
        menu.Items.Add(showMain);

        var showMini = new System.Windows.Controls.MenuItem { Header = "Mini Mode" };
        showMini.Click += (_, _) => ShowMiniWindow();
        menu.Items.Add(showMini);

        menu.Items.Add(new System.Windows.Controls.Separator());

        var exit = new System.Windows.Controls.MenuItem { Header = "Exit" };
        exit.Click += (_, _) => Shutdown();
        menu.Items.Add(exit);

        return menu;
    }

    private void ShowMainWindow()
    {
        _miniWindow?.Hide();

        if (_mainWindow == null || !_mainWindow.IsLoaded)
        {
            _mainWindow = _host!.Services.GetRequiredService<MainWindow>();
            _mainWindow.Closing += MainWindow_Closing;
        }

        _mainWindow.Show();
        _mainWindow.Activate();
        if (_mainWindow.WindowState == WindowState.Minimized)
            _mainWindow.WindowState = WindowState.Normal;
    }

    private void ShowMiniWindow()
    {
        if (_miniWindow == null || !_miniWindow.IsLoaded)
        {
            _miniWindow = _host!.Services.GetRequiredService<MiniWindow>();
        }

        _mainWindow?.Hide();
        _miniWindow.Show();
        _miniWindow.Activate();
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_config.MinimizeToTray)
        {
            e.Cancel = true;
            _mainWindow?.Hide();
        }
    }

    public void Receive(SwitchWindowModeMessage message)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (message.Value)
                ShowMiniWindow();
            else
                ShowMainWindow();
        });
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        _config.Save();

        _trayIcon?.Dispose();

        if (_host != null)
        {
            await _host.StopAsync(TimeSpan.FromSeconds(5));
            _host.Dispose();
        }
        base.OnExit(e);
    }
}
