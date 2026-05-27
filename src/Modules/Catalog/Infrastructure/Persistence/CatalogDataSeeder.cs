using Catalog.Domain.Products;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Infrastructure.Persistence;

public static class CatalogDataSeeder
{
    public static readonly Guid AcmeSellerId = new("11111111-1111-1111-1111-111111111111");
    public static readonly Guid AcmeTenantId = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    public static readonly Guid GlobexTenantId = new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    public static async Task SeedAsync(CatalogDbContext db, CancellationToken ct = default)
    {
        // Bypass the tenant query filter — we're seeding across tenants.
        if (await db.Products.IgnoreQueryFilters().AnyAsync(ct)) return;
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // Acme catalog (matches the inherited Tier 3 seed shape so existing
        // tests keep passing with the default tenant).
        var widget = Product.Create("Widget", Money.Usd(10m), 100, AcmeSellerId, AcmeTenantId, now).Value;
        var gizmo = Product.Create("Gizmo", Money.Usd(25m), 50, AcmeSellerId, AcmeTenantId, now).Value;
        var doohickey = Product.Create("Doohickey", Money.Usd(5m), 0, AcmeSellerId, AcmeTenantId, now).Value;

        // Globex catalog (deliberately different so multi-tenancy isolation
        // tests have something to find on the "other side").
        var globexGadget = Product.Create("Globex Gadget", Money.Usd(99m), 10, AcmeSellerId, GlobexTenantId, now).Value;

        foreach (var p in new[] { widget, gizmo, doohickey, globexGadget })
            p.ClearDomainEvents();

        db.Products.AddRange(widget, gizmo, doohickey, globexGadget);
        await db.SaveChangesAsync(ct);
    }
}
