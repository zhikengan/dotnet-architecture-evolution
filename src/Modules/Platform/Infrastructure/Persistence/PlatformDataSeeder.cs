using Microsoft.EntityFrameworkCore;
using Platform.Domain.FeatureFlags;
using Platform.Domain.Tenants;

namespace Platform.Infrastructure.Persistence;

public static class PlatformDataSeeder
{
    public static readonly Guid AcmeTenantId = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    public static readonly Guid GlobexTenantId = new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    public static async Task SeedAsync(PlatformDbContext db, CancellationToken ct = default)
    {
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        if (!await db.Tenants.AnyAsync(ct))
        {
            db.Tenants.Add(Tenant.Create(AcmeTenantId, "acme", "Acme Corp", now).Value);
            db.Tenants.Add(Tenant.Create(GlobexTenantId, "globex", "Globex Inc", now).Value);
        }

        // Seed feature flags per tenant (bypassing the query filter — seeder
        // crosses tenant boundaries by design).
        if (!await db.FeatureFlags.IgnoreQueryFilters().AnyAsync(ct))
        {
            db.FeatureFlags.Add(FeatureFlag.Create("EnablePremiumBadge", AcmeTenantId, enabled: true, rolloutPercentage: 0, now).Value);
            db.FeatureFlags.Add(FeatureFlag.Create("EnablePremiumBadge", GlobexTenantId, enabled: true, rolloutPercentage: 0, now).Value);
        }

        await db.SaveChangesAsync(ct);
    }
}
