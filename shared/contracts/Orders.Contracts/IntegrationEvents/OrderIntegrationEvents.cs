using BuildingBlocks.Domain;

namespace Orders.Contracts.IntegrationEvents;

public sealed record OrderPlacedIntegrationEvent(
    Guid MessageId,
    DateTime OccurredAt,
    Guid TenantId,
    Guid OrderId,
    Guid BuyerId,
    Guid ProductId,
    int Quantity) : IIntegrationEvent;

public sealed record OrderConfirmedIntegrationEvent(
    Guid MessageId,
    DateTime OccurredAt,
    Guid TenantId,
    Guid OrderId,
    Guid BuyerId,
    Guid ProductId,
    int Quantity) : IIntegrationEvent;

public sealed record OrderCancelledIntegrationEvent(
    Guid MessageId,
    DateTime OccurredAt,
    Guid TenantId,
    Guid OrderId,
    Guid ProductId,
    int Quantity,
    bool StockWasDecremented) : IIntegrationEvent;

public sealed record OrderFailedIntegrationEvent(
    Guid MessageId,
    DateTime OccurredAt,
    Guid TenantId,
    Guid OrderId,
    Guid BuyerId,
    string Reason) : IIntegrationEvent;
