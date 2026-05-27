using BuildingBlocks.Domain;
using Identity.Application.Abstractions;
using Identity.Domain.Tenants;
using Identity.Domain.Tenants.Errors;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Identity.Application.Tenants.Queries;

public sealed record TenantDto(Guid Id, string Name);

public sealed record GetTenantByIdQuery(Guid TenantId) : IRequest<Result<TenantDto>>;

public sealed class GetTenantByIdHandler(IIdentityDbContext db)
    : IRequestHandler<GetTenantByIdQuery, Result<TenantDto>>
{
    public async Task<Result<TenantDto>> Handle(GetTenantByIdQuery q, CancellationToken ct)
    {
        var tid = new TenantId(q.TenantId);
        var t = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(x => x.Id == tid, ct);
        if (t is null) return Result.Failure<TenantDto>(TenantErrors.NotFound);
        return Result.Success(new TenantDto(t.Id.Value, t.Name));
    }
}
