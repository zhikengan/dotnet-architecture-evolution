using BuildingBlocks.Domain;
using Orders.Domain.Orders.Errors;
using Orders.Domain.Orders.Events;

namespace Orders.Domain.Orders;

public sealed class Order : AggregateRoot<OrderId>
{
    public Guid BuyerId { get; private set; }
    public Guid ProductId { get; private set; }
    public int Quantity { get; private set; }
    public OrderStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public string? FailureReason { get; private set; }

    private Order() { }

    public static Result<Order> Create(Guid buyerId, Guid productId, int quantity, DateTime now)
    {
        if (buyerId == Guid.Empty) return Result.Failure<Order>(OrderErrors.InvalidBuyer);
        if (productId == Guid.Empty) return Result.Failure<Order>(OrderErrors.InvalidProduct);
        if (quantity < 1) return Result.Failure<Order>(OrderErrors.InvalidQuantity);

        var order = new Order
        {
            Id = OrderId.New(),
            BuyerId = buyerId,
            ProductId = productId,
            Quantity = quantity,
            Status = OrderStatus.Pending,
            CreatedAt = now,
        };
        order.RaiseDomainEvent(new OrderPlaced(order.Id, buyerId, productId, quantity));
        return Result.Success(order);
    }

    public Result Confirm()
    {
        if (Status != OrderStatus.Pending) return Result.Failure(OrderErrors.NotPending);
        Status = OrderStatus.Confirmed;
        RaiseDomainEvent(new OrderConfirmed(Id));
        return Result.Success();
    }

    public Result Cancel(Guid byBuyerId)
    {
        if (byBuyerId != BuyerId) return Result.Failure(OrderErrors.NotOwner);
        if (Status == OrderStatus.Cancelled) return Result.Failure(OrderErrors.AlreadyCancelled);
        if (Status == OrderStatus.Failed) return Result.Failure(OrderErrors.NotCancellable);

        var wasConfirmed = Status == OrderStatus.Confirmed;
        Status = OrderStatus.Cancelled;
        RaiseDomainEvent(new OrderCancelled(Id, ProductId, Quantity, wasConfirmed));
        return Result.Success();
    }

    public Result ForceCancel()
    {
        if (Status == OrderStatus.Cancelled) return Result.Failure(OrderErrors.AlreadyCancelled);
        if (Status == OrderStatus.Failed) return Result.Failure(OrderErrors.NotCancellable);

        var wasConfirmed = Status == OrderStatus.Confirmed;
        Status = OrderStatus.Cancelled;
        RaiseDomainEvent(new OrderCancelled(Id, ProductId, Quantity, wasConfirmed));
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
