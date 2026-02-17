using System.Windows;
using Application = System.Windows.Application;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using HomeLinkMonitor.Models;

namespace HomeLinkMonitor.ViewModels;

public partial class TrayViewModel : ObservableObject
{
    [RelayCommand]
    private void ShowMainWindow()
    {
        WeakReferenceMessenger.Default.Send(new SwitchWindowModeMessage(false));
    }

    [RelayCommand]
    private void ShowMiniWindow()
    {
        WeakReferenceMessenger.Default.Send(new SwitchWindowModeMessage(true));
    }

    [RelayCommand]
    private static void ExitApplication()
    {
        Application.Current.Shutdown();
    }
}
