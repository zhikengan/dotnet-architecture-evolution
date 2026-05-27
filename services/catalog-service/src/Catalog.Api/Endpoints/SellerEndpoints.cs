using BuildingBlocks.Api;
using BuildingBlocks.Application;
using Catalog.Application.Products.CreateProduct;
using Catalog.Application.Products.Queries;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Catalog.Api.Endpoints;

public static class SellerEndpoints
{
    public static IEndpointRouteBuilder MapSellerEndpoints(this IEndpointRouteBuilder app)
    {
        var grp = app.MapGroup("/api/seller").RequireAuthorization("seller");

        grp.MapPost("/products", async (CreateProductCommand cmd, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(cmd, ct);
            return result.ToHttpResult(value => Results.Created($"/api/seller/products/{value.Id}", value));
        });

        grp.MapGet("/products", async (ICurrentUser user, ISender sender, CancellationToken ct) =>
        {
            if (!user.IsAuthenticated) return Results.Unauthorized();
            var result = await sender.Send(new ListProductsForSellerQuery(user.UserId), ct);
            return result.ToHttpResult();
        });

        return app;
    }
}
