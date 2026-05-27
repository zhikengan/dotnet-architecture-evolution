using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Platform.Api.Grpc;
using Platform.Application.Abstractions;

namespace Platform.Api.GrpcServices;

public sealed class PlatformGrpcService(IPlatformDbContext db) : Platform.Api.Grpc.PlatformService.PlatformServiceBase
{
    public override async Task<IsFeatureEnabledReply> IsFeatureEnabled(IsFeatureEnabledRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.TenantId, out var tenantId))
            return new IsFeatureEnabledReply { IsEnabled = false };

        var flag = await db.FeatureFlags.AsNoTracking()
            .FirstOrDefaultAsync(f => f.TenantId == tenantId && f.Key == request.Key, context.CancellationToken);
        return new IsFeatureEnabledReply { IsEnabled = flag is { IsEnabled: true } };
    }
}
