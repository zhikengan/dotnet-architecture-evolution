using BuildingBlocks.Application;
using MassTransit;
using Microsoft.Extensions.Logging;
using Notifications.Application.Abstractions;
using Notifications.Contracts.IntegrationEvents;
using Notifications.Domain.Notifications;
using Orders.Contracts.IntegrationEvents;

namespace Notifications.Application.EventHandlers.Integration;

public sealed class WhenOrderFailed_SendNotification(
    INotificationsDbContext db,
    IClock clock,
    ILogger<WhenOrderFailed_SendNotification> logger)
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
