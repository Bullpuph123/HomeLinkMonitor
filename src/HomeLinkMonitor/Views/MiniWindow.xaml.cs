using System.Windows;
using System.Windows.Input;
using HomeLinkMonitor.ViewModels;

namespace HomeLinkMonitor.Views;

public partial class MiniWindow : Window
{
    public MiniWindow(MiniViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
    }

    private void Window_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is MiniViewModel vm)
        {
            vm.SwitchToMainModeCommand.Execute(null);
        }
    }
}
