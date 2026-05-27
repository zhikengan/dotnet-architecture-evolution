using BuildingBlocks.Application;
using BuildingBlocks.Application.Behaviors;
using BuildingBlocks.Domain;
using FluentValidation;
using MassTransit;
using MediatR;
using Orders.Application.Abstractions;
using Orders.Contracts.IntegrationEvents;
using Orders.Domain.Orders.Errors;
using OrderAggregate = global::Orders.Domain.Orders.Order;

namespace Orders.Application.Orders.PlaceOrder;

public sealed record PlaceOrderCommand(Guid BuyerId, Guid ProductId, int Quantity, string? IdempotencyKey = null)
    : IRequest<Result<PlaceOrderResult>>, IAuthorizationRequirement
{
    public string[] AllowedRoles { get; } = ["Buyer"];
}

public sealed record PlaceOrderResult(Guid OrderId, string Status);

public sealed class PlaceOrderValidator : AbstractValidator<PlaceOrderCommand>
{
    public PlaceOrderValidator()
    {
        RuleFor(x => x.BuyerId).NotEqual(Guid.Empty);
        RuleFor(x => x.ProductId).NotEqual(Guid.Empty);
        RuleFor(x => x.Quantity).GreaterThan(0);
    }
}

public sealed class PlaceOrderHandler(
    IOrdersDbContext db,
    IClock clock,
    ITenantContext tenant,
    IPublishEndpoint bus)
    : IRequestHandler<PlaceOrderCommand, Result<PlaceOrderResult>>
{
    public async Task<Result<PlaceOrderResult>> Handle(PlaceOrderCommand cmd, CancellationToken ct)
    {
        if (!tenant.IsSet || tenant.TenantId == Guid.Empty)
            return Result.Failure<PlaceOrderResult>(OrderErrors.InvalidTenant);

        var order = OrderAggregate.Create(cmd.BuyerId, cmd.ProductId, cmd.Quantity, tenant.TenantId, clock.UtcNow);
        if (order.IsFailure) return Result.Failure<PlaceOrderResult>(order.Error);

        db.Orders.Add(order.Value);

        // Kicks off the saga. MassTransit's EF Core bus outbox writes the
        // message into the same DbContext transaction as the order insert,
        // so publish + persist are atomic.
        await bus.Publish(new OrderPlacedIntegrationEvent(
            MessageId: Guid.NewGuid(),
            OccurredAt: clock.UtcNow,
            TenantId: tenant.TenantId,
            OrderId: order.Value.Id.Value,
            BuyerId: order.Value.BuyerId,
            ProductId: order.Value.ProductId,
            Quantity: order.Value.Quantity), ct);

        await db.SaveChangesAsync(ct);
        return Result.Success(new PlaceOrderResult(order.Value.Id.Value, order.Value.Status.ToString()));
    }
}
