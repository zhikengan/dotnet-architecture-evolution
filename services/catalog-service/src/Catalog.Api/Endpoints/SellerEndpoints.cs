using BuildingBlocks.Api;
using BuildingBlocks.Application;
using Catalog.Application.Products;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Security.Claims;

namespace Catalog.Api.Endpoints;

public static class SellerEndpoints
{
    public static IEndpointRouteBuilder MapSellerEndpoints(this IEndpointRouteBuilder app)
    {
        var grp = app.MapGroup("/api/seller").RequireAuthorization("seller");

        grp.MapPost("/products", async (
            CreateProductCommand cmd,
            CreateProductHandler handler,
            IValidator<CreateProductCommand> validator,
            CancellationToken ct) =>
        {
            var validation = await validator.ValidateAsync(cmd, ct);
            if (!validation.IsValid)
                return Results.BadRequest(new { error = "Validation", details = validation.Errors.Select(e => e.ErrorMessage) });

            var result = await handler.HandleAsync(cmd, ct);
            return result.ToHttpResult(value => Results.Created($"/api/seller/products/{value.Id}", value));
        });

        grp.MapGet("/products", async (
            HttpContext ctx,
            ListProductsForSellerHandler handler,
            CancellationToken ct) =>
        {
            var sellerIdClaim = ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? ctx.User.FindFirst("sub")?.Value;
            if (!Guid.TryParse(sellerIdClaim, out var sellerId))
                return Results.Unauthorized();
            var result = await handler.HandleAsync(sellerId, ct);
            return result.ToHttpResult();
        });

        return app;
    }
}
