using System.Security.Claims;
using BuildingBlocks.Application;

namespace Marketplace.Api.Authentication;

/// <summary>
/// <see cref="ICurrentUser"/> backed by the request's <see cref="ClaimsPrincipal"/>.
/// Reads <c>NameIdentifier</c> (subject), <c>Role</c>, and <c>tenant_id</c>
/// claims populated by the JwtBearer middleware after it validates the bearer
/// token. Lives at the host rather than BuildingBlocks because it's bound to
/// <c>HttpContext</c> — an HTTP-host concept the shared kernel doesn't need.
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

    public Guid TenantId
    {
        get
        {
            var raw = accessor.HttpContext?.User.FindFirstValue(TenantMiddleware.TenantClaimType);
            return Guid.TryParse(raw, out var g) ? g : Guid.Empty;
        }
    }
}
