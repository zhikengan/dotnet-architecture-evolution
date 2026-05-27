using BuildingBlocks.Infrastructure.EventBus;
using Catalog.Application.Abstractions;
using Catalog.Domain.Products;
using Catalog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orders.Contracts.IntegrationEvents;

namespace Catalog.Application.EventHandlers.Integration;

public sealed class WhenOrderCancelled_ReturnStock(
    ICatalogDbContext db,
    CatalogInboxStore inbox,
    ILogger<WhenOrderCancelled_ReturnStock> logger) : IIntegrationEventHandler<OrderCancelledIntegrationEvent>
{
    private const string ConsumerName = nameof(WhenOrderCancelled_ReturnStock);

    public async Task HandleAsync(OrderCancelledIntegrationEvent evt, CancellationToken ct)
    {
        if (await inbox.HasProcessedAsync(evt.MessageId, ConsumerName, ct))
            return;

        if (!evt.StockWasDecremented)
        {
            inbox.MarkProcessed(evt.MessageId, ConsumerName);
            await db.SaveChangesAsync(ct);
            return;
        }

        var productId = new ProductId(evt.ProductId);
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == productId, ct);
        if (product is null)
        {
            logger.LogWarning("OrderCancelled for unknown product {ProductId} (order={OrderId})", evt.ProductId, evt.OrderId);
            inbox.MarkProcessed(evt.MessageId, ConsumerName);
            await db.SaveChangesAsync(ct);
            return;
        }

        product.Return(evt.Quantity, evt.OrderId);
        inbox.MarkProcessed(evt.MessageId, ConsumerName);
        await db.SaveChangesAsync(ct);
    }
}
