using Marketplace.Application.Abstractions;
using Marketplace.Domain.Common;
using Marketplace.Domain.Orders;
using Marketplace.Domain.Products;
using Marketplace.Domain.Products.Errors;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Application.Orders.PlaceOrder;

public sealed class PlaceOrderHandler(IAppDbContext db, IClock clock, IUnitOfWork uow)
    : IRequestHandler<PlaceOrderCommand, Result<PlaceOrderResult>>
{
    public async Task<Result<PlaceOrderResult>> Handle(PlaceOrderCommand cmd, CancellationToken ct)
    {
        var productId = new ProductId(cmd.ProductId);

        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == productId, ct);
        if (product is null)
            return Result.Failure<PlaceOrderResult>(ProductErrors.NotFound);

        if (product.Status != ProductStatus.Published)
            return Result.Failure<PlaceOrderResult>(ProductErrors.NotPublished);

        var orderResult = Order.Create(cmd.BuyerId, productId, cmd.Quantity, clock.UtcNow);
        if (orderResult.IsFailure)
            return Result.Failure<PlaceOrderResult>(orderResult.Error);
        var order = orderResult.Value;

        var decrementResult = product.Decrement(cmd.Quantity);
        if (decrementResult.IsFailure)
        {
            order.Fail(decrementResult.Error.Message);
            db.Orders.Add(order);
            await uow.SaveChangesAsync(ct);
            return Result.Failure<PlaceOrderResult>(decrementResult.Error);
        }

        order.Confirm();
        db.Orders.Add(order);
        await uow.SaveChangesAsync(ct);
        return Result.Success(new PlaceOrderResult(order.Id.Value, order.Status.ToString()));
    }
}
