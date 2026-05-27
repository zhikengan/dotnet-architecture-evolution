using BuildingBlocks.Domain;

namespace Notifications.Domain.Notifications;

public readonly record struct NotificationId(Guid Value)
{
    public static NotificationId New() => new(Guid.NewGuid());
}

public sealed class Notification : AggregateRoot<NotificationId>, IMultiTenant
{
    public Guid TenantId { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public string Recipient { get; private set; } = string.Empty;
    public Guid? RelatedOrderId { get; private set; }
    public string Body { get; private set; } = string.Empty;
    public DateTime SentAt { get; private set; }

    private Notification() { }

    public static Notification Create(Guid tenantId, string type, string recipient, Guid? orderId, string body, DateTime now) => new()
    {
        Id = NotificationId.New(),
        TenantId = tenantId,
        Type = type,
        Recipient = recipient,
        RelatedOrderId = orderId,
        Body = body,
        SentAt = now,
    };
}
