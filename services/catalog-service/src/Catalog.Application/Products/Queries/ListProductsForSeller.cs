using BuildingBlocks.Domain;
using Catalog.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Application.Products.Queries;

public sealed record SellerProductDto(Guid Id, string Name, decimal Price, int Stock, string Status);

public sealed record ListProductsForSellerQuery(Guid SellerId) : IRequest<Result<IReadOnlyList<SellerProductDto>>>;

public sealed class ListProductsForSellerHandler(ICatalogDbContext db)
    : IRequestHandler<ListProductsForSellerQuery, Result<IReadOnlyList<SellerProductDto>>>
{
    public async Task<Result<IReadOnlyList<SellerProductDto>>> Handle(ListProductsForSellerQuery q, CancellationToken ct)
    {
        var products = await db.Products.AsNoTracking()
            .Where(p => p.SellerId == q.SellerId)
            .OrderBy(p => p.Name)
            .ToListAsync(ct);
        IReadOnlyList<SellerProductDto> dtos = products
            .Select(p => new SellerProductDto(p.Id.Value, p.Name, p.Price.Amount, p.Stock.Value, p.Status.ToString()))
            .ToList();
        return Result.Success(dtos);
    }
}
