using BuildingBlocks.Domain;
using Platform.Domain.FeatureFlags.Errors;

namespace Platform.Domain.FeatureFlags;

public sealed class FeatureFlag : Entity<string>
{
    public bool Enabled { get; private set; }
    public int RolloutPercentage { get; private set; }
    public List<Guid> EnabledUserIds { get; private set; } = new();
    public DateTime UpdatedAt { get; private set; }

    private FeatureFlag() { }

    public static Result<FeatureFlag> Create(string name, bool enabled, int rolloutPercentage, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 100)
            return Result.Failure<FeatureFlag>(FeatureFlagErrors.InvalidName);
        if (rolloutPercentage is < 0 or > 100)
            return Result.Failure<FeatureFlag>(FeatureFlagErrors.InvalidRolloutPercentage);

        return Result.Success(new FeatureFlag
        {
            Id = name,
            Enabled = enabled,
            RolloutPercentage = rolloutPercentage,
            UpdatedAt = now,
        });
    }

    public Result SetRolloutPercentage(int percentage, DateTime now)
    {
        if (percentage is < 0 or > 100)
            return Result.Failure(FeatureFlagErrors.InvalidRolloutPercentage);
        RolloutPercentage = percentage;
        UpdatedAt = now;
        return Result.Success();
    }

    public void Toggle(DateTime now)
    {
        Enabled = !Enabled;
        UpdatedAt = now;
    }

    public void EnableForUser(Guid userId, DateTime now)
    {
        if (!EnabledUserIds.Contains(userId)) EnabledUserIds.Add(userId);
        UpdatedAt = now;
    }
}
