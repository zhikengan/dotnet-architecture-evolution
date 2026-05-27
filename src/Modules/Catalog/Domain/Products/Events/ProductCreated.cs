using BuildingBlocks.Domain;

namespace Catalog.Domain.Products.Events;

public sealed record ProductCreated(ProductId ProductId, string Name, decimal Price, int Stock, Guid SellerId) : IDomainEvent;
public sealed record StockDecremented(ProductId ProductId, Guid OrderId, int Quantity, int RemainingStock) : IDomainEvent;
public sealed record StockReturned(ProductId ProductId, Guid OrderId, int Quantity, int CurrentStock) : IDomainEvent;
public sealed record StockDecrementFailed(ProductId ProductId, Guid OrderId, string Reason) : IDomainEvent;
