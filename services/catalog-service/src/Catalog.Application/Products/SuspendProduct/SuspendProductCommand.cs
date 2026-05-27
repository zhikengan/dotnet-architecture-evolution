using BuildingBlocks.Application.Behaviors;
using BuildingBlocks.Domain;
using Catalog.Application.Abstractions;
using Catalog.Domain.Products;
using Catalog.Domain.Products.Errors;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Application.Products.SuspendProduct;

public sealed record SuspendProductCommand(Guid ProductId) : IRequest<Result>, IAuthorizationRequirement
{
    public string[] AllowedRoles { get; } = ["Admin"];
}

public sealed class SuspendProductHandler(ICatalogDbContext db) : IRequestHandler<SuspendProductCommand, Result>
{
    public async Task<Result> Handle(SuspendProductCommand cmd, CancellationToken ct)
    {
        var id = new ProductId(cmd.ProductId);
        var product = await db.Products.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == id, ct);
        if (product is null) return Result.Failure(ProductErrors.NotFound);
        product.Suspend();
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
