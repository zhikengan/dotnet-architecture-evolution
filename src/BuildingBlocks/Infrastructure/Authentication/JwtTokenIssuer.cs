using System.Security.Claims;
using System.Security.Cryptography;
using BuildingBlocks.Application;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace BuildingBlocks.Infrastructure.Authentication;

/// <summary>
/// Signs RS256 JWTs against the configured RSA private key. The matching
/// public key is published via the host's JWKS discovery endpoint. Issuer
/// state is intentionally singleton-scoped — the RSA key materializes once
/// at host startup and is reused.
/// </summary>
public sealed class JwtTokenIssuer : IDisposable
{
    private readonly JwtOptions _opts;
    private readonly IClock _clock;
    private readonly RSA _privateKey;
    private readonly SigningCredentials _signingCredentials;

    public JwtTokenIssuer(IOptions<JwtOptions> options, IClock clock)
    {
        _opts = options.Value;
        _clock = clock;
        _privateKey = RSA.Create();
        _privateKey.ImportFromPem(_opts.PrivateKeyPem);
        var key = new RsaSecurityKey(_privateKey) { KeyId = _opts.KeyId };
        _signingCredentials = new SigningCredentials(key, SecurityAlgorithms.RsaSha256);
    }

    public (string Token, DateTime ExpiresAt) Mint(Guid userId, string role, Guid tenantId)
    {
        var now = _clock.UtcNow;
        var expires = now.AddMinutes(_opts.LifetimeMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _opts.Issuer,
            Audience = _opts.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expires,
            SigningCredentials = _signingCredentials,
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Role, role),
                new Claim("role", role),
                new Claim("tenant_id", tenantId.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            ]),
        };

        var handler = new JsonWebTokenHandler();
        return (handler.CreateToken(descriptor), expires);
    }

    public void Dispose() => _privateKey.Dispose();
}
