using FluentValidation;

namespace Marketplace.Application.Orders.ForceCancelOrder;

public sealed class ForceCancelOrderValidator : AbstractValidator<ForceCancelOrderCommand>
{
    public ForceCancelOrderValidator()
    {
        RuleFor(x => x.OrderId).NotEqual(Guid.Empty);
    }
}
