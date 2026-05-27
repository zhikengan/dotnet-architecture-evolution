using Marketplace.Domain.Common;
using Marketplace.Domain.Products;

namespace Marketplace.Domain.Orders.Events;

public sealed record OrderCancelled(OrderId OrderId, ProductId ProductId, int Quantity, bool StockWasDecremented) : IDomainEvent;
