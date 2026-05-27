using BuildingBlocks.Application;
using BuildingBlocks.Domain;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Orders.Application.Abstractions;
using Orders.Contracts.IntegrationEvents;
using Orders.Domain.Orders;
using Orders.Domain.Orders.Errors;
using OrderIdValue = global::Orders.Domain.Orders.OrderId;

namespace Orders.Application.Orders;

public sealed record CancelOwnOrderCommand(Guid OrderId, Guid BuyerId);
public sealed record ForceCancelOrderCommand(Guid OrderId);

public sealed class CancelOwnOrderHandler(IOrdersDbContext db, IClock clock, ITenantContext tenant, IPublishEndpoint bus)
{
    public async Task<Result> HandleAsync(CancelOwnOrderCommand cmd, CancellationToken ct)
    {
        var oid = new OrderIdValue(cmd.OrderId);
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == oid, ct);
        if (order is null) return Result.Failure(OrderErrors.NotFound);

        var result = order.Cancel(cmd.BuyerId);
        if (result.IsFailure) return result;

        await PublishCancelledAsync(bus, order, clock, tenant.TenantId, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    internal static Task PublishCancelledAsync(IPublishEndpoint bus, Order order, IClock clock, Guid tenantId, CancellationToken ct)
    {
        var stockWasDecremented = order.DomainEvents.OfType<Domain.Orders.Events.OrderCancelled>().LastOrDefault()?.StockWasDecremented ?? false;
        return bus.Publish(new OrderCancelledIntegrationEvent(
            MessageId: Guid.NewGuid(),
            OccurredAt: clock.UtcNow,
            TenantId: tenantId,
            OrderId: order.Id.Value,
            ProductId: order.ProductId,
            Quantity: order.Quantity,
            StockWasDecremented: stockWasDecremented), ct);
    }
}

public sealed class ForceCancelOrderHandler(IOrdersDbContext db, IClock clock, ITenantContext tenant, IPublishEndpoint bus)
{
    public async Task<Result> HandleAsync(ForceCancelOrderCommand cmd, CancellationToken ct)
    {
        var oid = new OrderIdValue(cmd.OrderId);
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == oid, ct);
        if (order is null) return Result.Failure(OrderErrors.NotFound);

        var result = order.ForceCancel();
        if (result.IsFailure) return result;

        await CancelOwnOrderHandler.PublishCancelledAsync(bus, order, clock, tenant.TenantId, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
