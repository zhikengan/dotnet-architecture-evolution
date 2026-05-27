using BuildingBlocks.Application;
using Microsoft.AspNetCore.Http;

namespace BuildingBlocks.Api;

public sealed class CorrelationMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context, ICorrelationContext correlation)
    {
        var incoming = context.Request.Headers[HeaderName].ToString();
        var id = string.IsNullOrWhiteSpace(incoming) ? Guid.NewGuid().ToString("N") : incoming;

        if (correlation is CorrelationContext ctx) ctx.CorrelationId = id;

        context.Response.Headers[HeaderName] = id;
        await next(context);
    }
}
