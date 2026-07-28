using DocuRelay.Contracts;

namespace DocuRelay.Api;

public sealed class DossierStore
{
    private readonly Dictionary<Guid, Dossier> _dossiers = Seed();
    private readonly object _gate = new();

    public IReadOnlyList<DossierSummary> List()
    {
        lock (_gate)
        {
            return _dossiers.Values
                .OrderByDescending(dossier => dossier.UpdatedAt)
                .Select(dossier => dossier.ToSummary())
                .ToArray();
        }
    }

    public DossierDetail? Get(Guid id)
    {
        lock (_gate)
        {
            return _dossiers.TryGetValue(id, out var dossier) ? dossier.ToDetail() : null;
        }
    }

    public async Task<UploadReceipt?> AddDocumentAsync(
        Guid dossierId,
        string fileName,
        string contentType,
        long sizeBytes,
        Stream content,
        CancellationToken cancellationToken)
    {
        // Reading the stream makes the endpoint exercise the same multipart path as production.
        await content.CopyToAsync(Stream.Null, cancellationToken);

        lock (_gate)
        {
            if (!_dossiers.TryGetValue(dossierId, out var dossier))
            {
                return null;
            }

            var document = new Document(Guid.NewGuid(), fileName, contentType, sizeBytes, DateTimeOffset.UtcNow, "Synced");
            dossier.Documents.Add(document);
            dossier.UpdatedAt = document.UploadedAt;
            return new UploadReceipt(document.Id, document.FileName, document.UploadedAt, document.Status);
        }
    }

    private static Dictionary<Guid, Dossier> Seed()
    {
        var first = new Dossier(Guid.Parse("cb0a7aad-05ff-42d7-b6dd-182b98ff11da"), "DR-2026-0042", "Cooling unit maintenance", "Brugge", DateTimeOffset.UtcNow.AddHours(-2));
        first.Documents.Add(new Document(Guid.NewGuid(), "inspection-photo.jpg", "image/jpeg", 394_824, DateTimeOffset.UtcNow.AddDays(-1), "Synced"));
        var second = new Dossier(Guid.Parse("5f119a79-69e5-49bf-a77a-b3d37e72579f"), "DR-2026-0041", "Safety documentation review", "Zeebrugge", DateTimeOffset.UtcNow.AddDays(-1));
        return new[] { first, second }.ToDictionary(dossier => dossier.Id);
    }

    private sealed class Dossier(Guid id, string reference, string title, string location, DateTimeOffset updatedAt)
    {
        public Guid Id { get; } = id;
        public string Reference { get; } = reference;
        public string Title { get; } = title;
        public string Location { get; } = location;
        public DateTimeOffset UpdatedAt { get; set; } = updatedAt;
        public List<Document> Documents { get; } = [];

        public DossierSummary ToSummary() => new(Id, Reference, Title, Location, UpdatedAt, Documents.Count);

        public DossierDetail ToDetail() => new(
            Id,
            Reference,
            Title,
            Location,
            UpdatedAt,
            Documents.Select(document => new DocumentSummary(document.Id, document.FileName, document.ContentType, document.SizeBytes, document.UploadedAt, document.Status)).ToArray());
    }

    private sealed record Document(Guid Id, string FileName, string ContentType, long SizeBytes, DateTimeOffset UploadedAt, string Status);
}
