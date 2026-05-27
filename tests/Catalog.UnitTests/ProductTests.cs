using Catalog.Domain.Products;
using Catalog.Domain.Products.Errors;
using Catalog.Domain.Products.Events;

namespace Catalog.UnitTests;

public class ProductTests
{
    private static readonly DateTime Now = new(2026, 5, 27, 10, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Seller = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Tenant = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public void Create_with_valid_data_raises_ProductCreated()
    {
        var r = Product.Create("Widget", Money.Usd(10m), 50, Seller, Tenant, Now);
        r.IsSuccess.Should().BeTrue();
        r.Value.Status.Should().Be(ProductStatus.Published);
        r.Value.Stock.Value.Should().Be(50);
        r.Value.DomainEvents.Should().ContainSingle(e => e is ProductCreated);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_with_empty_name_fails(string name)
    {
        Product.Create(name, Money.Usd(10m), 50, Seller, Tenant, Now)
            .Error.Should().Be(ProductErrors.InvalidName);
    }

    [Fact]
    public void Create_with_too_long_name_fails()
    {
        Product.Create(new string('x', 201), Money.Usd(10m), 50, Seller, Tenant, Now)
            .Error.Should().Be(ProductErrors.InvalidName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_with_non_positive_price_fails(decimal price)
    {
        Product.Create("Widget", Money.Usd(price), 50, Seller, Tenant, Now)
            .Error.Should().Be(ProductErrors.InvalidPrice);
    }

    [Fact]
    public void Create_with_negative_stock_fails()
    {
        Product.Create("Widget", Money.Usd(10m), -1, Seller, Tenant, Now)
            .Error.Should().Be(ProductErrors.NegativeStock);
    }

    [Fact]
    public void Decrement_raises_StockDecremented_on_success()
    {
        var p = Product.Create("Widget", Money.Usd(10m), 10, Seller, Tenant, Now).Value;
        p.ClearDomainEvents();
        p.Decrement(3, Guid.NewGuid()).IsSuccess.Should().BeTrue();
        p.Stock.Value.Should().Be(7);
        p.DomainEvents.Should().ContainSingle(e => e is StockDecremented);
    }

    [Fact]
    public void Decrement_below_zero_raises_StockDecrementFailed_and_does_not_change_stock()
    {
        var p = Product.Create("Widget", Money.Usd(10m), 5, Seller, Tenant, Now).Value;
        p.ClearDomainEvents();
        var result = p.Decrement(10, Guid.NewGuid());
        result.IsFailure.Should().BeTrue();
        p.Stock.Value.Should().Be(5);
        p.DomainEvents.Should().ContainSingle(e => e is StockDecrementFailed);
    }

    [Fact]
    public void Decrement_on_suspended_product_raises_StockDecrementFailed()
    {
        var p = Product.Create("Widget", Money.Usd(10m), 5, Seller, Tenant, Now).Value;
        p.Suspend();
        p.ClearDomainEvents();
        var result = p.Decrement(1, Guid.NewGuid());
        result.Error.Should().Be(ProductErrors.NotPublished);
        p.DomainEvents.Should().ContainSingle(e => e is StockDecrementFailed);
    }

    [Fact]
    public void Return_raises_StockReturned()
    {
        var p = Product.Create("Widget", Money.Usd(10m), 5, Seller, Tenant, Now).Value;
        p.ClearDomainEvents();
        p.Return(3, Guid.NewGuid()).IsSuccess.Should().BeTrue();
        p.Stock.Value.Should().Be(8);
        p.DomainEvents.Should().ContainSingle(e => e is StockReturned);
    }
}

public class MoneyAndStockTests
{
    [Fact]
    public void Money_Usd_creates_with_USD_currency() =>
        Money.Usd(10m).Currency.Should().Be("USD");

    [Theory]
    [InlineData("EUR")]
    [InlineData("")]
    public void Money_Create_with_non_USD_fails(string currency) =>
        Money.Create(10m, currency).Error.Should().Be(ProductErrors.UnsupportedCurrency);

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(100)]
    public void Stock_Create_with_non_negative_succeeds(int v) =>
        Stock.Create(v).IsSuccess.Should().BeTrue();

    [Fact]
    public void Stock_Decrement_with_invalid_quantity_fails()
    {
        var s = Stock.Create(10).Value;
        s.Decrement(0).Error.Should().Be(ProductErrors.InvalidDecrement);
        s.Decrement(-1).Error.Should().Be(ProductErrors.InvalidDecrement);
    }
}
