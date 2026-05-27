using BuildingBlocks.Application;
using MassTransit;
using Microsoft.Extensions.Logging;
using Notifications.Application.Abstractions;
using Notifications.Contracts.IntegrationEvents;
using Notifications.Domain.Notifications;
using Orders.Contracts.IntegrationEvents;

namespace Notifications.Application.EventHandlers.Integration;

/// <summary>
/// Persists an `OrderConfirmed` notification record and emits
/// <see cref="NotificationSentIntegrationEvent"/>. In a real system this is
/// where SendGrid / Twilio / FCM would be invoked; the demo just records the
/// row so it can be observed via the admin query endpoint.
/// </summary>
public sealed class WhenOrderConfirmed_SendNotification(
    INotificationsDbContext db,
    IClock clock,
    ILogger<WhenOrderConfirmed_SendNotification> logger)
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
