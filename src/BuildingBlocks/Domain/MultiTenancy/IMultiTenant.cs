namespace BuildingBlocks.Domain.MultiTenancy;

/// <summary>
/// Every aggregate root in every module must implement this. An architecture
/// test enforces it — see <c>IMultiTenantArchitectureTests</c>. The contract
/// is the basis for EF Core global query filters that prevent cross-tenant
/// data leakage at the read path.
/// </summary>
public interface IMultiTenant
{
    Guid TenantId { get; }
}
