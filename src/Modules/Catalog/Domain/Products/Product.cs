using BuildingBlocks.Domain;
using BuildingBlocks.Domain.MultiTenancy;
using Catalog.Domain.Products.Errors;
using Catalog.Domain.Products.Events;

namespace Catalog.Domain.Products;

public sealed class Product : AggregateRoot<ProductId>, IMultiTenant
{
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public Money Price { get; private set; } = null!;
    public Stock Stock { get; private set; }
    public Guid SellerId { get; private set; }
    public ProductStatus Status { get; private set; } = ProductStatus.Draft;
    public DateTime CreatedAt { get; private set; }

    private Product() { }

    public static Result<Product> Create(string name, Money price, int stock, Guid sellerId, Guid tenantId, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200)
            return Result.Failure<Product>(ProductErrors.InvalidName);
        if (price.Amount <= 0)
            return Result.Failure<Product>(ProductErrors.InvalidPrice);
        if (sellerId == Guid.Empty)
            return Result.Failure<Product>(ProductErrors.InvalidName);
        if (tenantId == Guid.Empty)
            return Result.Failure<Product>(ProductErrors.InvalidTenant);

        var stockVo = Stock.Create(stock);
        if (stockVo.IsFailure) return Result.Failure<Product>(stockVo.Error);

        var product = new Product
        {
            Id = ProductId.New(),
            TenantId = tenantId,
            Name = name,
            Price = price,
            Stock = stockVo.Value,
            SellerId = sellerId,
            Status = ProductStatus.Published,
            CreatedAt = now,
        };
        product.RaiseDomainEvent(new ProductCreated(product.Id, tenantId, name, price.Amount, stock, sellerId));
        return Result.Success(product);
    }

    public Result Decrement(int quantity, Guid orderId)
    {
        if (Status != ProductStatus.Published)
        {
            RaiseDomainEvent(new StockDecrementFailed(Id, TenantId, orderId, "Product not published"));
            return Result.Failure(ProductErrors.NotPublished);
        }
        var newStock = Stock.Decrement(quantity);
        if (newStock.IsFailure)
        {
            RaiseDomainEvent(new StockDecrementFailed(Id, TenantId, orderId, newStock.Error.Message));
            return Result.Failure(newStock.Error);
        }
        Stock = newStock.Value;
        RaiseDomainEvent(new StockDecremented(Id, TenantId, orderId, quantity, Stock.Value));
        return Result.Success();
    }

    public Result Return(int quantity, Guid orderId)
    {
        var newStock = Stock.Return(quantity);
        if (newStock.IsFailure) return Result.Failure(newStock.Error);
        Stock = newStock.Value;
        RaiseDomainEvent(new StockReturned(Id, TenantId, orderId, quantity, Stock.Value));
        return Result.Success();
    }

    public void Suspend() => Status = ProductStatus.Suspended;
}
