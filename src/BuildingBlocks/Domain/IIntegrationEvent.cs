namespace BuildingBlocks.Domain;

public interface IIntegrationEvent
{
    Guid MessageId { get; }
    DateTime OccurredAt { get; }

    /// <summary>
    /// Tenant the event was raised under. The OutboxProcessor / EventBus
    /// set <see cref="MultiTenancy.IMultiTenant"/> ambient context from this
    /// before invoking subscribers, so consumer DbContexts apply the right
    /// query filter without per-handler boilerplate.
    /// </summary>
    Guid TenantId { get; }
}
