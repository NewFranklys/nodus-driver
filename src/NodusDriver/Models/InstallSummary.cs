namespace NodusDriver.Models;

public sealed record InstallSummary(
    bool DownloadSucceeded,
    bool InstallSucceeded,
    bool RebootRequired,
    int ResultCode,
    string Message);
