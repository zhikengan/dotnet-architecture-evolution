using Marketplace.Application.Orders.PlaceOrder;
using Marketplace.Application.Tests.Common;
using Marketplace.Application.Tests.Common.Builders;
using Marketplace.Domain.Orders;
using Marketplace.Domain.Products.Errors;

namespace Marketplace.Application.Tests.Orders.PlaceOrder;

public class PlaceOrderHandlerTests : TestBase
{
    private static readonly Guid Buyer = new("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task With_sufficient_stock_creates_confirmed_order_and_decrements()
    {
        var product = new ProductBuilder().WithStock(10).Build();
        DbContext.Products.Add(product);
        await DbContext.SaveChangesAsync();

        var handler = new PlaceOrderHandler(DbContext, Clock, UnitOfWork);
        var result = await handler.Handle(new PlaceOrderCommand(Buyer, product.Id.Value, 3), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(nameof(OrderStatus.Confirmed));

        var saved = DbContext.Orders.Single();
        saved.Status.Should().Be(OrderStatus.Confirmed);
        saved.Quantity.Value.Should().Be(3);
        DbContext.Products.Single().Stock.Value.Should().Be(7);
    }

    [Fact]
    public async Task With_insufficient_stock_persists_failed_order_and_returns_failure()
    {
        var product = new ProductBuilder().WithStock(2).Build();
        DbContext.Products.Add(product);
        await DbContext.SaveChangesAsync();

        var handler = new PlaceOrderHandler(DbContext, Clock, UnitOfWork);
        var result = await handler.Handle(new PlaceOrderCommand(Buyer, product.Id.Value, 10), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProductErrors.InsufficientStock);

        var saved = DbContext.Orders.Single();
        saved.Status.Should().Be(OrderStatus.Failed);
        DbContext.Products.Single().Stock.Value.Should().Be(2);
    }

    [Fact]
    public async Task When_product_not_found_returns_NotFound_and_persists_nothing()
    {
        var handler = new PlaceOrderHandler(DbContext, Clock, UnitOfWork);
        var result = await handler.Handle(new PlaceOrderCommand(Buyer, Guid.NewGuid(), 1), CancellationToken.None);

        result.Error.Should().Be(ProductErrors.NotFound);
        DbContext.Orders.Should().BeEmpty();
    }

    [Fact]
    public async Task When_product_is_suspended_returns_NotPublished()
    {
        var product = new ProductBuilder().Suspended().Build();
        DbContext.Products.Add(product);
        await DbContext.SaveChangesAsync();

        var handler = new PlaceOrderHandler(DbContext, Clock, UnitOfWork);
        var result = await handler.Handle(new PlaceOrderCommand(Buyer, product.Id.Value, 1), CancellationToken.None);

        result.Error.Should().Be(ProductErrors.NotPublished);
        DbContext.Orders.Should().BeEmpty();
    }
}
