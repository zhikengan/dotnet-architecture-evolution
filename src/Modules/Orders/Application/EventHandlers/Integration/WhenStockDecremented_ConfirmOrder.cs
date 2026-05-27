using BuildingBlocks.Infrastructure.EventBus;
using Catalog.Contracts.IntegrationEvents;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orders.Application.Abstractions;
using Orders.Infrastructure.Persistence;
using OrderIdValue = global::Orders.Domain.Orders.OrderId;

namespace Orders.Application.EventHandlers.Integration;

public sealed class WhenStockDecremented_ConfirmOrder(
    IOrdersDbContext db,
    OrdersInboxStore inbox,
    ILogger<WhenStockDecremented_ConfirmOrder> logger) : IIntegrationEventHandler<StockDecrementedIntegrationEvent>
{
    private const string ConsumerName = nameof(WhenStockDecremented_ConfirmOrder);

    public async Task HandleAsync(StockDecrementedIntegrationEvent evt, CancellationToken ct)
    {
        if (await inbox.HasProcessedAsync(evt.MessageId, ConsumerName, ct)) return;

        var oid = new OrderIdValue(evt.OrderId);
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == oid, ct);
        if (order is null)
        {
            logger.LogWarning("StockDecremented for unknown order {OrderId}", evt.OrderId);
            inbox.MarkProcessed(evt.MessageId, ConsumerName);
            await db.SaveChangesAsync(ct);
            return;
        }

        order.Confirm();
        inbox.MarkProcessed(evt.MessageId, ConsumerName);
        await db.SaveChangesAsync(ct);
    }
}

public sealed class WhenStockDecrementFailed_FailOrder(
    IOrdersDbContext db,
    OrdersInboxStore inbox,
    ILogger<WhenStockDecrementFailed_FailOrder> logger) : IIntegrationEventHandler<StockDecrementFailedIntegrationEvent>
{
    private const string ConsumerName = nameof(WhenStockDecrementFailed_FailOrder);

    public async Task HandleAsync(StockDecrementFailedIntegrationEvent evt, CancellationToken ct)
    {
        if (await inbox.HasProcessedAsync(evt.MessageId, ConsumerName, ct)) return;

        var oid = new OrderIdValue(evt.OrderId);
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == oid, ct);
        if (order is null)
        {
            logger.LogWarning("StockDecrementFailed for unknown order {OrderId}", evt.OrderId);
            inbox.MarkProcessed(evt.MessageId, ConsumerName);
            await db.SaveChangesAsync(ct);
            return;
        }

        order.Fail(evt.Reason);
        inbox.MarkProcessed(evt.MessageId, ConsumerName);
        await db.SaveChangesAsync(ct);
    }
}
