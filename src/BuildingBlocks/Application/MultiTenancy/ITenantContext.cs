namespace BuildingBlocks.Application.MultiTenancy;

/// <summary>
/// Scoped, ambient tenant id for the current unit of work. In the API host
/// it is set per-request from the <c>tenant_id</c> JWT claim. In background
/// processing (outbox dispatch, scheduled jobs) it is set per-message from
/// the event payload before handlers run.
/// </summary>
public interface ITenantContext
{
    Guid TenantId { get; }
    bool IsSet { get; }
}

/// <summary>
/// Same scoped instance as <see cref="ITenantContext"/> but with the
/// mutator surface. Split so domain/application code can depend on the
/// read-only side and only the infrastructure plumbing can write.
/// </summary>
public interface ITenantContextSetter
{
    void SetTenant(Guid tenantId);
}
