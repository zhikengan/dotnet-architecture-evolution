using BuildingBlocks.Application;
using Catalog.Contracts.IntegrationEvents;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orders.Application.Abstractions;
using Orders.Contracts.IntegrationEvents;
using OrderIdValue = global::Orders.Domain.Orders.OrderId;

namespace Orders.Application.Consumers;

/// <summary>
/// Catalog confirmed stock decrement → confirm the order and publish
/// <see cref="OrderConfirmedIntegrationEvent"/> so notifications-service can
/// send the customer their confirmation.
/// </summary>
public sealed class WhenStockDecrementedConsumer(
    IOrdersDbContext db,
    IClock clock,
    ITenantContext tenant,
    ILogger<WhenStockDecrementedConsumer> logger)
    : IConsumer<StockDecrementedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<StockDecrementedIntegrationEvent> context)
    {
        var evt = context.Message;
        tenant.Set(evt.TenantId);

        var oid = new OrderIdValue(evt.OrderId);
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == oid, context.CancellationToken);
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

public sealed class WhenStockDecrementFailedConsumer(
    IOrdersDbContext db,
    IClock clock,
    ITenantContext tenant,
    ILogger<WhenStockDecrementFailedConsumer> logger)
    : IConsumer<StockDecrementFailedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<StockDecrementFailedIntegrationEvent> context)
    {
        var evt = context.Message;
        tenant.Set(evt.TenantId);

        var oid = new OrderIdValue(evt.OrderId);
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == oid, context.CancellationToken);
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
