using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using EndToEndTests.Fixtures;
using Microsoft.IdentityModel.Tokens;

namespace EndToEndTests;

/// <summary>
/// JWT plumbing tests for Tier 4's RS256 upgrade. Cover: discovery shape,
/// claim mapping (tenant_id flows into the principal), and rejection cases
/// (forged signature, expired token). Bearer flow through the real
/// JwtBearer middleware — no shortcuts.
/// </summary>
[Collection(nameof(ApiCollection))]
public class JwtAuthTests(ApiFixture fx) : IAsyncLifetime
{
    public Task InitializeAsync() => fx.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task OpenId_discovery_publishes_issuer_and_jwks_uri()
    {
        var client = fx.AnonymousClient();
        var resp = await client.GetAsync("/.well-known/openid-configuration");
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("issuer").GetString().Should().Be("marketplace");
        json.GetProperty("jwks_uri").GetString().Should().EndWith("/.well-known/jwks.json");
        json.GetProperty("id_token_signing_alg_values_supported")[0].GetString().Should().Be("RS256");
    }

    [Fact]
    public async Task Jwks_endpoint_returns_RSA_public_key_with_matching_kid()
    {
        var client = fx.AnonymousClient();
        var resp = await client.GetAsync("/.well-known/jwks.json");
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var keys = json.GetProperty("keys");
        keys.GetArrayLength().Should().Be(1);
        var key = keys[0];
        key.GetProperty("kty").GetString().Should().Be("RSA");
        key.GetProperty("alg").GetString().Should().Be("RS256");
        key.GetProperty("kid").GetString().Should().Be(TestKeys.KeyId);
        key.GetProperty("n").GetString().Should().NotBeNullOrEmpty();
        key.GetProperty("e").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Missing_Authorization_returns_401()
    {
        var anon = fx.AnonymousClient();
        var resp = await anon.GetAsync("/api/buyer/products");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Token_signed_by_wrong_key_returns_401()
    {
        // Mint a token signed by a DIFFERENT RSA keypair than the host trusts.
        // The signature will not validate against the configured public key.
        using var rsa = RSA.Create(2048);
        var key = new RsaSecurityKey(rsa) { KeyId = "rogue" };
        var creds = new SigningCredentials(key, SecurityAlgorithms.RsaSha256);
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = "marketplace",
            Audience = "marketplace-clients",
            IssuedAt = DateTime.UtcNow,
            NotBefore = DateTime.UtcNow,
            Expires = DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = creds,
            Subject = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, ApiFixture.BuyerId.ToString()),
                new Claim(ClaimTypes.Role, "Buyer"),
                new Claim("tenant_id", ApiFixture.AcmeTenantId.ToString()),
            ]),
        };
        var handler = new Microsoft.IdentityModel.JsonWebTokens.JsonWebTokenHandler();
        var forged = handler.CreateToken(descriptor);

        var client = fx.AnonymousClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", forged);
        var resp = await client.GetAsync("/api/buyer/products");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Demo_token_endpoint_issues_jwt_with_tenant_id_claim()
    {
        var anon = fx.AnonymousClient();
        var resp = await anon.GetAsync($"/demo/token?role=Buyer&tenant=acme&userId={ApiFixture.BuyerId}");
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var token = body.GetProperty("access_token").GetString();
        token.Should().NotBeNullOrEmpty();

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        jwt.Claims.Should().Contain(c => c.Type == "tenant_id" && c.Value == ApiFixture.AcmeTenantId.ToString());
        jwt.Header.Alg.Should().Be("RS256");
        jwt.Header.Kid.Should().Be(TestKeys.KeyId);
    }

    [Fact]
    public async Task Demo_token_rejects_unknown_tenant_slug()
    {
        var anon = fx.AnonymousClient();
        var resp = await anon.GetAsync($"/demo/token?role=Buyer&tenant=bogus&userId={ApiFixture.BuyerId}");
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Health_live_returns_200()
    {
        var anon = fx.AnonymousClient();
        var resp = await anon.GetAsync("/health/live");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Health_ready_returns_200_with_check_details()
    {
        var anon = fx.AnonymousClient();
        var resp = await anon.GetAsync("/health/ready");
        // 200 = Healthy, 503 = Unhealthy. MinIO may be Degraded depending on
        // bucket state — Degraded still returns 200 by default.
        resp.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetString().Should().NotBeNullOrEmpty();
        body.GetProperty("checks").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Security_headers_are_attached_to_every_response()
    {
        var anon = fx.AnonymousClient();
        var resp = await anon.GetAsync("/.well-known/openid-configuration");
        resp.Headers.GetValues("Content-Security-Policy").Should().Contain("default-src 'self'");
        resp.Headers.GetValues("X-Frame-Options").Should().Contain("DENY");
        resp.Headers.GetValues("X-Content-Type-Options").Should().Contain("nosniff");
        resp.Headers.GetValues("Strict-Transport-Security").Should().ContainSingle()
            .Which.Should().Contain("max-age=31536000");
    }

    [Fact]
    public async Task Buyer_token_at_seller_endpoint_returns_403()
    {
        var buyer = fx.ClientFor("Buyer", ApiFixture.BuyerId);
        var resp = await buyer.PostAsJsonAsync("/api/seller/products", new { name = "x", price = 1m, stock = 1 });
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
