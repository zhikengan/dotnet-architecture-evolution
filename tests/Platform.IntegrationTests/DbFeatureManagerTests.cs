using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Platform.Domain.FeatureFlags;
using Platform.Infrastructure.FeatureManagement;
using Platform.Infrastructure.Persistence;

namespace Platform.IntegrationTests;

[Collection(nameof(PlatformDbCollection))]
public class DbFeatureManagerTests(PlatformDbFixture fx) : IAsyncLifetime
{
    public Task InitializeAsync() => fx.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task IsEnabledAsync_returns_false_when_flag_is_missing()
    {
        var mgr = CreateManager(out _);
        var enabled = await mgr.IsEnabledAsync("DoesNotExist", Guid.NewGuid());
        enabled.Should().BeFalse();
    }

    [Fact]
    public async Task IsEnabledAsync_returns_true_for_explicitly_enabled_user()
    {
        var user = Guid.NewGuid();
        await using (var db = fx.CreateContext())
        {
            var flag = FeatureFlag.Create("F", enabled: false, rolloutPercentage: 0, DateTime.UtcNow).Value;
            flag.EnableForUser(user, DateTime.UtcNow);
            db.FeatureFlags.Add(flag);
            await db.SaveChangesAsync();
        }
        var mgr = CreateManager(out _);
        (await mgr.IsEnabledAsync("F", user)).Should().BeTrue();
    }

    [Fact]
    public async Task IsEnabledAsync_returns_true_for_all_users_at_100_percent_rollout()
    {
        await using (var db = fx.CreateContext())
        {
            var flag = FeatureFlag.Create("F", enabled: true, rolloutPercentage: 100, DateTime.UtcNow).Value;
            db.FeatureFlags.Add(flag);
            await db.SaveChangesAsync();
        }
        var mgr = CreateManager(out _);
        for (var i = 0; i < 10; i++)
            (await mgr.IsEnabledAsync("F", Guid.NewGuid())).Should().BeTrue();
    }

    [Fact]
    public async Task IsEnabledAsync_returns_false_for_disabled_flag_even_at_high_rollout()
    {
        await using (var db = fx.CreateContext())
        {
            var flag = FeatureFlag.Create("F", enabled: false, rolloutPercentage: 100, DateTime.UtcNow).Value;
            db.FeatureFlags.Add(flag);
            await db.SaveChangesAsync();
        }
        var mgr = CreateManager(out _);
        (await mgr.IsEnabledAsync("F", Guid.NewGuid())).Should().BeFalse();
    }

    private DbFeatureManager CreateManager(out IMemoryCache cache)
    {
        var services = new ServiceCollection();
        services.AddSingleton<PlatformDbContext>(_ => fx.CreateContext());
        var sp = services.BuildServiceProvider();
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
        cache = new MemoryCache(new MemoryCacheOptions());
        return new DbFeatureManager(scopeFactory, cache, new PlatformOptions { CacheSeconds = 1 });
    }
}
