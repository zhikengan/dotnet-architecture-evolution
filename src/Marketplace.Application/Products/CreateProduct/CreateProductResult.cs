namespace Marketplace.Application.Products.CreateProduct;

public sealed record CreateProductResult(
    Guid Id,
    string Name,
    decimal Price,
    int Stock,
    string Status);
