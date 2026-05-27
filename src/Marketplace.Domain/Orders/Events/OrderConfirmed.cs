using Marketplace.Domain.Common;

namespace Marketplace.Domain.Orders.Events;

public sealed record OrderConfirmed(OrderId OrderId) : IDomainEvent;
