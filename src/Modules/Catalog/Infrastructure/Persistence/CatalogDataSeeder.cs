using Catalog.Domain.Products;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Infrastructure.Persistence;

public static class CatalogDataSeeder
{
    public static readonly Guid AcmeSellerId = new("11111111-1111-1111-1111-111111111111");

    public static async Task SeedAsync(CatalogDbContext db, CancellationToken ct = default)
    {
        if (await db.Products.AnyAsync(ct)) return;
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var widget = Product.Create("Widget", Money.Usd(10m), 100, AcmeSellerId, now).Value;
        var gizmo = Product.Create("Gizmo", Money.Usd(25m), 50, AcmeSellerId, now).Value;
        var doohickey = Product.Create("Doohickey", Money.Usd(5m), 0, AcmeSellerId, now).Value;
        widget.ClearDomainEvents();
        gizmo.ClearDomainEvents();
        doohickey.ClearDomainEvents();
        db.Products.AddRange(widget, gizmo, doohickey);
        await db.SaveChangesAsync(ct);
    }
}
