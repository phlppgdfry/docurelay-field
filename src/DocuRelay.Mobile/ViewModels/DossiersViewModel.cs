using System.Collections.ObjectModel;
using System.Windows.Input;
using DocuRelay.Contracts;
using DocuRelay.Mobile.Models;
using DocuRelay.Mobile.Services;

namespace DocuRelay.Mobile.ViewModels;

public sealed class DossiersViewModel : ObservableObject
{
    private readonly DossierRepository _repository;
    private readonly SyncService _syncService;
    private DossierSummary? _selectedDossier;
    private string _status = "Ready to refresh dossiers.";
    private string _documentProcessingStatus = "Select a dossier to view document processing.";
    private UploadQueueSummary _queueSummary = UploadQueueSummary.Empty;
    private bool _isBusy;

    public DossiersViewModel(DossierRepository repository, SyncService syncService)
    {
        _repository = repository;
        _syncService = syncService;
        RefreshCommand = new AsyncCommand(RefreshAsync);
        QueueDocumentCommand = new AsyncCommand(QueueDocumentAsync);
        SyncCommand = new AsyncCommand(SyncAsync);
        RetryFailedUploadsCommand = new AsyncCommand(RetryFailedUploadsAsync);
    }

    public ObservableCollection<DossierSummary> Dossiers { get; } = [];
    public ObservableCollection<DocumentSummary> Documents { get; } = [];

    public DossierSummary? SelectedDossier
    {
        get => _selectedDossier;
        set
        {
            if (SetProperty(ref _selectedDossier, value))
            {
                OnPropertyChanged(nameof(CanQueueDocument));
                OnPropertyChanged(nameof(HasSelectedDossier));
                if (value is not null) _ = LoadDocumentProcessingStatusAsync(value.Id);
                else Documents.Clear();
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

    public string DocumentProcessingStatus
    {
        get => _documentProcessingStatus;
        private set => SetProperty(ref _documentProcessingStatus, value);
    }

    public UploadQueueSummary QueueSummary
    {
        get => _queueSummary;
        private set
        {
            if (SetProperty(ref _queueSummary, value))
            {
                OnPropertyChanged(nameof(HasFailedUploads));
            }
        }
    }

    public bool CanQueueDocument => SelectedDossier is not null;
    public bool HasSelectedDossier => SelectedDossier is not null;
    public bool HasDocuments => Documents.Count > 0;
    public bool HasFailedUploads => QueueSummary.Failed > 0;
    public ICommand RefreshCommand { get; }
    public ICommand QueueDocumentCommand { get; }
    public ICommand SyncCommand { get; }
    public ICommand RetryFailedUploadsCommand { get; }

    public async Task LoadAsync()
    {
        if (Dossiers.Count == 0) await RefreshAsync();
        await RefreshQueueSummaryAsync();
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
            if (SelectedDossier is not null)
            {
                await LoadDocumentProcessingStatusAsync(SelectedDossier.Id);
            }
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
        await RefreshQueueSummaryAsync();
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
            await RefreshQueueSummaryAsync();
        }
        catch (Exception exception)
        {
            Status = $"Sync stopped: {exception.Message}";
        }
        finally { IsBusy = false; }
    }

    private async Task RetryFailedUploadsAsync()
    {
        IsBusy = true;
        try
        {
            var restored = await _syncService.RetryFailedUploadsAsync();
            if (restored == 0)
            {
                Status = "There are no failed uploads to retry.";
                return;
            }

            Status = $"Retrying {restored} failed upload(s).";
            var result = await _syncService.SyncAsync(CancellationToken.None);
            Status = $"Retry complete: {result.Synced} uploaded, {result.Failed} need attention.";
            await RefreshAsync();
        }
        catch (Exception exception)
        {
            Status = $"Retry stopped: {exception.Message}";
        }
        finally
        {
            await RefreshQueueSummaryAsync();
            IsBusy = false;
        }
    }

    private async Task LoadDocumentProcessingStatusAsync(Guid dossierId)
    {
        try
        {
            var detail = await _repository.GetDossierDetailAsync(dossierId, CancellationToken.None);
            if (detail is null)
            {
                Documents.Clear();
                OnPropertyChanged(nameof(HasDocuments));
                DocumentProcessingStatus = "Connect to the API to view server processing status.";
                return;
            }

            Documents.Clear();
            foreach (var document in detail.Documents) Documents.Add(document);
            OnPropertyChanged(nameof(HasDocuments));

            DocumentProcessingStatus = detail.Documents.Count == 0
                ? "No server documents yet. Add a document and sync the queue."
                : string.Join(" · ", detail.Documents.Take(3).Select(document => $"{document.FileName}: {document.Status}"));
        }
        catch (Exception exception)
        {
            DocumentProcessingStatus = $"Could not load document status: {exception.Message}";
        }
    }

    private async Task RefreshQueueSummaryAsync()
    {
        QueueSummary = await _syncService.GetQueueSummaryAsync();
    }
}
