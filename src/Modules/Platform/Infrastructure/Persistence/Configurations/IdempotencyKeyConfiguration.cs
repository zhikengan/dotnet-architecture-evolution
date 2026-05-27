using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Platform.Domain.IdempotencyKeys;

namespace Platform.Infrastructure.Persistence.Configurations;

public sealed class IdempotencyKeyConfiguration : IEntityTypeConfiguration<IdempotencyKey>
{
    public void Configure(EntityTypeBuilder<IdempotencyKey> e)
    {
        e.ToTable("idempotency_keys");
        e.HasKey(k => k.Key);
        e.Property(k => k.Key).HasColumnName("key").HasMaxLength(200);
        e.Property(k => k.ResponseJson).HasColumnName("response_json");
        e.Property(k => k.CreatedAt).HasColumnName("created_at");
    }
}
