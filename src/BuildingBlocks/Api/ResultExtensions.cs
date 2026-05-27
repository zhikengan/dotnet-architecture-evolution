using BuildingBlocks.Domain;
using Microsoft.AspNetCore.Http;

namespace BuildingBlocks.Api;

public static class ResultExtensions
{
    public static IResult ToHttpResult<T>(this Result<T> result, Func<T, IResult> onSuccess) =>
        result.IsSuccess ? onSuccess(result.Value) : MapError(result.Error);

    public static IResult ToHttpResult(this Result result, Func<IResult> onSuccess) =>
        result.IsSuccess ? onSuccess() : MapError(result.Error);

    public static IResult MapError(Error error) =>
        Results.Problem(
            type: $"errors/{error.Code}",
            title: error.Code,
            detail: error.Message,
            statusCode: StatusCodeFor(error.Code));

    public static int StatusCodeFor(string code) => code switch
    {
        var c when c.EndsWith(".NotFound") || c == "Product.NotPublished"
            => StatusCodes.Status404NotFound,
        "Order.NotOwner" or "Authorization.Forbidden"
            => StatusCodes.Status403Forbidden,
        "Authorization.NotAuthenticated"
            => StatusCodes.Status401Unauthorized,
        "Stock.Insufficient" or "Order.AlreadyCancelled" or "Order.NotCancellable" or "Order.NotPending" or "Order.AlreadyFailed"
            => StatusCodes.Status422UnprocessableEntity,
        "Validation"
            => StatusCodes.Status400BadRequest,
        var c when c.StartsWith("Product.") || c.StartsWith("Order.") || c.StartsWith("Stock.") || c.StartsWith("Quantity.") || c.StartsWith("FeatureFlag.")
            => StatusCodes.Status400BadRequest,
        _ => StatusCodes.Status500InternalServerError,
    };
}
