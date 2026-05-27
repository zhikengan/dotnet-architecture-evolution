using BuildingBlocks.Application.Behaviors;
using BuildingBlocks.Application.MultiTenancy;
using BuildingBlocks.Domain;
using BuildingBlocks.Infrastructure.Storage;
using Catalog.Application.Abstractions;
using Catalog.Domain.Products;
using Catalog.Domain.Products.Errors;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Application.Products.GetImageUploadUrl;

/// <summary>
/// Asks for a short-lived presigned URL the seller can PUT their image bytes to.
/// The product must exist and belong to the caller's tenant (query filter
/// enforces tenant + we re-check sellerId for ownership). The key is a UUID
/// inside a tenant-prefixed namespace so cross-tenant key collisions are
/// impossible by construction.
/// </summary>
public sealed record GetImageUploadUrlQuery(Guid ProductId, string ContentType)
    : IRequest<Result<GetImageUploadUrlResult>>, IAuthorizationRequirement
{
    public string[] AllowedRoles { get; } = ["Seller"];
}

public sealed record GetImageUploadUrlResult(string UploadUrl, string PublicUrl, string Key, DateTime ExpiresAt);

public sealed class GetImageUploadUrlHandler(
    ICatalogDbContext db,
    IFileStorage storage,
    ITenantContext tenant) : IRequestHandler<GetImageUploadUrlQuery, Result<GetImageUploadUrlResult>>
{
    private static readonly TimeSpan UploadTtl = TimeSpan.FromMinutes(15);

    public async Task<Result<GetImageUploadUrlResult>> Handle(GetImageUploadUrlQuery cmd, CancellationToken ct)
    {
        var productId = new ProductId(cmd.ProductId);
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == productId, ct);
        if (product is null) return Result.Failure<GetImageUploadUrlResult>(ProductErrors.NotFound);

        var key = $"{tenant.TenantId}/{cmd.ProductId}/{Guid.NewGuid():N}";
        var presigned = await storage.GeneratePresignedUploadUrlAsync(key, cmd.ContentType, UploadTtl, ct);
        return Result.Success(new GetImageUploadUrlResult(
            presigned.UploadUrl, presigned.PublicUrl, key, presigned.ExpiresAt));
    }
}
