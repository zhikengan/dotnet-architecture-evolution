using Microsoft.EntityFrameworkCore;
using Orders.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Orders.IntegrationTests;

public sealed class OrdersDbFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .WithDatabase("orders_test")
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

    public OrdersDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<OrdersDbContext>().UseNpgsql(ConnectionString).Options);

    public async Task ResetAsync()
    {
        await using var db = CreateContext();
        db.OutboxMessages.RemoveRange(db.OutboxMessages);
        db.InboxMessages.RemoveRange(db.InboxMessages);
        db.Orders.RemoveRange(db.Orders);
        await db.SaveChangesAsync();
    }
}

[CollectionDefinition(nameof(OrdersDbCollection))]
public class OrdersDbCollection : ICollectionFixture<OrdersDbFixture>;
