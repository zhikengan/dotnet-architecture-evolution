using System.Security.Claims;
using BuildingBlocks.Application.MultiTenancy;

namespace Marketplace.Api.Authentication;

/// <summary>
/// Reads the <c>tenant_id</c> claim off the authenticated principal and writes it
/// to the scoped <see cref="ITenantContextSetter"/> so EF query filters see the
/// right tenant for the rest of the request. Must run after
/// <c>UseAuthentication()</c> and before any endpoint handler.
/// </summary>
public sealed class TenantMiddleware(RequestDelegate next)
{
    public const string TenantClaimType = "tenant_id";

    public async Task InvokeAsync(HttpContext ctx, ITenantContextSetter setter)
    {
        var raw = ctx.User.FindFirstValue(TenantClaimType);
        if (Guid.TryParse(raw, out var tenantId))
        {
            setter.SetTenant(tenantId);
        }
        await next(ctx);
    }
}
