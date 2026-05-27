using BuildingBlocks.Infrastructure.Authentication;

namespace Marketplace.Api.Endpoints.Dev;

/// <summary>
/// Dev-only token mint. Registered ONLY in the Development environment.
/// There is no login, no password, no IdP — the caller asserts (userId, role)
/// and gets back a real signed JWT that the JwtBearer middleware will accept.
/// Tier 4 replaces this with an actual issuer service.
/// </summary>
public static class DevTokenEndpoints
{
    private static readonly string[] AllowedRoles = ["Buyer", "Seller", "Admin"];

    public static IEndpointRouteBuilder MapDevTokenEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/dev/token", (DevTokenRequest body, JwtTokenIssuer issuer) =>
        {
            if (body.UserId == Guid.Empty)
                return Results.BadRequest(new { error = "userId is required" });
            if (string.IsNullOrWhiteSpace(body.Role) || !AllowedRoles.Contains(body.Role, StringComparer.Ordinal))
                return Results.BadRequest(new { error = $"role must be one of: {string.Join(", ", AllowedRoles)}" });

            var (token, expires) = issuer.Mint(body.UserId, body.Role);
            return Results.Ok(new
            {
                access_token = token,
                token_type = "Bearer",
                expires_at = expires,
                userId = body.UserId,
                role = body.Role,
            });
        })
        .WithTags("Dev")
        .AllowAnonymous();

        return app;
    }

    public sealed record DevTokenRequest(Guid UserId, string Role);
}
