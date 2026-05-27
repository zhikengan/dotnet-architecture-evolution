using Microsoft.EntityFrameworkCore;
using Platform.Application.Abstractions;
using Platform.Domain.FeatureFlags;
using Platform.Domain.IdempotencyKeys;

namespace Platform.Infrastructure.Persistence;

public sealed class PlatformDbContext(DbContextOptions<PlatformDbContext> options) : DbContext(options), IPlatformDbContext
{
    public const string Schema = "platform";

    public DbSet<FeatureFlag> FeatureFlags => Set<FeatureFlag>();
    public DbSet<IdempotencyKey> IdempotencyKeys => Set<IdempotencyKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PlatformDbContext).Assembly);
    }
}
