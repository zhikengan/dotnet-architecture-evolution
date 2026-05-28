using Platform.Application.FeatureFlags.Queries;
using Platform.Domain.FeatureFlags;

namespace Platform.UnitTests;

public class FeatureFlagTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Tenant = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public void Create_with_valid_rollout_succeeds()
    {
        var flag = FeatureFlag.Create(Tenant, "X", isEnabled: true, rolloutPercentage: 50, Now);
        flag.IsEnabled.Should().BeTrue();
        flag.RolloutPercentage.Should().Be(50);
        flag.EnabledUserIds.Should().BeEmpty();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Create_with_out_of_range_rollout_throws(int pct) =>
        FluentActions.Invoking(() => FeatureFlag.Create(Tenant, "X", true, pct, Now))
            .Should().Throw<ArgumentOutOfRangeException>();

    [Fact]
    public void Toggle_flips_and_raises_FeatureFlagToggled()
    {
        var f = FeatureFlag.Create(Tenant, "X", isEnabled: true, rolloutPercentage: 0, Now);
        f.Toggle(false, Now.AddMinutes(1));
        f.IsEnabled.Should().BeFalse();
        f.DomainEvents.Should().ContainSingle(e => e is FeatureFlagToggled);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void SetRolloutPercentage_out_of_range_fails(int pct) =>
        FeatureFlag.Create(Tenant, "X", true, 0, Now).SetRolloutPercentage(pct, Now).IsFailure.Should().BeTrue();

    [Fact]
    public void EnableForUser_is_idempotent_and_unique()
    {
        var f = FeatureFlag.Create(Tenant, "X", true, 0, Now);
        var user = Guid.NewGuid();
        f.EnableForUser(user, Now);
        f.EnableForUser(user, Now);
        f.EnabledUserIds.Should().ContainSingle(g => g == user);
    }
}

public class FeatureFlagBucketTests
{
    [Fact]
    public void Same_user_and_key_yield_same_bucket()
    {
        var user = Guid.NewGuid();
        var first = IsFeatureEnabledHandler.ComputeBucket(user, "X");
        for (var i = 0; i < 10; i++)
            IsFeatureEnabledHandler.ComputeBucket(user, "X").Should().Be(first);
    }

    [Fact]
    public void Bucket_stays_in_0_99()
    {
        for (var i = 0; i < 500; i++)
        {
            var b = IsFeatureEnabledHandler.ComputeBucket(Guid.NewGuid(), "X");
            b.Should().BeInRange(0, 99);
        }
    }

    [Fact]
    public void Different_keys_for_same_user_typically_diverge()
    {
        var user = Guid.NewGuid();
        var buckets = new[] { "A", "B", "C", "D" }
            .Select(k => IsFeatureEnabledHandler.ComputeBucket(user, k))
            .Distinct()
            .Count();
        buckets.Should().BeGreaterThan(1);
    }
}
