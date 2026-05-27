using Marketplace.Domain.Common;
using Marketplace.Domain.Orders.Errors;

namespace Marketplace.Domain.Orders;

public readonly struct Quantity : IEquatable<Quantity>
{
    public int Value { get; }

    private Quantity(int value) => Value = value;

    public static Result<Quantity> Create(int value) =>
        value < 1
            ? Result.Failure<Quantity>(OrderErrors.InvalidQuantity)
            : Result.Success(new Quantity(value));

    public bool Equals(Quantity other) => Value == other.Value;
    public override bool Equals(object? obj) => obj is Quantity q && Equals(q);
    public override int GetHashCode() => Value;
    public override string ToString() => Value.ToString();

    public static bool operator ==(Quantity left, Quantity right) => left.Equals(right);
    public static bool operator !=(Quantity left, Quantity right) => !left.Equals(right);
}
