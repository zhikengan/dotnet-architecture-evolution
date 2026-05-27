using Catalog.Api.Grpc;
using Catalog.Application.Products.Queries;
using Grpc.Core;
using MediatR;

namespace Catalog.Api.GrpcServices;

/// <summary>
/// gRPC adapter — translates wire messages to MediatR queries against the
/// Application layer. Persistence, tenant filtering, and authorization live
/// inside the handlers, not here.
/// </summary>
public sealed class CatalogGrpcService(ISender sender) : Catalog.Api.Grpc.CatalogService.CatalogServiceBase
{
    public override async Task<ProductReply> GetProduct(GetProductRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.ProductId, out var pid))
            return new ProductReply { Found = false };

        var result = await sender.Send(new GetProductByIdQuery(pid), context.CancellationToken);
        if (result.IsFailure) return new ProductReply { Found = false };
        return MapToReply(result.Value);
    }

    public override async Task<ListProductsReply> ListProducts(ListProductsRequest request, ServerCallContext context)
    {
        var result = await sender.Send(new ListAllProductsQuery(), context.CancellationToken);
        var reply = new ListProductsReply();
        if (result.IsSuccess) reply.Products.AddRange(result.Value.Select(MapToReply));
        return reply;
    }

    private static ProductReply MapToReply(ProductDetailDto dto) => new()
    {
        Found = true,
        ProductId = dto.Id.ToString(),
        TenantId = dto.TenantId.ToString(),
        Name = dto.Name,
        Price = (double)dto.Price,
        Stock = dto.Stock,
        Status = dto.Status,
        SellerId = dto.SellerId.ToString(),
    };
}
