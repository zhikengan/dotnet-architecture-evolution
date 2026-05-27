namespace BuildingBlocks.Infrastructure.Storage;

/// <summary>
/// Abstraction over object storage. Implementations point at MinIO in dev/CI
/// and AWS S3 in prod via the same SDK. Used for binary content (product
/// images, uploaded files) — never for application/domain state, which
/// stays in PostgreSQL.
/// </summary>
public interface IFileStorage
{
    /// <summary>
    /// Generates a presigned URL the caller can PUT to directly, bypassing
    /// the API host so we don't double-bandwidth uploads.
    /// </summary>
    Task<PresignedUploadResult> GeneratePresignedUploadUrlAsync(string key, string contentType, TimeSpan ttl, CancellationToken ct = default);

    /// <summary>Returns a public URL for reads of the given object key.</summary>
    string GeneratePublicUrl(string key);

    /// <summary>True if an object with this key exists in storage.</summary>
    Task<bool> ExistsAsync(string key, CancellationToken ct = default);
}

public sealed record PresignedUploadResult(string UploadUrl, string PublicUrl, DateTime ExpiresAt);
