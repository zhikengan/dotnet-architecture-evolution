namespace BuildingBlocks.Application;

/// <summary>
/// Ambient tenant for the current request / message handler. Resolved from
/// the JWT in API requests and from the integration event's <c>TenantId</c>
/// in MassTransit consumers.
/// </summary>
public interface ITenantContext
{
    bool IsSet { get; }
    Guid TenantId { get; }
    void Set(Guid tenantId);
}

public sealed class TenantContext : ITenantContext
{
    public bool IsSet { get; private set; }
    public Guid TenantId { get; private set; }

    public void Set(Guid tenantId)
    {
        TenantId = tenantId;
        IsSet = true;
    }
}
