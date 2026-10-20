namespace NodusDriver.Models;

public sealed record DriverUpdate(
    string UpdateId,
    int RevisionNumber,
    string Title,
    string Manufacturer,
    string Model,
    string DriverClass,
    string DriverVersionDate,
    bool RebootRequired,
    long MaxDownloadSize);
