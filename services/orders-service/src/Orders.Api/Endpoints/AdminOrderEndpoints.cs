using BuildingBlocks.Api;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Orders.Application.Orders.ForceCancelOrder;
using Orders.Application.Orders.Queries;

namespace Orders.Api.Endpoints;

public static class AdminOrderEndpoints
{
    public static IEndpointRouteBuilder MapAdminOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var grp = app.MapGroup("/api/admin/orders").RequireAuthorization("admin");

        grp.MapGet("", async (ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new ListOrdersForAdminQuery(), ct);
            return result.ToHttpResult();
        });

        grp.MapPost("{id:guid}/cancel", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new ForceCancelOrderCommand(id), ct);
            return result.ToHttpResult();
        });

        return app;
    }
}
