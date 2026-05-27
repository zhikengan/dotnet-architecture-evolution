using BuildingBlocks.Domain;

namespace Notifications.Contracts.IntegrationEvents;

public sealed record NotificationSentIntegrationEvent(
    Guid MessageId,
    DateTime OccurredAt,
    Guid TenantId,
    Guid NotificationId,
    Guid? RelatedOrderId,
    string Type,
    string Recipient) : IIntegrationEvent;
