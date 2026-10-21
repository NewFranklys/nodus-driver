using System.Diagnostics;
using System.IO;
using System.Management;
using System.Text.Json;
using NodusDriver.Models;

namespace NodusDriver.Services;

public sealed class RecoveryService
{
    private const int MaxSnapshots = 10;
    private readonly LogService _log;

    public RecoveryService(LogService log) => _log = log;

    public static string RecoveryRootPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "NODUS Driver",
        "Recovery");

    public async Task<RecoverySnapshot> CreateProtectionSnapshotAsync(
        IReadOnlyList<InstalledDriver> previousDrivers,
        IReadOnlyList<DriverUpdate> updates)
    {
        var id = $"{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}"[..24];
        var snapshotDirectory = Path.Combine(RecoveryRootPath, id);
        var driverDirectory = Path.Combine(snapshotDirectory, "drivers");
        Directory.CreateDirectory(driverDirectory);

        _log.Info($"Creating recovery snapshot {id} before {updates.Count} driver update(s).");

        var (restorePointCreated, restorePointMessage) = await TryCreateRestorePointAsync();
        var records = new List<RecoveryDriverRecord>();
        var backedUp = 0;

        var uniqueDrivers = previousDrivers
            .Where(x => !string.IsNullOrWhiteSpace(x.InfName))
            .GroupBy(x => x.InfName!, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .ToList();

        foreach (var driver in uniqueDrivers)
        {
            var infName = driver.InfName ?? string.Empty;
            if (!infName.StartsWith("oem", StringComparison.OrdinalIgnoreCase) ||
                !infName.EndsWith(".inf", StringComparison.OrdinalIgnoreCase))
            {
                records.Add(new RecoveryDriverRecord(
                    driver.DeviceName,
                    driver.Manufacturer,
                    driver.DriverVersion,
                    infName,
                    false,
                    "Windows inbox driver; no exportable OEM package was reported."));
                continue;
            }

            var export = await ExportDriverPackageAsync(infName, driverDirectory);
            if (export.Success) backedUp++;

            records.Add(new RecoveryDriverRecord(
                driver.DeviceName,
                driver.Manufacturer,
                driver.DriverVersion,
                infName,
                export.Success,
                export.Message));
        }

        var hasProtection = restorePointCreated || backedUp > 0;
        var snapshot = new RecoverySnapshot(
            id,
            DateTimeOffset.Now,
            hasProtection ? "Protected" : "Protection failed",
            restorePointCreated,
            restorePointMessage,
            driverDirectory,
            updates.Count,
            backedUp,
            false,
            false,
            false,
            hasProtection
                ? "Recovery protection was created before Windows Update was allowed to change drivers."
                : "NODUS could not create a restore point or export a matched OEM driver package. No driver changes were started.",
            records);

        await SaveSnapshotAsync(snapshot);
        PruneOldSnapshots();
        return snapshot;
    }

    public async Task<RecoverySnapshot> CompleteSnapshotAsync(RecoverySnapshot snapshot, InstallSummary install)
    {
        var completed = snapshot with
        {
            Status = install.InstallSucceeded ? "Installed" : "Install failed",
            InstallStarted = true,
            InstallSucceeded = install.InstallSucceeded,
            RebootRequired = install.RebootRequired,
            Message = install.Message
        };

        await SaveSnapshotAsync(completed);
        return completed;
    }

    public IReadOnlyList<RecoverySnapshot> LoadSnapshots()
    {
        try
        {
            if (!Directory.Exists(RecoveryRootPath))
                return [];

            var snapshots = new List<RecoverySnapshot>();
            foreach (var file in Directory.EnumerateFiles(RecoveryRootPath, "snapshot.json", SearchOption.AllDirectories))
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var snapshot = JsonSerializer.Deserialize<RecoverySnapshot>(json);
                    if (snapshot is not null)
                        snapshots.Add(snapshot);
                }
                catch (Exception ex)
                {
                    _log.Error($"Could not read recovery snapshot: {file}", ex);
                }
            }

            return snapshots.OrderByDescending(x => x.CreatedAt).ToList();
        }
        catch (Exception ex)
        {
            _log.Error("Could not enumerate recovery history.", ex);
            return [];
        }
    }

    private async Task SaveSnapshotAsync(RecoverySnapshot snapshot)
    {
        var snapshotDirectory = Path.Combine(RecoveryRootPath, snapshot.Id);
        Directory.CreateDirectory(snapshotDirectory);
        var path = Path.Combine(snapshotDirectory, "snapshot.json");
        var options = new JsonSerializerOptions { WriteIndented = true };
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(snapshot, options));
    }

    private async Task<(bool Success, string Message)> TryCreateRestorePointAsync()
    {
        try
        {
            return await Task.Run(() =>
            {
                var options = new ConnectionOptions { EnablePrivileges = true };
                var scope = new ManagementScope(@"\\.\root\default", options);
                scope.Connect();

                using var restoreClass = new ManagementClass(scope, new ManagementPath("SystemRestore"), null);
                using var input = restoreClass.GetMethodParameters("CreateRestorePoint");
                input["Description"] = $"NODUS Driver before update {DateTime.Now:yyyy-MM-dd HH:mm}";
                input["RestorePointType"] = 0u;
                input["EventType"] = 100u;

                using var output = restoreClass.InvokeMethod("CreateRestorePoint", input, null);
                var returnValue = Convert.ToUInt32(output?["ReturnValue"] ?? 1u);
                if (returnValue == 0)
                {
                    _log.Info("Windows System Restore point created successfully.");
                    return (true, "Windows restore point created successfully.");
                }

                _log.Info($"Windows System Restore returned code {returnValue}.");
                return (false, $"Windows System Restore did not create a point (code {returnValue}). It may be disabled or rate-limited on this PC.");
            });
        }
        catch (Exception ex)
        {
            _log.Error("Could not create a Windows restore point.", ex);
            return (false, $"Restore point unavailable: {ex.Message}");
        }
    }

    private async Task<(bool Success, string Message)> ExportDriverPackageAsync(string infName, string targetDirectory)
    {
        try
        {
            var pnputil = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "pnputil.exe");
            var startInfo = new ProcessStartInfo(pnputil)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            startInfo.ArgumentList.Add("/export-driver");
            startInfo.ArgumentList.Add(infName);
            startInfo.ArgumentList.Add(targetDirectory);

            using var process = Process.Start(startInfo);
            if (process is null)
                return (false, "Windows could not start PnPUtil for this package.");

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            if (process.ExitCode == 0)
            {
                _log.Info($"Exported driver package {infName} into recovery snapshot.");
                return (true, "OEM driver package exported successfully.");
            }

            var detail = string.IsNullOrWhiteSpace(stderr) ? stdout.Trim() : stderr.Trim();
            _log.Info($"PnPUtil could not export {infName}; exit={process.ExitCode}; {detail}");
            return (false, string.IsNullOrWhiteSpace(detail)
                ? $"PnPUtil returned exit code {process.ExitCode}."
                : detail);
        }
        catch (Exception ex)
        {
            _log.Error($"Could not export driver package {infName}.", ex);
            return (false, ex.Message);
        }
    }

    private void PruneOldSnapshots()
    {
        try
        {
            if (!Directory.Exists(RecoveryRootPath)) return;

            var directories = new DirectoryInfo(RecoveryRootPath)
                .EnumerateDirectories()
                .OrderByDescending(x => x.CreationTimeUtc)
                .Skip(MaxSnapshots)
                .ToList();

            foreach (var directory in directories)
            {
                try
                {
                    directory.Delete(true);
                    _log.Info($"Pruned old recovery snapshot {directory.Name}.");
                }
                catch (Exception ex)
                {
                    _log.Error($"Could not prune old recovery snapshot {directory.FullName}.", ex);
                }
            }
        }
        catch (Exception ex)
        {
            _log.Error("Could not prune recovery snapshots.", ex);
        }
    }
}
