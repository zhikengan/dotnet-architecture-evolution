using BuildingBlocks.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Platform.Application.Abstractions;

namespace Platform.Application.FeatureFlags.ListFeatureFlags;

public sealed class ListFeatureFlagsHandler(IPlatformDbContext db) : IRequestHandler<ListFeatureFlagsQuery, Result<IReadOnlyList<FeatureFlagDto>>>
{
    public async Task<Result<IReadOnlyList<FeatureFlagDto>>> Handle(ListFeatureFlagsQuery query, CancellationToken ct)
    {
        var flags = await db.FeatureFlags.AsNoTracking().OrderBy(f => f.Id).ToListAsync(ct);
        IReadOnlyList<FeatureFlagDto> dtos = flags
            .Select(f => new FeatureFlagDto(f.Id, f.Enabled, f.RolloutPercentage, f.EnabledUserIds, f.UpdatedAt))
            .ToList();
        return Result.Success(dtos);
    }
}
