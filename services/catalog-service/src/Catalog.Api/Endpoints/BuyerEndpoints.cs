using BuildingBlocks.Api;
using Catalog.Application.Products;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Catalog.Api.Endpoints;

public static class BuyerEndpoints
{
    public static IEndpointRouteBuilder MapBuyerEndpoints(this IEndpointRouteBuilder app)
    {
        var grp = app.MapGroup("/api/buyer").RequireAuthorization("buyer");

        grp.MapGet("/products", async (ListProductsForBuyerHandler handler, CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(ct);
            return result.ToHttpResult();
        });

        return app;
    }
}
