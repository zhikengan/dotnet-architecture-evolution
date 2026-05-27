using BuildingBlocks.Domain;
using Identity.Application.Abstractions;
using Identity.Application.Authentication;
using Identity.Domain.Users;
using Identity.Domain.Users.Errors;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Identity.Application.Users.Queries;

public sealed record IssueDemoTokenResult(string Token, Guid UserId, Guid TenantId, string Role);

/// <summary>
/// Looks up the seeded demo user, optionally overrides their role, and mints
/// an RS256 JWT signed by the local identity key. The signing key lives in
/// Infrastructure (<see cref="IJwtTokenIssuer"/>) — Application stays pure.
/// </summary>
public sealed record IssueDemoTokenQuery(Guid UserId, string? RoleOverride) : IRequest<Result<IssueDemoTokenResult>>;

public sealed class IssueDemoTokenHandler(IIdentityDbContext db, IJwtTokenIssuer issuer)
    : IRequestHandler<IssueDemoTokenQuery, Result<IssueDemoTokenResult>>
{
    public async Task<Result<IssueDemoTokenResult>> Handle(IssueDemoTokenQuery q, CancellationToken ct)
    {
        var uid = new UserId(q.UserId);
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == uid, ct);
        if (user is null) return Result.Failure<IssueDemoTokenResult>(UserErrors.NotFound);

        var role = string.IsNullOrWhiteSpace(q.RoleOverride) ? user.Role.ToString() : q.RoleOverride;
        var token = issuer.Issue(user.Id.Value, role, user.TenantId);
        return Result.Success(new IssueDemoTokenResult(token, user.Id.Value, user.TenantId, role));
    }
}
