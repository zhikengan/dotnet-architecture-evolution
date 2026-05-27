using Marketplace.Domain.Common;

namespace Marketplace.Domain.Orders.Errors;

public static class OrderErrors
{
    public static readonly Error InvalidBuyer = new("Order.InvalidBuyer", "BuyerId is required");
    public static readonly Error InvalidQuantity = new("Order.InvalidQuantity", "Quantity must be a positive integer");
    public static readonly Error NotFound = new("Order.NotFound", "Order not found");
    public static readonly Error NotOwner = new("Order.NotOwner", "Order belongs to another buyer");
    public static readonly Error NotPending = new("Order.NotPending", "Order is not in pending state");
    public static readonly Error AlreadyCancelled = new("Order.AlreadyCancelled", "Order is already cancelled");
    public static readonly Error AlreadyFailed = new("Order.AlreadyFailed", "Order has already failed");
    public static readonly Error NotCancellable = new("Order.NotCancellable", "Order cannot be cancelled in its current state");
}
