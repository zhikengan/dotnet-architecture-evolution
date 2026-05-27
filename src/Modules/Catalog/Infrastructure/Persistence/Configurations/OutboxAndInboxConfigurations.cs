using BuildingBlocks.Infrastructure.Inbox;
using BuildingBlocks.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Infrastructure.Persistence.Configurations;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> e)
    {
        e.ToTable("outbox_messages");
        e.HasKey(m => m.Id);
        e.Property(m => m.Id).HasColumnName("id");
        e.Property(m => m.Type).HasColumnName("type").HasMaxLength(500).IsRequired();
        e.Property(m => m.Payload).HasColumnName("payload").HasColumnType("jsonb").IsRequired();
        e.Property(m => m.OccurredAt).HasColumnName("occurred_at");
        e.Property(m => m.ProcessedAt).HasColumnName("processed_at");
        e.Property(m => m.RetryCount).HasColumnName("retry_count");
        e.Property(m => m.LastError).HasColumnName("last_error");
        e.HasIndex(m => m.ProcessedAt);
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
