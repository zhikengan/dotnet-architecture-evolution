using BuildingBlocks.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Platform.Application.Abstractions;

namespace Platform.Application.FeatureFlags.Queries;

public sealed record IsFeatureEnabledQuery(Guid TenantId, string Key, Guid? UserId) : IRequest<Result<bool>>;

public sealed class IsFeatureEnabledHandler(IPlatformDbContext db)
    : IRequestHandler<IsFeatureEnabledQuery, Result<bool>>
{
    public async Task<Result<bool>> Handle(IsFeatureEnabledQuery q, CancellationToken ct)
    {
        if (q.TenantId == Guid.Empty || string.IsNullOrWhiteSpace(q.Key))
            return Result.Success(false);

        var flag = await db.FeatureFlags.AsNoTracking()
            .FirstOrDefaultAsync(f => f.TenantId == q.TenantId && f.Key == q.Key, ct);
        return Result.Success(flag is { IsEnabled: true });
    }
}
