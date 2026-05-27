using Marketplace.Domain.Products;
using Marketplace.Domain.Products.Errors;

namespace Marketplace.Domain.Tests.Products;

public class MoneyTests
{
    [Fact]
    public void Usd_creates_money_with_USD_currency()
    {
        var money = Money.Usd(10m);
        money.Amount.Should().Be(10m);
        money.Currency.Should().Be("USD");
    }

    [Fact]
    public void Create_with_USD_succeeds()
    {
        var result = Money.Create(15.50m, "USD");
        result.IsSuccess.Should().BeTrue();
        result.Value.Amount.Should().Be(15.50m);
    }

    [Theory]
    [InlineData("EUR")]
    [InlineData("GBP")]
    [InlineData("usd")]
    [InlineData("")]
    public void Create_with_non_USD_fails_with_UnsupportedCurrency(string currency)
    {
        var result = Money.Create(10m, currency);
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProductErrors.UnsupportedCurrency);
    }

    [Fact]
    public void Two_money_values_with_same_amount_and_currency_are_equal()
    {
        Money.Usd(10m).Should().Be(Money.Usd(10m));
    }

    [Fact]
    public void Two_money_values_with_different_amounts_are_not_equal()
    {
        Money.Usd(10m).Should().NotBe(Money.Usd(11m));
    }
}
