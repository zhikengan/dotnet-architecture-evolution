using BuildingBlocks.Application;
using BuildingBlocks.Application.Behaviors;
using BuildingBlocks.Domain;
using Catalog.Application.Abstractions;
using Catalog.Domain.Products;
using FluentValidation;
using MediatR;

namespace Catalog.Application.Products.CreateProduct;

public sealed record CreateProductCommand(string Name, decimal Price, int Stock, Guid SellerId)
    : IRequest<Result<CreateProductResult>>, IAuthorizationRequirement
{
    public string[] AllowedRoles { get; } = ["Seller"];
}

public sealed record CreateProductResult(Guid Id, string Name, decimal Price, int Stock, string Status);

public sealed class CreateProductValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Price).GreaterThan(0);
        RuleFor(x => x.Stock).GreaterThanOrEqualTo(0);
        RuleFor(x => x.SellerId).NotEqual(Guid.Empty);
    }
}

public sealed class CreateProductHandler(ICatalogDbContext db, IClock clock) : IRequestHandler<CreateProductCommand, Result<CreateProductResult>>
{
    public async Task<Result<CreateProductResult>> Handle(CreateProductCommand cmd, CancellationToken ct)
    {
        var money = Money.Create(cmd.Price, Money.UsdCode);
        if (money.IsFailure) return Result.Failure<CreateProductResult>(money.Error);

        var product = Product.Create(cmd.Name, money.Value, cmd.Stock, cmd.SellerId, clock.UtcNow);
        if (product.IsFailure) return Result.Failure<CreateProductResult>(product.Error);

        db.Products.Add(product.Value);
        await db.SaveChangesAsync(ct);

        var p = product.Value;
        return Result.Success(new CreateProductResult(p.Id.Value, p.Name, p.Price.Amount, p.Stock.Value, p.Status.ToString()));
    }
}
