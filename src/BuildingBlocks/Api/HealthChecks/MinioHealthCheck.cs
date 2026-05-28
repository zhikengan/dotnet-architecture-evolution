using Amazon.S3;
using BuildingBlocks.Infrastructure.Storage;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace BuildingBlocks.Api.HealthChecks;

/// <summary>
/// Lightweight liveness probe for the S3-compatible store (MinIO in dev/CI,
/// AWS S3 in prod). Calls <c>HeadBucket</c>; success = bucket reachable.
/// Returns degraded (not unhealthy) on failure so dependent infra hiccups
/// don't take the API offline — the seller upload flow is degraded, but
/// browse/order paths still work.
/// </summary>
public sealed class MinioHealthCheck(IOptions<StorageOptions> options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var opts = options.Value;
        var s3Config = new AmazonS3Config
        {
            ServiceURL = opts.Endpoint,
            ForcePathStyle = true,
            UseHttp = opts.Endpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase),
            AuthenticationRegion = opts.Region,
        };
        var creds = new Amazon.Runtime.BasicAWSCredentials(opts.AccessKey, opts.SecretKey);
        using var s3 = new AmazonS3Client(creds, s3Config);
        try
        {
            await s3.GetBucketLocationAsync(opts.Bucket, cancellationToken);
            return HealthCheckResult.Healthy($"bucket={opts.Bucket}");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Degraded($"Storage unreachable: {ex.Message}");
        }
    }
}
