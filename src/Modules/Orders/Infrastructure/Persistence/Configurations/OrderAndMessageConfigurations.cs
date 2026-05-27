using BuildingBlocks.Infrastructure.Inbox;
using BuildingBlocks.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orders.Domain.Orders;

namespace Orders.Infrastructure.Persistence.Configurations;

public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> e)
    {
        e.ToTable("orders");
        e.HasKey(o => o.Id);
        e.Property(o => o.Id).HasConversion(id => id.Value, v => new OrderId(v)).HasColumnName("id");
        e.Property(o => o.TenantId).HasColumnName("tenant_id").IsRequired();
        e.HasIndex(o => o.TenantId);
        e.Property(o => o.BuyerId).HasColumnName("buyer_id");
        e.Property(o => o.ProductId).HasColumnName("product_id");
        e.Property(o => o.Quantity).HasColumnName("quantity");
        e.Property(o => o.Status).HasConversion<int>().HasColumnName("status");
        e.Property(o => o.CreatedAt).HasColumnName("created_at");
        e.Property(o => o.FailureReason).HasColumnName("failure_reason").HasMaxLength(500);
        e.HasIndex(o => o.BuyerId);
        e.HasIndex(o => o.ProductId);
        e.Ignore(o => o.DomainEvents);
    }
}

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> e)
    {
        e.ToTable("outbox_messages");
        e.HasKey(m => m.Id);
        e.Property(m => m.Id).HasColumnName("id");
        e.Property(m => m.Type).HasColumnName("type").HasMaxLength(500).IsRequired();
        e.Property(m => m.Payload).HasColumnName("payload").HasColumnType("jsonb").IsRequired();
        e.Property(m => m.TenantId).HasColumnName("tenant_id");
        e.Property(m => m.OccurredAt).HasColumnName("occurred_at");
        e.Property(m => m.ProcessedAt).HasColumnName("processed_at");
        e.Property(m => m.RetryCount).HasColumnName("retry_count");
        e.Property(m => m.LastError).HasColumnName("last_error");
        e.HasIndex(m => m.ProcessedAt);
        e.HasIndex(m => m.TenantId);
    }
}

public sealed class InboxMessageConfiguration : IEntityTypeConfiguration<InboxMessage>
{
    public void Configure(EntityTypeBuilder<InboxMessage> e)
    {
        e.ToTable("inbox_messages");
        e.HasKey(m => new { m.MessageId, m.ConsumerName });
        e.Property(m => m.MessageId).HasColumnName("message_id");
        e.Property(m => m.ConsumerName).HasColumnName("consumer_name").HasMaxLength(200);
        e.Property(m => m.ProcessedAt).HasColumnName("processed_at");
    }
}
