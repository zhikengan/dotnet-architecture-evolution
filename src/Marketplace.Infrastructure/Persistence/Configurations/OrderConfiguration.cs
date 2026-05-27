using Marketplace.Domain.Orders;
using Marketplace.Domain.Products;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Persistence.Configurations;

public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> e)
    {
        e.ToTable("orders");

        e.HasKey(o => o.Id);
        e.Property(o => o.Id)
            .HasConversion(id => id.Value, v => new OrderId(v))
            .HasColumnName("id");

        e.Property(o => o.BuyerId).HasColumnName("buyer_id");

        e.Property(o => o.ProductId)
            .HasConversion(id => id.Value, v => new ProductId(v))
            .HasColumnName("product_id");

        e.Property(o => o.Quantity)
            .HasConversion(q => q.Value, v => Quantity.Create(v).Value)
            .HasColumnName("quantity");

        e.Property(o => o.Status).HasConversion<int>().HasColumnName("status");
        e.Property(o => o.CreatedAt).HasColumnName("created_at");
        e.Property(o => o.FailureReason).HasColumnName("failure_reason").HasMaxLength(500);

        e.HasIndex(o => o.BuyerId);
        e.HasIndex(o => o.ProductId);

        e.Ignore(o => o.DomainEvents);
    }
}
