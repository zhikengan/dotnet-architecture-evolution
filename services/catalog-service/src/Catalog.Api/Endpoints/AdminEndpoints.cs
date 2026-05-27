using BuildingBlocks.Api;
using BuildingBlocks.Application;
using Catalog.Application.Abstractions;
using Catalog.Application.Products;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Api.Endpoints;

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var grp = app.MapGroup("/api/admin").RequireAuthorization("admin");

        grp.MapGet("/products", async (ListProductsForAdminHandler handler, CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(ct);
            return result.ToHttpResult();
        });

        grp.MapPost("/products/{id:guid}/suspend", async (Guid id, ICatalogDbContext db, CancellationToken ct) =>
        {
            var product = await db.Products.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == new Catalog.Domain.Products.ProductId(id), ct);
            if (product is null) return Results.NotFound();
            product.Suspend();
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        return app;
    }
}
