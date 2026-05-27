using BuildingBlocks.Api;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
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

        return app;
    }
}
