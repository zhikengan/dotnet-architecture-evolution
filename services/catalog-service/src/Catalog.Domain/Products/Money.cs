using BuildingBlocks.Domain;
using Catalog.Domain.Products.Errors;

namespace Catalog.Domain.Products;

public sealed class Money : ValueObject
{
    public const string UsdCode = "USD";
    public decimal Amount { get; }
    public string Currency { get; }

    private Money(decimal amount, string currency) { Amount = amount; Currency = currency; }

    public static Money Usd(decimal amount) => new(amount, UsdCode);

    public static Result<Money> Create(decimal amount, string currency)
    {
        if (string.IsNullOrWhiteSpace(currency) || currency != UsdCode)
            return Result.Failure<Money>(ProductErrors.UnsupportedCurrency);
        return Result.Success(new Money(amount, currency));
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Amount;
        yield return Currency;
    }
}
