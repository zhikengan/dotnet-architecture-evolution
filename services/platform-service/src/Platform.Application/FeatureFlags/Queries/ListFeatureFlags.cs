using BuildingBlocks.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Platform.Application.Abstractions;

namespace Platform.Application.FeatureFlags.Queries;

public sealed record FeatureFlagDto(Guid Id, Guid TenantId, string Key, bool IsEnabled, DateTime UpdatedAt);

public sealed record ListFeatureFlagsQuery : IRequest<Result<IReadOnlyList<FeatureFlagDto>>>;

public sealed class ListFeatureFlagsHandler(IPlatformDbContext db)
    : IRequestHandler<ListFeatureFlagsQuery, Result<IReadOnlyList<FeatureFlagDto>>>
{
    public async Task<Result<IReadOnlyList<FeatureFlagDto>>> Handle(ListFeatureFlagsQuery _, CancellationToken ct)
    {
        var flags = await db.FeatureFlags.AsNoTracking()
            .OrderBy(f => f.Key)
            .ToListAsync(ct);
        IReadOnlyList<FeatureFlagDto> dtos = flags
            .Select(f => new FeatureFlagDto(f.Id.Value, f.TenantId, f.Key, f.IsEnabled, f.UpdatedAt))
            .ToList();
        return Result.Success(dtos);
    }
}
