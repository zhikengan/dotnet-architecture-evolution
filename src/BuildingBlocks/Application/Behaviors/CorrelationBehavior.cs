using MediatR;
using Microsoft.AspNetCore.Http;

namespace BuildingBlocks.Application.Behaviors;

public sealed class CorrelationBehavior<TRequest, TResponse>(
    ICorrelationContext correlation,
    IHttpContextAccessor httpContextAccessor)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var headerId = httpContextAccessor.HttpContext?.Request.Headers["X-Correlation-Id"].ToString();
        if (correlation is CorrelationContext ctx && !string.IsNullOrWhiteSpace(headerId))
        {
            ctx.CorrelationId = headerId;
        }
        return next(ct);
    }
}
