using BuildingBlocks.Domain;

namespace Catalog.Contracts.IntegrationEvents;

public sealed record ProductCreatedIntegrationEvent(
    Guid MessageId,
    DateTime OccurredAt,
    Guid ProductId,
    string Name,
    decimal Price,
    int Stock,
    Guid SellerId) : IIntegrationEvent;
