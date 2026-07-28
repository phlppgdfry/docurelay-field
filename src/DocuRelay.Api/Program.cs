using DocuRelay.Api;
using DocuRelay.Contracts;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<DossierStore>();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/api/dossiers", (DossierStore store) => Results.Ok(store.List()));

app.MapGet("/api/dossiers/{id:guid}", (Guid id, DossierStore store) =>
    store.Get(id) is { } dossier ? Results.Ok(dossier) : Results.NotFound());

app.MapPost("/api/dossiers/{id:guid}/documents", async (
    Guid id,
    IFormFile file,
    DossierStore store,
    CancellationToken cancellationToken) =>
{
    if (file.Length is 0)
    {
        return Results.BadRequest(new { error = "A non-empty document is required." });
    }

    await using var stream = file.OpenReadStream();
    var receipt = await store.AddDocumentAsync(id, file.FileName, file.ContentType, file.Length, stream, cancellationToken);
    return receipt is { } value ? Results.Created($"/api/dossiers/{id}/documents/{value.DocumentId}", value) : Results.NotFound();
}).DisableAntiforgery();

app.Run();

public partial class Program;
