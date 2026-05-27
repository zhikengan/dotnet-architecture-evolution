using BuildingBlocks.Infrastructure.Authentication;

namespace Marketplace.Api.Endpoints.Demo;

/// <summary>
/// Demo token issuer endpoint. Mounted ONLY in Development. Caller asserts
/// (userId, role, tenant) via query params and gets back an RS256-signed JWT
/// whose public key the JwtBearer middleware validates against. The OIDC
/// discovery + JWKS endpoints publish the matching public key so relying
/// parties never hold the signing material.
/// </summary>
public static class DemoTokenEndpoint
{
    private static readonly Dictionary<string, Guid> KnownTenants = new(StringComparer.OrdinalIgnoreCase)
    {
        ["acme"] = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
        ["globex"] = new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
    };
    private static readonly string[] AllowedRoles = ["Buyer", "Seller", "Admin"];

    public static IEndpointRouteBuilder MapDemoTokenEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/demo/token", (string role, string tenant, Guid? userId, JwtTokenIssuer issuer) =>
        {
            if (string.IsNullOrWhiteSpace(role) || !AllowedRoles.Contains(role, StringComparer.Ordinal))
                return Results.BadRequest(new { error = $"role must be one of: {string.Join(", ", AllowedRoles)}" });

            // Tenant can be either a slug (acme/globex) or a raw Guid.
            Guid tenantId;
            if (!KnownTenants.TryGetValue(tenant ?? string.Empty, out tenantId) &&
                !Guid.TryParse(tenant, out tenantId))
            {
                return Results.BadRequest(new { error = $"tenant must be a known slug ({string.Join(", ", KnownTenants.Keys)}) or a Guid" });
            }

            var resolvedUserId = userId ?? Guid.NewGuid();
            var (token, expires) = issuer.Mint(resolvedUserId, role, tenantId);
            return Results.Ok(new
            {
                access_token = token,
                token_type = "Bearer",
                expires_at = expires,
                userId = resolvedUserId,
                role,
                tenantId,
            });
        })
        .WithTags("Demo")
        .AllowAnonymous();

        return app;
    }
}
