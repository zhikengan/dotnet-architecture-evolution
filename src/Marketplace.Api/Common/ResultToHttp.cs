using Marketplace.Domain.Common;

namespace Marketplace.Api.Common;

public static class ResultToHttp
{
    public static IResult Map<T>(Result<T> result, Func<T, IResult> onSuccess) =>
        result.IsSuccess ? onSuccess(result.Value) : MapError(result.Error);

    public static IResult Map(Result result, Func<IResult> onSuccess) =>
        result.IsSuccess ? onSuccess() : MapError(result.Error);

    private static IResult MapError(Error error)
    {
        var status = error.Code switch
        {
            "Product.NotFound" or "Product.NotPublished" or "Order.NotFound"
                => StatusCodes.Status404NotFound,
            "Order.NotOwner"
                => StatusCodes.Status403Forbidden,
            "Stock.Insufficient" or "Order.AlreadyCancelled" or "Order.NotCancellable" or "Order.AlreadyFailed" or "Order.NotPending"
                => StatusCodes.Status422UnprocessableEntity,
            "Validation"
                => StatusCodes.Status400BadRequest,
            _ when error.Code.StartsWith("Product.") || error.Code.StartsWith("Order.") || error.Code.StartsWith("Stock.")
                => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status500InternalServerError,
        };

        return Results.Problem(
            type: $"errors/{error.Code}",
            title: error.Code,
            detail: error.Message,
            statusCode: status);
    }
}
