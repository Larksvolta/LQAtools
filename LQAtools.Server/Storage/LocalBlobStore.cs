using System.Security.Cryptography;

namespace LQAtools.Server.Storage;

public sealed class LocalBlobStore : IBlobStore
{
    private readonly string _root;

    public LocalBlobStore(string root)
    {
        _root = root;
        Directory.CreateDirectory(_root);
    }

    public async Task<StoredBlob> SaveAsync(Stream content, string fileName, string contentType, CancellationToken ct = default)
    {
        var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        buffer.Position = 0;

        var hash = Convert.ToHexString(SHA256.HashData(buffer.ToArray())).ToLowerInvariant();
        var ext = NormalizeExtension(Path.GetExtension(fileName), contentType);
        var key = $"{hash[..2]}/{hash}{ext}";

        var path = Path.Combine(_root, key.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (!File.Exists(path))
        {
            buffer.Position = 0;
            await using var output = File.Create(path);
            await buffer.CopyToAsync(output, ct);
        }

        return new StoredBlob(key, hash, buffer.Length);
    }

    public Task<Stream?> OpenReadAsync(string key, CancellationToken ct = default)
    {
        var path = ResolvePath(key);
        if (path is null || !File.Exists(path))
            return Task.FromResult<Stream?>(null);
        return Task.FromResult<Stream?>(File.OpenRead(path));
    }

    public Task<bool> ExistsAsync(string key, CancellationToken ct = default)
    {
        var path = ResolvePath(key);
        return Task.FromResult(path is not null && File.Exists(path));
    }

    public Task DeleteAsync(string key, CancellationToken ct = default)
    {
        var path = ResolvePath(key);
        if (path is not null && File.Exists(path))
            File.Delete(path);
        return Task.CompletedTask;
    }

    private string? ResolvePath(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Contains("..") || Path.IsPathRooted(key))
            return null;
        var path = Path.GetFullPath(Path.Combine(_root, key.Replace('/', Path.DirectorySeparatorChar)));
        var root = Path.GetFullPath(_root) + Path.DirectorySeparatorChar;
        return path.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? path : null;
    }

    private static string NormalizeExtension(string extension, string contentType)
    {
        if (!string.IsNullOrWhiteSpace(extension) && extension.Length <= 10)
            return extension.ToLowerInvariant();

        return contentType.ToLowerInvariant() switch
        {
            "image/png" => ".png",
            "image/jpeg" => ".jpg",
            "image/gif" => ".gif",
            "image/bmp" => ".bmp",
            "image/webp" => ".webp",
            _ => ".bin"
        };
    }
}
