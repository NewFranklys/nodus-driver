namespace NodusDriver.Models;

public sealed record ElevatedInstallRequest(
    IReadOnlyList<DriverUpdate> Updates,
    IReadOnlyList<InstalledDriver> PreviousDrivers,
    string ResultPath);
