using BuildingBlocks.Domain;

namespace BuildingBlocks.Infrastructure.Outbox;

public interface IOutboxWriter
{
    void Add<TEvent>(TEvent integrationEvent) where TEvent : IIntegrationEvent;
}
