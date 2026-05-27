using BuildingBlocks.Application;
using MassTransit;
using Microsoft.Extensions.Logging;
using Notifications.Application.Abstractions;
using Notifications.Contracts.IntegrationEvents;
using Notifications.Domain.Notifications;
using Orders.Contracts.IntegrationEvents;

namespace Notifications.Application.Consumers;

/// <summary>
/// Records a notification per order lifecycle event. In real systems this
/// would dispatch email/SMS/push; here we just persist a row and emit
/// <see cref="NotificationSentIntegrationEvent"/> so downstream audit
/// consumers can observe the send.
/// </summary>
public sealed class WhenOrderConfirmedConsumer(
    INotificationsDbContext db,
    IClock clock,
    ILogger<WhenOrderConfirmedConsumer> logger)
    : IConsumer<OrderConfirmedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<OrderConfirmedIntegrationEvent> context)
    {
        var evt = context.Message;
        var notification = Notification.Create(
            tenantId: evt.TenantId,
            type: "OrderConfirmed",
            recipient: evt.BuyerId.ToString(),
            orderId: evt.OrderId,
            body: $"Your order {evt.OrderId} for product {evt.ProductId} (qty {evt.Quantity}) is confirmed.",
            now: clock.UtcNow);
        db.Notifications.Add(notification);

        await context.Publish(new NotificationSentIntegrationEvent(
            MessageId: Guid.NewGuid(),
            OccurredAt: clock.UtcNow,
            TenantId: evt.TenantId,
            NotificationId: notification.Id.Value,
            RelatedOrderId: evt.OrderId,
            Type: notification.Type,
            Recipient: notification.Recipient));

        await db.SaveChangesAsync(context.CancellationToken);
        logger.LogInformation("Sent {Type} notification for order {OrderId}", notification.Type, evt.OrderId);
    }
}

public sealed class WhenOrderCancelledConsumer(
    INotificationsDbContext db,
    IClock clock,
    ILogger<WhenOrderCancelledConsumer> logger)
    : IConsumer<OrderCancelledIntegrationEvent>
{
    public async Task Consume(ConsumeContext<OrderCancelledIntegrationEvent> context)
    {
        var evt = context.Message;
        var notification = Notification.Create(
            tenantId: evt.TenantId,
            type: "OrderCancelled",
            recipient: "buyer",
            orderId: evt.OrderId,
            body: $"Your order {evt.OrderId} has been cancelled.",
            now: clock.UtcNow);
        db.Notifications.Add(notification);

        await context.Publish(new NotificationSentIntegrationEvent(
            MessageId: Guid.NewGuid(),
            OccurredAt: clock.UtcNow,
            TenantId: evt.TenantId,
            NotificationId: notification.Id.Value,
            RelatedOrderId: evt.OrderId,
            Type: notification.Type,
            Recipient: notification.Recipient));

        await db.SaveChangesAsync(context.CancellationToken);
        logger.LogInformation("Sent {Type} notification for order {OrderId}", notification.Type, evt.OrderId);
    }
}

public sealed class WhenOrderFailedConsumer(
    INotificationsDbContext db,
    IClock clock,
    ILogger<WhenOrderFailedConsumer> logger)
    : IConsumer<OrderFailedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<OrderFailedIntegrationEvent> context)
    {
        var evt = context.Message;
        var notification = Notification.Create(
            tenantId: evt.TenantId,
            type: "OrderFailed",
            recipient: evt.BuyerId.ToString(),
            orderId: evt.OrderId,
            body: $"We couldn't place your order {evt.OrderId}: {evt.Reason}.",
            now: clock.UtcNow);
        db.Notifications.Add(notification);

        await context.Publish(new NotificationSentIntegrationEvent(
            MessageId: Guid.NewGuid(),
            OccurredAt: clock.UtcNow,
            TenantId: evt.TenantId,
            NotificationId: notification.Id.Value,
            RelatedOrderId: evt.OrderId,
            Type: notification.Type,
            Recipient: notification.Recipient));

        await db.SaveChangesAsync(context.CancellationToken);
        logger.LogInformation("Sent {Type} notification for order {OrderId}", notification.Type, evt.OrderId);
    }
}
