using BuildingBlocks.Api;
using BuildingBlocks.Application;
using Catalog.Application.Products.Queries.ListProductsForBuyer;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Orders.Application.Orders.CancelOwnOrder;
using Microsoft.AspNetCore.RateLimiting;
using Orders.Application.Orders.PlaceOrder;
using Orders.Application.Orders.Queries;

namespace Marketplace.Api.Endpoints;

public static class BuyerEndpoints
{
    public static IEndpointRouteBuilder MapBuyerEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/buyer").RequireAuthorization("Buyer").WithTags("Buyer");

        g.MapGet("/products", async (ICurrentUser user, ISender mediator, CancellationToken ct) =>
        {
            var r = await mediator.Send(new ListProductsForBuyerQuery(user.UserId), ct);
            return r.ToHttpResult(Results.Ok);
        }).RequireRateLimiting(RateLimiting.ReadsPolicy);

        g.MapPost("/orders", async (PlaceOrderBody body, ICurrentUser user, ISender mediator, HttpContext ctx, CancellationToken ct) =>
        {
            var idempotencyKey = ctx.Request.Headers["Idempotency-Key"].ToString();
            var r = await mediator.Send(
                new PlaceOrderCommand(user.UserId, body.ProductId, body.Quantity, string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey),
                ct);
            return r.ToHttpResult(rs => Results.Created($"/api/buyer/orders/{rs.OrderId}", rs));
        }).RequireRateLimiting(RateLimiting.WritesPolicy);

        g.MapPost("/orders/{id:guid}/cancel", async (Guid id, ICurrentUser user, ISender mediator, CancellationToken ct) =>
        {
            var r = await mediator.Send(new CancelOwnOrderCommand(id, user.UserId), ct);
            return r.ToHttpResult(() => Results.Ok(new { id, status = "Cancelled" }));
        }).RequireRateLimiting(RateLimiting.WritesPolicy);

        g.MapGet("/orders", async (ICurrentUser user, ISender mediator, CancellationToken ct) =>
        {
            var r = await mediator.Send(new ListOrdersForBuyerQuery(user.UserId), ct);
            return r.ToHttpResult(Results.Ok);
        }).RequireRateLimiting(RateLimiting.ReadsPolicy);

        g.MapGet("/orders/{id:guid}", async (Guid id, ICurrentUser user, ISender mediator, CancellationToken ct) =>
        {
            var r = await mediator.Send(new GetOrderForBuyerQuery(id, user.UserId), ct);
            return r.ToHttpResult(Results.Ok);
        }).RequireRateLimiting(RateLimiting.ReadsPolicy);

        return app;
    }

    public sealed record PlaceOrderBody(Guid ProductId, int Quantity);
}
