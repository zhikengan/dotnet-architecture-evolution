using BuildingBlocks.Api;
using Catalog.Application.Products.Queries.ListProductsForAdmin;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Orders.Application.Orders.ForceCancelOrder;
using Orders.Application.Orders.Queries;
using Platform.Application.FeatureFlags.EnableForUser;
using Platform.Application.FeatureFlags.ListFeatureFlags;
using Platform.Application.FeatureFlags.ToggleFlag;
using Platform.Application.FeatureFlags.UpdateRolloutPercentage;

namespace Marketplace.Api.Endpoints;

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/admin").RequireAuthorization("Admin").WithTags("Admin");

        g.MapGet("/products", async (ISender mediator, CancellationToken ct) =>
        {
            var r = await mediator.Send(new ListProductsForAdminQuery(), ct);
            return r.ToHttpResult(Results.Ok);
        });

        g.MapGet("/orders", async (ISender mediator, CancellationToken ct) =>
        {
            var r = await mediator.Send(new ListOrdersForAdminQuery(), ct);
            return r.ToHttpResult(Results.Ok);
        });

        g.MapPost("/orders/{id:guid}/cancel", async (Guid id, ISender mediator, CancellationToken ct) =>
        {
            var r = await mediator.Send(new ForceCancelOrderCommand(id), ct);
            return r.ToHttpResult(() => Results.Ok(new { id, status = "Cancelled" }));
        });

        g.MapGet("/feature-flags", async (ISender mediator, CancellationToken ct) =>
        {
            var r = await mediator.Send(new ListFeatureFlagsQuery(), ct);
            return r.ToHttpResult(Results.Ok);
        });

        g.MapPut("/feature-flags/{name}/rollout", async (string name, RolloutBody body, ISender mediator, CancellationToken ct) =>
        {
            var r = await mediator.Send(new UpdateRolloutPercentageCommand(name, body.Percentage), ct);
            return r.ToHttpResult(() => Results.Ok(new { name, percentage = body.Percentage }));
        });

        g.MapPut("/feature-flags/{name}/users/{userId:guid}", async (string name, Guid userId, ISender mediator, CancellationToken ct) =>
        {
            var r = await mediator.Send(new EnableForUserCommand(name, userId), ct);
            return r.ToHttpResult(() => Results.Ok(new { name, userId }));
        });

        g.MapPost("/feature-flags/{name}/toggle", async (string name, ISender mediator, CancellationToken ct) =>
        {
            var r = await mediator.Send(new ToggleFlagCommand(name), ct);
            return r.ToHttpResult(() => Results.Ok(new { name, toggled = true }));
        });

        return app;
    }

    public sealed record RolloutBody(int Percentage);
}
