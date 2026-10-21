using System.Collections.ObjectModel;
using NodusDriver.Infrastructure;
using NodusDriver.Models;
using NodusDriver.Services;

namespace NodusDriver.ViewModels;

public sealed class RecoveryViewModel : ObservableObject
{
    private readonly RecoveryService _recovery;
    private RecoverySnapshot? _selectedSnapshot;

    public RecoveryViewModel()
    {
        _recovery = new RecoveryService(new LogService());
        Refresh();
    }

    public ObservableCollection<RecoverySnapshot> Snapshots { get; } = [];
    public string RecoveryRootPath => RecoveryService.RecoveryRootPath;

    public RecoverySnapshot? SelectedSnapshot
    {
        get => _selectedSnapshot;
        set
        {
            if (SetProperty(ref _selectedSnapshot, value))
                OnPropertyChanged(nameof(HasSelectedSnapshot));
        }
    }

    public bool HasSelectedSnapshot => SelectedSnapshot is not null;
    public bool HasSnapshots => Snapshots.Count > 0;
    public int SnapshotCount => Snapshots.Count;
    public string SnapshotCountText => SnapshotCount == 1 ? "1 recovery snapshot" : $"{SnapshotCount} recovery snapshots";
    public string LatestSnapshotText => Snapshots.Count == 0
        ? "No protected driver changes yet"
        : $"Latest · {Snapshots[0].CreatedDisplay}";

    public void Refresh()
    {
        var selectedId = SelectedSnapshot?.Id;
        Snapshots.Clear();

        foreach (var snapshot in _recovery.LoadSnapshots())
            Snapshots.Add(snapshot);

        SelectedSnapshot = Snapshots.FirstOrDefault(x => x.Id == selectedId) ?? Snapshots.FirstOrDefault();
        OnPropertyChanged(nameof(HasSnapshots));
        OnPropertyChanged(nameof(SnapshotCount));
        OnPropertyChanged(nameof(SnapshotCountText));
        OnPropertyChanged(nameof(LatestSnapshotText));
    }
}
