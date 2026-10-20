using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace NodusDriver;

public partial class App : Application
{
    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    private static string CrashLogPath
    {
        get
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NODUS Driver",
                "logs");
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, "startup-crash.log");
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        WriteCrash("UI", e.Exception);
        MessageBox.Show(
            $"NODUS Driver could not start correctly.\n\n{e.Exception.Message}\n\nA crash log was saved to:\n{CrashLogPath}",
            "NODUS Driver",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
        Shutdown(-1);
    }

    private static void OnDomainUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
            WriteCrash("AppDomain", exception);
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        WriteCrash("Task", e.Exception);
        e.SetObserved();
    }

    private static void WriteCrash(string source, Exception exception)
    {
        try
        {
            File.AppendAllText(
                CrashLogPath,
                $"[{DateTimeOffset.Now:O}] {source}\n{exception}\n\n");
        }
        catch
        {
            // Crash reporting must never cause a second crash.
        }
    }
}
