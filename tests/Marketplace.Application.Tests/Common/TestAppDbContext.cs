using Marketplace.Application.Abstractions;
using Marketplace.Domain.Orders;
using Marketplace.Domain.Products;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Application.Tests.Common;

public sealed class TestAppDbContext(DbContextOptions<TestAppDbContext> options)
    : DbContext(options), IAppDbContext
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Product>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.Id).HasConversion(id => id.Value, v => new ProductId(v));
            e.Property(p => p.Status).HasConversion(s => s.Value, v => ProductStatus.FromValue(v));
            e.Property(p => p.Stock).HasConversion(s => s.Value, v => Stock.Create(v).Value);
            e.OwnsOne(p => p.Price, m =>
            {
                m.Property(x => x.Amount);
                m.Property(x => x.Currency);
            });
            e.Ignore(p => p.DomainEvents);
        });

        b.Entity<Order>(e =>
        {
            e.HasKey(o => o.Id);
            e.Property(o => o.Id).HasConversion(id => id.Value, v => new OrderId(v));
            e.Property(o => o.ProductId).HasConversion(id => id.Value, v => new ProductId(v));
            e.Property(o => o.Quantity).HasConversion(q => q.Value, v => Quantity.Create(v).Value);
            e.Ignore(o => o.DomainEvents);
        });
    }
}
