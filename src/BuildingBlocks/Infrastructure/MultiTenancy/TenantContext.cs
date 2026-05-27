using BuildingBlocks.Application.MultiTenancy;

namespace BuildingBlocks.Infrastructure.MultiTenancy;

/// <summary>
/// Scoped impl of <see cref="ITenantContext"/> + <see cref="ITenantContextSetter"/>.
/// Register once as scoped and resolve via either interface.
/// </summary>
public sealed class TenantContext : ITenantContext, ITenantContextSetter
{
    public Guid TenantId { get; private set; } = Guid.Empty;
    public bool IsSet { get; private set; }

    public void SetTenant(Guid tenantId)
    {
        TenantId = tenantId;
        IsSet = true;
    }
}
