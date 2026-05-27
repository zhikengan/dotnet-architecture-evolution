using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure.Outbox;
using Catalog.Application.Abstractions;
using Catalog.Contracts.IntegrationEvents;
using Catalog.Domain.Products.Events;
using MediatR;

namespace Catalog.Application.EventHandlers.Domain;

public sealed class PublishProductCreatedHandler(ICatalogDbContext db, IClock clock) : INotificationHandler<ProductCreated>
{
    public Task Handle(ProductCreated e, CancellationToken ct)
    {
        db.OutboxMessages.Enqueue(new ProductCreatedIntegrationEvent(
            Guid.NewGuid(), clock.UtcNow, e.ProductId.Value, e.Name, e.Price, e.Stock, e.SellerId));
        return Task.CompletedTask;
    }
}

public sealed class PublishStockDecrementedHandler(ICatalogDbContext db, IClock clock) : INotificationHandler<StockDecremented>
{
    public Task Handle(StockDecremented e, CancellationToken ct)
    {
        db.OutboxMessages.Enqueue(new StockDecrementedIntegrationEvent(
            Guid.NewGuid(), clock.UtcNow, e.OrderId, e.ProductId.Value, e.Quantity));
        return Task.CompletedTask;
    }
}

public sealed class PublishStockDecrementFailedHandler(ICatalogDbContext db, IClock clock) : INotificationHandler<StockDecrementFailed>
{
    public Task Handle(StockDecrementFailed e, CancellationToken ct)
    {
        db.OutboxMessages.Enqueue(new StockDecrementFailedIntegrationEvent(
            Guid.NewGuid(), clock.UtcNow, e.OrderId, e.ProductId.Value, e.Reason));
        return Task.CompletedTask;
    }
}
