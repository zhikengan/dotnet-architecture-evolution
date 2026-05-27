using Marketplace.Domain.Common;

namespace Marketplace.Domain.Orders.Events;

public sealed record OrderFailed(OrderId OrderId, string Reason) : IDomainEvent;
