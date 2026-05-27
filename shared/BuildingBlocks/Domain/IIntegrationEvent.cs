namespace BuildingBlocks.Domain;

/// <summary>
/// Cross-service integration event. Producers publish via MassTransit's
/// <c>IPublishEndpoint</c>; consumers register as <c>IConsumer&lt;T&gt;</c>.
/// MassTransit auto-propagates <c>MessageId</c> as the dedup key for the
/// inbox; <c>TenantId</c> is carried for downstream tenant resolution.
/// </summary>
public interface IIntegrationEvent
{
    Guid MessageId { get; }
    DateTime OccurredAt { get; }
    Guid TenantId { get; }
}
