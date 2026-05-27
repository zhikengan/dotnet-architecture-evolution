using Microsoft.Extensions.Options;

namespace BuildingBlocks.Infrastructure.Storage;

/// <summary>
/// Filesystem-backed storage for unit tests and offline dev. The "presigned"
/// URL is a <c>file://</c> URI so the test can verify the upload flow without
/// running a real S3-compatible server. Never use in production — the URL
/// does not actually upload anything.
/// </summary>
public sealed class LocalFileStorage : IFileStorage
{
    private readonly string _root;

    public LocalFileStorage(IOptions<StorageOptions> options)
    {
        _root = string.IsNullOrWhiteSpace(options.Value.LocalRoot)
            ? Path.Combine(Path.GetTempPath(), "marketplace-local-storage")
            : options.Value.LocalRoot;
        Directory.CreateDirectory(_root);
    }

    public Task<PresignedUploadResult> GeneratePresignedUploadUrlAsync(
        string key, string contentType, TimeSpan ttl, CancellationToken ct = default)
    {
        var path = Path.Combine(_root, key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var uri = new Uri(path).AbsoluteUri;
        return Task.FromResult(new PresignedUploadResult(uri, uri, DateTime.UtcNow.Add(ttl)));
    }

    public string GeneratePublicUrl(string key) => new Uri(Path.Combine(_root, key)).AbsoluteUri;

    public Task<bool> ExistsAsync(string key, CancellationToken ct = default) =>
        Task.FromResult(File.Exists(Path.Combine(_root, key)));
}
