using BuildingBlocks.Domain;
using Catalog.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Application.Products.Queries;

/// <summary>
/// Cross-tenant flat list of all products. Used by the gRPC transport;
/// callers (admin BFF, ops tooling) are responsible for authz.
/// </summary>
public sealed record ListAllProductsQuery : IRequest<Result<IReadOnlyList<ProductDetailDto>>>;

public sealed class ListAllProductsHandler(ICatalogDbContext db)
    : IRequestHandler<ListAllProductsQuery, Result<IReadOnlyList<ProductDetailDto>>>
{
    public async Task<Result<IReadOnlyList<ProductDetailDto>>> Handle(ListAllProductsQuery _, CancellationToken ct)
    {
        var products = await db.Products.IgnoreQueryFilters().AsNoTracking()
            .OrderBy(p => p.Name)
            .ToListAsync(ct);
        IReadOnlyList<ProductDetailDto> dtos = products
            .Select(p => new ProductDetailDto(p.Id.Value, p.TenantId, p.Name, p.Price.Amount, p.Stock.Value, p.Status.ToString(), p.SellerId))
            .ToList();
        return Result.Success(dtos);
    }
}
