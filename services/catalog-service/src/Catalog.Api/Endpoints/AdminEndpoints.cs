using BuildingBlocks.Api;
using Catalog.Application.Products.Queries;
using Catalog.Application.Products.SuspendProduct;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Catalog.Api.Endpoints;

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var grp = app.MapGroup("/api/admin").RequireAuthorization("admin");

        grp.MapGet("/products", async (ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new ListProductsForAdminQuery(), ct);
            return result.ToHttpResult();
        });

        grp.MapPost("/products/{id:guid}/suspend", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new SuspendProductCommand(id), ct);
            return result.ToHttpResult();
        });

        return app;
    }
}
