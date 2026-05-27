using BuildingBlocks.Domain;
using Catalog.Application.Abstractions;
using Catalog.Domain.Products;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Application.Products;

public sealed record BuyerProductDto(Guid Id, string Name, decimal Price, bool InStock);
public sealed record AdminProductDto(Guid Id, string Name, decimal Price, int Stock, string Status, Guid SellerId, Guid TenantId);
public sealed record SellerProductDto(Guid Id, string Name, decimal Price, int Stock, string Status);

public sealed class ListProductsForBuyerHandler(ICatalogDbContext db)
{
    public async Task<Result<IReadOnlyList<BuyerProductDto>>> HandleAsync(CancellationToken ct)
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

public sealed class ListProductsForAdminHandler(ICatalogDbContext db)
{
    public async Task<Result<IReadOnlyList<AdminProductDto>>> HandleAsync(CancellationToken ct)
    {
        var products = await db.Products.AsNoTracking().OrderBy(p => p.Name).ToListAsync(ct);
        IReadOnlyList<AdminProductDto> dtos = products
            .Select(p => new AdminProductDto(p.Id.Value, p.Name, p.Price.Amount, p.Stock.Value, p.Status.ToString(), p.SellerId, p.TenantId))
            .ToList();
        return Result.Success(dtos);
    }
}

public sealed class ListProductsForSellerHandler(ICatalogDbContext db)
{
    public async Task<Result<IReadOnlyList<SellerProductDto>>> HandleAsync(Guid sellerId, CancellationToken ct)
    {
        var products = await db.Products.AsNoTracking().Where(p => p.SellerId == sellerId).OrderBy(p => p.Name).ToListAsync(ct);
        IReadOnlyList<SellerProductDto> dtos = products
            .Select(p => new SellerProductDto(p.Id.Value, p.Name, p.Price.Amount, p.Stock.Value, p.Status.ToString()))
            .ToList();
        return Result.Success(dtos);
    }
}
