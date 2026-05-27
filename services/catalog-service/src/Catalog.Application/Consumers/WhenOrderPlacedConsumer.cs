using BuildingBlocks.Application;
using Catalog.Application.Abstractions;
using Catalog.Contracts.IntegrationEvents;
using Catalog.Domain.Products;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orders.Contracts.IntegrationEvents;

namespace Catalog.Application.Consumers;

/// <summary>
/// Catalog side of the PlaceOrder saga. Tries to decrement stock for the
/// ordered product; emits <see cref="StockDecrementedIntegrationEvent"/> on
/// success or <see cref="StockDecrementFailedIntegrationEvent"/> otherwise.
/// MassTransit's inbox handles dedup; the EF outbox makes the publish
/// transactional with the SaveChanges below.
/// </summary>
public sealed class WhenOrderPlacedConsumer(
    ICatalogDbContext db,
    IClock clock,
    ITenantContext tenant,
    ILogger<WhenOrderPlacedConsumer> logger)
    : IConsumer<OrderPlacedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<OrderPlacedIntegrationEvent> context)
    {
        var evt = context.Message;
        tenant.Set(evt.TenantId);

        var productId = new ProductId(evt.ProductId);
        var product = await db.Products.IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == productId, context.CancellationToken);

        if (product is null)
        {
            logger.LogWarning("OrderPlaced for unknown product {ProductId} (order={OrderId})", evt.ProductId, evt.OrderId);
            await context.Publish(new StockDecrementFailedIntegrationEvent(
                MessageId: Guid.NewGuid(),
                OccurredAt: clock.UtcNow,
                TenantId: evt.TenantId,
                OrderId: evt.OrderId,
                ProductId: evt.ProductId,
                Reason: "Product not found"));
            return;
        }

        var result = product.Decrement(evt.Quantity, evt.OrderId);
        if (result.IsFailure)
        {
            await context.Publish(new StockDecrementFailedIntegrationEvent(
                MessageId: Guid.NewGuid(),
                OccurredAt: clock.UtcNow,
                TenantId: evt.TenantId,
                OrderId: evt.OrderId,
                ProductId: evt.ProductId,
                Reason: result.Error.Message));
            // Do NOT save the stale aggregate state — discard.
            return;
        }

        await context.Publish(new StockDecrementedIntegrationEvent(
            MessageId: Guid.NewGuid(),
            OccurredAt: clock.UtcNow,
            TenantId: evt.TenantId,
            OrderId: evt.OrderId,
            ProductId: evt.ProductId,
            Quantity: evt.Quantity,
            RemainingStock: product.Stock.Value));

        await db.SaveChangesAsync(context.CancellationToken);
    }
}
