using System.Windows;
using System.Windows.Input;
using NodusDriver.ViewModels;

namespace NodusDriver.Views;

public partial class MainWindow : Window
{
    private bool _started;

    public MainWindow() => InitializeComponent();

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (_started) return;
        _started = true;

        if (DataContext is MainViewModel vm)
            vm.ScanCommand.Execute(null);
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        else
            DragMove();
    }

    private void OpenDrivers_Click(object sender, RoutedEventArgs e) => MainTabs.SelectedIndex = 1;
    private void OpenHardware_Click(object sender, RoutedEventArgs e) => MainTabs.SelectedIndex = 2;
    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
