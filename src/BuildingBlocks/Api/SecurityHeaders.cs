using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace BuildingBlocks.Api;

/// <summary>
/// Defensive response headers. The marketplace API doesn't render HTML so the
/// CSP is intentionally tight (default-src 'self'). HSTS opts browsers into
/// HTTPS-only for a year. X-Frame-Options/X-Content-Type-Options stop the
/// classic embed + sniff attacks. Mounted before auth so even 401s carry them.
/// </summary>
public static class SecurityHeaders
{
    public static IApplicationBuilder UseMarketplaceSecurityHeaders(this IApplicationBuilder app) =>
        app.Use(static async (ctx, next) =>
        {
            var headers = ctx.Response.Headers;
            headers["Content-Security-Policy"] = "default-src 'self'";
            headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
            headers["X-Frame-Options"] = "DENY";
            headers["X-Content-Type-Options"] = "nosniff";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            await next();
        });
}
