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
using Orders.Domain.Orders.Events;

namespace Orders.Application.Orders.CancelOwnOrder;

public sealed record CancelOwnOrderCommand(Guid OrderId, Guid BuyerId)
    : IRequest<Result>, IAuthorizationRequirement
{
    public string[] AllowedRoles { get; } = ["Buyer"];
}

public sealed class CancelOwnOrderValidator : AbstractValidator<CancelOwnOrderCommand>
{
    public CancelOwnOrderValidator()
    {
        RuleFor(x => x.OrderId).NotEqual(Guid.Empty);
        RuleFor(x => x.BuyerId).NotEqual(Guid.Empty);
    }
}

public sealed class CancelOwnOrderHandler(
    IOrdersDbContext db,
    IClock clock,
    ITenantContext tenant,
    IPublishEndpoint bus)
    : IRequestHandler<CancelOwnOrderCommand, Result>
{
    public async Task<Result> Handle(CancelOwnOrderCommand cmd, CancellationToken ct)
    {
        var oid = new OrderId(cmd.OrderId);
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == oid, ct);
        if (order is null) return Result.Failure(OrderErrors.NotFound);

        var r = order.Cancel(cmd.BuyerId);
        if (r.IsFailure) return r;

        var cancelEvent = order.DomainEvents.OfType<OrderCancelled>().Last();
        await bus.Publish(new OrderCancelledIntegrationEvent(
            MessageId: Guid.NewGuid(),
            OccurredAt: clock.UtcNow,
            TenantId: tenant.TenantId,
            OrderId: order.Id.Value,
            ProductId: order.ProductId,
            Quantity: order.Quantity,
            StockWasDecremented: cancelEvent.StockWasDecremented), ct);

        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
