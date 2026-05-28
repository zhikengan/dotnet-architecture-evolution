using BuildingBlocks.Application;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Orders.Domain.Orders;
using Orders.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Orders.IntegrationTests;

public sealed class OrdersDbFixture : IAsyncLifetime
{
    public static readonly Guid AcmeTenant = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .WithDatabase("orders_test")
        .WithUsername("orders")
        .WithPassword("orders")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var db = NewContext(AcmeTenant);
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    public OrdersDbContext NewContext(Guid tenantId)
    {
        var tenant = Substitute.For<ITenantContext>();
        tenant.TenantId.Returns(tenantId);
        tenant.IsSet.Returns(true);
        var opt = new DbContextOptionsBuilder<OrdersDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
        return new OrdersDbContext(opt, tenant);
    }
}

[CollectionDefinition(nameof(OrdersDbCollection))]
public class OrdersDbCollection : ICollectionFixture<OrdersDbFixture>;

[Collection(nameof(OrdersDbCollection))]
public class OrderPersistenceTests(OrdersDbFixture fx)
{
    [Fact]
    public async Task Order_round_trips_with_status_enum_persisted_as_string()
    {
        var order = Order.Create(Guid.NewGuid(), Guid.NewGuid(), 2, OrdersDbFixture.AcmeTenant, DateTime.UtcNow).Value;
        order.ClearDomainEvents();

        await using (var db = fx.NewContext(OrdersDbFixture.AcmeTenant))
        {
            db.Orders.Add(order);
            await db.SaveChangesAsync();
        }

        await using var read = fx.NewContext(OrdersDbFixture.AcmeTenant);
        var loaded = await read.Orders.SingleAsync(o => o.Id == order.Id);
        loaded.Status.Should().Be(OrderStatus.Pending);
        loaded.Quantity.Should().Be(2);
    }

    [Fact]
    public async Task Confirmed_order_persists_status_change()
    {
        var order = Order.Create(Guid.NewGuid(), Guid.NewGuid(), 1, OrdersDbFixture.AcmeTenant, DateTime.UtcNow).Value;
        order.Confirm();
        order.ClearDomainEvents();

        await using (var db = fx.NewContext(OrdersDbFixture.AcmeTenant))
        {
            db.Orders.Add(order);
            await db.SaveChangesAsync();
        }

        await using var read = fx.NewContext(OrdersDbFixture.AcmeTenant);
        var loaded = await read.Orders.SingleAsync(o => o.Id == order.Id);
        loaded.Status.Should().Be(OrderStatus.Confirmed);
    }
}
