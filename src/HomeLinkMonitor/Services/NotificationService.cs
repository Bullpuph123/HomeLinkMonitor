using Microsoft.Extensions.Logging;
using Microsoft.Toolkit.Uwp.Notifications;

namespace HomeLinkMonitor.Services;

public interface INotificationService
{
    void ShowNotification(string severity, string message);
}

public class NotificationService : INotificationService
{
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(ILogger<NotificationService> logger)
    {
        _logger = logger;
    }

    public void ShowNotification(string severity, string message)
    {
        try
        {
            var icon = severity switch
            {
                "Critical" => "\u26a0\ufe0f",
                "Warning" => "\u26a0\ufe0f",
                _ => "\u2139\ufe0f"
            };

            new ToastContentBuilder()
                .AddText($"HomeLink Monitor - {severity}")
                .AddText(message)
                .SetToastScenario(severity == "Critical" ? ToastScenario.Alarm : ToastScenario.Default)
                .Show();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to show toast notification");
        }
    }
}
