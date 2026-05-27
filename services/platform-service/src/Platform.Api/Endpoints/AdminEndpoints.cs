using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Platform.Application.Abstractions;

namespace Platform.Api.Endpoints;

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var grp = app.MapGroup("/api/admin/feature-flags");

        grp.MapGet("", async (IPlatformDbContext db, CancellationToken ct) =>
        {
            var flags = await db.FeatureFlags.AsNoTracking()
                .Select(f => new { id = f.Id.Value, tenantId = f.TenantId, key = f.Key, isEnabled = f.IsEnabled, updatedAt = f.UpdatedAt })
                .ToListAsync(ct);
            return Results.Ok(flags);
        }).RequireAuthorization("admin");

        return app;
    }
}
