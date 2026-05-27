using Marketplace.Domain.Common;
using Marketplace.Domain.Products.Errors;
using Marketplace.Domain.Products.Events;

namespace Marketplace.Domain.Products;

public sealed class Product : AggregateRoot<ProductId>
{
    public string Name { get; private set; } = string.Empty;
    public Money Price { get; private set; } = null!;
    public Stock Stock { get; private set; }
    public Guid SellerId { get; private set; }
    public ProductStatus Status { get; private set; } = ProductStatus.Draft;
    public DateTime CreatedAt { get; private set; }

    private Product() { }

    public static Result<Product> Create(string name, Money price, int stock, Guid sellerId, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200)
            return Result.Failure<Product>(ProductErrors.InvalidName);
        if (price.Amount <= 0)
            return Result.Failure<Product>(ProductErrors.InvalidPrice);
        if (sellerId == Guid.Empty)
            return Result.Failure<Product>(ProductErrors.InvalidName);

        var stockVo = Stock.Create(stock);
        if (stockVo.IsFailure) return Result.Failure<Product>(stockVo.Error);

        var product = new Product
        {
            Id = ProductId.New(),
            Name = name,
            Price = price,
            Stock = stockVo.Value,
            SellerId = sellerId,
            Status = ProductStatus.Published,
            CreatedAt = now,
        };
        product.RaiseDomainEvent(new ProductCreated(product.Id, sellerId));
        return Result.Success(product);
    }

    public Result Decrement(int quantity)
    {
        if (Status != ProductStatus.Published)
            return Result.Failure(ProductErrors.NotPublished);

        var newStock = Stock.Decrement(quantity);
        if (newStock.IsFailure) return Result.Failure(newStock.Error);

        Stock = newStock.Value;
        RaiseDomainEvent(new StockDecremented(Id, quantity, Stock.Value));
        return Result.Success();
    }

    public Result Return(int quantity)
    {
        var newStock = Stock.Return(quantity);
        if (newStock.IsFailure) return Result.Failure(newStock.Error);

        Stock = newStock.Value;
        RaiseDomainEvent(new StockReturned(Id, quantity, Stock.Value));
        return Result.Success();
    }

    public void Suspend() => Status = ProductStatus.Suspended;
}
