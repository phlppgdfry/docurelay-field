using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DocuRelay.Api;
using DocuRelay.Contracts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace DocuRelay.Api.IntegrationTests;

public sealed class DocumentProcessingWorkflowTests : IClassFixture<DocuRelayApiFactory>
{
    private readonly HttpClient _client;

    public DocumentProcessingWorkflowTests(DocuRelayApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Demo_console_reads_the_same_json_field_names_the_api_returns()
    {
        var page = await _client.GetStringAsync("/demo");
        var dossiers = await _client.GetStringAsync("/api/dossiers");

        // The console once read processingStatus/uploadedAtUtc while the API returns
        // status/uploadedAt, and then reported a healthy API as unreachable.
        Assert.Contains("\"reference\"", dossiers);
        Assert.Contains("doc.status", page);
        Assert.Contains("doc.uploadedAt", page);
        Assert.DoesNotContain("processingStatus", page);
        Assert.DoesNotContain("uploadedAtUtc", page);
    }

    [Fact]
    public async Task Upload_is_dispatched_and_completed_by_the_local_worker()
    {
        var dossiers = await _client.GetFromJsonAsync<IReadOnlyList<DossierSummary>>("/api/dossiers");
        var dossier = Assert.Single(dossiers!, item => item.Reference == "DR-2026-0042");

        using var form = new MultipartFormDataContent();
        using var content = new ByteArrayContent("maintenance evidence"u8.ToArray());
        content.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        form.Add(content, "file", "maintenance-note.txt");

        var uploadResponse = await _client.PostAsync($"/api/dossiers/{dossier.Id}/documents", form);
        Assert.Equal(HttpStatusCode.Created, uploadResponse.StatusCode);
        var receipt = await uploadResponse.Content.ReadFromJsonAsync<UploadReceipt>();
        Assert.NotNull(receipt);
        Assert.Equal("Pending", receipt.Status);

        DocumentSummary? document = null;
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var detail = await _client.GetFromJsonAsync<DossierDetail>($"/api/dossiers/{dossier.Id}");
            document = detail!.Documents.SingleOrDefault(item => item.Id == receipt.DocumentId);
            if (document?.Status == "Completed") break;
            await Task.Delay(250);
        }

        Assert.NotNull(document);
        Assert.Equal("maintenance-note.txt", document.FileName);
        Assert.Equal("Completed", document.Status);
    }
}

public sealed class DocuRelayApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
    }
}
