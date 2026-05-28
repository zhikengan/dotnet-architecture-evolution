using System.IdentityModel.Tokens.Jwt;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure.Time;
using Identity.Application.Authentication;
using Identity.Infrastructure.Authentication;
using Microsoft.Extensions.Options;

namespace Identity.UnitTests;

public class JwtTokenIssuerTests
{
    [Fact]
    public void Issue_produces_an_RS256_jwt_with_role_and_tenant_claims()
    {
        var opt = Options.Create(new JwtOptions
        {
            Issuer = "marketplace-identity",
            Audience = "marketplace",
            KeyId = "test-key",
            LifetimeMinutes = 60,
            // Empty PEM → JwtTokenIssuer generates a fresh 2048-bit key on init.
            PrivateKeyPem = string.Empty,
        });
        IClock clock = new SystemClock();
        var issuer = new JwtTokenIssuer(opt, clock);

        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var token = issuer.Issue(userId, "Buyer", tenantId);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        jwt.Claims.Should().Contain(c => c.Type == "role" && c.Value == "Buyer");
        jwt.Claims.Should().Contain(c => c.Type == "tenant_id" && c.Value == tenantId.ToString());
        jwt.Header.Alg.Should().Be("RS256");
        jwt.Header.Kid.Should().Be("test-key");
    }

    [Fact]
    public void GetJwks_returns_RSA_public_key_metadata()
    {
        var opt = Options.Create(new JwtOptions
        {
            Issuer = "marketplace-identity",
            Audience = "marketplace",
            KeyId = "test-key",
            LifetimeMinutes = 60,
            PrivateKeyPem = string.Empty,
        });
        var issuer = new JwtTokenIssuer(opt, new SystemClock());
        var jwks = issuer.GetJwks();
        // Serialize → deserialize round-trip to inspect the anonymous shape.
        var json = System.Text.Json.JsonSerializer.Serialize(jwks);
        json.Should().Contain("\"kty\":\"RSA\"")
            .And.Contain("\"alg\":\"RS256\"")
            .And.Contain("\"kid\":\"test-key\"");
    }
}
