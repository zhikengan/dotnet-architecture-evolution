using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Platform.Domain.Emails;

namespace Platform.Infrastructure.Persistence.Configurations;

public sealed class SentEmailConfiguration : IEntityTypeConfiguration<SentEmail>
{
    public void Configure(EntityTypeBuilder<SentEmail> e)
    {
        e.ToTable("sent_emails");
        e.HasKey(s => s.Id);
        e.Property(s => s.Id).HasColumnName("id");
        e.Property(s => s.TenantId).HasColumnName("tenant_id");
        e.Property(s => s.Template).HasColumnName("template").HasMaxLength(100);
        e.Property(s => s.Recipient).HasColumnName("recipient").HasMaxLength(500);
        e.Property(s => s.RelatedEntityId).HasColumnName("related_entity_id");
        e.Property(s => s.SentAt).HasColumnName("sent_at");
        e.HasIndex(s => s.TenantId);
    }
}
