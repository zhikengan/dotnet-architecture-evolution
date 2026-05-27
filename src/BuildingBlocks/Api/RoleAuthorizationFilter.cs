using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace BuildingBlocks.Api;

public sealed class RoleAuthorizationFilter(string requiredRole) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext ctx, EndpointFilterDelegate next)
    {
        var headers = ctx.HttpContext.Request.Headers;
        var role = headers["X-User-Role"].ToString();
        var idHeader = headers["X-User-Id"].ToString();

        if (string.IsNullOrWhiteSpace(role) || !Guid.TryParse(idHeader, out _))
            return Results.StatusCode(StatusCodes.Status401Unauthorized);

        if (!string.Equals(role, requiredRole, StringComparison.Ordinal))
            return Results.StatusCode(StatusCodes.Status403Forbidden);

        return await next(ctx);
    }
}

public static class RoleAuthorizationExtensions
{
    public static TBuilder RequireRole<TBuilder>(this TBuilder builder, string role)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.AddEndpointFilter(new RoleAuthorizationFilter(role));
        return builder;
    }
}
