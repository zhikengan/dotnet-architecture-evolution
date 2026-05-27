using Identity.Domain.Tenants;
using Identity.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Persistence;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("users");
        b.HasKey(u => u.Id);
        b.Property(u => u.Id).HasConversion(v => v.Value, v => new UserId(v));
        b.Property(u => u.TenantId).IsRequired();
        b.Property(u => u.Email).IsRequired().HasMaxLength(200);
        b.Property(u => u.Role).HasConversion<string>().HasMaxLength(20);
        b.Property(u => u.CreatedAt);
        b.HasIndex(u => u.Email).IsUnique();
        b.Ignore(u => u.DomainEvents);
    }
}

public sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> b)
    {
        b.ToTable("tenants");
        b.HasKey(t => t.Id);
        b.Property(t => t.Id).HasConversion(v => v.Value, v => new TenantId(v));
        b.Property(t => t.Name).IsRequired().HasMaxLength(100);
        b.Property(t => t.CreatedAt);
        b.Ignore(t => t.DomainEvents);
    }
}
