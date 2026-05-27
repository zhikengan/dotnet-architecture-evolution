using Marketplace.Application.Abstractions;
using Marketplace.Domain.Common;
using Marketplace.Domain.Products;
using MediatR;

namespace Marketplace.Application.Products.CreateProduct;

public sealed class CreateProductHandler(IAppDbContext db, IClock clock, IUnitOfWork uow)
    : IRequestHandler<CreateProductCommand, Result<CreateProductResult>>
{
    public async Task<Result<CreateProductResult>> Handle(CreateProductCommand cmd, CancellationToken ct)
    {
        var moneyResult = Money.Create(cmd.Price, Money.UsdCode);
        if (moneyResult.IsFailure)
            return Result.Failure<CreateProductResult>(moneyResult.Error);

        var productResult = Product.Create(cmd.Name, moneyResult.Value, cmd.Stock, cmd.SellerId, clock.UtcNow);
        if (productResult.IsFailure)
            return Result.Failure<CreateProductResult>(productResult.Error);

        var product = productResult.Value;
        db.Products.Add(product);
        await uow.SaveChangesAsync(ct);

        return Result.Success(new CreateProductResult(
            product.Id.Value,
            product.Name,
            product.Price.Amount,
            product.Stock.Value,
            product.Status.Name));
    }
}
