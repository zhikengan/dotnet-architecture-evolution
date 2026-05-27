using Marketplace.Domain.Products;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Infrastructure.Persistence;

public static class DataSeeder
{
    public static readonly Guid AcmeSellerId = new("11111111-1111-1111-1111-111111111111");
    public static readonly DateTime SeedTime = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public static async Task SeedAsync(AppDbContext db, CancellationToken ct = default)
    {
        if (await db.Products.AnyAsync(ct)) return;

        var products = new[]
        {
            Product.Create("Widget", Money.Usd(10m), 100, AcmeSellerId, SeedTime).Value,
            Product.Create("Gizmo", Money.Usd(25m), 50, AcmeSellerId, SeedTime).Value,
            Product.Create("Doohickey", Money.Usd(5m), 0, AcmeSellerId, SeedTime).Value,
        };

        foreach (var p in products) p.ClearDomainEvents();

        db.Products.AddRange(products);
        await db.SaveChangesAsync(ct);
    }
}
