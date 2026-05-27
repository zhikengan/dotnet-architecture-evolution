using BuildingBlocks.Domain;

namespace Identity.Contracts.IntegrationEvents;

public sealed record UserCreatedIntegrationEvent(
    Guid MessageId,
    DateTime OccurredAt,
    Guid TenantId,
    Guid UserId,
    string Email,
    string Role) : IIntegrationEvent;

public sealed record TenantCreatedIntegrationEvent(
    Guid MessageId,
    DateTime OccurredAt,
    Guid TenantId,
    string Name) : IIntegrationEvent;
