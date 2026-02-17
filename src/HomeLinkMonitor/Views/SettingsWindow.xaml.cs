using System.Windows;
using HomeLinkMonitor.ViewModels;

namespace HomeLinkMonitor.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
