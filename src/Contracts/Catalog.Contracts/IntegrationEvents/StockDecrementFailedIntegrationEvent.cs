using BuildingBlocks.Domain;

namespace Catalog.Contracts.IntegrationEvents;

public sealed record StockDecrementFailedIntegrationEvent(
    Guid MessageId,
    DateTime OccurredAt,
    Guid TenantId,
    Guid OrderId,
    Guid ProductId,
    string Reason) : IIntegrationEvent;
