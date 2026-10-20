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
    private bool _hasScanned;
    private string _status = "Starting NODUS Driver";
    private string _statusDetail = "Preparing to check this PC for compatible driver updates.";
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
    public bool HasUpdates => Updates.Count > 0;
    public int SelectedCount => Updates.Count(x => x.IsSelected);
    public string InstallButtonText => SelectedCount switch
    {
        0 => "Select an update",
        1 => "Update 1 driver",
        _ => $"Update {SelectedCount} drivers"
    };

    private async Task ScanAsync()
    {
        IsBusy = true;
        RebootRequired = false;
        HasScanned = false;
        Status = "Checking this PC";
        StatusDetail = "Reading installed devices and asking Windows Update for compatible drivers…";

        try
        {
            var installedTask = _drivers.GetInstalledDriversAsync();
            var updateTask = _windowsUpdate.SearchDriverUpdatesAsync();
            await Task.WhenAll(installedTask, updateTask);

            var installedDrivers = installedTask.Result;
            InstalledDriverCount = installedDrivers.Count;
            Updates.Clear();

            foreach (var update in updateTask.Result)
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
                Status = "Your drivers are up to date";
                StatusDetail = "Windows Update did not report any newer compatible drivers for this PC.";
            }
            else
            {
                Status = $"{Updates.Count} update{(Updates.Count == 1 ? "" : "s")} available";
                StatusDetail = "They are selected by default. Review the list and update when you're ready.";
            }
        }
        catch (Exception ex)
        {
            HasScanned = true;
            Status = "Driver check failed";
            StatusDetail = ex.Message;
            _log.Error("Scan failed in UI flow.", ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task InstallSelectedAsync()
    {
        var selected = Updates.Where(x => x.IsSelected).Select(x => x.Update).ToList();
        if (selected.Count == 0) return;

        IsBusy = true;
        RebootRequired = false;
        Status = "Windows permission required";
        StatusDetail = $"NODUS will ask Windows for administrator permission to install {selected.Count} selected driver update{(selected.Count == 1 ? "" : "s")}.";

        try
        {
            var result = await _elevatedInstaller.InstallAsync(selected, () =>
            {
                Status = selected.Count == 1 ? "Installing driver" : "Installing drivers";
                StatusDetail = selected.Count == 1
                    ? "Windows permission granted. NODUS is downloading and installing the selected driver through Windows Update…"
                    : $"Windows permission granted. NODUS is downloading and installing {selected.Count} selected drivers through Windows Update…";
            });

            RebootRequired = result.RebootRequired;

            if (result.InstallSucceeded)
            {
                Status = result.RebootRequired ? "Restart required" : "Update complete";
                StatusDetail = result.Message;

                if (!result.RebootRequired)
                    await ScanAsync();
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
            IsBusy = false;
        }
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
        OnPropertyChanged(nameof(HasUpdates));
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(InstallButtonText));
        InstallCommand.RaiseCanExecuteChanged();
    }
}
