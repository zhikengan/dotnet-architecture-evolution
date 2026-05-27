using System.Text.Json;
using MediatR;

namespace BuildingBlocks.Application.Behaviors;

public sealed class IdempotencyBehavior<TRequest, TResponse>(IIdempotencyStore store)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (request is not IIdempotentCommand idempotent || string.IsNullOrWhiteSpace(idempotent.IdempotencyKey))
            return await next(ct);

        var key = idempotent.IdempotencyKey;
        var cached = await store.TryGetAsync(key, ct);
        if (cached is not null)
        {
            var rehydrated = JsonSerializer.Deserialize<TResponse>(cached);
            if (rehydrated is not null) return rehydrated;
        }

        var response = await next(ct);
        var json = JsonSerializer.Serialize(response);
        await store.SaveAsync(key, json, ct);
        return response;
    }
}
