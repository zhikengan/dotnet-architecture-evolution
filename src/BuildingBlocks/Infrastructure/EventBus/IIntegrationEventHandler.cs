using BuildingBlocks.Domain;

namespace BuildingBlocks.Infrastructure.EventBus;

public interface IIntegrationEventHandler<in TEvent> where TEvent : IIntegrationEvent
{
    Task HandleAsync(TEvent integrationEvent, CancellationToken ct);
}
