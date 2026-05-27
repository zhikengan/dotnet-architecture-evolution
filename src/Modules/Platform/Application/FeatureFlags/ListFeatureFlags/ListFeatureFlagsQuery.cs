using BuildingBlocks.Domain;
using MediatR;

namespace Platform.Application.FeatureFlags.ListFeatureFlags;

public sealed record FeatureFlagDto(string Name, bool Enabled, int RolloutPercentage, IReadOnlyList<Guid> EnabledUserIds, DateTime UpdatedAt);

public sealed record ListFeatureFlagsQuery : IRequest<Result<IReadOnlyList<FeatureFlagDto>>>;
