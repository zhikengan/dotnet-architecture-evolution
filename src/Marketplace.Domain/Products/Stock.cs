using Marketplace.Domain.Common;
using Marketplace.Domain.Products.Errors;

namespace Marketplace.Domain.Products;

public readonly struct Stock : IEquatable<Stock>
{
    public int Value { get; }

    private Stock(int value) => Value = value;

    public static Stock Zero { get; } = new(0);

    public static Result<Stock> Create(int value) =>
        value < 0
            ? Result.Failure<Stock>(ProductErrors.NegativeStock)
            : Result.Success(new Stock(value));

    public Result<Stock> Decrement(int quantity)
    {
        if (quantity <= 0)
            return Result.Failure<Stock>(ProductErrors.InvalidDecrement);
        if (quantity > Value)
            return Result.Failure<Stock>(ProductErrors.InsufficientStock);
        return Result.Success(new Stock(Value - quantity));
    }

    public Result<Stock> Return(int quantity) =>
        quantity <= 0
            ? Result.Failure<Stock>(ProductErrors.InvalidReturn)
            : Result.Success(new Stock(Value + quantity));

    public bool Equals(Stock other) => Value == other.Value;
    public override bool Equals(object? obj) => obj is Stock s && Equals(s);
    public override int GetHashCode() => Value;
    public override string ToString() => Value.ToString();

    public static bool operator ==(Stock left, Stock right) => left.Equals(right);
    public static bool operator !=(Stock left, Stock right) => !left.Equals(right);
}
