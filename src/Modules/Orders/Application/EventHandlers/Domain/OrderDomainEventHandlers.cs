using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure.Outbox;
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
            Guid.NewGuid(), clock.UtcNow, e.OrderId.Value, e.BuyerId, e.ProductId, e.Quantity));
        return Task.CompletedTask;
    }
}

public sealed class PublishOrderCancelledHandler(IOrdersDbContext db, IClock clock) : INotificationHandler<OrderCancelled>
{
    public Task Handle(OrderCancelled e, CancellationToken ct)
    {
        db.OutboxMessages.Enqueue(new OrderCancelledIntegrationEvent(
            Guid.NewGuid(), clock.UtcNow, e.OrderId.Value, e.ProductId, e.Quantity, e.StockWasDecremented));
        return Task.CompletedTask;
    }
}
