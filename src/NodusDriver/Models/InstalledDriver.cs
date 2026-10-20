namespace NodusDriver.Models;

public sealed record InstalledDriver(
    string DeviceName,
    string Manufacturer,
    string DriverVersion,
    string? DriverDate,
    string? DeviceClass,
    string? InfName,
    bool IsSigned);
