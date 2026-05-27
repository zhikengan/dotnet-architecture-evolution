using BuildingBlocks.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Orders.Application.Abstractions;
using Orders.Domain.Orders.Errors;
using OrderIdValue = global::Orders.Domain.Orders.OrderId;

namespace Orders.Application.Orders.Queries;

public sealed record OrderDetailDto(
    Guid Id,
    Guid TenantId,
    Guid BuyerId,
    Guid ProductId,
    int Quantity,
    string Status,
    DateTime CreatedAt);

/// <summary>
/// Cross-tenant lookup used by the gRPC transport for admin views.
/// </summary>
public sealed record GetOrderByIdQuery(Guid OrderId) : IRequest<Result<OrderDetailDto>>;

public sealed class GetOrderByIdHandler(IOrdersDbContext db)
    : IRequestHandler<GetOrderByIdQuery, Result<OrderDetailDto>>
{
    public async Task<Result<OrderDetailDto>> Handle(GetOrderByIdQuery q, CancellationToken ct)
    {
        var oid = new OrderIdValue(q.OrderId);
        var o = await db.Orders.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == oid, ct);
        if (o is null) return Result.Failure<OrderDetailDto>(OrderErrors.NotFound);
        return Result.Success(new OrderDetailDto(
            o.Id.Value, o.TenantId, o.BuyerId, o.ProductId, o.Quantity, o.Status.ToString(), o.CreatedAt));
    }
}
