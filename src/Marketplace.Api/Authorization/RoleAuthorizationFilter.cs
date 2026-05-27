namespace Marketplace.Api.Authorization;

public sealed class RoleAuthorizationFilter(string requiredRole) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var headers = context.HttpContext.Request.Headers;
        var roleHeader = headers["X-User-Role"].ToString();
        var idHeader = headers["X-User-Id"].ToString();

        var hasRole = !string.IsNullOrWhiteSpace(roleHeader);
        var hasUserId = Guid.TryParse(idHeader, out _);

        if (!hasRole || !hasUserId)
            return Results.StatusCode(StatusCodes.Status401Unauthorized);

        if (!string.Equals(roleHeader, requiredRole, StringComparison.Ordinal))
            return Results.StatusCode(StatusCodes.Status403Forbidden);

        return await next(context);
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
