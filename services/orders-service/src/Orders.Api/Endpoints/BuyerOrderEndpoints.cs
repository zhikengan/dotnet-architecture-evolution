using BuildingBlocks.Api;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Orders.Application.Orders;
using System.Security.Claims;

namespace Orders.Api.Endpoints;

public static class BuyerOrderEndpoints
{
    public static IEndpointRouteBuilder MapBuyerOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var grp = app.MapGroup("/api/buyer/orders").RequireAuthorization("buyer");

        grp.MapPost("", async (
            PlaceOrderCommand cmd,
            PlaceOrderHandler handler,
            IValidator<PlaceOrderCommand> validator,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var validation = await validator.ValidateAsync(cmd, ct);
            if (!validation.IsValid)
                return Results.BadRequest(new { error = "Validation", details = validation.Errors.Select(e => e.ErrorMessage) });

            var buyerIdClaim = ctx.User.FindFirst("sub")?.Value;
            if (Guid.TryParse(buyerIdClaim, out var sub) && cmd.BuyerId != sub)
                cmd = cmd with { BuyerId = sub };

            var result = await handler.HandleAsync(cmd, ct);
            return result.ToHttpResult(value => Results.Created($"/api/buyer/orders/{value.OrderId}", value));
        });

        grp.MapGet("", async (HttpContext ctx, ListOrdersForBuyerHandler handler, CancellationToken ct) =>
        {
            if (!Guid.TryParse(ctx.User.FindFirst("sub")?.Value, out var buyerId)) return Results.Unauthorized();
            var result = await handler.HandleAsync(buyerId, ct);
            return result.ToHttpResult();
        });

        grp.MapGet("{id:guid}", async (Guid id, HttpContext ctx, GetOrderForBuyerHandler handler, CancellationToken ct) =>
        {
            if (!Guid.TryParse(ctx.User.FindFirst("sub")?.Value, out var buyerId)) return Results.Unauthorized();
            var result = await handler.HandleAsync(id, buyerId, ct);
            return result.ToHttpResult();
        });

        grp.MapPost("{id:guid}/cancel", async (Guid id, HttpContext ctx, CancelOwnOrderHandler handler, CancellationToken ct) =>
        {
            if (!Guid.TryParse(ctx.User.FindFirst("sub")?.Value, out var buyerId)) return Results.Unauthorized();
            var result = await handler.HandleAsync(new CancelOwnOrderCommand(id, buyerId), ct);
            return result.ToHttpResult();
        });

        return app;
    }
}
