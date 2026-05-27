using Catalog.Domain.Products;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Infrastructure.Persistence.Configurations;

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> e)
    {
        e.ToTable("products");
        e.HasKey(p => p.Id);
        e.Property(p => p.Id).HasConversion(id => id.Value, v => new ProductId(v)).HasColumnName("id");
        e.Property(p => p.TenantId).HasColumnName("tenant_id").IsRequired();
        e.HasIndex(p => p.TenantId);
        e.Property(p => p.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        e.OwnsOne(p => p.Price, m =>
        {
            m.Property(x => x.Amount).HasColumnName("price_amount").HasColumnType("numeric(18,2)");
            m.Property(x => x.Currency).HasColumnName("price_currency").HasMaxLength(3).IsRequired();
        });
        e.Property(p => p.Stock).HasConversion(s => s.Value, v => Stock.Create(v).Value).HasColumnName("stock");
        e.Property(p => p.Status).HasConversion<int>().HasColumnName("status");
        e.Property(p => p.SellerId).HasColumnName("seller_id");
        e.Property(p => p.CreatedAt).HasColumnName("created_at");
        e.Ignore(p => p.DomainEvents);
    }
}
