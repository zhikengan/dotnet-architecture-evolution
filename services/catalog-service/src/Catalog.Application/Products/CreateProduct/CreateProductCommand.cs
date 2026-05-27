using BuildingBlocks.Application;
using BuildingBlocks.Application.Behaviors;
using BuildingBlocks.Domain;
using Catalog.Application.Abstractions;
using Catalog.Contracts.IntegrationEvents;
using Catalog.Domain.Products;
using Catalog.Domain.Products.Errors;
using FluentValidation;
using MassTransit;
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

public sealed class CreateProductHandler(
    ICatalogDbContext db,
    IClock clock,
    ITenantContext tenant,
    IPublishEndpoint bus)
    : IRequestHandler<CreateProductCommand, Result<CreateProductResult>>
{
    public async Task<Result<CreateProductResult>> Handle(CreateProductCommand cmd, CancellationToken ct)
    {
        if (!tenant.IsSet || tenant.TenantId == Guid.Empty)
            return Result.Failure<CreateProductResult>(ProductErrors.InvalidTenant);

        var money = Money.Create(cmd.Price, Money.UsdCode);
        if (money.IsFailure) return Result.Failure<CreateProductResult>(money.Error);

        var product = Product.Create(cmd.Name, money.Value, cmd.Stock, cmd.SellerId, tenant.TenantId, clock.UtcNow);
        if (product.IsFailure) return Result.Failure<CreateProductResult>(product.Error);

        db.Products.Add(product.Value);

        await bus.Publish(new ProductCreatedIntegrationEvent(
            MessageId: Guid.NewGuid(),
            OccurredAt: clock.UtcNow,
            TenantId: tenant.TenantId,
            ProductId: product.Value.Id.Value,
            Name: product.Value.Name,
            Price: product.Value.Price.Amount,
            Stock: product.Value.Stock.Value,
            SellerId: product.Value.SellerId), ct);

        await db.SaveChangesAsync(ct);

        var p = product.Value;
        return Result.Success(new CreateProductResult(p.Id.Value, p.Name, p.Price.Amount, p.Stock.Value, p.Status.ToString()));
    }
}
