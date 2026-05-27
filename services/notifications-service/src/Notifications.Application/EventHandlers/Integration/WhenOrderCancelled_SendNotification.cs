using BuildingBlocks.Application;
using MassTransit;
using Microsoft.Extensions.Logging;
using Notifications.Application.Abstractions;
using Notifications.Contracts.IntegrationEvents;
using Notifications.Domain.Notifications;
using Orders.Contracts.IntegrationEvents;

namespace Notifications.Application.EventHandlers.Integration;

public sealed class WhenOrderCancelled_SendNotification(
    INotificationsDbContext db,
    IClock clock,
    ILogger<WhenOrderCancelled_SendNotification> logger)
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
