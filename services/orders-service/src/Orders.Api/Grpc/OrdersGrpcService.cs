using Grpc.Core;
using MediatR;
using Orders.Api.Grpc;
using Orders.Application.Orders.Queries;

namespace Orders.Api.GrpcServices;

/// <summary>
/// gRPC adapter — delegates to MediatR queries against the Application layer.
/// </summary>
public sealed class OrdersGrpcService(ISender sender) : Orders.Api.Grpc.OrdersService.OrdersServiceBase
{
    public override async Task<OrderReply> GetOrder(GetOrderRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.OrderId, out var id))
            return new OrderReply { Found = false };

        var result = await sender.Send(new GetOrderByIdQuery(id), context.CancellationToken);
        if (result.IsFailure) return new OrderReply { Found = false };

        var dto = result.Value;
        return new OrderReply
        {
            Found = true,
            OrderId = dto.Id.ToString(),
            TenantId = dto.TenantId.ToString(),
            BuyerId = dto.BuyerId.ToString(),
            ProductId = dto.ProductId.ToString(),
            Quantity = dto.Quantity,
            Status = dto.Status,
            CreatedAt = dto.CreatedAt.ToString("O"),
        };
    }
}
