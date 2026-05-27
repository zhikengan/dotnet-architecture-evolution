using FluentValidation;

namespace Marketplace.Application.Products.CreateProduct;

public sealed class CreateProductValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Product name is required")
            .MaximumLength(200).WithMessage("Product name must be 200 characters or fewer");

        RuleFor(x => x.Price)
            .GreaterThan(0).WithMessage("Product price must be positive");

        RuleFor(x => x.Stock)
            .GreaterThanOrEqualTo(0).WithMessage("Stock cannot be negative");

        RuleFor(x => x.SellerId)
            .NotEqual(Guid.Empty).WithMessage("SellerId is required");
    }
}
