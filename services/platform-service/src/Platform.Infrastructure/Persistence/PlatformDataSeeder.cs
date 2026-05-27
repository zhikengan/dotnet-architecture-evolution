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
        db.FeatureFlags.AddRange(
            FeatureFlag.Create(AcmeTenantId, "EnablePremiumBadge", isEnabled: true, clock.UtcNow),
            FeatureFlag.Create(GlobexTenantId, "EnablePremiumBadge", isEnabled: false, clock.UtcNow));
        await db.SaveChangesAsync(ct);
    }
}
