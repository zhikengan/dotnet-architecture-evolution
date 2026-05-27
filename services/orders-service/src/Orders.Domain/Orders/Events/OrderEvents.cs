using BuildingBlocks.Domain;

namespace Orders.Domain.Orders.Events;

public sealed record OrderPlaced(OrderId OrderId, Guid TenantId, Guid BuyerId, Guid ProductId, int Quantity) : IDomainEvent;
public sealed record OrderConfirmed(OrderId OrderId, Guid TenantId, Guid BuyerId, Guid ProductId, int Quantity) : IDomainEvent;
public sealed record OrderCancelled(OrderId OrderId, Guid TenantId, Guid ProductId, int Quantity, bool StockWasDecremented) : IDomainEvent;
public sealed record OrderFailed(OrderId OrderId, Guid TenantId, Guid BuyerId, string Reason) : IDomainEvent;
