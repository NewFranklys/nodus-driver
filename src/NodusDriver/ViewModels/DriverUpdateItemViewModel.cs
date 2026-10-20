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
    public string CategoryLabel => FriendlyCategory(Update.DriverClass);
    public string DriverVersionDate => Update.DriverVersionDate;
    public string CurrentVersion => InstalledDriver?.DriverVersion ?? "Not identified";
    public string? OfferedVersion => TryExtractVersion(Update.Title);
    public string Size => Update.MaxDownloadSize <= 0 ? "Size unavailable" : FormatBytes(Update.MaxDownloadSize);

    public bool HasInstalledMatch => InstalledDriver is not null;
    public bool HasOfferedVersion => !string.IsNullOrWhiteSpace(OfferedVersion);
    public bool IsSameVersion =>
        HasInstalledMatch &&
        HasOfferedVersion &&
        string.Equals(CurrentVersion, OfferedVersion, StringComparison.OrdinalIgnoreCase);

    public string OfferKind => !HasOfferedVersion
        ? "Offer"
        : IsSameVersion
            ? "Package"
            : HasInstalledMatch
                ? "Upgrade"
                : "Offer";

    public string OfferLabel => OfferKind switch
    {
        "Upgrade" => "NEW VERSION",
        "Package" => "PACKAGE REFRESH",
        _ => "WINDOWS UPDATE"
    };

    public string VersionSummary
    {
        get
        {
            if (!HasInstalledMatch && !HasOfferedVersion)
                return "Compatible driver package available";

            if (!HasInstalledMatch)
                return $"Version {OfferedVersion} available";

            if (!HasOfferedVersion)
                return $"Current {CurrentVersion} · target version not reported";

            if (IsSameVersion)
                return $"Version {CurrentVersion} · Windows Update package available";

            return $"Current {CurrentVersion}  →  {OfferedVersion}";
        }
    }

    public string MetadataSummary => $"{Manufacturer}  ·  {CategoryLabel}  ·  {Size}";

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

    private static string FriendlyCategory(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "Driver";

        return value.Replace(" ", string.Empty, StringComparison.OrdinalIgnoreCase).ToLowerInvariant() switch
        {
            "networking" or "net" => "Network",
            "securitydevices" or "security" => "Security",
            "otherhardware" or "system" => "System component",
            "display" => "Display",
            "media" => "Audio / media",
            "bluetooth" => "Bluetooth",
            "hidclass" => "Input device",
            "usb" => "USB",
            _ => value
        };
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
