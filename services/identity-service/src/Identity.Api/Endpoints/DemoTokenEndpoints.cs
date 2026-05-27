using BuildingBlocks.Application;
using Identity.Application.Abstractions;
using Identity.Application.Authentication;
using Identity.Domain.Users;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Identity.Api.Endpoints;

public static class DemoTokenEndpoints
{
    public static IEndpointRouteBuilder MapDemoTokenEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/demo/token", async (
            [FromQuery] string role,
            [FromQuery] Guid userId,
            [FromQuery] string? tenant,
            IIdentityDbContext db,
            IJwtTokenIssuer issuer,
            CancellationToken ct) =>
        {
            var uid = new UserId(userId);
            var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == uid, ct);
            if (user is null)
            {
                return Results.NotFound(new { error = "User.NotFound", message = $"No seeded user for {userId}" });
            }

            var roleStr = string.IsNullOrWhiteSpace(role) ? user.Role.ToString() : role;
            var token = issuer.Issue(user.Id.Value, roleStr, user.TenantId);
            return Results.Ok(new
            {
                token,
                userId = user.Id.Value,
                tenantId = user.TenantId,
                role = roleStr,
            });
        }).WithName("DemoToken").AllowAnonymous();

        app.MapGet("/.well-known/jwks.json", (IJwtTokenIssuer issuer) =>
            Results.Json(issuer.GetJwks()))
            .AllowAnonymous();

        app.MapGet("/.well-known/openid-configuration", (HttpContext ctx, IClock clock) =>
        {
            var origin = $"{ctx.Request.Scheme}://{ctx.Request.Host}";
            return Results.Json(new
            {
                issuer = "marketplace-identity",
                jwks_uri = $"{origin}/.well-known/jwks.json",
                id_token_signing_alg_values_supported = new[] { "RS256" },
            });
        }).AllowAnonymous();

        return app;
    }
}
