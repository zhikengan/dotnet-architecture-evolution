using System.ComponentModel.DataAnnotations;

namespace BuildingBlocks.Infrastructure.Storage;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>"S3" (MinIO + AWSSDK) or "Local" (filesystem) — DI picks the impl.</summary>
    [Required]
    public string Provider { get; init; } = "S3";

    /// <summary>S3 endpoint URL. For MinIO: http://minio:9000 in docker, http://localhost:9000 in dev.</summary>
    public string Endpoint { get; init; } = string.Empty;

    /// <summary>Public-facing base URL clients use to GET objects. May differ from <see cref="Endpoint"/> behind a CDN.</summary>
    public string PublicEndpoint { get; init; } = string.Empty;

    public string AccessKey { get; init; } = string.Empty;
    public string SecretKey { get; init; } = string.Empty;
    public string Region { get; init; } = "us-east-1";

    [Required]
    public string Bucket { get; init; } = "product-images";

    /// <summary>Where the Local provider writes files (tests + offline dev).</summary>
    public string LocalRoot { get; init; } = string.Empty;
}
