using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Platform.IntegrationTests;

public sealed class PlatformDbFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .WithDatabase("platform_test")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    public PlatformDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<PlatformDbContext>().UseNpgsql(ConnectionString).Options);

    public async Task ResetAsync()
    {
        await using var db = CreateContext();
        db.FeatureFlags.RemoveRange(db.FeatureFlags);
        db.IdempotencyKeys.RemoveRange(db.IdempotencyKeys);
        await db.SaveChangesAsync();
    }
}

[CollectionDefinition(nameof(PlatformDbCollection))]
public class PlatformDbCollection : ICollectionFixture<PlatformDbFixture>;
