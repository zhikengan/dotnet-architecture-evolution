using BuildingBlocks.Application;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace BuildingBlocks.Api;

/// <summary>
/// Reads <see cref="ICurrentUser"/> from the validated JWT principal. Claims:
/// <c>sub</c> (user id), <c>role</c>, <c>tenant_id</c>. Unauthenticated
/// principals report <see cref="IsAuthenticated"/> = false.
/// </summary>
public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public Guid UserId =>
        Guid.TryParse(Principal?.FindFirst("sub")?.Value
                    ?? Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id)
            ? id : Guid.Empty;

    public string Role => Principal?.FindFirst("role")?.Value ?? string.Empty;

    public Guid TenantId =>
        Guid.TryParse(Principal?.FindFirst("tenant_id")?.Value, out var id) ? id : Guid.Empty;
}
