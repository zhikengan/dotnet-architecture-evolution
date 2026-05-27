using System.Security.Claims;
using System.Text;
using Marketplace.Application.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Marketplace.Infrastructure.Authentication;

/// <summary>
/// Mints signed HS256 JWTs for the configured issuer/audience. Lives here in
/// Infrastructure (not Api) because it's the "real-world adapter" for token
/// signing — sibling to <c>SystemClock</c> and <c>AppDbContext</c>. The API
/// layer wires the JwtBearer middleware that validates the same tokens.
/// </summary>
public sealed class JwtTokenIssuer(IOptions<JwtOptions> options, IClock clock)
{
    private readonly JwtOptions _opts = options.Value;

    public (string Token, DateTime ExpiresAt) Mint(Guid userId, string role)
    {
        var now = clock.UtcNow;
        var expires = now.AddMinutes(_opts.LifetimeMinutes);

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opts.Key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _opts.Issuer,
            Audience = _opts.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expires,
            SigningCredentials = creds,
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Role, role),
                new Claim("role", role),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            ]),
        };

        var handler = new JsonWebTokenHandler();
        return (handler.CreateToken(descriptor), expires);
    }
}
