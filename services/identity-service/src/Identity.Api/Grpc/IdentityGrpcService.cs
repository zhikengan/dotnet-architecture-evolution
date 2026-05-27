using Grpc.Core;
using Identity.Api.Grpc;
using Identity.Application.Abstractions;
using Identity.Domain.Tenants;
using Identity.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Identity.Api.GrpcServices;

public sealed class IdentityGrpcService(IIdentityDbContext db) : Identity.Api.Grpc.IdentityService.IdentityServiceBase
{
    public override async Task<UserReply> GetUser(GetUserRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.UserId, out var id))
            return new UserReply { Found = false };

        var uid = new UserId(id);
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == uid, context.CancellationToken);
        if (user is null) return new UserReply { Found = false };

        return new UserReply
        {
            Found = true,
            UserId = user.Id.Value.ToString(),
            TenantId = user.TenantId.ToString(),
            Email = user.Email,
            Role = user.Role.ToString(),
        };
    }

    public override async Task<TenantReply> GetTenant(GetTenantRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.TenantId, out var id))
            return new TenantReply { Found = false };

        var tid = new TenantId(id);
        var tenant = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tid, context.CancellationToken);
        if (tenant is null) return new TenantReply { Found = false };

        return new TenantReply
        {
            Found = true,
            TenantId = tenant.Id.Value.ToString(),
            Name = tenant.Name,
        };
    }
}
