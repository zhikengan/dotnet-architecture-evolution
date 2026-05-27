using System.Security.Cryptography;
using System.Text;
using BuildingBlocks.Application.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Platform.Contracts;
using Platform.Domain.FeatureFlags;
using Platform.Infrastructure.Persistence;

namespace Platform.Infrastructure.FeatureManagement;

/// <summary>
/// Scoped because it needs the request's <see cref="ITenantContext"/> for both
/// the cache key and the DbContext's tenant query filter. A previous design
/// resolved the DbContext via <c>IServiceScopeFactory</c> from a singleton —
/// that loses the ambient tenant.
/// </summary>
public sealed class DbFeatureManager(PlatformDbContext db, ITenantContext tenant, IMemoryCache cache, PlatformOptions options) : IFeatureFlagQuery
{
    public async ValueTask<bool> IsEnabledAsync(string flagName, Guid userId, CancellationToken ct = default)
    {
        var flag = await GetFlagAsync(flagName, ct);
        if (flag is null) return false;

        if (flag.EnabledUserIds.Contains(userId)) return true;
        if (!flag.Enabled) return false;
        if (flag.RolloutPercentage <= 0) return false;
        if (flag.RolloutPercentage >= 100) return true;

        var bucket = ComputeBucket(userId, flagName);
        return bucket < flag.RolloutPercentage;
    }

    private async Task<FeatureFlag?> GetFlagAsync(string name, CancellationToken ct)
    {
        var cacheKey = $"flag::{tenant.TenantId}::{name}";

        if (cache.TryGetValue<FeatureFlag>(cacheKey, out var cached))
            return cached;

        var flag = await db.FeatureFlags.AsNoTracking().FirstOrDefaultAsync(f => f.Id == name, ct);
        if (flag is not null)
        {
            cache.Set(cacheKey, flag, TimeSpan.FromSeconds(options.CacheSeconds));
        }
        return flag;
    }

    public static int ComputeBucket(Guid userId, string flagName)
    {
        var input = Encoding.UTF8.GetBytes(userId.ToString("N") + ":" + flagName);
        var hash = SHA256.HashData(input);
        // Take first 4 bytes as uint, mod 100
        var n = (uint)((hash[0] << 24) | (hash[1] << 16) | (hash[2] << 8) | hash[3]);
        return (int)(n % 100u);
    }
}

public sealed class PlatformOptions
{
    public const string SectionName = "FeatureFlags";
    public int CacheSeconds { get; init; } = 30;
}
