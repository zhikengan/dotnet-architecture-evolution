using BuildingBlocks.Domain;
using Catalog.Application.Abstractions;
using Catalog.Domain.Products;
using Catalog.Domain.Products.Errors;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Application.Products.Queries;

public sealed record ProductDetailDto(
    Guid Id,
    Guid TenantId,
    string Name,
    decimal Price,
    int Stock,
    string Status,
    Guid SellerId);

/// <summary>
/// Cross-tenant product lookup (bypasses tenant filter). Used by the gRPC
/// transport for sync lookups from BFFs/admin; the caller is responsible for
/// authorizing whether they may see another tenant's data.
/// </summary>
public sealed record GetProductByIdQuery(Guid ProductId) : IRequest<Result<ProductDetailDto>>;

public sealed class GetProductByIdHandler(ICatalogDbContext db)
    : IRequestHandler<GetProductByIdQuery, Result<ProductDetailDto>>
{
    public async Task<Result<ProductDetailDto>> Handle(GetProductByIdQuery q, CancellationToken ct)
    {
        var id = new ProductId(q.ProductId);
        var p = await db.Products.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return Result.Failure<ProductDetailDto>(ProductErrors.NotFound);
        return Result.Success(new ProductDetailDto(p.Id.Value, p.TenantId, p.Name, p.Price.Amount, p.Stock.Value, p.Status.ToString(), p.SellerId));
    }
}
