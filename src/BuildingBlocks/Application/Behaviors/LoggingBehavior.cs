using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Application.Behaviors;

public sealed class LoggingBehavior<TRequest, TResponse>(
    ICorrelationContext correlation,
    ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var requestName = typeof(TRequest).Name;
        var sw = Stopwatch.StartNew();
        try
        {
            using var scope = logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = correlation.CorrelationId,
                ["Request"] = requestName,
            });

            var response = await next(ct);
            sw.Stop();
            logger.LogInformation(
                "Handled {RequestName} in {ElapsedMs}ms (CorrelationId={CorrelationId})",
                requestName, sw.ElapsedMilliseconds, correlation.CorrelationId);
            return response;
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger.LogError(ex,
                "Failed {RequestName} after {ElapsedMs}ms (CorrelationId={CorrelationId})",
                requestName, sw.ElapsedMilliseconds, correlation.CorrelationId);
            throw;
        }
    }
}
