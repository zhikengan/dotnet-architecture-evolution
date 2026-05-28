using Orders.Domain.Orders;
using Orders.Domain.Orders.Errors;
using Orders.Domain.Orders.Events;

namespace Orders.UnitTests;

public class OrderTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Buyer = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid OtherBuyer = new("99999999-9999-9999-9999-999999999999");
    private static readonly Guid ProductId = Guid.NewGuid();
    private static readonly Guid Tenant = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public void Create_starts_Pending_and_raises_OrderPlaced()
    {
        var r = Order.Create(Buyer, ProductId, 3, Tenant, Now);
        r.IsSuccess.Should().BeTrue();
        r.Value.Status.Should().Be(OrderStatus.Pending);
        r.Value.DomainEvents.Should().ContainSingle(e => e is OrderPlaced);
    }

    [Fact]
    public void Create_with_zero_quantity_fails() =>
        Order.Create(Buyer, ProductId, 0, Tenant, Now).Error.Should().Be(OrderErrors.InvalidQuantity);

    [Fact]
    public void Create_with_empty_buyer_fails() =>
        Order.Create(Guid.Empty, ProductId, 1, Tenant, Now).Error.Should().Be(OrderErrors.InvalidBuyer);

    [Fact]
    public void Create_with_empty_tenant_fails() =>
        Order.Create(Buyer, ProductId, 1, Guid.Empty, Now).Error.Should().Be(OrderErrors.InvalidTenant);

    [Fact]
    public void Confirm_pending_transitions_to_Confirmed()
    {
        var o = Order.Create(Buyer, ProductId, 1, Tenant, Now).Value;
        o.ClearDomainEvents();
        o.Confirm().IsSuccess.Should().BeTrue();
        o.Status.Should().Be(OrderStatus.Confirmed);
        o.DomainEvents.Should().ContainSingle(e => e is OrderConfirmed);
    }

    [Fact]
    public void Confirm_non_pending_fails()
    {
        var o = Order.Create(Buyer, ProductId, 1, Tenant, Now).Value;
        o.Confirm();
        o.Confirm().Error.Should().Be(OrderErrors.NotPending);
    }

    [Fact]
    public void Cancel_own_confirmed_raises_OrderCancelled_with_StockWasDecremented_true()
    {
        var o = Order.Create(Buyer, ProductId, 1, Tenant, Now).Value;
        o.Confirm();
        o.ClearDomainEvents();
        o.Cancel(Buyer).IsSuccess.Should().BeTrue();
        var evt = o.DomainEvents.OfType<OrderCancelled>().Should().ContainSingle().Subject;
        evt.StockWasDecremented.Should().BeTrue();
    }

    [Fact]
    public void Cancel_own_pending_raises_OrderCancelled_with_StockWasDecremented_false()
    {
        var o = Order.Create(Buyer, ProductId, 1, Tenant, Now).Value;
        o.ClearDomainEvents();
        o.Cancel(Buyer).IsSuccess.Should().BeTrue();
        var evt = o.DomainEvents.OfType<OrderCancelled>().Should().ContainSingle().Subject;
        evt.StockWasDecremented.Should().BeFalse();
    }

    [Fact]
    public void Cancel_by_other_buyer_fails_NotOwner()
    {
        var o = Order.Create(Buyer, ProductId, 1, Tenant, Now).Value;
        o.Cancel(OtherBuyer).Error.Should().Be(OrderErrors.NotOwner);
    }

    [Fact]
    public void ForceCancel_confirmed_succeeds_and_reports_stock_was_decremented()
    {
        var o = Order.Create(Buyer, ProductId, 1, Tenant, Now).Value;
        o.Confirm();
        o.ClearDomainEvents();
        o.ForceCancel().IsSuccess.Should().BeTrue();
        var evt = o.DomainEvents.OfType<OrderCancelled>().Should().ContainSingle().Subject;
        evt.StockWasDecremented.Should().BeTrue();
    }

    [Fact]
    public void Fail_pending_transitions_to_Failed_with_reason()
    {
        var o = Order.Create(Buyer, ProductId, 1, Tenant, Now).Value;
        o.ClearDomainEvents();
        o.Fail("insufficient stock");
        o.Status.Should().Be(OrderStatus.Failed);
        o.FailureReason.Should().Be("insufficient stock");
        o.DomainEvents.Should().ContainSingle(e => e is OrderFailed);
    }
}
