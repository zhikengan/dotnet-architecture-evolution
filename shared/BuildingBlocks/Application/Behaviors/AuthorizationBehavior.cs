using BuildingBlocks.Domain;
using MediatR;

namespace BuildingBlocks.Application.Behaviors;

public sealed class AuthorizationBehavior<TRequest, TResponse>(ICurrentUser currentUser)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : Result
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (request is not IAuthorizationRequirement req) return await next(ct);

        if (!currentUser.IsAuthenticated)
            return (TResponse)(object)Result.Failure(new Error("Auth.Unauthenticated", "Not authenticated"));

        if (req.AllowedRoles.Length > 0 && !req.AllowedRoles.Contains(currentUser.Role, StringComparer.OrdinalIgnoreCase))
            return (TResponse)(object)Result.Failure(new Error("Auth.Forbidden", $"Role '{currentUser.Role}' not in [{string.Join(',', req.AllowedRoles)}]"));

        return await next(ct);
    }
}
