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
        IsSystemDevice = IsSystemOrVirtual(DeviceName, RawClass, Category);
    }

    public string DeviceName { get; }
    public string Manufacturer { get; }
    public string DriverVersion { get; }
    public string RawClass { get; }
    public string Category { get; }
    public bool IsSystemDevice { get; }

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
            "computer" or "firmware" or "processor" or "ports" or "volume" or "volumesnapshot" => "System",
            _ => "Other"
        };
    }

    private static bool IsSystemOrVirtual(string deviceName, string rawClass, string category)
    {
        if (category == "System") return true;

        var name = deviceName.ToLowerInvariant();
        var className = rawClass.ToLowerInvariant();

        if (className is "softwarecomponent" or "computer" or "firmware" or "processor" or "volume" or "volumesnapshot")
            return true;

        string[] systemNameFragments =
        [
            "wan miniport",
            "virtual adapter",
            "virtual network",
            "enumerator",
            "kernel",
            "remote desktop",
            "root print queue",
            "generic software device",
            "composite bus"
        ];

        return systemNameFragments.Any(name.Contains);
    }
}
