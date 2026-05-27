using FluentValidation;

namespace Marketplace.Application.Orders.CancelOwnOrder;

public sealed class CancelOwnOrderValidator : AbstractValidator<CancelOwnOrderCommand>
{
    public CancelOwnOrderValidator()
    {
        RuleFor(x => x.OrderId).NotEqual(Guid.Empty);
        RuleFor(x => x.BuyerId).NotEqual(Guid.Empty);
    }
}
