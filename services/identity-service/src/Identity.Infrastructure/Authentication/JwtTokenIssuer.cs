using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using BuildingBlocks.Application;
using Identity.Application.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Identity.Infrastructure.Authentication;

/// <summary>
/// Signs RS256 JWTs for the marketplace. Registered as a singleton — the RSA
/// is intentionally NOT disposed: <c>Microsoft.IdentityModel</c> caches
/// signature providers keyed off the <see cref="SecurityKey"/>, and a parallel
/// test fixture that disposed its own RSA would invalidate cached providers
/// held by other fixtures (a real bug Tier 4 fixed; see Tier 4 ADR-0010). The
/// GC owns this RSA's lifetime alongside the singleton.
/// </summary>
public sealed class JwtTokenIssuer(IOptions<JwtOptions> options, IClock clock) : IJwtTokenIssuer
{
    private readonly JwtOptions _opt = options.Value;
    private readonly RSA _rsa = LoadRsa(options.Value);

    public string Issue(Guid userId, string role, Guid tenantId)
    {
        var key = new RsaSecurityKey(_rsa) { KeyId = _opt.KeyId };
        var credentials = new SigningCredentials(key, SecurityAlgorithms.RsaSha256);

        var now = clock.UtcNow;
        var token = new JwtSecurityToken(
            issuer: _opt.Issuer,
            audience: _opt.Audience,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim("role", role),
                new Claim("tenant_id", tenantId.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            ],
            notBefore: now,
            expires: now.AddMinutes(_opt.LifetimeMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public object GetJwks()
    {
        var parameters = _rsa.ExportParameters(false);
        return new
        {
            keys = new[]
            {
                new
                {
                    kty = "RSA",
                    use = "sig",
                    kid = _opt.KeyId,
                    alg = "RS256",
                    n = Base64UrlEncoder.Encode(parameters.Modulus!),
                    e = Base64UrlEncoder.Encode(parameters.Exponent!),
                }
            }
        };
    }

    private static RSA LoadRsa(JwtOptions opt)
    {
        var rsa = RSA.Create();
        if (!string.IsNullOrWhiteSpace(opt.PrivateKeyPem))
        {
            rsa.ImportFromPem(opt.PrivateKeyPem);
        }
        else
        {
            // Demo: generate a fresh 2048-bit key on startup. Real systems persist
            // the key in a secret store so JWTs survive restarts.
            rsa.KeySize = 2048;
        }
        return rsa;
    }
}
