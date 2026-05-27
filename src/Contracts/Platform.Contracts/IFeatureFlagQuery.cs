namespace Platform.Contracts;

public interface IFeatureFlagQuery
{
    ValueTask<bool> IsEnabledAsync(string flagName, Guid userId, CancellationToken ct = default);
}
