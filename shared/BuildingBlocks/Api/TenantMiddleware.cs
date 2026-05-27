using BuildingBlocks.Application;
using Microsoft.AspNetCore.Http;

namespace BuildingBlocks.Api;

/// <summary>
/// Pulls the <c>tenant_id</c> claim off the validated JWT and seeds the
/// per-request <see cref="ITenantContext"/>. Runs after authentication so the
/// principal is populated.
/// </summary>
public sealed class TenantMiddleware(RequestDelegate next)
{
    public const string ClaimName = "tenant_id";

    public async Task InvokeAsync(HttpContext context, ITenantContext tenant)
    {
        var claim = context.User?.FindFirst(ClaimName)?.Value;
        if (Guid.TryParse(claim, out var tenantId))
        {
            tenant.Set(tenantId);
        }
        await next(context);
    }
}
