namespace NodusDriver.Services;

public sealed class LogService
{
    private readonly string _logPath;
    private readonly object _gate = new();

    public LogService()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NODUS Driver", "logs");
        Directory.CreateDirectory(dir);
        _logPath = Path.Combine(dir, $"nodus-driver-{DateTime.Now:yyyy-MM-dd}.log");
    }

    public string LogPath => _logPath;

    public void Info(string message) => Write("INFO", message);
    public void Error(string message, Exception? ex = null) => Write("ERROR", ex is null ? message : $"{message} | {ex}");

    private void Write(string level, string message)
    {
        lock (_gate)
            File.AppendAllText(_logPath, $"{DateTime.Now:O} [{level}] {message}{Environment.NewLine}");
    }
}
