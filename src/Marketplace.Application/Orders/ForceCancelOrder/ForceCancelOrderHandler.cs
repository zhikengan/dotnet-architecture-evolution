using Marketplace.Application.Abstractions;
using Marketplace.Application.Orders.CancelOwnOrder;
using Marketplace.Domain.Common;
using Marketplace.Domain.Orders;
using Marketplace.Domain.Orders.Errors;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Application.Orders.ForceCancelOrder;

public sealed class ForceCancelOrderHandler(IAppDbContext db, IUnitOfWork uow)
    : IRequestHandler<ForceCancelOrderCommand, Result>
{
    public async Task<Result> Handle(ForceCancelOrderCommand cmd, CancellationToken ct)
    {
        var orderId = new OrderId(cmd.OrderId);
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == orderId, ct);
        if (order is null) return Result.Failure(OrderErrors.NotFound);

        var cancel = order.ForceCancel();
        if (cancel.IsFailure) return cancel;

        await CancelOwnOrderHandler.ReturnStockIfNeededAsync(db, order, ct);
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
