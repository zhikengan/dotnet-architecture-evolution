using BuildingBlocks.Domain;

namespace Platform.Domain.FeatureFlags.Errors;

public static class FeatureFlagErrors
{
    public static readonly Error NotFound = new("FeatureFlag.NotFound", "Feature flag not found");
    public static readonly Error InvalidName = new("FeatureFlag.InvalidName", "Flag name must be 1-100 characters");
    public static readonly Error InvalidRolloutPercentage = new("FeatureFlag.InvalidRolloutPercentage", "Rollout percentage must be between 0 and 100");
}
