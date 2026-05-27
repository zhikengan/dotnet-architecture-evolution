namespace BuildingBlocks.Infrastructure.Outbox;

public sealed class OutboxMessage
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;

    /// <summary>
    /// Tenant the producer was acting under. The OutboxProcessor lifts
    /// this onto the ambient <c>ITenantContext</c> before dispatching so
    /// subscriber query filters resolve to the right tenant. Stored on
    /// the row (rather than only inside the payload) so operators can
    /// see at a glance which tenant a stuck message belongs to.
    /// </summary>
    public Guid TenantId { get; set; }

    public DateTime OccurredAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public int RetryCount { get; set; }
    public string? LastError { get; set; }
}
