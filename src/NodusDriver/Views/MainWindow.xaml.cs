using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using NodusDriver.ViewModels;

namespace NodusDriver.Views;

public partial class MainWindow : Window
{
    private bool _started;

    public MainWindow()
    {
        InitializeComponent();
        AddRecoveryTab();
        ReplacePreviewVersion(this);
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (_started) return;
        _started = true;

        ReplacePreviewVersion(this);

        if (DataContext is MainViewModel vm)
            vm.ScanCommand.Execute(null);
    }

    private void AddRecoveryTab()
    {
        var tab = new TabItem
        {
            Header = "Recovery",
            Content = new RecoveryView(),
            Style = (Style)FindResource("NavTabItem")
        };
        MainTabs.Items.Add(tab);
    }

    private static void ReplacePreviewVersion(DependencyObject root)
    {
        var children = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < children; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is TextBlock text && text.Text.Contains("0.2.1", StringComparison.Ordinal))
                text.Text = text.Text.Replace("0.2.1", "0.3.0", StringComparison.Ordinal);

            ReplacePreviewVersion(child);
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        else
            DragMove();
    }

    private void HardwareFilter_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { Tag: string category } && DataContext is MainViewModel vm)
            vm.SelectedHardwareCategory = category;
    }

    private void OpenDrivers_Click(object sender, RoutedEventArgs e) => MainTabs.SelectedIndex = 1;
    private void OpenHardware_Click(object sender, RoutedEventArgs e) => MainTabs.SelectedIndex = 2;
    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
