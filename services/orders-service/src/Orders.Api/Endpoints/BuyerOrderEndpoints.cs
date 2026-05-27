using BuildingBlocks.Api;
using BuildingBlocks.Application;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Orders.Application.Orders.CancelOwnOrder;
using Orders.Application.Orders.PlaceOrder;
using Orders.Application.Orders.Queries;

namespace Orders.Api.Endpoints;

public static class BuyerOrderEndpoints
{
    public static IEndpointRouteBuilder MapBuyerOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var grp = app.MapGroup("/api/buyer/orders").RequireAuthorization("buyer");

        grp.MapPost("", async (PlaceOrderCommand cmd, ICurrentUser user, ISender sender, CancellationToken ct) =>
        {
            if (!user.IsAuthenticated) return Results.Unauthorized();
            // Force the BuyerId on the command to come from the JWT — clients can't impersonate.
            if (cmd.BuyerId != user.UserId) cmd = cmd with { BuyerId = user.UserId };
            var result = await sender.Send(cmd, ct);
            return result.ToHttpResult(value => Results.Created($"/api/buyer/orders/{value.OrderId}", value));
        });

        grp.MapGet("", async (ICurrentUser user, ISender sender, CancellationToken ct) =>
        {
            if (!user.IsAuthenticated) return Results.Unauthorized();
            var result = await sender.Send(new ListOrdersForBuyerQuery(user.UserId), ct);
            return result.ToHttpResult();
        });

        grp.MapGet("{id:guid}", async (Guid id, ICurrentUser user, ISender sender, CancellationToken ct) =>
        {
            if (!user.IsAuthenticated) return Results.Unauthorized();
            var result = await sender.Send(new GetOrderForBuyerQuery(id, user.UserId), ct);
            return result.ToHttpResult();
        });

        grp.MapPost("{id:guid}/cancel", async (Guid id, ICurrentUser user, ISender sender, CancellationToken ct) =>
        {
            if (!user.IsAuthenticated) return Results.Unauthorized();
            var result = await sender.Send(new CancelOwnOrderCommand(id, user.UserId), ct);
            return result.ToHttpResult();
        });

        return app;
    }
}
