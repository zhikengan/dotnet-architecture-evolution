namespace Marketplace.Domain.Products;

public sealed class ProductStatus : IEquatable<ProductStatus>
{
    public static readonly ProductStatus Draft = new(0, nameof(Draft));
    public static readonly ProductStatus Published = new(1, nameof(Published));
    public static readonly ProductStatus Suspended = new(2, nameof(Suspended));

    public int Value { get; }
    public string Name { get; }

    private ProductStatus(int value, string name)
    {
        Value = value;
        Name = name;
    }

    public static IReadOnlyCollection<ProductStatus> All { get; } = [Draft, Published, Suspended];

    public static ProductStatus FromValue(int value) => All.FirstOrDefault(s => s.Value == value)
        ?? throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown ProductStatus value");

    public static ProductStatus FromName(string name) => All.FirstOrDefault(s => s.Name == name)
        ?? throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown ProductStatus name");

    public bool Equals(ProductStatus? other) => other is not null && Value == other.Value;
    public override bool Equals(object? obj) => obj is ProductStatus s && Equals(s);
    public override int GetHashCode() => Value;
    public override string ToString() => Name;

    public static bool operator ==(ProductStatus? left, ProductStatus? right) => Equals(left, right);
    public static bool operator !=(ProductStatus? left, ProductStatus? right) => !Equals(left, right);
}
