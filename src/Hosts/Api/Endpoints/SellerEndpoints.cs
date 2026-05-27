using BuildingBlocks.Api;
using BuildingBlocks.Application;
using Catalog.Application.Products.CreateProduct;
using Catalog.Application.Products.Queries.ListProductsForSeller;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Marketplace.Api.Endpoints;

public static class SellerEndpoints
{
    public static IEndpointRouteBuilder MapSellerEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/seller").RequireAuthorization("Seller").WithTags("Seller");

        g.MapPost("/products", async (CreateProductBody body, ICurrentUser user, ISender mediator, CancellationToken ct) =>
        {
            var r = await mediator.Send(new CreateProductCommand(body.Name, body.Price, body.Stock, user.UserId), ct);
            return r.ToHttpResult(rs => Results.Created($"/api/admin/products/{rs.Id}", rs));
        });

        g.MapGet("/products", async (ICurrentUser user, ISender mediator, CancellationToken ct) =>
        {
            var r = await mediator.Send(new ListProductsForSellerQuery(user.UserId), ct);
            return r.ToHttpResult(Results.Ok);
        });

        return app;
    }

    public sealed record CreateProductBody(string Name, decimal Price, int Stock);
}
