using Marketplace.Domain.Common;

namespace Marketplace.Domain.Products.Events;

public sealed record StockDecremented(ProductId ProductId, int Quantity, int RemainingStock) : IDomainEvent;
