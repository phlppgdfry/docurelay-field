namespace DocuRelay.Mobile.Models;

public sealed class QueuedUpload
{
    [SQLite.PrimaryKey]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string DossierId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public string LocalPath { get; set; } = string.Empty;
    public int AttemptCount { get; set; }
    public string State { get; set; } = UploadState.Pending.ToString();
    public string? LastError { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public enum UploadState
{
    Pending,
    Retrying,
    Synced,
    Failed
}

