using System.Security.Cryptography;
using System.Text;
using BuildingBlocks.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Platform.Application.Abstractions;

namespace Platform.Application.FeatureFlags.Queries;

public sealed record IsFeatureEnabledQuery(Guid TenantId, string Key, Guid? UserId) : IRequest<Result<bool>>;

/// <summary>
/// Evaluates a flag for a (tenant, key, user) tuple. Matches Tier 4's
/// semantics in order:
/// <list type="number">
///   <item>If the flag doesn't exist → false.</item>
///   <item>If the user is in <c>EnabledUserIds</c> → true (even with rollout 0%).</item>
///   <item>If global <c>IsEnabled</c> is false → false.</item>
///   <item>Otherwise bucket the user via SHA256(userId + key) mod 100 and
///         compare to <c>RolloutPercentage</c>. Anonymous (no userId) sees the
///         flag only when rollout is &gt;= 100%.</item>
/// </list>
/// </summary>
public sealed class IsFeatureEnabledHandler(IPlatformDbContext db)
    : IRequestHandler<IsFeatureEnabledQuery, Result<bool>>
{
    public async Task<Result<bool>> Handle(IsFeatureEnabledQuery q, CancellationToken ct)
    {
        if (q.TenantId == Guid.Empty || string.IsNullOrWhiteSpace(q.Key))
            return Result.Success(false);

        var flag = await db.FeatureFlags.AsNoTracking()
            .FirstOrDefaultAsync(f => f.TenantId == q.TenantId && f.Key == q.Key, ct);
        if (flag is null) return Result.Success(false);

        if (q.UserId is { } uid && flag.EnabledUserIds.Contains(uid))
            return Result.Success(true);

        if (!flag.IsEnabled) return Result.Success(false);
        if (flag.RolloutPercentage >= 100) return Result.Success(true);
        if (flag.RolloutPercentage <= 0) return Result.Success(false);

        if (q.UserId is not { } userId) return Result.Success(false);
        var bucket = ComputeBucket(userId, q.Key);
        return Result.Success(bucket < flag.RolloutPercentage);
    }

    /// <summary>SHA256(userId.N + ":" + key) mod 100 — sticky per user/flag.</summary>
    public static int ComputeBucket(Guid userId, string key)
    {
        var input = Encoding.UTF8.GetBytes(userId.ToString("N") + ":" + key);
        var hash = SHA256.HashData(input);
        var n = (uint)((hash[0] << 24) | (hash[1] << 16) | (hash[2] << 8) | hash[3]);
        return (int)(n % 100u);
    }
}
