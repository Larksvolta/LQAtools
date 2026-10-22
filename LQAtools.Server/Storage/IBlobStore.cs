namespace LQAtools.Server.Storage;

public sealed record StoredBlob(string Key, string ContentHash, long SizeBytes);

public interface IBlobStore
{
    Task<StoredBlob> SaveAsync(Stream content, string fileName, string contentType, CancellationToken ct = default);
    Task<Stream?> OpenReadAsync(string key, CancellationToken ct = default);
    Task<bool> ExistsAsync(string key, CancellationToken ct = default);
    Task DeleteAsync(string key, CancellationToken ct = default);
}
