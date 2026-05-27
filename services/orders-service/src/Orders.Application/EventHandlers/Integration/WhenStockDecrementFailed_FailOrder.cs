using BuildingBlocks.Application;
using Catalog.Contracts.IntegrationEvents;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orders.Application.Abstractions;
using Orders.Contracts.IntegrationEvents;
using OrderIdValue = global::Orders.Domain.Orders.OrderId;

namespace Orders.Application.EventHandlers.Integration;

/// <summary>
/// Catalog reported a stock decrement failure → fail the order and emit
/// <see cref="OrderFailedIntegrationEvent"/> so notifications-service can
/// inform the buyer.
/// </summary>
public sealed class WhenStockDecrementFailed_FailOrder(
    IOrdersDbContext db,
    IClock clock,
    ITenantContext tenant,
    ILogger<WhenStockDecrementFailed_FailOrder> logger)
    : IConsumer<StockDecrementFailedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<StockDecrementFailedIntegrationEvent> context)
    {
        var evt = context.Message;
        tenant.Set(evt.TenantId);

        var oid = new OrderIdValue(evt.OrderId);
        var order = await db.Orders.IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.Id == oid, context.CancellationToken);
        if (order is null)
        {
            logger.LogWarning("StockDecrementFailed for unknown order {OrderId}", evt.OrderId);
            return;
        }

        order.Fail(evt.Reason);

        await context.Publish(new OrderFailedIntegrationEvent(
            MessageId: Guid.NewGuid(),
            OccurredAt: clock.UtcNow,
            TenantId: evt.TenantId,
            OrderId: order.Id.Value,
            BuyerId: order.BuyerId,
            Reason: evt.Reason));

        await db.SaveChangesAsync(context.CancellationToken);
    }
}
