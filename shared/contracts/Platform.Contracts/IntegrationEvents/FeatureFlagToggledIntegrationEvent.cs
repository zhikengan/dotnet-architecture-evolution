using BuildingBlocks.Domain;

namespace Platform.Contracts.IntegrationEvents;

public sealed record FeatureFlagToggledIntegrationEvent(
    Guid MessageId,
    DateTime OccurredAt,
    Guid TenantId,
    string Key,
    bool IsEnabled) : IIntegrationEvent;
