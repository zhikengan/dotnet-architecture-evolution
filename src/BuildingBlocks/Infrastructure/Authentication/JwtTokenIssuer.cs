using System.Security.Claims;
using System.Text;
using BuildingBlocks.Application;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace BuildingBlocks.Infrastructure.Authentication;

/// <summary>
/// Signs HS256 JWTs for the configured issuer/audience. Lives in
/// <c>BuildingBlocks.Infrastructure</c> (shared kernel) so any host can
/// resolve it — it's a real-world adapter (crypto + <see cref="IClock"/>),
/// sibling to the outbox/inbox/event-bus services in this layer.
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
