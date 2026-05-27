using Microsoft.EntityFrameworkCore;
using Platform.Domain.FeatureFlags;
using Platform.Domain.IdempotencyKeys;
using Platform.Domain.Reporting;
using Platform.Domain.Tenants;

namespace Platform.Application.Abstractions;

public interface IPlatformDbContext
{
    DbSet<FeatureFlag> FeatureFlags { get; }
    DbSet<IdempotencyKey> IdempotencyKeys { get; }
    DbSet<Tenant> Tenants { get; }
    DbSet<DailyReport> DailyReports { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
