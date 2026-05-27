using BuildingBlocks.Domain;
using BuildingBlocks.Domain.MultiTenancy;

namespace Platform.Domain.Emails;

/// <summary>
/// Log of "emails sent" — at Tier 4 the SendOrderEmailService just inserts a
/// row and writes a log line, but the persistence shape mirrors what a real
/// email integration would record (recipient, template, idempotency-key).
/// Tenant-scoped so reports stay partitioned.
/// </summary>
public sealed class SentEmail : Entity<Guid>, IMultiTenant
{
    public Guid TenantId { get; private set; }
    public string Template { get; private set; } = string.Empty;
    public string Recipient { get; private set; } = string.Empty;
    public Guid? RelatedEntityId { get; private set; }
    public DateTime SentAt { get; private set; }

    private SentEmail() { }

    public static SentEmail Create(Guid tenantId, string template, string recipient, Guid? relatedEntityId, DateTime now) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Template = template,
            Recipient = recipient,
            RelatedEntityId = relatedEntityId,
            SentAt = now,
        };
}
