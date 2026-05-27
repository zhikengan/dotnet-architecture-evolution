using BuildingBlocks.Domain;
using MediatR;

namespace BuildingBlocks.Application.Behaviors;

public interface IAuthorizationRequirement
{
    string[] AllowedRoles { get; }
}

public sealed class AuthorizationBehavior<TRequest, TResponse>(ICurrentUser currentUser)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (request is not IAuthorizationRequirement requirement)
        {
            return next(ct);
        }

        if (!currentUser.IsAuthenticated)
        {
            var error = new Error("Authorization.NotAuthenticated", "Authentication required");
            return Task.FromResult(BuildFailure<TResponse>(error));
        }

        if (requirement.AllowedRoles.Length > 0 &&
            !requirement.AllowedRoles.Contains(currentUser.Role, StringComparer.Ordinal))
        {
            var error = new Error("Authorization.Forbidden", "Role not permitted for this operation");
            return Task.FromResult(BuildFailure<TResponse>(error));
        }

        return next(ct);
    }

    private static T BuildFailure<T>(Error error)
    {
        if (typeof(T) == typeof(Result))
            return (T)(object)Result.Failure(error);

        if (typeof(T).IsGenericType && typeof(T).GetGenericTypeDefinition() == typeof(Result<>))
        {
            var inner = typeof(T).GenericTypeArguments[0];
            var m = typeof(Result).GetMethods()
                .First(x => x.Name == nameof(Result.Failure) && x.IsGenericMethodDefinition)
                .MakeGenericMethod(inner);
            return (T)m.Invoke(null, [error])!;
        }

        throw new InvalidOperationException("AuthorizationBehavior only supports Result/Result<T> responses");
    }
}
