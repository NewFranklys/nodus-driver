using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using NodusDriver.Models;

namespace NodusDriver.Services;

public sealed class ElevatedInstallLauncher
{
    private readonly LogService _log;
    private readonly InstalledDriverService _drivers;

    public ElevatedInstallLauncher(LogService log)
    {
        _log = log;
        _drivers = new InstalledDriverService(log);
    }

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

        IReadOnlyList<InstalledDriver> previousDrivers;
        try
        {
            var installed = await _drivers.GetInstalledDriversAsync();
            previousDrivers = FindBackupCandidates(updates, installed);
            _log.Info($"Matched {previousDrivers.Count} installed driver package(s) for recovery protection.");
        }
        catch (Exception ex)
        {
            _log.Error("Could not enumerate installed drivers for recovery protection.", ex);
            previousDrivers = [];
        }

        var request = new ElevatedInstallRequest(updates, previousDrivers, resultPath);
        await File.WriteAllTextAsync(requestPath, JsonSerializer.Serialize(request));

        try
        {
            var executablePath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(executablePath))
                return new InstallSummary(false, false, false, -1, "NODUS could not locate its own executable.");

            _log.Info($"Requesting elevation to protect and install {updates.Count} driver update(s).");

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

    private static IReadOnlyList<InstalledDriver> FindBackupCandidates(
        IReadOnlyList<DriverUpdate> updates,
        IReadOnlyList<InstalledDriver> installedDrivers)
    {
        var result = new List<InstalledDriver>();

        foreach (var update in updates)
        {
            var best = FindBestInstalledMatch(update, installedDrivers);
            if (best is not null && !result.Any(x =>
                    string.Equals(x.InfName, best.InfName, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(x.DeviceName, best.DeviceName, StringComparison.OrdinalIgnoreCase)))
                result.Add(best);
        }

        return result;
    }

    private static InstalledDriver? FindBestInstalledMatch(
        DriverUpdate update,
        IReadOnlyList<InstalledDriver> installedDrivers)
    {
        InstalledDriver? best = null;
        var bestScore = 0;

        var model = Normalize(update.Model);
        var title = Normalize(update.Title);
        var manufacturer = Normalize(update.Manufacturer);
        var driverClass = Normalize(update.DriverClass);

        foreach (var candidate in installedDrivers)
        {
            var score = 0;
            var deviceName = Normalize(candidate.DeviceName);
            var candidateManufacturer = Normalize(candidate.Manufacturer);
            var candidateClass = Normalize(candidate.DeviceClass);

            if (model.Length >= 4 && deviceName.Length >= 4 &&
                (deviceName.Contains(model, StringComparison.Ordinal) || model.Contains(deviceName, StringComparison.Ordinal)))
                score += 100;

            if (deviceName.Length >= 5 && title.Contains(deviceName, StringComparison.Ordinal))
                score += 80;

            if (manufacturer.Length >= 3 && candidateManufacturer.Length >= 3 &&
                (manufacturer.Contains(candidateManufacturer, StringComparison.Ordinal) || candidateManufacturer.Contains(manufacturer, StringComparison.Ordinal)))
                score += 25;

            if (driverClass.Length > 0 && driverClass == candidateClass)
                score += 20;

            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        return bestScore >= 80 ? best : null;
    }

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        return new string(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
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
