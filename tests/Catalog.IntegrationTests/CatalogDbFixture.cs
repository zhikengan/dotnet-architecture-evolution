using BuildingBlocks.Application.MultiTenancy;
using BuildingBlocks.Infrastructure.MultiTenancy;
using Catalog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Catalog.IntegrationTests;

public sealed class CatalogDbFixture : IAsyncLifetime
{
    public static readonly Guid AcmeTenantId = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .WithDatabase("catalog_test")
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

    public CatalogDbContext CreateContext(Guid? tenantId = null)
    {
        var tenant = new TenantContext();
        ((ITenantContextSetter)tenant).SetTenant(tenantId ?? AcmeTenantId);
        return new CatalogDbContext(
            new DbContextOptionsBuilder<CatalogDbContext>().UseNpgsql(ConnectionString).Options,
            tenant);
    }

    public async Task ResetAsync()
    {
        await using var db = CreateContext();
        await db.OutboxMessages.ExecuteDeleteAsync();
        await db.InboxMessages.ExecuteDeleteAsync();
        await db.Products.IgnoreQueryFilters().ExecuteDeleteAsync();
    }
}

[CollectionDefinition(nameof(CatalogDbCollection))]
public class CatalogDbCollection : ICollectionFixture<CatalogDbFixture>;
