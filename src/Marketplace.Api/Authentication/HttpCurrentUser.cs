using System.Security.Claims;
using Marketplace.Application.Abstractions;

namespace Marketplace.Api.Authentication;

/// <summary>
/// <see cref="ICurrentUser"/> backed by the request's <see cref="ClaimsPrincipal"/>.
/// Reads <c>NameIdentifier</c> (subject) and <c>Role</c> claims that the
/// JwtBearer middleware populates after validating the bearer token.
/// </summary>
public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public bool IsAuthenticated => accessor.HttpContext?.User.Identity?.IsAuthenticated == true;

    public Guid UserId
    {
        get
        {
            var raw = accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(raw, out var g) ? g : Guid.Empty;
        }
    }

    public string Role =>
        accessor.HttpContext?.User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
}
