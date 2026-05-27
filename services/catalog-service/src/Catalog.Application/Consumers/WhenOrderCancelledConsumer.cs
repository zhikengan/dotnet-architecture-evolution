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
/// Returns stock when an order is cancelled — but only if stock was
/// previously decremented (i.e. the order had reached Confirmed). The
/// integration event carries this flag from orders-service.
/// </summary>
public sealed class WhenOrderCancelledConsumer(
    ICatalogDbContext db,
    IClock clock,
    ITenantContext tenant,
    ILogger<WhenOrderCancelledConsumer> logger)
    : IConsumer<OrderCancelledIntegrationEvent>
{
    public async Task Consume(ConsumeContext<OrderCancelledIntegrationEvent> context)
    {
        var evt = context.Message;
        if (!evt.StockWasDecremented) return;

        tenant.Set(evt.TenantId);
        var productId = new ProductId(evt.ProductId);
        var product = await db.Products.IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == productId, context.CancellationToken);
        if (product is null)
        {
            logger.LogWarning("OrderCancelled for unknown product {ProductId}", evt.ProductId);
            return;
        }

        var result = product.Return(evt.Quantity, evt.OrderId);
        if (result.IsFailure)
        {
            logger.LogWarning("Return failed for product {ProductId}: {Error}", evt.ProductId, result.Error);
            return;
        }

        await context.Publish(new StockReturnedIntegrationEvent(
            MessageId: Guid.NewGuid(),
            OccurredAt: clock.UtcNow,
            TenantId: evt.TenantId,
            OrderId: evt.OrderId,
            ProductId: evt.ProductId,
            Quantity: evt.Quantity,
            CurrentStock: product.Stock.Value));

        await db.SaveChangesAsync(context.CancellationToken);
    }
}
