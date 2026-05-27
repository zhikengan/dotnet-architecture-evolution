using BuildingBlocks.Application;
using BuildingBlocks.Application.Behaviors;
using BuildingBlocks.Domain;
using FluentValidation;
using MediatR;
using Orders.Application.Abstractions;
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

public sealed class PlaceOrderHandler(IOrdersDbContext db, IClock clock) : IRequestHandler<PlaceOrderCommand, Result<PlaceOrderResult>>
{
    public async Task<Result<PlaceOrderResult>> Handle(PlaceOrderCommand cmd, CancellationToken ct)
    {
        var order = OrderAggregate.Create(cmd.BuyerId, cmd.ProductId, cmd.Quantity, clock.UtcNow);
        if (order.IsFailure) return Result.Failure<PlaceOrderResult>(order.Error);

        db.Orders.Add(order.Value);
        await db.SaveChangesAsync(ct);
        return Result.Success(new PlaceOrderResult(order.Value.Id.Value, order.Value.Status.ToString()));
    }
}
