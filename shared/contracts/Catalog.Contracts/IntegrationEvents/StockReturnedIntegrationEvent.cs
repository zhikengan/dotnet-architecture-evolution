using BuildingBlocks.Domain;

namespace Catalog.Contracts.IntegrationEvents;

public sealed record StockReturnedIntegrationEvent(
    Guid MessageId,
    DateTime OccurredAt,
    Guid TenantId,
    Guid OrderId,
    Guid ProductId,
    int Quantity,
    int CurrentStock) : IIntegrationEvent;
