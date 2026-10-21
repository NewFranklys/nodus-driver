using System.Collections.ObjectModel;
using NodusDriver.Infrastructure;
using NodusDriver.Models;
using NodusDriver.Services;

namespace NodusDriver.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly InstalledDriverService _drivers;
    private readonly WindowsUpdateService _windowsUpdate;
    private readonly ElevatedInstallLauncher _elevatedInstaller;
    private readonly LogService _log;
    private bool _isBusy;
    private bool _isScanning;
    private bool _isInstalling;
    private bool _hasScanned;
    private string _status = "Ready to scan";
    private string _statusDetail = "NODUS will inspect this PC and ask Microsoft Windows Update for compatible driver packages.";
    private string _lastScan = "Not yet";
    private int _installedDriverCount;
    private bool _rebootRequired;

    public MainViewModel()
    {
        _log = new LogService();
        _drivers = new InstalledDriverService(_log);
        _windowsUpdate = new WindowsUpdateService(_log);
        _elevatedInstaller = new ElevatedInstallLauncher(_log);
        ScanCommand = new AsyncCommand(ScanAsync, () => !IsBusy);
        InstallCommand = new AsyncCommand(InstallSelectedAsync, () => !IsBusy && Updates.Any(x => x.IsSelected));
    }

    public ObservableCollection<DriverUpdateItemViewModel> Updates { get; } = [];
    public ObservableCollection<HardwareDeviceItemViewModel> HardwareDevices { get; } = [];
    public AsyncCommand ScanCommand { get; }
    public AsyncCommand InstallCommand { get; }
    public string LogPath => _log.LogPath;

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                ScanCommand.RaiseCanExecuteChanged();
                InstallCommand.RaiseCanExecuteChanged();
                OnPropertyChanged(nameof(IsIdle));
            }
        }
    }

    public bool IsIdle => !IsBusy;

    public bool IsScanning
    {
        get => _isScanning;
        private set => SetProperty(ref _isScanning, value);
    }

    public bool IsInstalling
    {
        get => _isInstalling;
        private set => SetProperty(ref _isInstalling, value);
    }

    public bool HasScanned
    {
        get => _hasScanned;
        private set => SetProperty(ref _hasScanned, value);
    }

    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public string StatusDetail { get => _statusDetail; private set => SetProperty(ref _statusDetail, value); }
    public string LastScan { get => _lastScan; private set => SetProperty(ref _lastScan, value); }
    public int InstalledDriverCount { get => _installedDriverCount; private set => SetProperty(ref _installedDriverCount, value); }
    public bool RebootRequired { get => _rebootRequired; private set => SetProperty(ref _rebootRequired, value); }

    public int UpdateCount => Updates.Count;
    public int ConfirmedUpgradeCount => Updates.Count(x => x.OfferKind == "Upgrade");
    public int PackageRefreshCount => Updates.Count(x => x.OfferKind == "Package");
    public int VersionKnownCurrentUnknownCount => Updates.Count(x => x.OfferKind == "Offer" && x.HasOfferedVersion && !x.HasInstalledMatch);
    public int TargetUnknownCount => Updates.Count(x => x.OfferKind == "Offer" && !x.HasOfferedVersion);
    public bool HasUpdates => Updates.Count > 0;
    public int SelectedCount => Updates.Count(x => x.IsSelected);
    public string PackageCountText => UpdateCount == 1 ? "1 package" : $"{UpdateCount} packages";
    public string InstallButtonText => SelectedCount switch
    {
        0 => "Select a driver",
        1 => "Update selected driver",
        _ => $"Update selected  ·  {SelectedCount}"
    };

    public int HardwareCount => HardwareDevices.Count;
    public int NetworkDeviceCount => HardwareDevices.Count(x => x.Category == "Network");
    public int DisplayDeviceCount => HardwareDevices.Count(x => x.Category == "Display");
    public int AudioDeviceCount => HardwareDevices.Count(x => x.Category == "Audio / media");
    public int BluetoothDeviceCount => HardwareDevices.Count(x => x.Category == "Bluetooth");
    public string HardwareCountText => HardwareCount == 1 ? "1 detected device" : $"{HardwareCount} detected devices";

    private async Task ScanAsync()
    {
        IsBusy = true;
        IsScanning = true;
        IsInstalling = false;
        RebootRequired = false;
        HasScanned = false;
        Status = "Detecting hardware";
        StatusDetail = "Reading Plug and Play devices and the drivers currently installed on this PC.";

        try
        {
            var installedDrivers = await _drivers.GetInstalledDriversAsync();
            InstalledDriverCount = installedDrivers.Count;
            PopulateHardwareInventory(installedDrivers);

            Status = "Checking Windows Update";
            StatusDetail = "Asking Microsoft Windows Update for driver packages that apply to this hardware.";
            var updateResults = await _windowsUpdate.SearchDriverUpdatesAsync();

            Status = "Comparing driver packages";
            StatusDetail = "Matching Windows Update offers against the drivers already installed on this PC.";
            Updates.Clear();

            foreach (var update in updateResults)
            {
                var installedMatch = FindBestInstalledMatch(update, installedDrivers);
                var item = new DriverUpdateItemViewModel(update, installedMatch);
                item.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(DriverUpdateItemViewModel.IsSelected))
                    {
                        InstallCommand.RaiseCanExecuteChanged();
                        OnPropertyChanged(nameof(SelectedCount));
                        OnPropertyChanged(nameof(InstallButtonText));
                    }
                };
                Updates.Add(item);
            }

            HasScanned = true;
            LastScan = DateTime.Now.ToString("HH:mm");
            RefreshUpdateProperties();

            if (Updates.Count == 0)
            {
                Status = "Drivers are up to date";
                StatusDetail = "Windows Update did not report any compatible driver packages that need attention.";
            }
            else
            {
                Status = Updates.Count == 1 ? "1 driver package available" : $"{Updates.Count} driver packages available";
                StatusDetail = BuildScanDetail();
            }
        }
        catch (Exception ex)
        {
            HasScanned = true;
            Status = "Driver scan failed";
            StatusDetail = ex.Message;
            _log.Error("Scan failed in UI flow.", ex);
        }
        finally
        {
            IsScanning = false;
            IsBusy = false;
        }
    }

    private string BuildScanDetail()
    {
        var parts = new List<string>();

        if (ConfirmedUpgradeCount > 0)
            parts.Add(ConfirmedUpgradeCount == 1 ? "1 confirmed newer version" : $"{ConfirmedUpgradeCount} confirmed newer versions");

        if (PackageRefreshCount > 0)
            parts.Add(PackageRefreshCount == 1 ? "1 same-version package refresh" : $"{PackageRefreshCount} same-version package refreshes");

        if (VersionKnownCurrentUnknownCount > 0)
            parts.Add(VersionKnownCurrentUnknownCount == 1
                ? "1 package with a reported available version whose current version could not be identified"
                : $"{VersionKnownCurrentUnknownCount} packages with reported available versions whose current versions could not be identified");

        if (TargetUnknownCount > 0)
            parts.Add(TargetUnknownCount == 1
                ? "1 compatible package with no target version reported"
                : $"{TargetUnknownCount} compatible packages with no target version reported");

        return $"Windows Update reported {string.Join(", ", parts)}. Review the details before installing.";
    }

    private async Task InstallSelectedAsync()
    {
        var selected = Updates.Where(x => x.IsSelected).Select(x => x.Update).ToList();
        if (selected.Count == 0) return;

        IsBusy = true;
        IsScanning = false;
        IsInstalling = true;
        RebootRequired = false;
        Status = "Windows permission required";
        StatusDetail = $"NODUS will ask Windows for administrator permission to install {selected.Count} selected driver package{(selected.Count == 1 ? "" : "s")}.";

        try
        {
            var result = await _elevatedInstaller.InstallAsync(selected, () =>
            {
                Status = selected.Count == 1 ? "Installing driver" : "Installing drivers";
                StatusDetail = selected.Count == 1
                    ? "Windows permission granted. NODUS is downloading and installing the selected package through Windows Update."
                    : $"Windows permission granted. NODUS is downloading and installing {selected.Count} selected packages through Windows Update.";
            });

            RebootRequired = result.RebootRequired;

            if (result.InstallSucceeded)
            {
                Status = result.RebootRequired ? "Restart required" : "Update complete";
                StatusDetail = result.Message;

                if (!result.RebootRequired)
                {
                    IsInstalling = false;
                    await ScanAsync();
                }
            }
            else if (result.ResultCode == 1223)
            {
                Status = "Update cancelled";
                StatusDetail = result.Message;
            }
            else
            {
                Status = "Update needs attention";
                StatusDetail = result.Message;
            }
        }
        catch (Exception ex)
        {
            Status = "Update failed";
            StatusDetail = ex.Message;
            _log.Error("Install failed in UI flow.", ex);
        }
        finally
        {
            IsInstalling = false;
            IsBusy = false;
        }
    }

    private void PopulateHardwareInventory(IReadOnlyList<InstalledDriver> installedDrivers)
    {
        HardwareDevices.Clear();

        foreach (var driver in installedDrivers
                     .OrderBy(x => x.DeviceClass)
                     .ThenBy(x => x.DeviceName))
        {
            HardwareDevices.Add(new HardwareDeviceItemViewModel(driver));
        }

        OnPropertyChanged(nameof(HardwareCount));
        OnPropertyChanged(nameof(NetworkDeviceCount));
        OnPropertyChanged(nameof(DisplayDeviceCount));
        OnPropertyChanged(nameof(AudioDeviceCount));
        OnPropertyChanged(nameof(BluetoothDeviceCount));
        OnPropertyChanged(nameof(HardwareCountText));
    }

    private static InstalledDriver? FindBestInstalledMatch(DriverUpdate update, IReadOnlyList<InstalledDriver> installedDrivers)
    {
        InstalledDriver? best = null;
        var bestScore = 0;

        var model = Normalize(update.Model);
        var title = Normalize(update.Title);
        var manufacturer = Normalize(update.Manufacturer);
        var driverClass = Normalize(update.DriverClass);

        foreach (var candidate in installedDrivers)
        {
            var score = 0;
            var deviceName = Normalize(candidate.DeviceName);
            var candidateManufacturer = Normalize(candidate.Manufacturer);
            var candidateClass = Normalize(candidate.DeviceClass);

            if (model.Length >= 4 && deviceName.Length >= 4 &&
                (deviceName.Contains(model, StringComparison.Ordinal) || model.Contains(deviceName, StringComparison.Ordinal)))
                score += 100;

            if (deviceName.Length >= 5 && title.Contains(deviceName, StringComparison.Ordinal))
                score += 80;

            if (manufacturer.Length >= 3 && candidateManufacturer.Length >= 3 &&
                (manufacturer.Contains(candidateManufacturer, StringComparison.Ordinal) || candidateManufacturer.Contains(manufacturer, StringComparison.Ordinal)))
                score += 25;

            if (driverClass.Length > 0 && driverClass == candidateClass)
                score += 20;

            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        return bestScore >= 80 ? best : null;
    }

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        return new string(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
    }

    private void RefreshUpdateProperties()
    {
        OnPropertyChanged(nameof(UpdateCount));
        OnPropertyChanged(nameof(ConfirmedUpgradeCount));
        OnPropertyChanged(nameof(PackageRefreshCount));
        OnPropertyChanged(nameof(VersionKnownCurrentUnknownCount));
        OnPropertyChanged(nameof(TargetUnknownCount));
        OnPropertyChanged(nameof(HasUpdates));
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(PackageCountText));
        OnPropertyChanged(nameof(InstallButtonText));
        InstallCommand.RaiseCanExecuteChanged();
    }
}
