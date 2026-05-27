using BuildingBlocks.Domain;
using FluentValidation;
using MediatR;

namespace BuildingBlocks.Application.Behaviors;

public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : Result
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (!validators.Any()) return await next(ct);

        var ctx = new ValidationContext<TRequest>(request);
        var failures = validators
            .Select(v => v.Validate(ctx))
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count == 0) return await next(ct);

        var error = new Error("Validation", string.Join("; ", failures.Select(f => f.ErrorMessage)));
        return (TResponse)(object)Result.Failure(error);
    }
}
