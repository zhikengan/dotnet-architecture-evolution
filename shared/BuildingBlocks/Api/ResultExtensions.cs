using BuildingBlocks.Domain;
using Microsoft.AspNetCore.Http;

namespace BuildingBlocks.Api;

public static class ResultExtensions
{
    public static IResult ToHttpResult(this Result result, Func<IResult>? onSuccess = null)
    {
        if (result.IsSuccess) return onSuccess?.Invoke() ?? Results.NoContent();
        return MapError(result.Error);
    }

    public static IResult ToHttpResult<T>(this Result<T> result, Func<T, IResult>? onSuccess = null)
    {
        if (result.IsSuccess) return onSuccess?.Invoke(result.Value) ?? Results.Ok(result.Value);
        return MapError(result.Error);
    }

    public static IResult MapError(Error error)
    {
        var code = error.Code;
        if (code.EndsWith(".NotFound", StringComparison.Ordinal)) return Results.NotFound(new { error.Code, error.Message });
        if (code.EndsWith(".NotOwner", StringComparison.Ordinal)) return Results.Forbid();
        if (code.StartsWith("Stock.Insufficient", StringComparison.Ordinal)
            || code.StartsWith("Product.NotPublished", StringComparison.Ordinal)
            || code.EndsWith(".AlreadyCancelled", StringComparison.Ordinal)
            || code.EndsWith(".NotCancellable", StringComparison.Ordinal)
            || code.EndsWith(".NotPending", StringComparison.Ordinal))
            return Results.UnprocessableEntity(new { error.Code, error.Message });
        return Results.BadRequest(new { error.Code, error.Message });
    }
}
