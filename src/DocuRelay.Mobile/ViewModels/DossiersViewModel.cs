using System.Collections.ObjectModel;
using System.Windows.Input;
using DocuRelay.Contracts;
using DocuRelay.Mobile.Services;

namespace DocuRelay.Mobile.ViewModels;

public sealed class DossiersViewModel : ObservableObject
{
    private readonly DossierRepository _repository;
    private readonly SyncService _syncService;
    private DossierSummary? _selectedDossier;
    private string _status = "Ready to refresh dossiers.";
    private bool _isBusy;

    public DossiersViewModel(DossierRepository repository, SyncService syncService)
    {
        _repository = repository;
        _syncService = syncService;
        RefreshCommand = new AsyncCommand(RefreshAsync);
        QueueDocumentCommand = new AsyncCommand(QueueDocumentAsync);
        SyncCommand = new AsyncCommand(SyncAsync);
    }

    public ObservableCollection<DossierSummary> Dossiers { get; } = [];

    public DossierSummary? SelectedDossier
    {
        get => _selectedDossier;
        set
        {
            if (SetProperty(ref _selectedDossier, value))
            {
                OnPropertyChanged(nameof(CanQueueDocument));
            }
        }
    }

    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public bool CanQueueDocument => SelectedDossier is not null;
    public ICommand RefreshCommand { get; }
    public ICommand QueueDocumentCommand { get; }
    public ICommand SyncCommand { get; }

    public async Task LoadAsync()
    {
        if (Dossiers.Count == 0) await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        IsBusy = true;
        try
        {
            var (dossiers, isFromCache) = await _repository.GetDossiersAsync(CancellationToken.None);
            Dossiers.Clear();
            foreach (var dossier in dossiers) Dossiers.Add(dossier);
            Status = isFromCache
                ? "Offline: showing the last locally cached dossiers."
                : $"Updated {DateTimeOffset.Now:t}. {Dossiers.Count} dossiers available.";
        }
        catch (Exception exception)
        {
            Status = $"Could not load dossiers: {exception.Message}";
        }
        finally { IsBusy = false; }
    }

    private async Task QueueDocumentAsync()
    {
        if (SelectedDossier is null)
        {
            Status = "Select a dossier before adding a document.";
            return;
        }

        var file = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "Select field documentation"
        });
        if (file is null) return;

        await _repository.QueueFileAsync(SelectedDossier.Id, file, CancellationToken.None);
        Status = $"{file.FileName} queued locally for {SelectedDossier.Reference}.";
    }

    private async Task SyncAsync()
    {
        IsBusy = true;
        try
        {
            var result = await _syncService.SyncAsync(CancellationToken.None);
            Status = result.Attempted == 0
                ? "No pending uploads."
                : $"Sync complete: {result.Synced} uploaded, {result.Failed} need attention.";
            await RefreshAsync();
        }
        catch (Exception exception)
        {
            Status = $"Sync stopped: {exception.Message}";
        }
        finally { IsBusy = false; }
    }
}

