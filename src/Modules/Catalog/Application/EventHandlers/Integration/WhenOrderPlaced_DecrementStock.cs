using BuildingBlocks.Infrastructure.EventBus;
using Catalog.Application.Abstractions;
using Catalog.Domain.Products;
using Catalog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orders.Contracts.IntegrationEvents;

namespace Catalog.Application.EventHandlers.Integration;

public sealed class WhenOrderPlaced_DecrementStock(
    ICatalogDbContext db,
    CatalogInboxStore inbox,
    ILogger<WhenOrderPlaced_DecrementStock> logger) : IIntegrationEventHandler<OrderPlacedIntegrationEvent>
{
    private const string ConsumerName = nameof(WhenOrderPlaced_DecrementStock);

    public async Task HandleAsync(OrderPlacedIntegrationEvent evt, CancellationToken ct)
    {
        if (await inbox.HasProcessedAsync(evt.MessageId, ConsumerName, ct))
        {
            logger.LogDebug("Skipping duplicate {Event} {MessageId}", nameof(OrderPlacedIntegrationEvent), evt.MessageId);
            return;
        }

        var productId = new ProductId(evt.ProductId);
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == productId, ct);
        if (product is null)
        {
            logger.LogWarning("OrderPlaced for unknown product {ProductId} (order={OrderId})", evt.ProductId, evt.OrderId);
            // Treat as decrement failure
            inbox.MarkProcessed(evt.MessageId, ConsumerName);
            await db.SaveChangesAsync(ct);
            return;
        }

        product.Decrement(evt.Quantity, evt.OrderId);
        inbox.MarkProcessed(evt.MessageId, ConsumerName);
        await db.SaveChangesAsync(ct);
    }
}
