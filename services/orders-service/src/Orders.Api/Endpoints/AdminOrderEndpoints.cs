using BuildingBlocks.Api;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Orders.Application.Orders;

namespace Orders.Api.Endpoints;

public static class AdminOrderEndpoints
{
    public static IEndpointRouteBuilder MapAdminOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var grp = app.MapGroup("/api/admin/orders").RequireAuthorization("admin");

        grp.MapGet("", async (ListOrdersForAdminHandler handler, CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(ct);
            return result.ToHttpResult();
        });

        grp.MapPost("{id:guid}/cancel", async (Guid id, ForceCancelOrderHandler handler, CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(new ForceCancelOrderCommand(id), ct);
            return result.ToHttpResult();
        });

        return app;
    }
}
