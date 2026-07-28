namespace DocuRelay.Contracts;

public sealed record DossierSummary(
    Guid Id,
    string Reference,
    string Title,
    string Location,
    DateTimeOffset UpdatedAt,
    int DocumentCount);

public sealed record DossierDetail(
    Guid Id,
    string Reference,
    string Title,
    string Location,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<DocumentSummary> Documents);

public sealed record DocumentSummary(
    Guid Id,
    string FileName,
    string ContentType,
    long SizeBytes,
    DateTimeOffset UploadedAt,
    string Status);

public sealed record UploadReceipt(
    Guid DocumentId,
    string FileName,
    DateTimeOffset UploadedAt,
    string Status);

