using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using NodusDriver.Services;
using NodusDriver.ViewModels;

namespace NodusDriver.Views;

public partial class RecoveryView : UserControl
{
    public RecoveryView()
    {
        InitializeComponent();
        Loaded += (_, _) => RefreshHistory();
        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible) RefreshHistory();
        };
    }

    private RecoveryViewModel? ViewModel => DataContext as RecoveryViewModel;

    private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshHistory();

    private void RefreshHistory() => ViewModel?.Refresh();

    private void OpenRecoveryFolder_Click(object sender, RoutedEventArgs e)
    {
        var path = RecoveryService.RecoveryRootPath;
        if (!Directory.Exists(path))
        {
            MessageBox.Show(
                "No recovery snapshots have been created yet. The folder will appear after the first protected driver installation.",
                "NODUS Driver",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        OpenFolder(path);
    }

    private void OpenSelectedBackup_Click(object sender, RoutedEventArgs e)
    {
        var path = ViewModel?.SelectedSnapshot?.BackupDirectory;
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            MessageBox.Show(
                "This snapshot does not contain an exported driver package folder.",
                "NODUS Driver",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        OpenFolder(path);
    }

    private void OpenSystemRestore_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("rstrui.exe") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Windows System Restore could not be opened.\n\n{ex.Message}",
                "NODUS Driver",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private static void OpenFolder(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"The recovery folder could not be opened.\n\n{ex.Message}",
                "NODUS Driver",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}
