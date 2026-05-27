namespace Marketplace.Application.Products.Queries.ListProductsForAdmin;

public sealed record AdminProductDto(
    Guid Id,
    string Name,
    decimal Price,
    int Stock,
    string Status,
    Guid SellerId,
    DateTime CreatedAt);
