using BuildingBlocks.Domain;
using Identity.Application.Abstractions;
using Identity.Domain.Users;
using Identity.Domain.Users.Errors;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Identity.Application.Users.Queries;

public sealed record UserDto(Guid Id, Guid TenantId, string Email, string Role);

public sealed record GetUserByIdQuery(Guid UserId) : IRequest<Result<UserDto>>;

public sealed class GetUserByIdHandler(IIdentityDbContext db)
    : IRequestHandler<GetUserByIdQuery, Result<UserDto>>
{
    public async Task<Result<UserDto>> Handle(GetUserByIdQuery q, CancellationToken ct)
    {
        var uid = new UserId(q.UserId);
        var u = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == uid, ct);
        if (u is null) return Result.Failure<UserDto>(UserErrors.NotFound);
        return Result.Success(new UserDto(u.Id.Value, u.TenantId, u.Email, u.Role.ToString()));
    }
}
