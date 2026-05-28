using BuildingBlocks.Api;
using BuildingBlocks.Application;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Platform.Application.FeatureFlags.Commands;
using Platform.Application.FeatureFlags.Queries;

namespace Platform.Api.Endpoints;

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var grp = app.MapGroup("/api/admin/feature-flags").RequireAuthorization("admin");

        grp.MapGet("", async (ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new ListFeatureFlagsQuery(), ct);
            return result.ToHttpResult();
        });

        grp.MapPut("{key}/rollout", async (
            string key,
            RolloutBody body,
            ITenantContext tenant,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new SetRolloutPercentageCommand(tenant.TenantId, key, body.Percentage), ct);
            return result.ToHttpResult(() => Results.Ok(new { key, percentage = body.Percentage }));
        });

        grp.MapPut("{key}/users/{userId:guid}", async (
            string key,
            Guid userId,
            ITenantContext tenant,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new EnableForUserCommand(tenant.TenantId, key, userId), ct);
            return result.ToHttpResult(() => Results.Ok(new { key, userId }));
        });

        grp.MapPost("{key}/toggle", async (
            string key,
            ToggleBody body,
            ITenantContext tenant,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new ToggleFlagCommand(tenant.TenantId, key, body.IsEnabled), ct);
            return result.ToHttpResult(() => Results.Ok(new { key, isEnabled = body.IsEnabled }));
        });

        return app;
    }

    public sealed record RolloutBody(int Percentage);
    public sealed record ToggleBody(bool IsEnabled);
}
