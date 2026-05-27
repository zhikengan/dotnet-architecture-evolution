using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace BuildingBlocks.Api;

public sealed class CorrelationMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers.TryGetValue(HeaderName, out var hdr) && !string.IsNullOrWhiteSpace(hdr)
            ? hdr.ToString()
            : Guid.NewGuid().ToString("N");

        context.Response.Headers[HeaderName] = correlationId;
        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }
}
