using BuildingBlocks.Domain;

namespace Platform.Domain.Tenants;

/// <summary>
/// Tenant directory entry. Owned by the Platform module so other modules
/// never need to reach across boundaries for tenant identity — they get a
/// <c>Guid TenantId</c> via <c>ITenantContext</c> and trust it. The Tenants
/// table is explicitly NOT tenant-filtered (it holds the global directory),
/// so the EF query filter helper deliberately skips it.
/// </summary>
public sealed class Tenant : Entity<Guid>
{
    public string Slug { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }

    private Tenant() { }

    public static Result<Tenant> Create(Guid id, string slug, string name, DateTime now)
    {
        if (id == Guid.Empty) return Result.Failure<Tenant>(new Error("Tenant.InvalidId", "TenantId is required"));
        if (string.IsNullOrWhiteSpace(slug) || slug.Length > 50)
            return Result.Failure<Tenant>(new Error("Tenant.InvalidSlug", "Slug must be 1-50 characters"));
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200)
            return Result.Failure<Tenant>(new Error("Tenant.InvalidName", "Name must be 1-200 characters"));
        return Result.Success(new Tenant { Id = id, Slug = slug, Name = name, CreatedAt = now });
    }
}
