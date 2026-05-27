using BuildingBlocks.Domain;
using Catalog.Application.Abstractions;
using Catalog.Domain.Products;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Platform.Contracts;

namespace Catalog.Application.Products.Queries.ListProductsForBuyer;

public sealed record BuyerProductDto(Guid Id, string Name, decimal Price, bool InStock, bool IsPremium);

public sealed record ListProductsForBuyerQuery(Guid BuyerId) : IRequest<Result<IReadOnlyList<BuyerProductDto>>>;

public sealed class ListProductsForBuyerHandler(ICatalogDbContext db, IFeatureFlagQuery featureFlags)
    : IRequestHandler<ListProductsForBuyerQuery, Result<IReadOnlyList<BuyerProductDto>>>
{
    public async Task<Result<IReadOnlyList<BuyerProductDto>>> Handle(ListProductsForBuyerQuery query, CancellationToken ct)
    {
        var published = ProductStatus.Published;
        var products = await db.Products.AsNoTracking()
            .Where(p => p.Status == published)
            .OrderBy(p => p.Name)
            .ToListAsync(ct);

        var isPremium = await featureFlags.IsEnabledAsync("EnablePremiumBadge", query.BuyerId, ct);
        IReadOnlyList<BuyerProductDto> dtos = products
            .Select(p => new BuyerProductDto(p.Id.Value, p.Name, p.Price.Amount, p.Stock.Value > 0, isPremium))
            .ToList();
        return Result.Success(dtos);
    }
}
