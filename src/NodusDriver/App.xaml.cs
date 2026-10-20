using System.IO;
using System.Windows;
using System.Windows.Threading;
using NodusDriver.Services;
using NodusDriver.Views;

namespace NodusDriver;

public partial class App : Application
{
    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (e.Args.Length >= 2 && string.Equals(e.Args[0], "--elevated-install", StringComparison.OrdinalIgnoreCase))
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var exitCode = ElevatedInstallRunner.RunAsync(e.Args[1]).GetAwaiter().GetResult();
            Shutdown(exitCode);
            return;
        }

        var window = new MainWindow();
        MainWindow = window;
        window.Show();
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
