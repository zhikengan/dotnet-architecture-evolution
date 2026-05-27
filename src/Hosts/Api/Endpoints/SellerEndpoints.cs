using BuildingBlocks.Api;
using BuildingBlocks.Application;
using Catalog.Application.Products.ConfirmProductImage;
using Catalog.Application.Products.CreateProduct;
using Catalog.Application.Products.GetImageUploadUrl;
using Catalog.Application.Products.Queries.ListProductsForSeller;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Marketplace.Api.Endpoints;

public static class SellerEndpoints
{
    public static IEndpointRouteBuilder MapSellerEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/seller").RequireAuthorization("Seller").WithTags("Seller");

        g.MapPost("/products", async (CreateProductBody body, ICurrentUser user, ISender mediator, CancellationToken ct) =>
        {
            var r = await mediator.Send(new CreateProductCommand(body.Name, body.Price, body.Stock, user.UserId), ct);
            return r.ToHttpResult(rs => Results.Created($"/api/admin/products/{rs.Id}", rs));
        });

        g.MapGet("/products", async (ICurrentUser user, ISender mediator, CancellationToken ct) =>
        {
            var r = await mediator.Send(new ListProductsForSellerQuery(user.UserId), ct);
            return r.ToHttpResult(Results.Ok);
        });

        // Two-step image upload: client requests a presigned URL, PUTs bytes
        // directly to storage, then confirms via the second endpoint. The
        // API host stays out of the upload bandwidth path.
        g.MapPost("/products/{id:guid}/image-upload-url", async (Guid id, GetImageUploadUrlBody body, ISender mediator, CancellationToken ct) =>
        {
            var r = await mediator.Send(new GetImageUploadUrlQuery(id, body.ContentType ?? "image/jpeg"), ct);
            return r.ToHttpResult(Results.Ok);
        });

        g.MapPost("/products/{id:guid}/image", async (Guid id, ConfirmProductImageBody body, ISender mediator, CancellationToken ct) =>
        {
            var r = await mediator.Send(new ConfirmProductImageCommand(id, body.Key), ct);
            return r.ToHttpResult(Results.Ok);
        });

        return app;
    }

    public sealed record CreateProductBody(string Name, decimal Price, int Stock);
    public sealed record GetImageUploadUrlBody(string? ContentType);
    public sealed record ConfirmProductImageBody(string Key);
}
