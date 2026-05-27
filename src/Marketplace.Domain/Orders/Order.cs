using Marketplace.Domain.Common;
using Marketplace.Domain.Orders.Errors;
using Marketplace.Domain.Orders.Events;
using Marketplace.Domain.Products;

namespace Marketplace.Domain.Orders;

public sealed class Order : AggregateRoot<OrderId>
{
    public Guid BuyerId { get; private set; }
    public ProductId ProductId { get; private set; }
    public Quantity Quantity { get; private set; }
    public OrderStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public string? FailureReason { get; private set; }

    private Order() { }

    public static Result<Order> Create(Guid buyerId, ProductId productId, int quantity, DateTime now)
    {
        if (buyerId == Guid.Empty)
            return Result.Failure<Order>(OrderErrors.InvalidBuyer);

        var qty = Quantity.Create(quantity);
        if (qty.IsFailure) return Result.Failure<Order>(qty.Error);

        var order = new Order
        {
            Id = OrderId.New(),
            BuyerId = buyerId,
            ProductId = productId,
            Quantity = qty.Value,
            Status = OrderStatus.Pending,
            CreatedAt = now,
        };
        order.RaiseDomainEvent(new OrderPlaced(order.Id, buyerId, productId, qty.Value.Value));
        return Result.Success(order);
    }

    public Result Confirm()
    {
        if (Status != OrderStatus.Pending)
            return Result.Failure(OrderErrors.NotPending);

        Status = OrderStatus.Confirmed;
        RaiseDomainEvent(new OrderConfirmed(Id));
        return Result.Success();
    }

    public Result Cancel(Guid byBuyerId)
    {
        if (byBuyerId != BuyerId)
            return Result.Failure(OrderErrors.NotOwner);
        if (Status is OrderStatus.Cancelled)
            return Result.Failure(OrderErrors.AlreadyCancelled);
        if (Status is OrderStatus.Failed)
            return Result.Failure(OrderErrors.NotCancellable);

        var wasConfirmed = Status == OrderStatus.Confirmed;
        Status = OrderStatus.Cancelled;
        RaiseDomainEvent(new OrderCancelled(Id, ProductId, Quantity.Value, wasConfirmed));
        return Result.Success();
    }

    public Result ForceCancel()
    {
        if (Status is OrderStatus.Cancelled)
            return Result.Failure(OrderErrors.AlreadyCancelled);
        if (Status is OrderStatus.Failed)
            return Result.Failure(OrderErrors.NotCancellable);

        var wasConfirmed = Status == OrderStatus.Confirmed;
        Status = OrderStatus.Cancelled;
        RaiseDomainEvent(new OrderCancelled(Id, ProductId, Quantity.Value, wasConfirmed));
        return Result.Success();
    }

    public void Fail(string reason)
    {
        if (Status != OrderStatus.Pending) return;

        Status = OrderStatus.Failed;
        FailureReason = reason;
        RaiseDomainEvent(new OrderFailed(Id, reason));
    }
}
