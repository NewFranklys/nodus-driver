using System.Collections.ObjectModel;
using NodusDriver.Infrastructure;
using NodusDriver.Services;

namespace NodusDriver.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly InstalledDriverService _drivers;
    private readonly WindowsUpdateService _windowsUpdate;
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

            InstalledDriverCount = installedTask.Result.Count;
            Updates.Clear();

            foreach (var update in updateTask.Result)
            {
                var item = new DriverUpdateItemViewModel(update);
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
        Status = "Updating drivers";
        StatusDetail = $"Downloading and installing {selected.Count} selected update{(selected.Count == 1 ? "" : "s")} through Windows Update…";

        try
        {
            var result = await _windowsUpdate.InstallAsync(selected);
            RebootRequired = result.RebootRequired;
            Status = result.InstallSucceeded ? "Update complete" : "Update needs attention";
            StatusDetail = result.Message;

            if (result.InstallSucceeded && !result.RebootRequired)
                await ScanAsync();
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

    private void RefreshUpdateProperties()
    {
        OnPropertyChanged(nameof(UpdateCount));
        OnPropertyChanged(nameof(HasUpdates));
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(InstallButtonText));
        InstallCommand.RaiseCanExecuteChanged();
    }
}
