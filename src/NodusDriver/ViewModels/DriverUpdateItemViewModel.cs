using System.Text.RegularExpressions;
using NodusDriver.Infrastructure;
using NodusDriver.Models;

namespace NodusDriver.ViewModels;

public sealed partial class DriverUpdateItemViewModel : ObservableObject
{
    private bool _isSelected = true;

    public DriverUpdateItemViewModel(DriverUpdate update, InstalledDriver? installedDriver)
    {
        Update = update;
        InstalledDriver = installedDriver;
    }

    public DriverUpdate Update { get; }
    public InstalledDriver? InstalledDriver { get; }

    public string DisplayName =>
        !string.IsNullOrWhiteSpace(Update.Model) &&
        !string.Equals(Update.Model, "Compatible hardware", StringComparison.OrdinalIgnoreCase)
            ? Update.Model
            : InstalledDriver?.DeviceName ?? Update.Title;

    public string Title => Update.Title;
    public string Manufacturer => Update.Manufacturer;
    public string Model => Update.Model;
    public string DriverClass => Update.DriverClass;
    public string DriverVersionDate => Update.DriverVersionDate;
    public string CurrentVersion => InstalledDriver?.DriverVersion ?? "Not identified";
    public string OfferedVersion => TryExtractVersion(Update.Title) ?? "Windows Update";
    public string VersionSummary => InstalledDriver is null
        ? $"Update offered · {OfferedVersion}"
        : $"Current {CurrentVersion}  →  {OfferedVersion}";
    public string Size => Update.MaxDownloadSize <= 0 ? "Size unavailable" : FormatBytes(Update.MaxDownloadSize);

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    private static string? TryExtractVersion(string title)
    {
        var matches = VersionToken().Matches(title);
        return matches.Count == 0 ? null : matches[^1].Value;
    }

    [GeneratedRegex(@"(?<!\d)\d+(?:\.\d+){1,5}(?!\d)")]
    private static partial Regex VersionToken();

    private static string FormatBytes(long value)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        double size = value;
        var unit = 0;
        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        return $"{size:0.#} {units[unit]}";
    }
}
