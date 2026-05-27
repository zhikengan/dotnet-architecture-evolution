using Marketplace.Application.Abstractions;
using Marketplace.Domain.Common;
using Marketplace.Domain.Products;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Application.Products.Queries.ListProductsForBuyer;

public sealed class ListProductsForBuyerHandler(IAppDbContext db)
    : IRequestHandler<ListProductsForBuyerQuery, Result<IReadOnlyList<BuyerProductDto>>>
{
    public async Task<Result<IReadOnlyList<BuyerProductDto>>> Handle(
        ListProductsForBuyerQuery query,
        CancellationToken ct)
    {
        var products = await db.Products
            .AsNoTracking()
            .Where(p => p.Status == ProductStatus.Published)
            .OrderBy(p => p.Name)
            .ToListAsync(ct);

        IReadOnlyList<BuyerProductDto> dtos = products
            .Select(p => new BuyerProductDto(p.Id.Value, p.Name, p.Price.Amount, p.Stock.Value > 0))
            .ToList();

        return Result.Success(dtos);
    }
}
