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
            "display" or "monitor" => "Display",
            "media" or "audioendpoint" or "sound" => "Audio",
            "bluetooth" => "Bluetooth",
            "hidclass" or "keyboard" or "mouse" => "Input",
            "usb" or "usbdevice" => "USB",
            "securitydevices" or "security" or "otherhardware" or "system" or "softwarecomponent" or
            "computer" or "firmware" or "processor" or "ports" => "System",
            _ => "Other"
        };
    }
}
