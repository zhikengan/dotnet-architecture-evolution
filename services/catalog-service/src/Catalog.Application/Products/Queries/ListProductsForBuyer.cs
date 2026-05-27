using BuildingBlocks.Domain;
using Catalog.Application.Abstractions;
using Catalog.Domain.Products;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Application.Products.Queries;

public sealed record BuyerProductDto(Guid Id, string Name, decimal Price, bool InStock);

public sealed record ListProductsForBuyerQuery : IRequest<Result<IReadOnlyList<BuyerProductDto>>>;

public sealed class ListProductsForBuyerHandler(ICatalogDbContext db)
    : IRequestHandler<ListProductsForBuyerQuery, Result<IReadOnlyList<BuyerProductDto>>>
{
    public async Task<Result<IReadOnlyList<BuyerProductDto>>> Handle(ListProductsForBuyerQuery _, CancellationToken ct)
    {
        var published = ProductStatus.Published;
        var products = await db.Products.AsNoTracking()
            .Where(p => p.Status == published)
            .OrderBy(p => p.Name)
            .ToListAsync(ct);
        IReadOnlyList<BuyerProductDto> dtos = products
            .Select(p => new BuyerProductDto(p.Id.Value, p.Name, p.Price.Amount, p.Stock.Value > 0))
            .ToList();
        return Result.Success(dtos);
    }
}
