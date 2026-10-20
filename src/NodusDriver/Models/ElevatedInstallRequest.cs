namespace NodusDriver.Models;

public sealed record ElevatedInstallRequest(
    IReadOnlyList<DriverUpdate> Updates,
    string ResultPath);
