using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Platform.Domain.FeatureFlags;

namespace Platform.Infrastructure.Persistence.Configurations;

public sealed class FeatureFlagConfiguration : IEntityTypeConfiguration<FeatureFlag>
{
    public void Configure(EntityTypeBuilder<FeatureFlag> e)
    {
        e.ToTable("feature_flags");
        // Composite key (TenantId, Name) — each tenant has its own copy of any
        // given flag, so the same flag name can be in different rollout states
        // across tenants.
        e.HasKey(f => new { f.TenantId, f.Id });
        e.Property(f => f.Id).HasColumnName("name").HasMaxLength(100);
        e.Property(f => f.TenantId).HasColumnName("tenant_id").IsRequired();
        e.Property(f => f.Enabled).HasColumnName("enabled");
        e.Property(f => f.RolloutPercentage).HasColumnName("rollout_percentage");
        e.Property(f => f.EnabledUserIds)
            .HasColumnName("enabled_user_ids")
            .HasColumnType("jsonb")
            .HasConversion(
                v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                v => System.Text.Json.JsonSerializer.Deserialize<List<Guid>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new(),
                new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<List<Guid>>(
                    (a, b) => a!.SequenceEqual(b!),
                    v => v.Aggregate(0, (acc, id) => HashCode.Combine(acc, id.GetHashCode())),
                    v => v.ToList()));
        e.Property(f => f.UpdatedAt).HasColumnName("updated_at");
    }
}
