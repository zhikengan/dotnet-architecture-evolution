using BuildingBlocks.Application;
using Identity.Domain.Tenants;
using Identity.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Persistence;

public static class IdentityDataSeeder
{
    public static readonly Guid AcmeTenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    public static readonly Guid GlobexTenantId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    public static readonly Guid SellerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid BuyerId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid AdminId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    public static async Task SeedAsync(IdentityDbContext db, IClock clock, CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        if (!await db.Tenants.AnyAsync(ct))
        {
            db.Tenants.AddRange(
                Tenant.Seed(AcmeTenantId, "acme", now),
                Tenant.Seed(GlobexTenantId, "globex", now));
        }
        if (!await db.Users.AnyAsync(ct))
        {
            db.Users.AddRange(
                User.Seed(SellerId, AcmeTenantId, "acme-seller@example.com", UserRole.Seller, now),
                User.Seed(BuyerId, AcmeTenantId, "john-buyer@example.com", UserRole.Buyer, now),
                User.Seed(AdminId, AcmeTenantId, "root-admin@example.com", UserRole.Admin, now));
        }
        await db.SaveChangesAsync(ct);
    }
}
