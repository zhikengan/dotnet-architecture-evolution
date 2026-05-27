using BuildingBlocks.Infrastructure.Authentication;
using Microsoft.Extensions.Options;

namespace Marketplace.Api.Endpoints.Demo;

/// <summary>
/// OIDC-shaped discovery endpoints. The host publishes its issuer + JWKS
/// at well-known paths so relying parties (Worker host, future SDKs) can
/// validate tokens by fetching the public key — no shared secret.
/// </summary>
public static class DiscoveryEndpoints
{
    public static IEndpointRouteBuilder MapDiscoveryEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/.well-known/openid-configuration", (HttpContext ctx, IOptions<JwtOptions> opts) =>
        {
            var baseUrl = $"{ctx.Request.Scheme}://{ctx.Request.Host}";
            return Results.Json(new
            {
                issuer = opts.Value.Issuer,
                jwks_uri = $"{baseUrl}/.well-known/jwks.json",
                id_token_signing_alg_values_supported = new[] { "RS256" },
                token_endpoint = $"{baseUrl}/demo/token",
                subject_types_supported = new[] { "public" },
            });
        }).AllowAnonymous().WithTags("Discovery");

        app.MapGet("/.well-known/jwks.json", (JwtPublicKeyProvider keys) =>
        {
            return Results.Json(new { keys = new[] { keys.ToJwk() } });
        }).AllowAnonymous().WithTags("Discovery");

        return app;
    }
}
