namespace BuildingBlocks.Infrastructure.EventBus;

public interface IEventBus
{
    Task PublishAsync(object integrationEvent, CancellationToken ct);
}
