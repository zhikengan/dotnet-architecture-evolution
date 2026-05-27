using BuildingBlocks.Application.MultiTenancy;
using BuildingBlocks.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Platform.IntegrationTests;

public sealed class PlatformDbFixture : IAsyncLifetime
{
    public static readonly Guid AcmeTenantId = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

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

    public PlatformDbContext CreateContext(Guid? tenantId = null)
    {
        var tenant = new TenantContext();
        ((ITenantContextSetter)tenant).SetTenant(tenantId ?? AcmeTenantId);
        return new PlatformDbContext(
            new DbContextOptionsBuilder<PlatformDbContext>().UseNpgsql(ConnectionString).Options,
            tenant);
    }

    public async Task ResetAsync()
    {
        await using var db = CreateContext();
        await db.FeatureFlags.IgnoreQueryFilters().ExecuteDeleteAsync();
        await db.IdempotencyKeys.ExecuteDeleteAsync();
        await db.Tenants.ExecuteDeleteAsync();
    }
}

[CollectionDefinition(nameof(PlatformDbCollection))]
public class PlatformDbCollection : ICollectionFixture<PlatformDbFixture>;
