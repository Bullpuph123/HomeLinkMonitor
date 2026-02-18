using System.Windows;
using HomeLinkMonitor.ViewModels;

namespace HomeLinkMonitor.Views;

public partial class TracerouteWindow : Window
{
    public TracerouteWindow(TracerouteViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (DataContext is TracerouteViewModel vm && vm.IsRunning)
        {
            vm.CancelCommand.Execute(null);
        }
        base.OnClosing(e);
    }
}
