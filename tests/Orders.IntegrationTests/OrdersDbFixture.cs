using BuildingBlocks.Application.MultiTenancy;
using BuildingBlocks.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Orders.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Orders.IntegrationTests;

public sealed class OrdersDbFixture : IAsyncLifetime
{
    public static readonly Guid AcmeTenantId = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

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

    public OrdersDbContext CreateContext(Guid? tenantId = null)
    {
        var tenant = new TenantContext();
        ((ITenantContextSetter)tenant).SetTenant(tenantId ?? AcmeTenantId);
        return new OrdersDbContext(
            new DbContextOptionsBuilder<OrdersDbContext>().UseNpgsql(ConnectionString).Options,
            tenant);
    }

    public async Task ResetAsync()
    {
        await using var db = CreateContext();
        await db.OutboxMessages.ExecuteDeleteAsync();
        await db.InboxMessages.ExecuteDeleteAsync();
        await db.Orders.IgnoreQueryFilters().ExecuteDeleteAsync();
    }
}

[CollectionDefinition(nameof(OrdersDbCollection))]
public class OrdersDbCollection : ICollectionFixture<OrdersDbFixture>;
