using Marketplace.Domain.Products;
using Marketplace.Domain.Products.Errors;

namespace Marketplace.Domain.Tests.Products;

public class StockTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(100)]
    public void Create_with_non_negative_succeeds(int value)
    {
        var result = Stock.Create(value);
        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be(value);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Create_with_negative_fails(int value)
    {
        var result = Stock.Create(value);
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProductErrors.NegativeStock);
    }

    [Fact]
    public void Decrement_with_valid_quantity_returns_lower_stock()
    {
        var stock = Stock.Create(10).Value;
        var result = stock.Decrement(3);
        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be(7);
    }

    [Fact]
    public void Decrement_below_zero_fails_with_InsufficientStock()
    {
        var stock = Stock.Create(5).Value;
        var result = stock.Decrement(10);
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProductErrors.InsufficientStock);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Decrement_with_non_positive_quantity_fails(int quantity)
    {
        var stock = Stock.Create(10).Value;
        var result = stock.Decrement(quantity);
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProductErrors.InvalidDecrement);
    }

    [Fact]
    public void Return_increases_stock()
    {
        var stock = Stock.Create(5).Value;
        var result = stock.Return(3);
        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be(8);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Return_with_non_positive_quantity_fails(int quantity)
    {
        var stock = Stock.Create(5).Value;
        var result = stock.Return(quantity);
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProductErrors.InvalidReturn);
    }
}
