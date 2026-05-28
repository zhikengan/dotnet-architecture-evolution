using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure.Time;
using Catalog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Testcontainers.PostgreSql;

namespace Catalog.IntegrationTests;

/// <summary>
/// Spins a real PostgreSQL container per test class and applies the catalog
/// service's EF migrations against it. Tests run against the same shape
/// production uses — no in-memory shortcuts.
/// </summary>
public sealed class CatalogDbFixture : IAsyncLifetime
{
    public static readonly Guid AcmeTenant = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .WithDatabase("catalog_test")
        .WithUsername("catalog")
        .WithPassword("catalog")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var db = NewContext(AcmeTenant);
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    public CatalogDbContext NewContext(Guid tenantId)
    {
        var tenant = Substitute.For<ITenantContext>();
        tenant.TenantId.Returns(tenantId);
        tenant.IsSet.Returns(true);
        var opt = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
        return new CatalogDbContext(opt, tenant);
    }

    public static IClock FixedClockAt(DateTime now)
    {
        var c = Substitute.For<IClock>();
        c.UtcNow.Returns(now);
        return c;
    }
}
