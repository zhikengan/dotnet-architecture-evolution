using BuildingBlocks.Application;
using Microsoft.EntityFrameworkCore;
using Platform.Domain.FeatureFlags;

namespace Platform.Infrastructure.Persistence;

public static class PlatformDataSeeder
{
    public static readonly Guid AcmeTenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    public static readonly Guid GlobexTenantId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    public static async Task SeedAsync(PlatformDbContext db, IClock clock, CancellationToken ct = default)
    {
        if (await db.FeatureFlags.AnyAsync(ct)) return;
        var now = clock.UtcNow;
        db.FeatureFlags.AddRange(
            // Acme: globally on, partial rollout — Tier 4 demo style.
            FeatureFlag.Create(AcmeTenantId, "EnablePremiumBadge", isEnabled: true, rolloutPercentage: 0, now),
            // Globex: off entirely.
            FeatureFlag.Create(GlobexTenantId, "EnablePremiumBadge", isEnabled: false, rolloutPercentage: 0, now));
        await db.SaveChangesAsync(ct);
    }
}
