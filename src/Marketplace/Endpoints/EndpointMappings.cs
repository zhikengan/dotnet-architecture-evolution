using Marketplace.Data;
using Marketplace.Models;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Endpoints;

public static class EndpointMappings
{
    public static IEndpointRouteBuilder MapSellerEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/seller/products", async (HttpContext ctx, AppDbContext db, CreateProductRequest req) =>
        {
            if (CheckRole(ctx, "Seller") is { } forbid) return forbid;
            if (!Guid.TryParse(ctx.Request.Headers["X-User-Id"].ToString(), out var sellerId))
                return Results.BadRequest(new { error = "missing or invalid X-User-Id header" });
            if (string.IsNullOrWhiteSpace(req.Name))
                return Results.BadRequest(new { error = "name is required" });
            if (req.Price <= 0)
                return Results.BadRequest(new { error = "price must be positive" });
            if (req.Stock < 0)
                return Results.BadRequest(new { error = "stock cannot be negative" });

            var product = new Product
            {
                Name = req.Name,
                Price = req.Price,
                Stock = req.Stock,
                SellerId = sellerId,
                Status = ProductStatus.Published,
            };
            db.Products.Add(product);
            await db.SaveChangesAsync();
            return Results.Created($"/api/admin/products/{product.Id}", product);
        });

        return app;
    }

    public static IEndpointRouteBuilder MapBuyerEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/buyer/products", async (HttpContext ctx, AppDbContext db) =>
        {
            if (CheckRole(ctx, "Buyer") is { } forbid) return forbid;
            var products = await db.Products
                .Where(p => p.Status == ProductStatus.Published)
                .ToListAsync();
            return Results.Ok(products);
        });

        app.MapPost("/api/buyer/orders", async (HttpContext ctx, AppDbContext db, PlaceOrderRequest req) =>
        {
            if (CheckRole(ctx, "Buyer") is { } forbid) return forbid;
            if (!Guid.TryParse(ctx.Request.Headers["X-User-Id"].ToString(), out var buyerId))
                return Results.BadRequest(new { error = "missing or invalid X-User-Id header" });
            if (req.Quantity < 1)
                return Results.BadRequest(new { error = "quantity must be at least 1" });

            var product = await db.Products.FirstOrDefaultAsync(p => p.Id == req.ProductId);
            if (product is null)
                return Results.NotFound(new { error = "product not found" });

            if (req.Quantity > product.Stock)
            {
                var failed = new Order
                {
                    BuyerId = buyerId,
                    ProductId = req.ProductId,
                    Quantity = req.Quantity,
                    Status = OrderStatus.Failed,
                };
                db.Orders.Add(failed);
                await db.SaveChangesAsync();
                return Results.UnprocessableEntity(new { error = "insufficient stock", order = failed });
            }

            product.Stock -= req.Quantity;
            var order = new Order
            {
                BuyerId = buyerId,
                ProductId = req.ProductId,
                Quantity = req.Quantity,
                Status = OrderStatus.Confirmed,
            };
            db.Orders.Add(order);
            await db.SaveChangesAsync();
            return Results.Created($"/api/buyer/orders/{order.Id}", order);
        });

        app.MapPost("/api/buyer/orders/{id:guid}/cancel", async (HttpContext ctx, AppDbContext db, Guid id) =>
        {
            if (CheckRole(ctx, "Buyer") is { } forbid) return forbid;
            return await CancelOrderAsync(db, id);
        });

        return app;
    }

    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/admin/products", async (HttpContext ctx, AppDbContext db) =>
        {
            if (CheckRole(ctx, "Admin") is { } forbid) return forbid;
            var products = await db.Products.ToListAsync();
            return Results.Ok(products);
        });

        app.MapPost("/api/admin/orders/{id:guid}/cancel", async (HttpContext ctx, AppDbContext db, Guid id) =>
        {
            if (CheckRole(ctx, "Admin") is { } forbid) return forbid;
            return await CancelOrderAsync(db, id);
        });

        return app;
    }

    private static async Task<IResult> CancelOrderAsync(AppDbContext db, Guid orderId)
    {
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == orderId);
        if (order is null)
            return Results.NotFound(new { error = "order not found" });
        if (order.Status == OrderStatus.Cancelled)
            return Results.UnprocessableEntity(new { error = "order already cancelled" });

        if (order.Status == OrderStatus.Confirmed)
        {
            var product = await db.Products.FirstOrDefaultAsync(p => p.Id == order.ProductId);
            if (product is not null)
                product.Stock += order.Quantity;
        }

        order.Status = OrderStatus.Cancelled;
        await db.SaveChangesAsync();
        return Results.Ok(order);
    }

    private static IResult? CheckRole(HttpContext ctx, string requiredRole) =>
        ctx.Request.Headers["X-User-Role"].ToString() == requiredRole ? null : Results.Forbid();
}

public record CreateProductRequest(string Name, decimal Price, int Stock);
public record PlaceOrderRequest(Guid ProductId, int Quantity);
