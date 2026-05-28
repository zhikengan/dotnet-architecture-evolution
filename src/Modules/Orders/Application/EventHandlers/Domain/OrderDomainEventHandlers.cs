using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure.Outbox;
using BuildingBlocks.Infrastructure.Telemetry;
using MediatR;
using Orders.Application.Abstractions;
using Orders.Contracts.IntegrationEvents;
using Orders.Domain.Orders.Events;

namespace Orders.Application.EventHandlers.Domain;

public sealed class PublishOrderPlacedHandler(IOrdersDbContext db, IClock clock) : INotificationHandler<OrderPlaced>
{
    public Task Handle(OrderPlaced e, CancellationToken ct)
    {
        db.OutboxMessages.Enqueue(new OrderPlacedIntegrationEvent(
            Guid.NewGuid(), clock.UtcNow, e.TenantId, e.OrderId.Value, e.BuyerId, e.ProductId, e.Quantity));
        return Task.CompletedTask;
    }
}

public sealed class PublishOrderConfirmedHandler(IOrdersDbContext db, IClock clock) : INotificationHandler<OrderConfirmed>
{
    public Task Handle(OrderConfirmed e, CancellationToken ct)
    {
        db.OutboxMessages.Enqueue(new OrderConfirmedIntegrationEvent(
            Guid.NewGuid(), clock.UtcNow, e.TenantId, e.OrderId.Value, e.BuyerId));
        return Task.CompletedTask;
    }
}

public sealed class PublishOrderCancelledHandler(IOrdersDbContext db, IClock clock) : INotificationHandler<OrderCancelled>
{
    public Task Handle(OrderCancelled e, CancellationToken ct)
    {
        db.OutboxMessages.Enqueue(new OrderCancelledIntegrationEvent(
            Guid.NewGuid(), clock.UtcNow, e.TenantId, e.OrderId.Value, e.ProductId, e.Quantity, e.StockWasDecremented));
        MarketplaceMeter.OrdersCancelled.Add(1,
            new KeyValuePair<string, object?>("tenant_id", e.TenantId),
            new KeyValuePair<string, object?>("reason", e.StockWasDecremented ? "with_stock_return" : "no_stock_return"));
        return Task.CompletedTask;
    }
}
