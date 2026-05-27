using Marketplace.Application.Abstractions;
using Marketplace.Domain.Common;
using Marketplace.Domain.Orders;
using Marketplace.Domain.Orders.Errors;
using Marketplace.Domain.Orders.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Application.Orders.CancelOwnOrder;

public sealed class CancelOwnOrderHandler(IAppDbContext db, IUnitOfWork uow)
    : IRequestHandler<CancelOwnOrderCommand, Result>
{
    public async Task<Result> Handle(CancelOwnOrderCommand cmd, CancellationToken ct)
    {
        var orderId = new OrderId(cmd.OrderId);
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == orderId, ct);
        if (order is null) return Result.Failure(OrderErrors.NotFound);

        var cancel = order.Cancel(cmd.BuyerId);
        if (cancel.IsFailure) return cancel;

        await ReturnStockIfNeededAsync(db, order, ct);
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    internal static async Task ReturnStockIfNeededAsync(IAppDbContext db, Order order, CancellationToken ct)
    {
        var cancelled = order.DomainEvents.OfType<OrderCancelled>().LastOrDefault();
        if (cancelled is null || !cancelled.StockWasDecremented) return;

        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == cancelled.ProductId, ct);
        product?.Return(cancelled.Quantity);
    }
}
