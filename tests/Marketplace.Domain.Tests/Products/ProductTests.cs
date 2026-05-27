using Marketplace.Domain.Products;
using Marketplace.Domain.Products.Errors;
using Marketplace.Domain.Products.Events;

namespace Marketplace.Domain.Tests.Products;

public class ProductTests
{
    private static readonly DateTime Now = new(2026, 5, 27, 10, 0, 0, DateTimeKind.Utc);
    private static readonly Guid SellerId = new("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void Create_with_valid_data_succeeds_and_raises_ProductCreated()
    {
        var result = Product.Create("Widget", Money.Usd(10m), 50, SellerId, Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(ProductStatus.Published);
        result.Value.Stock.Value.Should().Be(50);
        result.Value.DomainEvents.Should().ContainSingle(e => e is ProductCreated);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_with_empty_or_whitespace_name_fails(string name)
    {
        var result = Product.Create(name, Money.Usd(10m), 50, SellerId, Now);
        result.Error.Should().Be(ProductErrors.InvalidName);
    }

    [Fact]
    public void Create_with_too_long_name_fails()
    {
        var result = Product.Create(new string('x', 201), Money.Usd(10m), 50, SellerId, Now);
        result.Error.Should().Be(ProductErrors.InvalidName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Create_with_non_positive_price_fails(decimal price)
    {
        var result = Product.Create("Widget", Money.Usd(price), 50, SellerId, Now);
        result.Error.Should().Be(ProductErrors.InvalidPrice);
    }

    [Fact]
    public void Create_with_negative_stock_fails()
    {
        var result = Product.Create("Widget", Money.Usd(10m), -1, SellerId, Now);
        result.Error.Should().Be(ProductErrors.NegativeStock);
    }

    [Fact]
    public void Decrement_succeeds_and_raises_StockDecremented()
    {
        var product = Product.Create("Widget", Money.Usd(10m), 10, SellerId, Now).Value;
        product.ClearDomainEvents();

        var result = product.Decrement(3);

        result.IsSuccess.Should().BeTrue();
        product.Stock.Value.Should().Be(7);
        product.DomainEvents.Should().ContainSingle(e => e is StockDecremented);
    }

    [Fact]
    public void Decrement_below_zero_fails_and_does_not_change_stock_or_raise_event()
    {
        var product = Product.Create("Widget", Money.Usd(10m), 5, SellerId, Now).Value;
        product.ClearDomainEvents();

        var result = product.Decrement(10);

        result.IsFailure.Should().BeTrue();
        product.Stock.Value.Should().Be(5);
        product.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Decrement_on_suspended_product_fails()
    {
        var product = Product.Create("Widget", Money.Usd(10m), 5, SellerId, Now).Value;
        product.Suspend();
        product.ClearDomainEvents();

        var result = product.Decrement(1);

        result.Error.Should().Be(ProductErrors.NotPublished);
        product.Stock.Value.Should().Be(5);
        product.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Return_increases_stock_and_raises_StockReturned()
    {
        var product = Product.Create("Widget", Money.Usd(10m), 5, SellerId, Now).Value;
        product.ClearDomainEvents();

        var result = product.Return(3);

        result.IsSuccess.Should().BeTrue();
        product.Stock.Value.Should().Be(8);
        product.DomainEvents.Should().ContainSingle(e => e is StockReturned);
    }
}
