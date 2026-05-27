using BuildingBlocks.Domain;
using Catalog.Domain.Products.Errors;

namespace Catalog.Domain.Products;

public readonly struct Stock : IEquatable<Stock>
{
    public int Value { get; }
    private Stock(int value) => Value = value;

    public static Result<Stock> Create(int value) =>
        value < 0 ? Result.Failure<Stock>(ProductErrors.NegativeStock) : Result.Success(new Stock(value));

    public Result<Stock> Decrement(int quantity)
    {
        if (quantity <= 0) return Result.Failure<Stock>(ProductErrors.InvalidDecrement);
        if (quantity > Value) return Result.Failure<Stock>(ProductErrors.InsufficientStock);
        return Result.Success(new Stock(Value - quantity));
    }

    public Result<Stock> Return(int quantity) =>
        quantity <= 0 ? Result.Failure<Stock>(ProductErrors.InvalidReturn) : Result.Success(new Stock(Value + quantity));

    public bool Equals(Stock other) => Value == other.Value;
    public override bool Equals(object? obj) => obj is Stock s && Equals(s);
    public override int GetHashCode() => Value;
    public static bool operator ==(Stock a, Stock b) => a.Equals(b);
    public static bool operator !=(Stock a, Stock b) => !a.Equals(b);
}
