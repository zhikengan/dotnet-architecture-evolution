using BuildingBlocks.Application;
using BuildingBlocks.Domain;
using FluentValidation;
using MassTransit;
using Orders.Application.Abstractions;
using Orders.Contracts.IntegrationEvents;
using Orders.Domain.Orders.Errors;
using OrderAggregate = global::Orders.Domain.Orders.Order;

namespace Orders.Application.Orders;

public sealed record PlaceOrderCommand(Guid BuyerId, Guid ProductId, int Quantity, string? IdempotencyKey = null);

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
{
    public async Task<Result<PlaceOrderResult>> HandleAsync(PlaceOrderCommand cmd, CancellationToken ct)
    {
        if (!tenant.IsSet || tenant.TenantId == Guid.Empty)
            return Result.Failure<PlaceOrderResult>(OrderErrors.InvalidTenant);

        var order = OrderAggregate.Create(cmd.BuyerId, cmd.ProductId, cmd.Quantity, tenant.TenantId, clock.UtcNow);
        if (order.IsFailure) return Result.Failure<PlaceOrderResult>(order.Error);

        db.Orders.Add(order.Value);

        // Publish kicks off the saga. Outbox keeps it transactional with SaveChanges.
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
