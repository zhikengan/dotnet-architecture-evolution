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
/// Catalog confirmed stock decrement → confirm the order and publish
/// <see cref="OrderConfirmedIntegrationEvent"/> so notifications-service can
/// send the buyer their confirmation.
/// </summary>
public sealed class WhenStockDecremented_ConfirmOrder(
    IOrdersDbContext db,
    IClock clock,
    ITenantContext tenant,
    ILogger<WhenStockDecremented_ConfirmOrder> logger)
    : IConsumer<StockDecrementedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<StockDecrementedIntegrationEvent> context)
    {
        var evt = context.Message;
        tenant.Set(evt.TenantId);

        var oid = new OrderIdValue(evt.OrderId);
        var order = await db.Orders.IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.Id == oid, context.CancellationToken);
        if (order is null)
        {
            logger.LogWarning("StockDecremented for unknown order {OrderId}", evt.OrderId);
            return;
        }

        var result = order.Confirm();
        if (result.IsFailure) return;

        await context.Publish(new OrderConfirmedIntegrationEvent(
            MessageId: Guid.NewGuid(),
            OccurredAt: clock.UtcNow,
            TenantId: evt.TenantId,
            OrderId: order.Id.Value,
            BuyerId: order.BuyerId,
            ProductId: order.ProductId,
            Quantity: order.Quantity));

        await db.SaveChangesAsync(context.CancellationToken);
    }
}
