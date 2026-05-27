using MediatR;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Application.Behaviors;

public sealed class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var name = typeof(TRequest).Name;
        logger.LogInformation("Handling {Request}", name);
        try
        {
            var response = await next(ct);
            logger.LogInformation("Handled {Request}", name);
            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed {Request}", name);
            throw;
        }
    }
}
