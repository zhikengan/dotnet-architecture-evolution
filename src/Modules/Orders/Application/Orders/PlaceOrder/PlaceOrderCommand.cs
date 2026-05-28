using BuildingBlocks.Application;
using BuildingBlocks.Application.Behaviors;
using BuildingBlocks.Application.MultiTenancy;
using BuildingBlocks.Domain;
using BuildingBlocks.Infrastructure.Telemetry;
using FluentValidation;
using MediatR;
using Orders.Application.Abstractions;
using Orders.Domain.Orders.Errors;
using OrderAggregate = global::Orders.Domain.Orders.Order;

namespace Orders.Application.Orders.PlaceOrder;

public sealed record PlaceOrderCommand(Guid BuyerId, Guid ProductId, int Quantity, string? IdempotencyKey = null)
    : IRequest<Result<PlaceOrderResult>>, IIdempotentCommand, IAuthorizationRequirement
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

public sealed class PlaceOrderHandler(IOrdersDbContext db, IClock clock, ITenantContext tenant) : IRequestHandler<PlaceOrderCommand, Result<PlaceOrderResult>>
{
    public async Task<Result<PlaceOrderResult>> Handle(PlaceOrderCommand cmd, CancellationToken ct)
    {
        if (!tenant.IsSet || tenant.TenantId == Guid.Empty)
            return Result.Failure<PlaceOrderResult>(OrderErrors.InvalidTenant);

        var order = OrderAggregate.Create(cmd.BuyerId, cmd.ProductId, cmd.Quantity, tenant.TenantId, clock.UtcNow);
        if (order.IsFailure) return Result.Failure<PlaceOrderResult>(order.Error);

        db.Orders.Add(order.Value);
        await db.SaveChangesAsync(ct);
        MarketplaceMeter.OrdersPlaced.Add(1, new KeyValuePair<string, object?>("tenant_id", tenant.TenantId));
        return Result.Success(new PlaceOrderResult(order.Value.Id.Value, order.Value.Status.ToString()));
    }
}
