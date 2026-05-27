using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Orders.Api.Grpc;
using Orders.Application.Abstractions;
using OrderIdValue = global::Orders.Domain.Orders.OrderId;

namespace Orders.Api.GrpcServices;

public sealed class OrdersGrpcService(IOrdersDbContext db) : Orders.Api.Grpc.OrdersService.OrdersServiceBase
{
    public override async Task<OrderReply> GetOrder(GetOrderRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.OrderId, out var id))
            return new OrderReply { Found = false };

        var oid = new OrderIdValue(id);
        var dbContext = (DbContext)db;
        var order = await dbContext.Set<Domain.Orders.Order>().IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == oid, context.CancellationToken);
        if (order is null) return new OrderReply { Found = false };

        return new OrderReply
        {
            Found = true,
            OrderId = order.Id.Value.ToString(),
            TenantId = order.TenantId.ToString(),
            BuyerId = order.BuyerId.ToString(),
            ProductId = order.ProductId.ToString(),
            Quantity = order.Quantity,
            Status = order.Status.ToString(),
            CreatedAt = order.CreatedAt.ToString("O"),
        };
    }
}
