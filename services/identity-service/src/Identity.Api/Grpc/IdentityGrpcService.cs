using Grpc.Core;
using Identity.Api.Grpc;
using Identity.Application.Tenants.Queries;
using Identity.Application.Users.Queries;
using MediatR;

namespace Identity.Api.GrpcServices;

/// <summary>
/// gRPC adapter — delegates to MediatR queries against the Application layer.
/// </summary>
public sealed class IdentityGrpcService(ISender sender) : Identity.Api.Grpc.IdentityService.IdentityServiceBase
{
    public override async Task<UserReply> GetUser(GetUserRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.UserId, out var id))
            return new UserReply { Found = false };

        var result = await sender.Send(new GetUserByIdQuery(id), context.CancellationToken);
        if (result.IsFailure) return new UserReply { Found = false };
        var u = result.Value;
        return new UserReply
        {
            Found = true,
            UserId = u.Id.ToString(),
            TenantId = u.TenantId.ToString(),
            Email = u.Email,
            Role = u.Role,
        };
    }

    public override async Task<TenantReply> GetTenant(GetTenantRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.TenantId, out var id))
            return new TenantReply { Found = false };

        var result = await sender.Send(new GetTenantByIdQuery(id), context.CancellationToken);
        if (result.IsFailure) return new TenantReply { Found = false };
        var t = result.Value;
        return new TenantReply
        {
            Found = true,
            TenantId = t.Id.ToString(),
            Name = t.Name,
        };
    }
}
