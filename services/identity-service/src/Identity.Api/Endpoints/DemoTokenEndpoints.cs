using BuildingBlocks.Api;
using Identity.Application.Authentication;
using Identity.Application.Users.Queries;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Identity.Api.Endpoints;

public static class DemoTokenEndpoints
{
    public static IEndpointRouteBuilder MapDemoTokenEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/demo/token", async (
            [FromQuery] string? role,
            [FromQuery] Guid userId,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new IssueDemoTokenQuery(userId, role), ct);
            return result.ToHttpResult(value => Results.Ok(new
            {
                token = value.Token,
                userId = value.UserId,
                tenantId = value.TenantId,
                role = value.Role,
            }));
        }).WithName("DemoToken").AllowAnonymous();

        app.MapGet("/.well-known/jwks.json", (IJwtTokenIssuer issuer) =>
            Results.Json(issuer.GetJwks()))
            .AllowAnonymous();

        app.MapGet("/.well-known/openid-configuration", (HttpContext ctx) =>
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
