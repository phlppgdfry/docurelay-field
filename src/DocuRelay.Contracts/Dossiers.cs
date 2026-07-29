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

/// <summary>
/// The versioned contract sent from the API to Azure Service Bus.  Keep this project free of
/// Azure SDK dependencies so the producer and consumer agree on the payload, not on a library.
/// </summary>
public sealed record DocumentProcessingMessage(
    Guid DocumentId,
    Guid DossierId,
    DateTimeOffset QueuedAtUtc,
    string CorrelationId);
