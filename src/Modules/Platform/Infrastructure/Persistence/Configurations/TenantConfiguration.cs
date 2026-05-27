using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Platform.Domain.Tenants;

namespace Platform.Infrastructure.Persistence.Configurations;

public sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> e)
    {
        e.ToTable("tenants");
        e.HasKey(t => t.Id);
        e.Property(t => t.Id).HasColumnName("id");
        e.Property(t => t.Slug).HasColumnName("slug").HasMaxLength(50).IsRequired();
        e.HasIndex(t => t.Slug).IsUnique();
        e.Property(t => t.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        e.Property(t => t.CreatedAt).HasColumnName("created_at");
    }
}
