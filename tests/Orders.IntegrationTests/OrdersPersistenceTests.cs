using BuildingBlocks.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using Orders.Contracts.IntegrationEvents;
using OrderAggregate = global::Orders.Domain.Orders.Order;

namespace Orders.IntegrationTests;

[Collection(nameof(OrdersDbCollection))]
public class OrdersPersistenceTests(OrdersDbFixture fx) : IAsyncLifetime
{
    public Task InitializeAsync() => fx.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Order_round_trips_through_EF()
    {
        var buyer = Guid.NewGuid();
        var product = Guid.NewGuid();
        var order = OrderAggregate.Create(buyer, product, 3, DateTime.UtcNow).Value;
        order.ClearDomainEvents();

        await using (var db = fx.CreateContext())
        {
            db.Orders.Add(order);
            await db.SaveChangesAsync();
        }

        await using var read = fx.CreateContext();
        var loaded = await read.Orders.SingleAsync();
        loaded.BuyerId.Should().Be(buyer);
        loaded.ProductId.Should().Be(product);
        loaded.Quantity.Should().Be(3);
        loaded.Status.Should().Be(global::Orders.Domain.Orders.OrderStatus.Pending);
    }

    [Fact]
    public async Task InboxMessages_unique_per_messageId_and_consumerName()
    {
        var msgId = Guid.NewGuid();
        await using (var db = fx.CreateContext())
        {
            db.InboxMessages.Add(new BuildingBlocks.Infrastructure.Inbox.InboxMessage
            {
                MessageId = msgId, ConsumerName = "ConsumerA", ProcessedAt = DateTime.UtcNow,
            });
            // Different consumer with same MessageId is allowed (composite key)
            db.InboxMessages.Add(new BuildingBlocks.Infrastructure.Inbox.InboxMessage
            {
                MessageId = msgId, ConsumerName = "ConsumerB", ProcessedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        await using var read = fx.CreateContext();
        (await read.InboxMessages.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Outbox_Enqueue_persists_OrderPlacedIntegrationEvent()
    {
        var evt = new OrderPlacedIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 5);
        await using (var db = fx.CreateContext())
        {
            db.OutboxMessages.Enqueue(evt);
            await db.SaveChangesAsync();
        }
        await using var read = fx.CreateContext();
        var row = await read.OutboxMessages.SingleAsync();
        row.Type.Should().Contain(nameof(OrderPlacedIntegrationEvent));
        row.ProcessedAt.Should().BeNull();
    }
}
