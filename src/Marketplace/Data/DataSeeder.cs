using Marketplace.Models;

namespace Marketplace.Data;

public static class DataSeeder
{
    public static readonly Guid AcmeSellerId = new("11111111-1111-1111-1111-111111111111");

    public static readonly Guid WidgetId = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    public static readonly Guid GizmoId = new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    public static readonly Guid DoohickeyId = new("cccccccc-cccc-cccc-cccc-cccccccccccc");

    public static void Seed(AppDbContext db)
    {
        if (db.Products.Any())
        {
            return;
        }

        db.Products.AddRange(
            new Product
            {
                Id = WidgetId,
                Name = "Widget",
                Price = 10m,
                Stock = 100,
                SellerId = AcmeSellerId,
                Status = ProductStatus.Published,
            },
            new Product
            {
                Id = GizmoId,
                Name = "Gizmo",
                Price = 25m,
                Stock = 50,
                SellerId = AcmeSellerId,
                Status = ProductStatus.Published,
            },
            new Product
            {
                Id = DoohickeyId,
                Name = "Doohickey",
                Price = 5m,
                Stock = 0,
                SellerId = AcmeSellerId,
                Status = ProductStatus.Published,
            });

        db.SaveChanges();
    }
}
