using System.Management;
using NodusDriver.Models;

namespace NodusDriver.Services;

public sealed class InstalledDriverService
{
    private readonly LogService _log;

    public InstalledDriverService(LogService log) => _log = log;

    public Task<IReadOnlyList<InstalledDriver>> GetInstalledDriversAsync() => Task.Run<IReadOnlyList<InstalledDriver>>(() =>
    {
        var result = new List<InstalledDriver>();
        const string query = "SELECT DeviceName, Manufacturer, DriverVersion, DriverDate, DeviceClass, InfName, IsSigned FROM Win32_PnPSignedDriver WHERE DeviceName IS NOT NULL";

        using var searcher = new ManagementObjectSearcher(query);
        foreach (ManagementObject item in searcher.Get())
        {
            result.Add(new InstalledDriver(
                item["DeviceName"]?.ToString() ?? "Unknown device",
                item["Manufacturer"]?.ToString() ?? "Unknown manufacturer",
                item["DriverVersion"]?.ToString() ?? "—",
                item["DriverDate"]?.ToString(),
                item["DeviceClass"]?.ToString(),
                item["InfName"]?.ToString(),
                item["IsSigned"] is bool signed && signed));
        }

        _log.Info($"Enumerated {result.Count} installed PnP driver entries.");
        return result.OrderBy(x => x.DeviceName, StringComparer.CurrentCultureIgnoreCase).ToList();
    });
}
