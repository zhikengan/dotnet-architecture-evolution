using Marketplace.Domain.Common;
using Marketplace.Domain.Products;

namespace Marketplace.Domain.Orders.Events;

public sealed record OrderPlaced(OrderId OrderId, Guid BuyerId, ProductId ProductId, int Quantity) : IDomainEvent;
