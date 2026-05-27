using BuildingBlocks.Domain;

namespace Identity.Domain.Tenants.Errors;

public static class TenantErrors
{
    public static readonly Error InvalidName = new("Tenant.InvalidName", "Tenant name must be 1-100 characters");
    public static readonly Error NotFound = new("Tenant.NotFound", "Tenant not found");
}
