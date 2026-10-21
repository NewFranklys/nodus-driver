using NodusDriver.Models;

namespace NodusDriver.ViewModels;

public sealed class HardwareDeviceItemViewModel
{
    public HardwareDeviceItemViewModel(InstalledDriver driver)
    {
        DeviceName = string.IsNullOrWhiteSpace(driver.DeviceName) ? "Unknown device" : driver.DeviceName;
        Manufacturer = string.IsNullOrWhiteSpace(driver.Manufacturer) ? "Unknown manufacturer" : driver.Manufacturer;
        DriverVersion = string.IsNullOrWhiteSpace(driver.DriverVersion) ? "Not reported" : driver.DriverVersion;
        RawClass = driver.DeviceClass ?? string.Empty;
        Category = FriendlyCategory(RawClass);
    }

    public string DeviceName { get; }
    public string Manufacturer { get; }
    public string DriverVersion { get; }
    public string RawClass { get; }
    public string Category { get; }

    private static string FriendlyCategory(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "Other";

        return value.Replace(" ", string.Empty, StringComparison.OrdinalIgnoreCase).ToLowerInvariant() switch
        {
            "networking" or "net" => "Network",
            "securitydevices" or "security" => "Security",
            "otherhardware" or "system" => "System",
            "display" => "Display",
            "media" => "Audio / media",
            "bluetooth" => "Bluetooth",
            "hidclass" => "Input",
            "keyboard" => "Input",
            "mouse" => "Input",
            "usb" => "USB",
            "ports" => "Ports",
            "processor" => "Processor",
            _ => value
        };
    }
}
