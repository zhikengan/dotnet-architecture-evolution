namespace BuildingBlocks.Domain;

/// <summary>
/// Marker for aggregates and entities that are owned by a tenant. Each
/// service's DbContext applies a global query filter so reads automatically
/// scope to the current tenant.
/// </summary>
public interface IMultiTenant
{
    Guid TenantId { get; }
}
