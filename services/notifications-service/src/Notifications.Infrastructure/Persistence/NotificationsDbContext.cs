using Microsoft.EntityFrameworkCore;
using Notifications.Application.Abstractions;
using Notifications.Domain.Notifications;

namespace Notifications.Infrastructure.Persistence;

public sealed class NotificationsDbContext(DbContextOptions<NotificationsDbContext> options) : DbContext(options), INotificationsDbContext
{
    public const string Schema = "notifications";

    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.HasDefaultSchema(Schema);
        mb.Entity<Notification>(b =>
        {
            b.ToTable("notifications");
            b.HasKey(n => n.Id);
            b.Property(n => n.Id).HasConversion(v => v.Value, v => new NotificationId(v));
            b.Property(n => n.TenantId);
            b.Property(n => n.Type).HasMaxLength(50).IsRequired();
            b.Property(n => n.Recipient).HasMaxLength(200);
            b.Property(n => n.RelatedOrderId);
            b.Property(n => n.Body).HasMaxLength(1000);
            b.Property(n => n.SentAt);
            b.HasIndex(n => n.RelatedOrderId);
            b.Ignore(n => n.DomainEvents);
        });
    }
}
