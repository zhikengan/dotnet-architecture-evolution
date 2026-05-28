using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Api;

/// <summary>
/// Two policies for the marketplace BFFs:
///   <c>per-user-writes</c> — token bucket; applied to POST/PUT/DELETE.
///   <c>per-user-reads</c> — fixed window; applied to GET.
/// Partitioned on the JWT <c>sub</c> claim so a noisy client can't degrade the
/// experience for other tenants/users. Anonymous callers (discovery/health)
/// get a per-IP bucket. Returns 429 with <c>Retry-After</c>. Limits are
/// config-driven via <c>RateLimit:Writes</c> / <c>RateLimit:Reads</c>.
/// </summary>
public static class RateLimiting
{
    public const string WritesPolicy = "per-user-writes";
    public const string ReadsPolicy = "per-user-reads";

    public static IServiceCollection AddMarketplaceRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var writeLimit = configuration.GetValue("RateLimit:Writes", 10);
        var readLimit = configuration.GetValue("RateLimit:Reads", 100);

        services.AddRateLimiter(opts =>
        {
            opts.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            opts.OnRejected = static async (ctx, ct) =>
            {
                if (ctx.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    ctx.HttpContext.Response.Headers.RetryAfter =
                        ((int)retryAfter.TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
                ctx.HttpContext.Response.ContentType = "application/problem+json";
                await ctx.HttpContext.Response.WriteAsync(
                    """{"type":"https://tools.ietf.org/html/rfc6585#section-4","title":"Too Many Requests","status":429}""",
                    ct);
            };

            opts.AddPolicy(WritesPolicy, http => RateLimitPartition.GetTokenBucketLimiter(
                Partition(http),
                _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = writeLimit,
                    TokensPerPeriod = writeLimit,
                    ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                    AutoReplenishment = true,
                }));

            opts.AddPolicy(ReadsPolicy, http => RateLimitPartition.GetFixedWindowLimiter(
                Partition(http),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = readLimit,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));

            // BFFs route everything dynamically through YARP, so a single global
            // limiter branches per HTTP method into the appropriate partition.
            // Health/metadata endpoints (GET /health) are exempted to keep probes
            // and discovery cheap.
            opts.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(http =>
            {
                if (HttpMethods.IsGet(http.Request.Method))
                {
                    if (http.Request.Path.StartsWithSegments("/health") ||
                        http.Request.Path.StartsWithSegments("/.well-known"))
                    {
                        return RateLimitPartition.GetNoLimiter("exempt");
                    }
                    return RateLimitPartition.GetFixedWindowLimiter(Partition(http), _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = readLimit,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    });
                }
                return RateLimitPartition.GetTokenBucketLimiter(Partition(http), _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = writeLimit,
                    TokensPerPeriod = writeLimit,
                    ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                    AutoReplenishment = true,
                });
            });
        });

        return services;
    }

    private static string Partition(HttpContext http)
    {
        var sub = http.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                  ?? http.User?.FindFirst("sub")?.Value;
        return string.IsNullOrEmpty(sub)
            ? "anon::" + (http.Connection.RemoteIpAddress?.ToString() ?? "unknown")
            : sub;
    }
}
