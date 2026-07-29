using DocuRelay.Api;
using DocuRelay.Contracts;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi();

var sqlConnection = builder.Configuration.GetConnectionString("AzureSql");
if (string.IsNullOrWhiteSpace(sqlConnection))
{
    // Keeps the portfolio project runnable with no Azure account. Azure configuration always takes precedence.
    builder.Services.AddDbContext<DocuRelayDbContext>(options => options.UseInMemoryDatabase("DocuRelay.Local"));
}
else
{
    builder.Services.AddDbContext<DocuRelayDbContext>(options => options.UseSqlServer(sqlConnection));
}
builder.Services.AddHostedService<DatabaseInitializer>();
builder.Services.AddSingleton<IUploadedDocumentStorage, LocalUploadedDocumentStorage>();
builder.Services.AddDocumentQueue(builder.Configuration);
builder.Services.AddHostedService<OutboxDispatcher>();

var entraEnabled = !string.IsNullOrWhiteSpace(builder.Configuration["AzureAd:TenantId"]);
if (entraEnabled)
{
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));
    builder.Services.AddAuthorization();
}

var app = builder.Build();
app.MapOpenApi();
if (entraEnabled)
{
    app.UseAuthentication();
    app.UseAuthorization();
}

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "DocuRelay.Api" })).AllowAnonymous();
app.MapGet("/openapi", () => Results.Redirect("/openapi/v1.json")).AllowAnonymous();
app.MapGet("/demo", () => Results.Content(LocalDemoPage.Html, "text/html; charset=utf-8")).AllowAnonymous();

var api = app.MapGroup("/api");
if (entraEnabled)
{
    api.RequireAuthorization();
}

api.MapGet("/dossiers", async (DocuRelayDbContext database, CancellationToken cancellationToken) =>
{
    var dossiers = await database.Dossiers.AsNoTracking()
        .OrderByDescending(dossier => dossier.UpdatedAtUtc)
        .Select(dossier => new DossierSummary(dossier.Id, dossier.Reference, dossier.Title, dossier.Location, dossier.UpdatedAtUtc, dossier.Documents.Count))
        .ToListAsync(cancellationToken);
    return Results.Ok(dossiers);
});

api.MapGet("/dossiers/{id:guid}", async (Guid id, DocuRelayDbContext database, CancellationToken cancellationToken) =>
{
    var dossier = await database.Dossiers.AsNoTracking().Where(item => item.Id == id)
        .Select(item => new DossierDetail(item.Id, item.Reference, item.Title, item.Location, item.UpdatedAtUtc,
            item.Documents.OrderByDescending(document => document.UploadedAtUtc)
                .Select(document => new DocumentSummary(document.Id, document.FileName, document.ContentType, document.SizeBytes, document.UploadedAtUtc, document.ProcessingStatus)).ToList()))
        .SingleOrDefaultAsync(cancellationToken);
    return dossier is null ? Results.NotFound() : Results.Ok(dossier);
});

api.MapPost("/dossiers/{id:guid}/documents", async (
    Guid id,
    IFormFile file,
    DocuRelayDbContext database,
    IUploadedDocumentStorage storage,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    if (file.Length is 0 || file.Length > 10 * 1024 * 1024)
    {
        return Results.BadRequest(new { error = "A document between 1 byte and 10 MB is required." });
    }

    var dossier = await database.Dossiers.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
    if (dossier is null)
    {
        return Results.NotFound();
    }

    var document = new DocumentEntity
    {
        Id = Guid.NewGuid(),
        DossierId = id,
        FileName = Path.GetFileName(file.FileName),
        ContentType = file.ContentType ?? "application/octet-stream",
        SizeBytes = file.Length,
        UploadedAtUtc = DateTimeOffset.UtcNow,
        ProcessingStatus = "Pending",
        StoragePath = string.Empty
    };
    await using (var stream = file.OpenReadStream())
    {
        document.StoragePath = await storage.SaveAsync(document.Id, stream, cancellationToken);
    }
    database.Documents.Add(document);
    dossier.UpdatedAtUtc = document.UploadedAtUtc;
    var message = new DocumentProcessingMessage(document.Id, id, DateTimeOffset.UtcNow, httpContext.TraceIdentifier);
    database.OutboxMessages.Add(new OutboxMessageEntity
    {
        Id = Guid.NewGuid(),
        DocumentId = document.Id,
        Payload = System.Text.Json.JsonSerializer.Serialize(message),
        CreatedAtUtc = DateTimeOffset.UtcNow
    });
    await database.SaveChangesAsync(cancellationToken);
    return Results.Created($"/api/dossiers/{id}/documents/{document.Id}", new UploadReceipt(document.Id, document.FileName, document.UploadedAtUtc, document.ProcessingStatus));
}).DisableAntiforgery();

api.MapGet("/dossiers/{dossierId:guid}/documents/{documentId:guid}/content", async (
    Guid dossierId,
    Guid documentId,
    DocuRelayDbContext database,
    IUploadedDocumentStorage storage,
    CancellationToken cancellationToken) =>
{
    var document = await database.Documents.AsNoTracking()
        .SingleOrDefaultAsync(item => item.Id == documentId && item.DossierId == dossierId, cancellationToken);
    if (document is null)
    {
        return Results.NotFound();
    }

    var content = await storage.OpenReadAsync(document.StoragePath, cancellationToken);
    return content is null
        ? Results.NotFound(new { error = "The uploaded document content is unavailable." })
        : Results.File(content, document.ContentType, document.FileName, enableRangeProcessing: true);
});

app.Run();

public partial class Program;
