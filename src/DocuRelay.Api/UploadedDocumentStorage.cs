namespace DocuRelay.Api;

public interface IUploadedDocumentStorage
{
    Task<string> SaveAsync(Guid documentId, Stream content, CancellationToken cancellationToken);
    Task<Stream?> OpenReadAsync(string storagePath, CancellationToken cancellationToken);
}

/// <summary>
/// Local implementation used for a cost-free demo. Swap this implementation for Azure Blob
/// Storage later without changing the upload or download API contracts.
/// </summary>
public sealed class LocalUploadedDocumentStorage(IWebHostEnvironment environment) : IUploadedDocumentStorage
{
    private readonly string _root = Path.Combine(environment.ContentRootPath, "App_Data", "documents");

    public async Task<string> SaveAsync(Guid documentId, Stream content, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_root);
        var relativePath = Path.Combine("documents", documentId.ToString("N"));
        var absolutePath = Path.Combine(environment.ContentRootPath, "App_Data", relativePath);
        await using var destination = File.Create(absolutePath);
        await content.CopyToAsync(destination, cancellationToken);
        return relativePath;
    }

    public Task<Stream?> OpenReadAsync(string storagePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var absolutePath = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "App_Data", storagePath));
        var rootPath = Path.GetFullPath(_root) + Path.DirectorySeparatorChar;
        if (!absolutePath.StartsWith(rootPath, StringComparison.Ordinal) || !File.Exists(absolutePath))
        {
            return Task.FromResult<Stream?>(null);
        }

        return Task.FromResult<Stream?>(File.OpenRead(absolutePath));
    }
}
