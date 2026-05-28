using BuildingBlocks.Application;
using Catalog.Application.Abstractions;
using Catalog.Domain.Products;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Infrastructure.Persistence;

public sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options, ITenantContext tenant)
    : DbContext(options), ICatalogDbContext
{
    public const string Schema = "catalog";

    private readonly ITenantContext _tenant = tenant;

    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.HasDefaultSchema(Schema);

        // MassTransit EF Core outbox + inbox tables. AddEntityFrameworkOutbox
        // configures DI but does NOT inject entities into the EF model — that
        // is on us. Without these three lines, the bus outbox fails at runtime
        // with "relation OutboxState does not exist".
        mb.AddInboxStateEntity();
        mb.AddOutboxStateEntity();
        mb.AddOutboxMessageEntity();

        mb.Entity<Product>(b =>
        {
            b.ToTable("products");
            b.HasKey(p => p.Id);
            b.Property(p => p.Id).HasConversion(v => v.Value, v => new ProductId(v));
            b.Property(p => p.TenantId);
            b.Property(p => p.Name).HasMaxLength(200).IsRequired();
            b.OwnsOne(p => p.Price, pb =>
            {
                pb.Property(m => m.Amount).HasColumnName("price_amount").HasColumnType("numeric(18,2)");
                pb.Property(m => m.Currency).HasColumnName("price_currency").HasMaxLength(3);
            });
            b.Property(p => p.Stock).HasConversion(s => s.Value, v => Stock.Create(v).Value);
            b.Property(p => p.SellerId);
            b.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);
            b.Property(p => p.CreatedAt);
            b.Ignore(p => p.DomainEvents);

            // Tenant query filter — auto-scopes reads to the current tenant.
            // Consumers and seeders that need to bypass this call IgnoreQueryFilters().
            b.HasQueryFilter(p => p.TenantId == _tenant.TenantId);
        });
    }
}
