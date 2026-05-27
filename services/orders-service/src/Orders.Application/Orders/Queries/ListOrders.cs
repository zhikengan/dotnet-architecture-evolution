using BuildingBlocks.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Orders.Application.Abstractions;
using Orders.Domain.Orders.Errors;
using OrderIdValue = global::Orders.Domain.Orders.OrderId;

namespace Orders.Application.Orders.Queries;

public sealed record OrderDto(Guid Id, Guid BuyerId, Guid ProductId, int Quantity, string Status, DateTime CreatedAt, string? FailureReason);

public sealed record ListOrdersForBuyerQuery(Guid BuyerId) : IRequest<Result<IReadOnlyList<OrderDto>>>;
public sealed record ListOrdersForAdminQuery : IRequest<Result<IReadOnlyList<OrderDto>>>;
public sealed record GetOrderForBuyerQuery(Guid OrderId, Guid BuyerId) : IRequest<Result<OrderDto>>;

public sealed class ListOrdersForBuyerHandler(IOrdersDbContext db)
    : IRequestHandler<ListOrdersForBuyerQuery, Result<IReadOnlyList<OrderDto>>>
{
    public async Task<Result<IReadOnlyList<OrderDto>>> Handle(ListOrdersForBuyerQuery q, CancellationToken ct)
    {
        var orders = await db.Orders.AsNoTracking()
            .Where(o => o.BuyerId == q.BuyerId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(ct);
        IReadOnlyList<OrderDto> dtos = orders
            .Select(o => new OrderDto(o.Id.Value, o.BuyerId, o.ProductId, o.Quantity, o.Status.ToString(), o.CreatedAt, o.FailureReason))
            .ToList();
        return Result.Success(dtos);
    }
}

public sealed class ListOrdersForAdminHandler(IOrdersDbContext db)
    : IRequestHandler<ListOrdersForAdminQuery, Result<IReadOnlyList<OrderDto>>>
{
    public async Task<Result<IReadOnlyList<OrderDto>>> Handle(ListOrdersForAdminQuery _, CancellationToken ct)
    {
        var orders = await db.Orders.IgnoreQueryFilters().AsNoTracking().OrderByDescending(o => o.CreatedAt).ToListAsync(ct);
        IReadOnlyList<OrderDto> dtos = orders
            .Select(o => new OrderDto(o.Id.Value, o.BuyerId, o.ProductId, o.Quantity, o.Status.ToString(), o.CreatedAt, o.FailureReason))
            .ToList();
        return Result.Success(dtos);
    }
}

public sealed class GetOrderForBuyerHandler(IOrdersDbContext db)
    : IRequestHandler<GetOrderForBuyerQuery, Result<OrderDto>>
{
    public async Task<Result<OrderDto>> Handle(GetOrderForBuyerQuery q, CancellationToken ct)
    {
        var oid = new OrderIdValue(q.OrderId);
        var order = await db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == oid && o.BuyerId == q.BuyerId, ct);
        if (order is null) return Result.Failure<OrderDto>(OrderErrors.NotFound);
        return Result.Success(new OrderDto(order.Id.Value, order.BuyerId, order.ProductId, order.Quantity, order.Status.ToString(), order.CreatedAt, order.FailureReason));
    }
}
