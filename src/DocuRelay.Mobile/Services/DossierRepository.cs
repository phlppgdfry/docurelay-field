using DocuRelay.Contracts;
using DocuRelay.Mobile.Models;

namespace DocuRelay.Mobile.Services;

public sealed class DossierRepository(DossierApiClient apiClient, SqliteOfflineStore offlineStore)
{
    public async Task<(IReadOnlyList<DossierSummary> Dossiers, bool IsFromCache)> GetDossiersAsync(CancellationToken cancellationToken)
    {
        try
        {
            var dossiers = await apiClient.GetDossiersAsync(cancellationToken);
            await offlineStore.SaveDossiersAsync(dossiers);
            return (dossiers, false);
        }
        catch (HttpRequestException)
        {
            return (await offlineStore.GetDossiersAsync(), true);
        }
    }

    public async Task QueueFileAsync(Guid dossierId, FileResult file, CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(file.FileName);
        var localPath = Path.Combine(FileSystem.CacheDirectory, $"{Guid.NewGuid():N}{extension}");

        await using var input = await file.OpenReadAsync();
        await using var output = File.Create(localPath);
        await input.CopyToAsync(output, cancellationToken);

        await offlineStore.EnqueueAsync(new QueuedUpload
        {
            DossierId = dossierId.ToString(),
            FileName = file.FileName,
            ContentType = file.ContentType ?? "application/octet-stream",
            LocalPath = localPath
        });
    }

    public async Task<DossierDetail?> GetDossierDetailAsync(Guid dossierId, CancellationToken cancellationToken)
    {
        try
        {
            return await apiClient.GetDossierAsync(dossierId, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }
}
