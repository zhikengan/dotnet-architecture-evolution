using Marketplace.Application.Orders.CancelOwnOrder;
using Marketplace.Application.Tests.Common;
using Marketplace.Application.Tests.Common.Builders;
using Marketplace.Domain.Orders;
using Marketplace.Domain.Orders.Errors;

namespace Marketplace.Application.Tests.Orders.CancelOwnOrder;

public class CancelOwnOrderHandlerTests : TestBase
{
    private static readonly Guid Buyer = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid OtherBuyer = new("99999999-9999-9999-9999-999999999999");

    [Fact]
    public async Task Buyer_cancels_own_confirmed_order_and_stock_is_returned()
    {
        var product = new ProductBuilder().WithStock(10).Build();
        product.Decrement(3);
        product.ClearDomainEvents();
        DbContext.Products.Add(product);

        var order = new OrderBuilder().WithBuyer(Buyer).ForProduct(product.Id).WithQuantity(3).Confirmed().Build();
        DbContext.Orders.Add(order);
        await DbContext.SaveChangesAsync();

        var handler = new CancelOwnOrderHandler(DbContext, UnitOfWork);
        var result = await handler.Handle(new CancelOwnOrderCommand(order.Id.Value, Buyer), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        DbContext.Orders.Single().Status.Should().Be(OrderStatus.Cancelled);
        DbContext.Products.Single().Stock.Value.Should().Be(10);
    }

    [Fact]
    public async Task Buyer_cancelling_another_buyers_order_returns_NotOwner()
    {
        var product = new ProductBuilder().WithStock(10).Build();
        DbContext.Products.Add(product);
        var order = new OrderBuilder().WithBuyer(Buyer).ForProduct(product.Id).Confirmed().Build();
        DbContext.Orders.Add(order);
        await DbContext.SaveChangesAsync();

        var handler = new CancelOwnOrderHandler(DbContext, UnitOfWork);
        var result = await handler.Handle(new CancelOwnOrderCommand(order.Id.Value, OtherBuyer), CancellationToken.None);

        result.Error.Should().Be(OrderErrors.NotOwner);
        DbContext.Orders.Single().Status.Should().Be(OrderStatus.Confirmed);
    }

    [Fact]
    public async Task Cancelling_missing_order_returns_NotFound()
    {
        var handler = new CancelOwnOrderHandler(DbContext, UnitOfWork);
        var result = await handler.Handle(new CancelOwnOrderCommand(Guid.NewGuid(), Buyer), CancellationToken.None);

        result.Error.Should().Be(OrderErrors.NotFound);
    }
}
