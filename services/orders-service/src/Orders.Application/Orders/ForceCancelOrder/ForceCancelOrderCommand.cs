using BuildingBlocks.Application;
using BuildingBlocks.Application.Behaviors;
using BuildingBlocks.Domain;
using FluentValidation;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Orders.Application.Abstractions;
using Orders.Contracts.IntegrationEvents;
using Orders.Domain.Orders;
using Orders.Domain.Orders.Errors;

namespace Orders.Application.Orders.ForceCancelOrder;

public sealed record ForceCancelOrderCommand(Guid OrderId)
    : IRequest<Result>, IAuthorizationRequirement
{
    public string[] AllowedRoles { get; } = ["Admin"];
}

public sealed class ForceCancelOrderValidator : AbstractValidator<ForceCancelOrderCommand>
{
    public ForceCancelOrderValidator() => RuleFor(x => x.OrderId).NotEqual(Guid.Empty);
}

public sealed class ForceCancelOrderHandler(
    IOrdersDbContext db,
    IClock clock,
    ITenantContext tenant,
    IPublishEndpoint bus)
    : IRequestHandler<ForceCancelOrderCommand, Result>
{
    public async Task<Result> Handle(ForceCancelOrderCommand cmd, CancellationToken ct)
    {
        var oid = new OrderId(cmd.OrderId);
        var order = await db.Orders.IgnoreQueryFilters().FirstOrDefaultAsync(o => o.Id == oid, ct);
        if (order is null) return Result.Failure(OrderErrors.NotFound);

        // Capture pre-cancel state — Confirmed orders had stock decremented; Pending didn't.
        var stockWasDecremented = order.Status == OrderStatus.Confirmed;

        var r = order.ForceCancel();
        if (r.IsFailure) return r;

        await bus.Publish(new OrderCancelledIntegrationEvent(
            MessageId: Guid.NewGuid(),
            OccurredAt: clock.UtcNow,
            TenantId: tenant.TenantId == Guid.Empty ? order.TenantId : tenant.TenantId,
            OrderId: order.Id.Value,
            ProductId: order.ProductId,
            Quantity: order.Quantity,
            StockWasDecremented: stockWasDecremented), ct);

        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
