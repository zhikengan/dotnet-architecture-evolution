using BuildingBlocks.Application.MultiTenancy;
using BuildingBlocks.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Infrastructure.EventBus;

public sealed class InMemoryEventBus(
    IServiceProvider rootProvider,
    ILogger<InMemoryEventBus> logger) : IEventBus
{
    public async Task PublishAsync(object integrationEvent, CancellationToken ct)
    {
        var eventType = integrationEvent.GetType();
        var handlerType = typeof(IIntegrationEventHandler<>).MakeGenericType(eventType);

        using var scope = rootProvider.CreateScope();

        // Lift TenantId from the event onto the ambient context so subscriber
        // DbContexts apply the correct query filter. Done once per dispatch so
        // every handler in this scope sees the same tenant.
        if (integrationEvent is IIntegrationEvent envelope && envelope.TenantId != Guid.Empty)
        {
            scope.ServiceProvider.GetRequiredService<ITenantContextSetter>().SetTenant(envelope.TenantId);
        }

        var handlers = scope.ServiceProvider.GetServices(handlerType).Where(h => h is not null).ToList();

        if (handlers.Count == 0)
        {
            logger.LogDebug("No handlers for integration event {EventType}", eventType.Name);
            return;
        }

        var method = handlerType.GetMethod(nameof(IIntegrationEventHandler<DummyEvent>.HandleAsync))!;
        foreach (var handler in handlers)
        {
            try
            {
                var task = (Task)method.Invoke(handler, [integrationEvent, ct])!;
                await task;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Handler {Handler} failed for event {EventType}",
                    handler!.GetType().Name, eventType.Name);
                throw;
            }
        }
    }

    private sealed record DummyEvent(Guid MessageId, DateTime OccurredAt, Guid TenantId) : IIntegrationEvent;
}
