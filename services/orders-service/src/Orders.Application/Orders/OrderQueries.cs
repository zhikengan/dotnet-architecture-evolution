using BuildingBlocks.Domain;
using Microsoft.EntityFrameworkCore;
using Orders.Application.Abstractions;
using Orders.Domain.Orders.Errors;
using OrderIdValue = global::Orders.Domain.Orders.OrderId;

namespace Orders.Application.Orders;

public sealed record OrderDto(Guid Id, Guid BuyerId, Guid ProductId, int Quantity, string Status, DateTime CreatedAt, string? FailureReason);

public sealed class ListOrdersForBuyerHandler(IOrdersDbContext db)
{
    public async Task<Result<IReadOnlyList<OrderDto>>> HandleAsync(Guid buyerId, CancellationToken ct)
    {
        var orders = await db.Orders.AsNoTracking()
            .Where(o => o.BuyerId == buyerId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(ct);
        IReadOnlyList<OrderDto> dtos = orders
            .Select(o => new OrderDto(o.Id.Value, o.BuyerId, o.ProductId, o.Quantity, o.Status.ToString(), o.CreatedAt, o.FailureReason))
            .ToList();
        return Result.Success(dtos);
    }
}

public sealed class ListOrdersForAdminHandler(IOrdersDbContext db)
{
    public async Task<Result<IReadOnlyList<OrderDto>>> HandleAsync(CancellationToken ct)
    {
        var orders = await db.Orders.AsNoTracking().OrderByDescending(o => o.CreatedAt).ToListAsync(ct);
        IReadOnlyList<OrderDto> dtos = orders
            .Select(o => new OrderDto(o.Id.Value, o.BuyerId, o.ProductId, o.Quantity, o.Status.ToString(), o.CreatedAt, o.FailureReason))
            .ToList();
        return Result.Success(dtos);
    }
}

public sealed class GetOrderForBuyerHandler(IOrdersDbContext db)
{
    public async Task<Result<OrderDto>> HandleAsync(Guid orderId, Guid buyerId, CancellationToken ct)
    {
        var oid = new OrderIdValue(orderId);
        var order = await db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == oid && o.BuyerId == buyerId, ct);
        if (order is null) return Result.Failure<OrderDto>(OrderErrors.NotFound);
        return Result.Success(new OrderDto(order.Id.Value, order.BuyerId, order.ProductId, order.Quantity, order.Status.ToString(), order.CreatedAt, order.FailureReason));
    }
}
