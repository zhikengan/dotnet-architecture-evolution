using BuildingBlocks.Api;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Notifications.Application.Notifications.Queries;

namespace Notifications.Api.Endpoints;

public static class AdminNotificationEndpoints
{
    public static IEndpointRouteBuilder MapAdminNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/admin/notifications/by-order/{orderId:guid}",
            async (Guid orderId, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new ListNotificationsByOrderQuery(orderId), ct);
                return result.ToHttpResult();
            }).RequireAuthorization("admin");

        return app;
    }
}
