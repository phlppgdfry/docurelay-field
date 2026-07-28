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
        return await _database.Table<QueuedUpload>()
            .Where(upload => upload.State == UploadState.Pending.ToString() || upload.State == UploadState.Retrying.ToString())
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
}
