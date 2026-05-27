using BuildingBlocks.Domain;

namespace Identity.Domain.Users.Events;

public sealed record UserCreated(UserId UserId, Guid TenantId, string Email, UserRole Role) : IDomainEvent;
