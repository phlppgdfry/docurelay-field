using System.Net.Http.Json;
using DocuRelay.Contracts;

namespace DocuRelay.Mobile.Services;

public sealed class DossierApiClient(HttpClient httpClient)
{
    public async Task<IReadOnlyList<DossierSummary>> GetDossiersAsync(CancellationToken cancellationToken)
    {
        return await httpClient.GetFromJsonAsync<IReadOnlyList<DossierSummary>>("api/dossiers", cancellationToken)
            ?? [];
    }

    public async Task UploadAsync(
        Guid dossierId,
        string fileName,
        string contentType,
        Stream stream,
        CancellationToken cancellationToken)
    {
        using var form = new MultipartFormDataContent();
        using var content = new StreamContent(stream);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        form.Add(content, "file", fileName);

        using var response = await httpClient.PostAsync($"api/dossiers/{dossierId}/documents", form, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}

