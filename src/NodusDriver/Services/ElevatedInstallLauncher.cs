using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using NodusDriver.Models;

namespace NodusDriver.Services;

public sealed class ElevatedInstallLauncher
{
    private readonly LogService _log;

    public ElevatedInstallLauncher(LogService log) => _log = log;

    public async Task<InstallSummary> InstallAsync(
        IReadOnlyList<DriverUpdate> updates,
        Action? onElevationGranted = null)
    {
        if (updates.Count == 0)
            return new InstallSummary(false, false, false, 0, "No updates selected.");

        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NODUS Driver",
            "install");
        Directory.CreateDirectory(directory);

        var token = Guid.NewGuid().ToString("N");
        var requestPath = Path.Combine(directory, $"{token}.request.json");
        var resultPath = Path.Combine(directory, $"{token}.result.json");
        var request = new ElevatedInstallRequest(updates, resultPath);

        await File.WriteAllTextAsync(requestPath, JsonSerializer.Serialize(request));

        try
        {
            var executablePath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(executablePath))
                return new InstallSummary(false, false, false, -1, "NODUS could not locate its own executable.");

            _log.Info($"Requesting elevation to install {updates.Count} driver update(s).");

            var startInfo = new ProcessStartInfo(executablePath)
            {
                UseShellExecute = true,
                Verb = "runas",
                Arguments = $"--elevated-install \"{requestPath}\"",
                WorkingDirectory = AppContext.BaseDirectory
            };

            using var process = Process.Start(startInfo);
            if (process is null)
                return new InstallSummary(false, false, false, -1, "Windows could not start the elevated installer process.");

            _log.Info($"Elevation accepted. Elevated installer PID={process.Id}.");
            onElevationGranted?.Invoke();

            await process.WaitForExitAsync();

            if (!File.Exists(resultPath))
            {
                _log.Info($"Elevated installer exited with code {process.ExitCode} without a result file.");
                return new InstallSummary(false, false, false, process.ExitCode,
                    "The elevated installer ended without returning a result. Check the NODUS log for details.");
            }

            var json = await File.ReadAllTextAsync(resultPath);
            var result = JsonSerializer.Deserialize<InstallSummary>(json);
            return result ?? new InstallSummary(false, false, false, -1, "NODUS could not read the installation result.");
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            _log.Info("Driver installation cancelled at the Windows UAC prompt.");
            return new InstallSummary(false, false, false, 1223, "Installation cancelled. No drivers were changed.");
        }
        catch (Exception ex)
        {
            _log.Error("Could not start elevated driver installation.", ex);
            return new InstallSummary(false, false, false, ex.HResult, ex.Message);
        }
        finally
        {
            TryDelete(requestPath);
            TryDelete(resultPath);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
            // Temporary cleanup must not turn an install result into an error.
        }
    }
}
