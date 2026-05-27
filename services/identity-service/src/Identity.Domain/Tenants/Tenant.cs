using BuildingBlocks.Domain;
using Identity.Domain.Tenants.Errors;
using Identity.Domain.Tenants.Events;

namespace Identity.Domain.Tenants;

public sealed class Tenant : AggregateRoot<TenantId>
{
    public string Name { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }

    private Tenant() { }

    public static Result<Tenant> Create(string name, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 100)
            return Result.Failure<Tenant>(TenantErrors.InvalidName);

        var tenant = new Tenant
        {
            Id = TenantId.New(),
            Name = name,
            CreatedAt = now,
        };
        tenant.RaiseDomainEvent(new TenantCreated(tenant.Id, name));
        return Result.Success(tenant);
    }

    public static Tenant Seed(Guid id, string name, DateTime now) => new()
    {
        Id = new TenantId(id),
        Name = name,
        CreatedAt = now,
    };
}
