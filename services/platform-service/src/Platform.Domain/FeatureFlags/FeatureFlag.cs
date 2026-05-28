using BuildingBlocks.Domain;

namespace Platform.Domain.FeatureFlags;

public readonly record struct FeatureFlagId(Guid Value)
{
    public static FeatureFlagId New() => new(Guid.NewGuid());
}

/// <summary>
/// Tenant-scoped feature flag with the same semantics as Tier 4's modular
/// monolith: global on/off, percentage rollout, and an explicit user opt-in
/// list. <c>IsFeatureEnabledForUser</c> evaluates them in that order — opt-in
/// users see the flag even when rollout is 0%.
/// </summary>
public sealed class FeatureFlag : AggregateRoot<FeatureFlagId>, IMultiTenant
{
    public Guid TenantId { get; private set; }
    public string Key { get; private set; } = string.Empty;
    public bool IsEnabled { get; private set; }
    public int RolloutPercentage { get; private set; }
    public List<Guid> EnabledUserIds { get; private set; } = [];
    public DateTime UpdatedAt { get; private set; }

    private FeatureFlag() { }

    public static FeatureFlag Create(Guid tenantId, string key, bool isEnabled, int rolloutPercentage, DateTime now)
    {
        if (rolloutPercentage is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(rolloutPercentage));
        return new FeatureFlag
        {
            Id = FeatureFlagId.New(),
            TenantId = tenantId,
            Key = key,
            IsEnabled = isEnabled,
            RolloutPercentage = rolloutPercentage,
            UpdatedAt = now,
        };
    }

    public void Toggle(bool isEnabled, DateTime now)
    {
        IsEnabled = isEnabled;
        UpdatedAt = now;
        RaiseDomainEvent(new FeatureFlagToggled(Id, TenantId, Key, isEnabled));
    }

    public Result SetRolloutPercentage(int percentage, DateTime now)
    {
        if (percentage is < 0 or > 100)
            return Result.Failure(new Error("FeatureFlag.InvalidRollout", "Rollout percentage must be 0-100"));
        RolloutPercentage = percentage;
        UpdatedAt = now;
        return Result.Success();
    }

    public void EnableForUser(Guid userId, DateTime now)
    {
        if (!EnabledUserIds.Contains(userId)) EnabledUserIds.Add(userId);
        UpdatedAt = now;
    }
}

public sealed record FeatureFlagToggled(FeatureFlagId Id, Guid TenantId, string Key, bool IsEnabled) : IDomainEvent;
