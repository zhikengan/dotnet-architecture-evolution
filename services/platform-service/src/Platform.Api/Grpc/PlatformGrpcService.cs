using Grpc.Core;
using MediatR;
using Platform.Api.Grpc;
using Platform.Application.FeatureFlags.Queries;

namespace Platform.Api.GrpcServices;

/// <summary>
/// gRPC adapter — delegates to MediatR queries against the Application layer.
/// </summary>
public sealed class PlatformGrpcService(ISender sender) : Platform.Api.Grpc.PlatformService.PlatformServiceBase
{
    public override async Task<IsFeatureEnabledReply> IsFeatureEnabled(IsFeatureEnabledRequest request, ServerCallContext context)
    {
        Guid? userId = Guid.TryParse(request.UserId, out var u) ? u : null;
        Guid.TryParse(request.TenantId, out var tenantId);

        var result = await sender.Send(new IsFeatureEnabledQuery(tenantId, request.Key, userId), context.CancellationToken);
        return new IsFeatureEnabledReply { IsEnabled = result.IsSuccess && result.Value };
    }
}
