using Microsoft.EntityFrameworkCore;
using Platform.Domain.FeatureFlags;

namespace Platform.Infrastructure.Persistence;

public static class PlatformDataSeeder
{
    public static async Task SeedAsync(PlatformDbContext db, CancellationToken ct = default)
    {
        if (await db.FeatureFlags.AnyAsync(ct)) return;
        var flag = FeatureFlag.Create("EnablePremiumBadge", enabled: true, rolloutPercentage: 0, DateTime.UtcNow).Value;
        db.FeatureFlags.Add(flag);
        await db.SaveChangesAsync(ct);
    }
}
