using Marketplace.Application.Abstractions;
using Marketplace.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Application.Products.Queries.ListProductsForAdmin;

public sealed class ListProductsForAdminHandler(IAppDbContext db)
    : IRequestHandler<ListProductsForAdminQuery, Result<IReadOnlyList<AdminProductDto>>>
{
    public async Task<Result<IReadOnlyList<AdminProductDto>>> Handle(
        ListProductsForAdminQuery query,
        CancellationToken ct)
    {
        var products = await db.Products
            .AsNoTracking()
            .OrderBy(p => p.CreatedAt)
            .ToListAsync(ct);

        IReadOnlyList<AdminProductDto> dtos = products
            .Select(p => new AdminProductDto(
                p.Id.Value,
                p.Name,
                p.Price.Amount,
                p.Stock.Value,
                p.Status.Name,
                p.SellerId,
                p.CreatedAt))
            .ToList();

        return Result.Success(dtos);
    }
}
