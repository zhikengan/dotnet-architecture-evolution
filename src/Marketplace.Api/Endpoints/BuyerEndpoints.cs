using Marketplace.Api.Common;
using Marketplace.Application.Abstractions;
using Marketplace.Application.Orders.CancelOwnOrder;
using Marketplace.Application.Orders.PlaceOrder;
using Marketplace.Application.Products.Queries.ListProductsForBuyer;
using MediatR;

namespace Marketplace.Api.Endpoints;

public static class BuyerEndpoints
{
    public static IEndpointRouteBuilder MapBuyerEndpoints(this IEndpointRouteBuilder app)
    {
        var buyer = app.MapGroup("/api/buyer").RequireAuthorization("Buyer").WithTags("Buyer");

        buyer.MapGet("/products", async (ISender mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new ListProductsForBuyerQuery(), ct);
            return ResultToHttp.Map(result, products => Results.Ok(products));
        });

        buyer.MapPost("/orders", async (PlaceOrderBody body, ICurrentUser user, ISender mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(
                new PlaceOrderCommand(user.UserId, body.ProductId, body.Quantity),
                ct);
            return ResultToHttp.Map(result, r => Results.Created($"/api/buyer/orders/{r.OrderId}", r));
        });

        buyer.MapPost("/orders/{id:guid}/cancel", async (Guid id, ICurrentUser user, ISender mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new CancelOwnOrderCommand(id, user.UserId), ct);
            return ResultToHttp.Map(result, () => Results.Ok(new { id, status = "Cancelled" }));
        });

        return app;
    }

    public sealed record PlaceOrderBody(Guid ProductId, int Quantity);
}
