using BuildingBlocks.Domain;

namespace Platform.Domain.FeatureFlags;

public readonly record struct FeatureFlagId(Guid Value)
{
    public static FeatureFlagId New() => new(Guid.NewGuid());
}

public sealed class FeatureFlag : AggregateRoot<FeatureFlagId>, IMultiTenant
{
    public Guid TenantId { get; private set; }
    public string Key { get; private set; } = string.Empty;
    public bool IsEnabled { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private FeatureFlag() { }

    public static FeatureFlag Create(Guid tenantId, string key, bool isEnabled, DateTime now) => new()
    {
        Id = FeatureFlagId.New(),
        TenantId = tenantId,
        Key = key,
        IsEnabled = isEnabled,
        UpdatedAt = now,
    };

    public void Toggle(bool isEnabled, DateTime now)
    {
        IsEnabled = isEnabled;
        UpdatedAt = now;
        RaiseDomainEvent(new FeatureFlagToggled(Id, TenantId, Key, isEnabled));
    }
}

public sealed record FeatureFlagToggled(FeatureFlagId Id, Guid TenantId, string Key, bool IsEnabled) : IDomainEvent;
