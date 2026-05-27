using Marketplace.Domain.Orders;
using Marketplace.Domain.Products;

namespace Marketplace.Application.Tests.Common.Builders;

public sealed class OrderBuilder
{
    private Guid _buyerId = new("22222222-2222-2222-2222-222222222222");
    private ProductId _productId = ProductId.New();
    private int _qty = 1;
    private DateTime _now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private OrderStatus _target = OrderStatus.Pending;

    public OrderBuilder WithBuyer(Guid buyer) { _buyerId = buyer; return this; }
    public OrderBuilder ForProduct(ProductId p) { _productId = p; return this; }
    public OrderBuilder WithQuantity(int q) { _qty = q; return this; }
    public OrderBuilder At(DateTime now) { _now = now; return this; }
    public OrderBuilder Confirmed() { _target = OrderStatus.Confirmed; return this; }

    public Order Build()
    {
        var order = Order.Create(_buyerId, _productId, _qty, _now).Value;
        if (_target == OrderStatus.Confirmed) order.Confirm();
        order.ClearDomainEvents();
        return order;
    }
}
