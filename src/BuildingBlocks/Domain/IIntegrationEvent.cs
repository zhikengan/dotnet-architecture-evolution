namespace BuildingBlocks.Domain;

public interface IIntegrationEvent
{
    Guid MessageId { get; }
    DateTime OccurredAt { get; }
}
