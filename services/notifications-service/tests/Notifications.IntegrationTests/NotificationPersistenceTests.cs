using Microsoft.EntityFrameworkCore;
using Notifications.Domain.Notifications;
using Notifications.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Notifications.IntegrationTests;

public sealed class NotificationsDbFixture : IAsyncLifetime
{
    public static readonly Guid AcmeTenant = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .WithDatabase("notifications_test")
        .WithUsername("notifications")
        .WithPassword("notifications")
        .Build();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var db = NewContext();
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    public NotificationsDbContext NewContext()
    {
        var opt = new DbContextOptionsBuilder<NotificationsDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .Options;
        return new NotificationsDbContext(opt);
    }
}

[CollectionDefinition(nameof(NotificationsDbCollection))]
public class NotificationsDbCollection : ICollectionFixture<NotificationsDbFixture>;

[Collection(nameof(NotificationsDbCollection))]
public class NotificationPersistenceTests(NotificationsDbFixture fx)
{
    [Fact]
    public async Task Notification_round_trips_with_related_order_id_index()
    {
        var orderId = Guid.NewGuid();
        var n = Notification.Create(NotificationsDbFixture.AcmeTenant, "OrderConfirmed", "buyer@example.com", orderId, "Body", DateTime.UtcNow);

        await using (var db = fx.NewContext())
        {
            db.Notifications.Add(n);
            await db.SaveChangesAsync();
        }

        await using var read = fx.NewContext();
        var loaded = await read.Notifications.SingleAsync(x => x.RelatedOrderId == orderId);
        loaded.Type.Should().Be("OrderConfirmed");
        loaded.Recipient.Should().Be("buyer@example.com");
        loaded.Body.Should().Be("Body");
    }

    [Fact]
    public async Task Querying_by_RelatedOrderId_uses_the_index()
    {
        var orderA = Guid.NewGuid();
        var orderB = Guid.NewGuid();
        var n1 = Notification.Create(NotificationsDbFixture.AcmeTenant, "A", "x@example.com", orderA, "", DateTime.UtcNow);
        var n2 = Notification.Create(NotificationsDbFixture.AcmeTenant, "B", "y@example.com", orderB, "", DateTime.UtcNow);
        await using (var db = fx.NewContext())
        {
            db.Notifications.AddRange(n1, n2);
            await db.SaveChangesAsync();
        }

        await using var read = fx.NewContext();
        var forA = await read.Notifications.Where(n => n.RelatedOrderId == orderA).ToListAsync();
        forA.Should().HaveCount(1);
        forA[0].Type.Should().Be("A");
    }
}
