using Microsoft.EntityFrameworkCore;
using Platform.Domain.FeatureFlags;
using Platform.Domain.IdempotencyKeys;

namespace Platform.Application.Abstractions;

public interface IPlatformDbContext
{
    DbSet<FeatureFlag> FeatureFlags { get; }
    DbSet<IdempotencyKey> IdempotencyKeys { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
