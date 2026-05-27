using Orders.Domain.Orders;
using Orders.Domain.Orders.Errors;
using Orders.Domain.Orders.Events;

namespace Orders.UnitTests;

public class OrderTests
{
    private static readonly DateTime Now = new(2026, 5, 27, 10, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Buyer = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid OtherBuyer = new("99999999-9999-9999-9999-999999999999");
    private static readonly Guid Product = Guid.NewGuid();
    private static readonly Guid Tenant = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public void Create_starts_in_Pending_and_raises_OrderPlaced()
    {
        var r = Order.Create(Buyer, Product, 3, Tenant, Now);
        r.IsSuccess.Should().BeTrue();
        r.Value.Status.Should().Be(OrderStatus.Pending);
        r.Value.DomainEvents.Should().ContainSingle(e => e is OrderPlaced);
    }

    [Fact]
    public void Create_with_empty_buyer_fails() =>
        Order.Create(Guid.Empty, Product, 1, Tenant, Now).Error.Should().Be(OrderErrors.InvalidBuyer);

    [Fact]
    public void Create_with_empty_product_fails() =>
        Order.Create(Buyer, Guid.Empty, 1, Tenant, Now).Error.Should().Be(OrderErrors.InvalidProduct);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_with_non_positive_quantity_fails(int q) =>
        Order.Create(Buyer, Product, q, Tenant, Now).Error.Should().Be(OrderErrors.InvalidQuantity);

    [Fact]
    public void Confirm_pending_transitions_to_Confirmed()
    {
        var o = Order.Create(Buyer, Product, 1, Tenant, Now).Value;
        o.ClearDomainEvents();
        o.Confirm().IsSuccess.Should().BeTrue();
        o.Status.Should().Be(OrderStatus.Confirmed);
        o.DomainEvents.Should().ContainSingle(e => e is OrderConfirmed);
    }

    [Fact]
    public void Confirm_already_confirmed_fails()
    {
        var o = Order.Create(Buyer, Product, 1, Tenant, Now).Value;
        o.Confirm();
        o.Confirm().Error.Should().Be(OrderErrors.NotPending);
    }

    [Fact]
    public void Cancel_own_confirmed_returns_with_StockWasDecremented_true()
    {
        var o = Order.Create(Buyer, Product, 1, Tenant, Now).Value;
        o.Confirm();
        o.ClearDomainEvents();
        o.Cancel(Buyer).IsSuccess.Should().BeTrue();
        o.Status.Should().Be(OrderStatus.Cancelled);
        var evt = o.DomainEvents.OfType<OrderCancelled>().Should().ContainSingle().Subject;
        evt.StockWasDecremented.Should().BeTrue();
    }

    [Fact]
    public void Cancel_own_pending_returns_with_StockWasDecremented_false()
    {
        var o = Order.Create(Buyer, Product, 1, Tenant, Now).Value;
        o.ClearDomainEvents();
        o.Cancel(Buyer).IsSuccess.Should().BeTrue();
        var evt = o.DomainEvents.OfType<OrderCancelled>().Should().ContainSingle().Subject;
        evt.StockWasDecremented.Should().BeFalse();
    }

    [Fact]
    public void Cancel_by_other_buyer_fails_with_NotOwner()
    {
        var o = Order.Create(Buyer, Product, 1, Tenant, Now).Value;
        o.Cancel(OtherBuyer).Error.Should().Be(OrderErrors.NotOwner);
        o.Status.Should().Be(OrderStatus.Pending);
    }

    [Fact]
    public void Cancel_already_cancelled_fails()
    {
        var o = Order.Create(Buyer, Product, 1, Tenant, Now).Value;
        o.Cancel(Buyer);
        o.Cancel(Buyer).Error.Should().Be(OrderErrors.AlreadyCancelled);
    }

    [Fact]
    public void ForceCancel_confirmed_succeeds()
    {
        var o = Order.Create(Buyer, Product, 1, Tenant, Now).Value;
        o.Confirm();
        o.ClearDomainEvents();
        o.ForceCancel().IsSuccess.Should().BeTrue();
        o.Status.Should().Be(OrderStatus.Cancelled);
    }

    [Fact]
    public void Fail_pending_transitions_to_Failed_and_records_reason()
    {
        var o = Order.Create(Buyer, Product, 1, Tenant, Now).Value;
        o.ClearDomainEvents();
        o.Fail("no stock");
        o.Status.Should().Be(OrderStatus.Failed);
        o.FailureReason.Should().Be("no stock");
        o.DomainEvents.Should().ContainSingle(e => e is OrderFailed);
    }

    [Fact]
    public void Fail_non_pending_is_a_noop()
    {
        var o = Order.Create(Buyer, Product, 1, Tenant, Now).Value;
        o.Confirm();
        o.Fail("ignored");
        o.Status.Should().Be(OrderStatus.Confirmed);
    }
}
