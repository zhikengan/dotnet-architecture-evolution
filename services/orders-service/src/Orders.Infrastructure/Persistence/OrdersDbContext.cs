using BuildingBlocks.Application;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Orders.Application.Abstractions;
using Orders.Domain.Orders;

namespace Orders.Infrastructure.Persistence;

public sealed class OrdersDbContext(DbContextOptions<OrdersDbContext> options, ITenantContext tenant)
    : DbContext(options), IOrdersDbContext
{
    public const string Schema = "orders";

    private readonly ITenantContext _tenant = tenant;

    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.HasDefaultSchema(Schema);

        // MassTransit EF Core outbox + inbox tables (required for UseBusOutbox at runtime).
        mb.AddInboxStateEntity();
        mb.AddOutboxStateEntity();
        mb.AddOutboxMessageEntity();

        mb.Entity<Order>(b =>
        {
            b.ToTable("orders");
            b.HasKey(o => o.Id);
            b.Property(o => o.Id).HasConversion(v => v.Value, v => new OrderId(v));
            b.Property(o => o.TenantId);
            b.Property(o => o.BuyerId);
            b.Property(o => o.ProductId);
            b.Property(o => o.Quantity);
            b.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);
            b.Property(o => o.CreatedAt);
            b.Property(o => o.FailureReason).HasMaxLength(500);
            b.Ignore(o => o.DomainEvents);

            b.HasQueryFilter(o => o.TenantId == _tenant.TenantId);
        });
    }
}
