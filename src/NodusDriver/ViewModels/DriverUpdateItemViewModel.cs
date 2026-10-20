using NodusDriver.Infrastructure;
using NodusDriver.Models;

namespace NodusDriver.ViewModels;

public sealed class DriverUpdateItemViewModel : ObservableObject
{
    private bool _isSelected = true;

    public DriverUpdateItemViewModel(DriverUpdate update) => Update = update;

    public DriverUpdate Update { get; }
    public string Title => Update.Title;
    public string Manufacturer => Update.Manufacturer;
    public string Model => Update.Model;
    public string DriverClass => Update.DriverClass;
    public string DriverVersionDate => Update.DriverVersionDate;
    public string Size => Update.MaxDownloadSize <= 0 ? "Size unavailable" : FormatBytes(Update.MaxDownloadSize);

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

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
