using DocuRelay.Contracts;
using DocuRelay.Mobile.Models;

namespace DocuRelay.Mobile.Services;

public sealed class SqliteOfflineStore
{
    private readonly SQLite.SQLiteAsyncConnection _database;

    public SqliteOfflineStore()
    {
        var path = Path.Combine(FileSystem.AppDataDirectory, "docurelay.db3");
        _database = new SQLite.SQLiteAsyncConnection(path);
    }

    public async Task InitializeAsync()
    {
        await _database.CreateTableAsync<CachedDossier>();
        await _database.CreateTableAsync<QueuedUpload>();
    }

    public async Task SaveDossiersAsync(IEnumerable<DossierSummary> dossiers)
    {
        await InitializeAsync();
        await _database.DeleteAllAsync<CachedDossier>();
        var rows = dossiers.Select(dossier => new CachedDossier
        {
            Id = dossier.Id.ToString(),
            Reference = dossier.Reference,
            Title = dossier.Title,
            Location = dossier.Location,
            UpdatedAtUnixMs = dossier.UpdatedAt.ToUnixTimeMilliseconds(),
            DocumentCount = dossier.DocumentCount
        }).ToArray();
        await _database.InsertAllAsync(rows);
    }

    public async Task<IReadOnlyList<DossierSummary>> GetDossiersAsync()
    {
        await InitializeAsync();
        var rows = await _database.Table<CachedDossier>().OrderByDescending(row => row.UpdatedAtUnixMs).ToListAsync();
        return rows.Select(row => new DossierSummary(
            Guid.Parse(row.Id), row.Reference, row.Title, row.Location,
            DateTimeOffset.FromUnixTimeMilliseconds(row.UpdatedAtUnixMs), row.DocumentCount)).ToArray();
    }

    public async Task EnqueueAsync(QueuedUpload upload)
    {
        await InitializeAsync();
        await _database.InsertAsync(upload);
    }

    public async Task<IReadOnlyList<QueuedUpload>> GetOutstandingUploadsAsync()
    {
        await InitializeAsync();
        // sqlite-net translates captured values to SQL parameters. Calling ToString inside
        // the expression would otherwise be emitted as an unavailable SQLite tostring function.
        var pending = UploadState.Pending.ToString();
        var retrying = UploadState.Retrying.ToString();
        return await _database.Table<QueuedUpload>()
            .Where(upload => upload.State == pending || upload.State == retrying)
            .OrderBy(upload => upload.CreatedAt)
            .ToListAsync();
    }

    public async Task MarkSyncedAsync(QueuedUpload upload)
    {
        upload.State = UploadState.Synced.ToString();
        upload.LastError = null;
        await _database.UpdateAsync(upload);
    }

    public async Task MarkFailedAttemptAsync(QueuedUpload upload, string error)
    {
        upload.AttemptCount++;
        upload.LastError = error;
        upload.State = UploadRetryPolicy.IsTerminalFailure(upload.AttemptCount)
            ? UploadState.Failed.ToString()
            : UploadState.Retrying.ToString();
        await _database.UpdateAsync(upload);
    }

    public async Task<int> RetryFailedUploadsAsync()
    {
        await InitializeAsync();
        var failedState = UploadState.Failed.ToString();
        var failedUploads = await _database.Table<QueuedUpload>()
            .Where(upload => upload.State == failedState)
            .ToListAsync();

        foreach (var upload in failedUploads)
        {
            upload.AttemptCount = 0;
            upload.LastError = null;
            upload.State = UploadState.Pending.ToString();
            await _database.UpdateAsync(upload);
        }

        return failedUploads.Count;
    }

    public async Task<UploadQueueSummary> GetQueueSummaryAsync()
    {
        await InitializeAsync();
        var uploads = await _database.Table<QueuedUpload>().ToListAsync();
        var pending = UploadState.Pending.ToString();
        var retrying = UploadState.Retrying.ToString();
        var failed = UploadState.Failed.ToString();
        return new UploadQueueSummary(
            uploads.Count(upload => upload.State == pending),
            uploads.Count(upload => upload.State == retrying),
            uploads.Count(upload => upload.State == failed));
    }
}
