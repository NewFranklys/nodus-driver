using System.Text.Json.Serialization;

namespace NodusDriver.Models;

public sealed record RecoveryDriverRecord(
    string DeviceName,
    string Manufacturer,
    string DriverVersion,
    string InfName,
    bool BackupSucceeded,
    string BackupMessage)
{
    [JsonIgnore]
    public string VersionLabel => string.IsNullOrWhiteSpace(DriverVersion) ? "Version not reported" : $"Version {DriverVersion}";
}

public sealed record RecoverySnapshot(
    string Id,
    DateTimeOffset CreatedAt,
    string Status,
    bool RestorePointCreated,
    string RestorePointMessage,
    string BackupDirectory,
    int RequestedDriverCount,
    int BackedUpDriverCount,
    bool InstallStarted,
    bool InstallSucceeded,
    bool RebootRequired,
    string Message,
    IReadOnlyList<RecoveryDriverRecord> Drivers)
{
    [JsonIgnore]
    public bool HasProtection => RestorePointCreated || BackedUpDriverCount > 0;

    [JsonIgnore]
    public bool HasBackup => BackedUpDriverCount > 0 && Directory.Exists(BackupDirectory);

    [JsonIgnore]
    public string CreatedDisplay => CreatedAt.ToLocalTime().ToString("dd MMM yyyy · HH:mm");

    [JsonIgnore]
    public string Title => Drivers.Count switch
    {
        0 => RequestedDriverCount == 1 ? "1 driver update" : $"{RequestedDriverCount} driver updates",
        1 => Drivers[0].DeviceName,
        _ => $"{Drivers[0].DeviceName} + {Drivers.Count - 1} more"
    };

    [JsonIgnore]
    public string ResultLabel => !HasProtection
        ? "Protection failed"
        : !InstallStarted
            ? "Protected snapshot"
            : InstallSucceeded
                ? (RebootRequired ? "Installed · restart required" : "Installed")
                : "Install did not complete";

    [JsonIgnore]
    public string ProtectionLabel => RestorePointCreated && BackedUpDriverCount > 0
        ? "Restore point + driver backup"
        : RestorePointCreated
            ? "Windows restore point"
            : BackedUpDriverCount > 0
                ? (BackedUpDriverCount == 1 ? "1 driver package backed up" : $"{BackedUpDriverCount} driver packages backed up")
                : "No recovery protection";

    [JsonIgnore]
    public string DriverSummary => Drivers.Count == 0
        ? "No installed driver package could be matched for export."
        : Drivers.Count == 1
            ? $"{Drivers[0].DeviceName} · {Drivers[0].VersionLabel}"
            : $"{Drivers.Count} matched installed driver packages";
}
