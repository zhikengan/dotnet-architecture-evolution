using BuildingBlocks.Domain;

namespace Catalog.Contracts.IntegrationEvents;

public sealed record StockDecrementedIntegrationEvent(
    Guid MessageId,
    DateTime OccurredAt,
    Guid OrderId,
    Guid ProductId,
    int Quantity) : IIntegrationEvent;
