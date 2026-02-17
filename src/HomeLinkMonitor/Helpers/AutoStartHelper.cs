using Microsoft.Win32;
using Microsoft.Extensions.Logging;

namespace HomeLinkMonitor.Helpers;

public static class AutoStartHelper
{
    private const string RegistryKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "HomeLinkMonitor";

    public static void SetAutoStart(bool enabled, ILogger? logger = null)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKey, true);
            if (key == null) return;

            if (enabled)
            {
                var exePath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exePath))
                {
                    key.SetValue(AppName, $"\"{exePath}\" --minimized");
                }
            }
            else
            {
                key.DeleteValue(AppName, false);
            }
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to set auto-start registry key");
        }
    }

    public static bool IsAutoStartEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKey);
            return key?.GetValue(AppName) != null;
        }
        catch
        {
            return false;
        }
    }
}
