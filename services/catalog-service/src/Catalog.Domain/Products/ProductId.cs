namespace Catalog.Domain.Products;

public readonly record struct ProductId(Guid Value)
{
    public static ProductId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}

public enum ProductStatus
{
    Draft = 0,
    Published = 1,
    Suspended = 2,
}
