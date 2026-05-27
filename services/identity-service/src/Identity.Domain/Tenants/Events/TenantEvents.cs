using BuildingBlocks.Domain;

namespace Identity.Domain.Tenants.Events;

public sealed record TenantCreated(TenantId TenantId, string Name) : IDomainEvent;
