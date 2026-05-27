using Catalog.Api.Grpc;
using Catalog.Application.Abstractions;
using Catalog.Domain.Products;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Api.GrpcServices;

public sealed class CatalogGrpcService(ICatalogDbContext db) : Catalog.Api.Grpc.CatalogService.CatalogServiceBase
{
    public override async Task<ProductReply> GetProduct(GetProductRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.ProductId, out var pid))
            return new ProductReply { Found = false };

        var id = new ProductId(pid);
        var product = await db.Products.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, context.CancellationToken);
        if (product is null) return new ProductReply { Found = false };

        return Map(product);
    }

    public override async Task<ListProductsReply> ListProducts(ListProductsRequest request, ServerCallContext context)
    {
        var products = await db.Products.IgnoreQueryFilters().AsNoTracking().ToListAsync(context.CancellationToken);
        var reply = new ListProductsReply();
        reply.Products.AddRange(products.Select(Map));
        return reply;
    }

    private static ProductReply Map(Product p) => new()
    {
        Found = true,
        ProductId = p.Id.Value.ToString(),
        TenantId = p.TenantId.ToString(),
        Name = p.Name,
        Price = (double)p.Price.Amount,
        Stock = p.Stock.Value,
        Status = p.Status.ToString(),
        SellerId = p.SellerId.ToString(),
    };
}
