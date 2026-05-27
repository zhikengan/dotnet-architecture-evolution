namespace Marketplace.Application.Products.Queries.ListProductsForBuyer;

public sealed record BuyerProductDto(Guid Id, string Name, decimal Price, bool InStock);
