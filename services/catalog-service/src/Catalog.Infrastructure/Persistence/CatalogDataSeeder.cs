using BuildingBlocks.Application;
using Catalog.Domain.Products;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Infrastructure.Persistence;

public static class CatalogDataSeeder
{
    public static readonly Guid AcmeTenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    public static readonly Guid SellerId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public static readonly Guid WidgetId = Guid.Parse("c0c0c0c0-0000-0000-0000-000000000001");
    public static readonly Guid GizmoId = Guid.Parse("c0c0c0c0-0000-0000-0000-000000000002");
    public static readonly Guid DoohickeyId = Guid.Parse("c0c0c0c0-0000-0000-0000-000000000003");

    public static async Task SeedAsync(CatalogDbContext db, IClock clock, CancellationToken ct = default)
    {
        if (await db.Products.IgnoreQueryFilters().AnyAsync(ct)) return;
        var now = clock.UtcNow;
        db.Products.AddRange(
            Product.Seed(WidgetId, AcmeTenantId, "Widget", 10m, 100, SellerId, now),
            Product.Seed(GizmoId, AcmeTenantId, "Gizmo", 25m, 50, SellerId, now),
            Product.Seed(DoohickeyId, AcmeTenantId, "Doohickey", 5m, 0, SellerId, now));
        await db.SaveChangesAsync(ct);
    }
}
