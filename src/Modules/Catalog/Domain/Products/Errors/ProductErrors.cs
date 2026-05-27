using BuildingBlocks.Domain;

namespace Catalog.Domain.Products.Errors;

public static class ProductErrors
{
    public static readonly Error InvalidName = new("Product.InvalidName", "Product name must be 1-200 characters");
    public static readonly Error InvalidPrice = new("Product.InvalidPrice", "Product price must be positive");
    public static readonly Error UnsupportedCurrency = new("Product.UnsupportedCurrency", "Only USD is supported at this tier");
    public static readonly Error NotFound = new("Product.NotFound", "Product not found");
    public static readonly Error NotPublished = new("Product.NotPublished", "Product is not published");
    public static readonly Error NegativeStock = new("Stock.Negative", "Stock cannot be negative");
    public static readonly Error InvalidDecrement = new("Stock.InvalidDecrement", "Decrement quantity must be positive");
    public static readonly Error InsufficientStock = new("Stock.Insufficient", "Insufficient stock");
    public static readonly Error InvalidReturn = new("Stock.InvalidReturn", "Return quantity must be positive");
}
