using System.Threading.RateLimiting;
using BuildingBlocks.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Api;

/// <summary>
/// Two policies for the marketplace:
/// <list type="bullet">
///   <item><c>per-user-writes</c> — token bucket; applied to POST/PUT/DELETE.</item>
///   <item><c>per-user-reads</c> — fixed window; applied to GET.</item>
/// </list>
/// Both partition on <see cref="ICurrentUser.UserId"/> so a noisy client can't
/// degrade the experience for other tenants/users. Returns 429 with a
/// <c>Retry-After</c> header so clients can back off correctly. Limits are
/// config-driven via <c>RateLimit:Writes</c> / <c>RateLimit:Reads</c> so tests
/// can opt into looser bounds without rewriting the policies.
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

            opts.AddPolicy(WritesPolicy, http =>
            {
                var user = http.RequestServices.GetRequiredService<ICurrentUser>();
                var partitionKey = user.UserId == Guid.Empty ? PartitionAnonymous(http) : user.UserId.ToString();
                return RateLimitPartition.GetTokenBucketLimiter(partitionKey, _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = writeLimit,
                    TokensPerPeriod = writeLimit,
                    ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                    AutoReplenishment = true,
                });
            });

            opts.AddPolicy(ReadsPolicy, http =>
            {
                var user = http.RequestServices.GetRequiredService<ICurrentUser>();
                var partitionKey = user.UserId == Guid.Empty ? PartitionAnonymous(http) : user.UserId.ToString();
                return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = readLimit,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                });
            });
        });

        return services;
    }

    // Anonymous callers (e.g., discovery endpoints) get their own bucket keyed
    // off the remote IP so a stampede on /demo/token doesn't drag down everyone.
    private static string PartitionAnonymous(HttpContext http) =>
        "anon::" + (http.Connection.RemoteIpAddress?.ToString() ?? "unknown");
}
