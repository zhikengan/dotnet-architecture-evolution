using Marketplace.Domain.Common;

namespace Marketplace.Domain.Products.Events;

public sealed record ProductCreated(ProductId ProductId, Guid SellerId) : IDomainEvent;
