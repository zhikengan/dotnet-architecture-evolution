using BuildingBlocks.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Platform.Application.FeatureFlags.Queries;
using Platform.Domain.FeatureFlags;
using Platform.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Platform.IntegrationTests;

public sealed class PlatformDbFixture : IAsyncLifetime
{
    public static readonly Guid AcmeTenant = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .WithDatabase("platform_test")
        .WithUsername("platform")
        .WithPassword("platform")
        .Build();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var db = NewContext();
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    public PlatformDbContext NewContext()
    {
        var opt = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .Options;
        return new PlatformDbContext(opt);
    }
}

[CollectionDefinition(nameof(PlatformDbCollection))]
public class PlatformDbCollection : ICollectionFixture<PlatformDbFixture>;

[Collection(nameof(PlatformDbCollection))]
public class FeatureFlagPersistenceTests(PlatformDbFixture fx)
{
    [Fact]
    public async Task FeatureFlag_round_trips_with_jsonb_user_list()
    {
        var flag = FeatureFlag.Create(PlatformDbFixture.AcmeTenant, "RoundTrip", isEnabled: true, rolloutPercentage: 75, DateTime.UtcNow);
        var user = Guid.NewGuid();
        flag.EnableForUser(user, DateTime.UtcNow);

        await using (var db = fx.NewContext())
        {
            db.FeatureFlags.Add(flag);
            await db.SaveChangesAsync();
        }

        await using var read = fx.NewContext();
        var loaded = await read.FeatureFlags.SingleAsync(f => f.Id == flag.Id);
        loaded.IsEnabled.Should().BeTrue();
        loaded.RolloutPercentage.Should().Be(75);
        loaded.EnabledUserIds.Should().ContainSingle(g => g == user);
    }

    [Fact]
    public async Task IsFeatureEnabledHandler_respects_opt_in_at_zero_rollout()
    {
        var optInUser = Guid.NewGuid();
        var anonUser = Guid.NewGuid();
        var flag = FeatureFlag.Create(PlatformDbFixture.AcmeTenant, "OptIn", isEnabled: true, rolloutPercentage: 0, DateTime.UtcNow);
        flag.EnableForUser(optInUser, DateTime.UtcNow);
        await using (var db = fx.NewContext())
        {
            db.FeatureFlags.Add(flag);
            await db.SaveChangesAsync();
        }

        await using var read = fx.NewContext();
        var handler = new IsFeatureEnabledHandler(read);

        (await handler.Handle(new IsFeatureEnabledQuery(PlatformDbFixture.AcmeTenant, "OptIn", optInUser), CancellationToken.None))
            .Value.Should().BeTrue("explicit opt-in beats 0% rollout");
        (await handler.Handle(new IsFeatureEnabledQuery(PlatformDbFixture.AcmeTenant, "OptIn", anonUser), CancellationToken.None))
            .Value.Should().BeFalse("non-opt-in user at 0% rollout sees off");
    }

    [Fact]
    public async Task IsFeatureEnabledHandler_returns_true_at_100_percent_rollout()
    {
        var flag = FeatureFlag.Create(PlatformDbFixture.AcmeTenant, "FullRollout", isEnabled: true, rolloutPercentage: 100, DateTime.UtcNow);
        await using (var db = fx.NewContext())
        {
            db.FeatureFlags.Add(flag);
            await db.SaveChangesAsync();
        }

        await using var read = fx.NewContext();
        var handler = new IsFeatureEnabledHandler(read);
        for (var i = 0; i < 5; i++)
        {
            (await handler.Handle(new IsFeatureEnabledQuery(PlatformDbFixture.AcmeTenant, "FullRollout", Guid.NewGuid()), CancellationToken.None))
                .Value.Should().BeTrue();
        }
    }
}
