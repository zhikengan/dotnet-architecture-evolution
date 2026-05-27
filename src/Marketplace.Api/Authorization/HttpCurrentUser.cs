using Marketplace.Application.Abstractions;

namespace Marketplace.Api.Authorization;

public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public bool IsAuthenticated => UserId != Guid.Empty && !string.IsNullOrEmpty(Role);

    public Guid UserId
    {
        get
        {
            var raw = accessor.HttpContext?.Request.Headers["X-User-Id"].ToString();
            return Guid.TryParse(raw, out var g) ? g : Guid.Empty;
        }
    }

    public string Role => accessor.HttpContext?.Request.Headers["X-User-Role"].ToString() ?? string.Empty;
}
