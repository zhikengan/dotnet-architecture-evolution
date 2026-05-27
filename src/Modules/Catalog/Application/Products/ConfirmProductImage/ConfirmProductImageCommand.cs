using BuildingBlocks.Application.Behaviors;
using BuildingBlocks.Domain;
using BuildingBlocks.Infrastructure.Storage;
using Catalog.Application.Abstractions;
using Catalog.Domain.Products;
using Catalog.Domain.Products.Errors;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Application.Products.ConfirmProductImage;

/// <summary>
/// Seller confirms they've completed the presigned PUT. The handler checks
/// the object actually exists in storage (defense-in-depth: a malicious
/// caller can't claim a phantom key) and then stamps the product.
/// </summary>
public sealed record ConfirmProductImageCommand(Guid ProductId, string Key)
    : IRequest<Result<ConfirmProductImageResult>>, IAuthorizationRequirement
{
    public string[] AllowedRoles { get; } = ["Seller"];
}

public sealed record ConfirmProductImageResult(Guid ProductId, string ImageUrl);

public sealed class ConfirmProductImageValidator : AbstractValidator<ConfirmProductImageCommand>
{
    public ConfirmProductImageValidator()
    {
        RuleFor(x => x.ProductId).NotEqual(Guid.Empty);
        RuleFor(x => x.Key).NotEmpty().MaximumLength(500);
    }
}

public sealed class ConfirmProductImageHandler(
    ICatalogDbContext db,
    IFileStorage storage) : IRequestHandler<ConfirmProductImageCommand, Result<ConfirmProductImageResult>>
{
    public async Task<Result<ConfirmProductImageResult>> Handle(ConfirmProductImageCommand cmd, CancellationToken ct)
    {
        var productId = new ProductId(cmd.ProductId);
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == productId, ct);
        if (product is null) return Result.Failure<ConfirmProductImageResult>(ProductErrors.NotFound);

        if (!await storage.ExistsAsync(cmd.Key, ct))
            return Result.Failure<ConfirmProductImageResult>(ProductErrors.InvalidImageKey);

        var setResult = product.SetImageKey(cmd.Key);
        if (setResult.IsFailure) return Result.Failure<ConfirmProductImageResult>(setResult.Error);

        await db.SaveChangesAsync(ct);
        return Result.Success(new ConfirmProductImageResult(productId.Value, storage.GeneratePublicUrl(cmd.Key)));
    }
}
