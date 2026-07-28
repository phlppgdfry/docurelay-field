namespace DocuRelay.Mobile.Models;

public sealed class CachedDossier
{
    [SQLite.PrimaryKey]
    public string Id { get; set; } = string.Empty;

    public string Reference { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public long UpdatedAtUnixMs { get; set; }
    public int DocumentCount { get; set; }
}

