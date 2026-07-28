using DocuRelay.Mobile.Models;

namespace DocuRelay.Mobile.Services;

public sealed class SyncService(SqliteOfflineStore offlineStore, DossierApiClient apiClient)
{
    public async Task<SyncResult> SyncAsync(CancellationToken cancellationToken)
    {
        var synced = 0;
        var failed = 0;
        var queue = await offlineStore.GetOutstandingUploadsAsync();

        foreach (var upload in queue)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!File.Exists(upload.LocalPath))
            {
                await offlineStore.MarkFailedAttemptAsync(upload, "The locally queued file is no longer available.");
                failed++;
                continue;
            }

            try
            {
                await using var stream = File.OpenRead(upload.LocalPath);
                await apiClient.UploadAsync(Guid.Parse(upload.DossierId), upload.FileName, upload.ContentType, stream, cancellationToken);
                await offlineStore.MarkSyncedAsync(upload);
                File.Delete(upload.LocalPath);
                synced++;
            }
            catch (HttpRequestException exception)
            {
                await offlineStore.MarkFailedAttemptAsync(upload, exception.Message);
                failed++;
            }
        }

        return new SyncResult(queue.Count, synced, failed);
    }
}

public sealed record SyncResult(int Attempted, int Synced, int Failed);
