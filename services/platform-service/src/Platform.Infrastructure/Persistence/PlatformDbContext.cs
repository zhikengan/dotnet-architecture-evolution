using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Platform.Application.Abstractions;
using Platform.Domain.FeatureFlags;
using Platform.Domain.IdempotencyKeys;

namespace Platform.Infrastructure.Persistence;

public sealed class PlatformDbContext(DbContextOptions<PlatformDbContext> options) : DbContext(options), IPlatformDbContext
{
    public const string Schema = "platform";

    public DbSet<FeatureFlag> FeatureFlags => Set<FeatureFlag>();
    public DbSet<IdempotencyKey> IdempotencyKeys => Set<IdempotencyKey>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.HasDefaultSchema(Schema);

        // MassTransit EF Core outbox + inbox tables (required for UseBusOutbox at runtime).
        mb.AddInboxStateEntity();
        mb.AddOutboxStateEntity();
        mb.AddOutboxMessageEntity();

        mb.Entity<FeatureFlag>(b =>
        {
            b.ToTable("feature_flags");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasConversion(v => v.Value, v => new FeatureFlagId(v));
            b.Property(x => x.TenantId);
            b.Property(x => x.Key).HasMaxLength(120).IsRequired();
            b.Property(x => x.IsEnabled);
            b.Property(x => x.RolloutPercentage);
            b.Property(x => x.EnabledUserIds)
                .HasColumnName("enabled_user_ids")
                .HasColumnType("jsonb")
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                    v => System.Text.Json.JsonSerializer.Deserialize<List<Guid>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new(),
                    new ValueComparer<List<Guid>>(
                        (a, c) => a!.SequenceEqual(c!),
                        v => v.Aggregate(0, (acc, id) => HashCode.Combine(acc, id.GetHashCode())),
                        v => v.ToList()));
            b.Property(x => x.UpdatedAt);
            b.HasIndex(x => new { x.TenantId, x.Key }).IsUnique();
            b.Ignore(x => x.DomainEvents);
        });

        mb.Entity<IdempotencyKey>(b =>
        {
            b.ToTable("idempotency_keys");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasMaxLength(200);
            b.Property(x => x.TenantId);
            b.Property(x => x.ResultJson);
            b.Property(x => x.SeenAt);
            b.Ignore("DomainEvents");
        });
    }
}
