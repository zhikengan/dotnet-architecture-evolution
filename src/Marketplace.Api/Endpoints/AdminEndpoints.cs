using Marketplace.Api.Authorization;
using Marketplace.Api.Common;
using Marketplace.Application.Orders.ForceCancelOrder;
using Marketplace.Application.Products.Queries.ListProductsForAdmin;
using MediatR;

namespace Marketplace.Api.Endpoints;

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin").RequireRole("Admin").WithTags("Admin");

        admin.MapGet("/products", async (ISender mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new ListProductsForAdminQuery(), ct);
            return ResultToHttp.Map(result, products => Results.Ok(products));
        });

        admin.MapPost("/orders/{id:guid}/cancel", async (Guid id, ISender mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new ForceCancelOrderCommand(id), ct);
            return ResultToHttp.Map(result, () => Results.Ok(new { id, status = "Cancelled" }));
        });

        return app;
    }
}
