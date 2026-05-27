using Catalog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Catalog.IntegrationTests;

public sealed class CatalogDbFixture : IAsyncLifetime
{
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

    public CatalogDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<CatalogDbContext>().UseNpgsql(ConnectionString).Options);

    public async Task ResetAsync()
    {
        await using var db = CreateContext();
        db.OutboxMessages.RemoveRange(db.OutboxMessages);
        db.InboxMessages.RemoveRange(db.InboxMessages);
        db.Products.RemoveRange(db.Products);
        await db.SaveChangesAsync();
    }
}

[CollectionDefinition(nameof(CatalogDbCollection))]
public class CatalogDbCollection : ICollectionFixture<CatalogDbFixture>;
