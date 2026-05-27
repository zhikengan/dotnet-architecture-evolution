using Platform.Domain.FeatureFlags;
using Platform.Domain.FeatureFlags.Errors;
using Platform.Infrastructure.FeatureManagement;

namespace Platform.UnitTests;

public class FeatureFlagTests
{
    private static readonly DateTime Now = new(2026, 5, 27, 10, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Tenant = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public void Create_with_valid_data_succeeds()
    {
        var r = FeatureFlag.Create("X", Tenant, true, 50, Now);
        r.IsSuccess.Should().BeTrue();
        r.Value.Id.Should().Be("X");
        r.Value.Enabled.Should().BeTrue();
        r.Value.RolloutPercentage.Should().Be(50);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_with_empty_name_fails(string name)
    {
        var r = FeatureFlag.Create(name, Tenant, true, 0, Now);
        r.Error.Should().Be(FeatureFlagErrors.InvalidName);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Create_with_invalid_rollout_fails(int pct)
    {
        var r = FeatureFlag.Create("X", Tenant, true, pct, Now);
        r.Error.Should().Be(FeatureFlagErrors.InvalidRolloutPercentage);
    }

    [Fact]
    public void SetRolloutPercentage_updates_value_and_timestamp()
    {
        var flag = FeatureFlag.Create("X", Tenant, true, 0, Now).Value;
        var later = Now.AddHours(1);
        flag.SetRolloutPercentage(75, later).IsSuccess.Should().BeTrue();
        flag.RolloutPercentage.Should().Be(75);
        flag.UpdatedAt.Should().Be(later);
    }

    [Fact]
    public void SetRolloutPercentage_out_of_range_fails()
    {
        var flag = FeatureFlag.Create("X", Tenant, true, 0, Now).Value;
        flag.SetRolloutPercentage(101, Now).IsFailure.Should().BeTrue();
        flag.RolloutPercentage.Should().Be(0);
    }

    [Fact]
    public void Toggle_flips_Enabled()
    {
        var flag = FeatureFlag.Create("X", Tenant, true, 0, Now).Value;
        flag.Toggle(Now);
        flag.Enabled.Should().BeFalse();
        flag.Toggle(Now);
        flag.Enabled.Should().BeTrue();
    }

    [Fact]
    public void EnableForUser_adds_user_and_is_idempotent()
    {
        var flag = FeatureFlag.Create("X", Tenant, true, 0, Now).Value;
        var user = Guid.NewGuid();
        flag.EnableForUser(user, Now);
        flag.EnableForUser(user, Now);
        flag.EnabledUserIds.Should().HaveCount(1).And.Contain(user);
    }
}

public class FeatureFlagBucketTests
{
    [Fact]
    public void Same_user_and_flag_always_returns_same_bucket()
    {
        var userId = Guid.NewGuid();
        const string flag = "X";
        var first = DbFeatureManager.ComputeBucket(userId, flag);
        for (var i = 0; i < 10; i++)
            DbFeatureManager.ComputeBucket(userId, flag).Should().Be(first);
    }

    [Fact]
    public void Bucket_is_in_0_to_99_range()
    {
        for (var i = 0; i < 1000; i++)
        {
            var b = DbFeatureManager.ComputeBucket(Guid.NewGuid(), "X");
            b.Should().BeInRange(0, 99);
        }
    }

    [Fact]
    public void Different_flags_for_same_user_typically_yield_different_buckets()
    {
        var user = Guid.NewGuid();
        var a = DbFeatureManager.ComputeBucket(user, "FlagA");
        var b = DbFeatureManager.ComputeBucket(user, "FlagB");
        var c = DbFeatureManager.ComputeBucket(user, "FlagC");
        var distinct = new HashSet<int> { a, b, c };
        distinct.Count.Should().BeGreaterThan(1, "different flag names should generally fall into different buckets");
    }
}
