using BuildingBlocks.Application.Behaviors;
using BuildingBlocks.Domain;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Orders.Application.Abstractions;
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

public sealed class ForceCancelOrderHandler(IOrdersDbContext db) : IRequestHandler<ForceCancelOrderCommand, Result>
{
    public async Task<Result> Handle(ForceCancelOrderCommand cmd, CancellationToken ct)
    {
        var oid = new OrderId(cmd.OrderId);
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == oid, ct);
        if (order is null) return Result.Failure(OrderErrors.NotFound);

        var r = order.ForceCancel();
        if (r.IsFailure) return r;

        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
