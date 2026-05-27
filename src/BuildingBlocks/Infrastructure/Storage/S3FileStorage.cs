using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace BuildingBlocks.Infrastructure.Storage;

/// <summary>
/// MinIO-compatible S3 storage. <c>ForcePathStyle = true</c> is required for
/// MinIO; the same SDK works against real AWS S3 with that flag flipped off
/// + region-routed virtual hosting. Presigned URLs are short-lived (caller-
/// specified TTL) so a leaked URL self-expires.
/// </summary>
public sealed class S3FileStorage : IFileStorage
{
    private readonly StorageOptions _opts;
    private readonly IAmazonS3 _s3;

    public S3FileStorage(IOptions<StorageOptions> options)
    {
        _opts = options.Value;
        var s3Config = new AmazonS3Config
        {
            ServiceURL = _opts.Endpoint,
            ForcePathStyle = true,
            UseHttp = _opts.Endpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase),
            AuthenticationRegion = _opts.Region,
        };
        var creds = new BasicAWSCredentials(_opts.AccessKey, _opts.SecretKey);
        _s3 = new AmazonS3Client(creds, s3Config);
    }

    public async Task<PresignedUploadResult> GeneratePresignedUploadUrlAsync(
        string key, string contentType, TimeSpan ttl, CancellationToken ct = default)
    {
        var expiresAt = DateTime.UtcNow.Add(ttl);
        var req = new GetPreSignedUrlRequest
        {
            BucketName = _opts.Bucket,
            Key = key,
            Verb = HttpVerb.PUT,
            Expires = expiresAt,
            ContentType = contentType,
        };
        var url = await _s3.GetPreSignedURLAsync(req);
        return new PresignedUploadResult(url, GeneratePublicUrl(key), expiresAt);
    }

    public string GeneratePublicUrl(string key)
    {
        // PublicEndpoint allows the public URL to differ from the SDK endpoint
        // (e.g., minio:9000 from the API container vs localhost:9000 from the
        // browser). Falls back to the SDK endpoint when not configured.
        var basePublic = string.IsNullOrWhiteSpace(_opts.PublicEndpoint) ? _opts.Endpoint : _opts.PublicEndpoint;
        return $"{basePublic.TrimEnd('/')}/{_opts.Bucket}/{key}";
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken ct = default)
    {
        try
        {
            await _s3.GetObjectMetadataAsync(_opts.Bucket, key, ct);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }
}
