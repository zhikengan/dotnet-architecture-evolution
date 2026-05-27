using Marketplace.Domain.Orders;
using Marketplace.Domain.Orders.Errors;
using Marketplace.Domain.Orders.Events;
using Marketplace.Domain.Products;

namespace Marketplace.Domain.Tests.Orders;

public class OrderTests
{
    private static readonly DateTime Now = new(2026, 5, 27, 10, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Buyer = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid OtherBuyer = new("99999999-9999-9999-9999-999999999999");
    private static readonly ProductId Product = ProductId.New();

    [Fact]
    public void Create_with_valid_data_starts_in_Pending_and_raises_OrderPlaced()
    {
        var result = Order.Create(Buyer, Product, 2, Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(OrderStatus.Pending);
        result.Value.DomainEvents.Should().ContainSingle(e => e is OrderPlaced);
    }

    [Fact]
    public void Create_with_empty_buyer_fails()
    {
        var result = Order.Create(Guid.Empty, Product, 1, Now);
        result.Error.Should().Be(OrderErrors.InvalidBuyer);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_with_non_positive_quantity_fails(int qty)
    {
        var result = Order.Create(Buyer, Product, qty, Now);
        result.Error.Should().Be(OrderErrors.InvalidQuantity);
    }

    [Fact]
    public void Confirm_pending_transitions_to_Confirmed_and_raises_OrderConfirmed()
    {
        var order = Order.Create(Buyer, Product, 2, Now).Value;
        order.ClearDomainEvents();

        var result = order.Confirm();

        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Confirmed);
        order.DomainEvents.Should().ContainSingle(e => e is OrderConfirmed);
    }

    [Fact]
    public void Confirm_non_pending_fails()
    {
        var order = Order.Create(Buyer, Product, 2, Now).Value;
        order.Confirm();

        var second = order.Confirm();
        second.Error.Should().Be(OrderErrors.NotPending);
    }

    [Fact]
    public void Cancel_own_confirmed_transitions_to_Cancelled_and_raises_OrderCancelled()
    {
        var order = Order.Create(Buyer, Product, 2, Now).Value;
        order.Confirm();
        order.ClearDomainEvents();

        var result = order.Cancel(Buyer);

        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Cancelled);
        var evt = order.DomainEvents.OfType<OrderCancelled>().Should().ContainSingle().Subject;
        evt.StockWasDecremented.Should().BeTrue();
    }

    [Fact]
    public void Cancel_own_pending_succeeds_and_marks_event_as_no_stock_decrement()
    {
        var order = Order.Create(Buyer, Product, 2, Now).Value;
        order.ClearDomainEvents();

        var result = order.Cancel(Buyer);

        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Cancelled);
        var evt = order.DomainEvents.OfType<OrderCancelled>().Should().ContainSingle().Subject;
        evt.StockWasDecremented.Should().BeFalse();
    }

    [Fact]
    public void Cancel_by_other_buyer_fails_with_NotOwner()
    {
        var order = Order.Create(Buyer, Product, 2, Now).Value;
        var result = order.Cancel(OtherBuyer);
        result.Error.Should().Be(OrderErrors.NotOwner);
        order.Status.Should().Be(OrderStatus.Pending);
    }

    [Fact]
    public void Cancel_already_cancelled_fails()
    {
        var order = Order.Create(Buyer, Product, 2, Now).Value;
        order.Cancel(Buyer);

        var second = order.Cancel(Buyer);
        second.Error.Should().Be(OrderErrors.AlreadyCancelled);
    }

    [Fact]
    public void ForceCancel_confirmed_returns_stock_flag_true()
    {
        var order = Order.Create(Buyer, Product, 2, Now).Value;
        order.Confirm();
        order.ClearDomainEvents();

        var result = order.ForceCancel();

        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Cancelled);
        var evt = order.DomainEvents.OfType<OrderCancelled>().Should().ContainSingle().Subject;
        evt.StockWasDecremented.Should().BeTrue();
    }

    [Fact]
    public void ForceCancel_already_cancelled_fails()
    {
        var order = Order.Create(Buyer, Product, 2, Now).Value;
        order.Cancel(Buyer);

        var result = order.ForceCancel();
        result.Error.Should().Be(OrderErrors.AlreadyCancelled);
    }

    [Fact]
    public void Fail_pending_transitions_to_Failed_and_raises_OrderFailed()
    {
        var order = Order.Create(Buyer, Product, 2, Now).Value;
        order.ClearDomainEvents();

        order.Fail("insufficient stock");

        order.Status.Should().Be(OrderStatus.Failed);
        order.FailureReason.Should().Be("insufficient stock");
        order.DomainEvents.Should().ContainSingle(e => e is OrderFailed);
    }

    [Fact]
    public void Fail_non_pending_is_a_noop()
    {
        var order = Order.Create(Buyer, Product, 2, Now).Value;
        order.Confirm();

        order.Fail("anything");

        order.Status.Should().Be(OrderStatus.Confirmed);
    }
}
