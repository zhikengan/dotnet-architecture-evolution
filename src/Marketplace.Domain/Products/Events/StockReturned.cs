using Marketplace.Domain.Common;

namespace Marketplace.Domain.Products.Events;

public sealed record StockReturned(ProductId ProductId, int Quantity, int CurrentStock) : IDomainEvent;
