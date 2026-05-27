using BuildingBlocks.Domain;

namespace Orders.Domain.Orders.Events;

public sealed record OrderPlaced(OrderId OrderId, Guid BuyerId, Guid ProductId, int Quantity) : IDomainEvent;
public sealed record OrderConfirmed(OrderId OrderId) : IDomainEvent;
public sealed record OrderCancelled(OrderId OrderId, Guid ProductId, int Quantity, bool StockWasDecremented) : IDomainEvent;
public sealed record OrderFailed(OrderId OrderId, string Reason) : IDomainEvent;
