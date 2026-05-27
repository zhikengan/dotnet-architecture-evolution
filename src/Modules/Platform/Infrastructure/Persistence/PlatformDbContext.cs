using BuildingBlocks.Application.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Platform.Application.Abstractions;
using Platform.Domain.FeatureFlags;
using Platform.Domain.Emails;
using Platform.Domain.IdempotencyKeys;
using Platform.Domain.Reporting;
using Platform.Domain.Tenants;

namespace Platform.Infrastructure.Persistence;

public sealed class PlatformDbContext(DbContextOptions<PlatformDbContext> options, ITenantContext tenant) : DbContext(options), IPlatformDbContext
{
    public const string Schema = "platform";

    private readonly ITenantContext _tenant = tenant;

    public DbSet<FeatureFlag> FeatureFlags => Set<FeatureFlag>();
    public DbSet<IdempotencyKey> IdempotencyKeys => Set<IdempotencyKey>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<DailyReport> DailyReports => Set<DailyReport>();
    public DbSet<SentEmail> SentEmails => Set<SentEmail>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Tenant query filters capture per-instance state; suppress the
        // pending-changes warning since the runtime model is correct.
        optionsBuilder.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PlatformDbContext).Assembly);

        // FeatureFlag is tenant-scoped; Tenants is the global directory and
        // IdempotencyKey is keyed off the request's idempotency token so it
        // doesn't need a per-tenant filter at this tier.
        modelBuilder.Entity<FeatureFlag>().HasQueryFilter(f => f.TenantId == _tenant.TenantId);
    }
}
