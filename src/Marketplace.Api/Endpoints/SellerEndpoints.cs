using Marketplace.Api.Common;
using Marketplace.Application.Abstractions;
using Marketplace.Application.Products.CreateProduct;
using MediatR;

namespace Marketplace.Api.Endpoints;

public static class SellerEndpoints
{
    public static IEndpointRouteBuilder MapSellerEndpoints(this IEndpointRouteBuilder app)
    {
        var seller = app.MapGroup("/api/seller").RequireAuthorization("Seller").WithTags("Seller");

        seller.MapPost("/products", async (CreateProductBody body, ICurrentUser user, ISender mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(
                new CreateProductCommand(body.Name, body.Price, body.Stock, user.UserId),
                ct);
            return ResultToHttp.Map(result, r => Results.Created($"/api/admin/products/{r.Id}", r));
        });

        return app;
    }

    public sealed record CreateProductBody(string Name, decimal Price, int Stock);
}
