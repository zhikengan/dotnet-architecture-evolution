using BuildingBlocks.Domain;
using Catalog.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Application.Products.Queries;

public sealed record AdminProductDto(Guid Id, string Name, decimal Price, int Stock, string Status, Guid SellerId, Guid TenantId);

public sealed record ListProductsForAdminQuery : IRequest<Result<IReadOnlyList<AdminProductDto>>>;

public sealed class ListProductsForAdminHandler(ICatalogDbContext db)
    : IRequestHandler<ListProductsForAdminQuery, Result<IReadOnlyList<AdminProductDto>>>
{
    public async Task<Result<IReadOnlyList<AdminProductDto>>> Handle(ListProductsForAdminQuery _, CancellationToken ct)
    {
        var products = await db.Products.IgnoreQueryFilters().AsNoTracking().OrderBy(p => p.Name).ToListAsync(ct);
        IReadOnlyList<AdminProductDto> dtos = products
            .Select(p => new AdminProductDto(p.Id.Value, p.Name, p.Price.Amount, p.Stock.Value, p.Status.ToString(), p.SellerId, p.TenantId))
            .ToList();
        return Result.Success(dtos);
    }
}
