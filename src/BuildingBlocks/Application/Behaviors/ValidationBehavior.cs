using BuildingBlocks.Domain;
using FluentValidation;
using FluentValidation.Results;
using MediatR;

namespace BuildingBlocks.Application.Behaviors;

public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var list = validators as IList<IValidator<TRequest>> ?? [.. validators];
        if (list.Count == 0) return await next(ct);

        var context = new ValidationContext<TRequest>(request);
        var failures = new List<ValidationFailure>();
        foreach (var v in list)
        {
            var result = await v.ValidateAsync(context, ct);
            failures.AddRange(result.Errors.Where(e => e is not null));
        }

        if (failures.Count == 0) return await next(ct);

        var error = new Error("Validation", string.Join("; ", failures.Select(f => f.ErrorMessage)));

        if (typeof(TResponse) == typeof(Result))
            return (TResponse)(object)Result.Failure(error);

        var t = typeof(TResponse);
        if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Result<>))
        {
            var inner = t.GenericTypeArguments[0];
            var failureMethod = typeof(Result).GetMethods()
                .First(m => m.Name == nameof(Result.Failure) && m.IsGenericMethodDefinition)
                .MakeGenericMethod(inner);
            return (TResponse)failureMethod.Invoke(null, [error])!;
        }

        throw new ValidationException(failures);
    }
}
