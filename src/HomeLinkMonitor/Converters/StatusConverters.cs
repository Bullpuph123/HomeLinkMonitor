using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using HomeLinkMonitor.Models;
using Color = System.Windows.Media.Color;

namespace HomeLinkMonitor.Converters;

public class SignalToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int signal)
        {
            return signal switch
            {
                >= 70 => new SolidColorBrush(Color.FromRgb(0x66, 0xBB, 0x6A)), // Good green
                >= 40 => new SolidColorBrush(Color.FromRgb(0xFF, 0xB7, 0x4D)), // Warning orange
                > 0 => new SolidColorBrush(Color.FromRgb(0xEF, 0x53, 0x50)),   // Error red
                _ => new SolidColorBrush(Color.FromRgb(0x70, 0x70, 0x80))       // Muted
            };
        }
        return new SolidColorBrush(Color.FromRgb(0x70, 0x70, 0x80));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class LatencyToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double latency)
        {
            return latency switch
            {
                < 30 => new SolidColorBrush(Color.FromRgb(0x66, 0xBB, 0x6A)),
                < 60 => new SolidColorBrush(Color.FromRgb(0x4F, 0xC3, 0xF7)),
                < 100 => new SolidColorBrush(Color.FromRgb(0xFF, 0xB7, 0x4D)),
                _ => new SolidColorBrush(Color.FromRgb(0xEF, 0x53, 0x50))
            };
        }
        return new SolidColorBrush(Color.FromRgb(0x70, 0x70, 0x80));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class ConnectionStatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ConnectionStatus status)
        {
            return status switch
            {
                ConnectionStatus.Excellent => new SolidColorBrush(Color.FromRgb(0x66, 0xBB, 0x6A)),
                ConnectionStatus.Good => new SolidColorBrush(Color.FromRgb(0x66, 0xBB, 0x6A)),
                ConnectionStatus.Fair => new SolidColorBrush(Color.FromRgb(0xFF, 0xB7, 0x4D)),
                ConnectionStatus.Poor => new SolidColorBrush(Color.FromRgb(0xEF, 0x53, 0x50)),
                ConnectionStatus.Disconnected => new SolidColorBrush(Color.FromRgb(0xEF, 0x53, 0x50)),
                ConnectionStatus.NoInternet => new SolidColorBrush(Color.FromRgb(0xFF, 0xB7, 0x4D)),
                _ => new SolidColorBrush(Color.FromRgb(0x70, 0x70, 0x80))
            };
        }
        return new SolidColorBrush(Color.FromRgb(0x70, 0x70, 0x80));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class ConnectionStatusToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ConnectionStatus status)
        {
            return status switch
            {
                ConnectionStatus.Excellent => "Excellent",
                ConnectionStatus.Good => "Good",
                ConnectionStatus.Fair => "Fair",
                ConnectionStatus.Poor => "Poor",
                ConnectionStatus.Disconnected => "Disconnected",
                ConnectionStatus.NoInternet => "No Internet",
                _ => "Unknown"
            };
        }
        return "Unknown";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
            return b ? Visibility.Visible : Visibility.Collapsed;
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value != null ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b) return !b;
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b) return !b;
        return false;
    }
}
