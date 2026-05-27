using BuildingBlocks.Application.MultiTenancy;
using BuildingBlocks.Infrastructure.Inbox;
using BuildingBlocks.Infrastructure.Outbox;
using Catalog.Application.Abstractions;
using Catalog.Domain.Products;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Catalog.Infrastructure.Persistence;

public sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options, ITenantContext tenant) : DbContext(options), ICatalogDbContext
{
    public const string Schema = "catalog";

    private readonly ITenantContext _tenant = tenant;

    public DbSet<Product> Products => Set<Product>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Tenant query filters capture per-instance state, which EF Core's
        // model-change detection flags as a pending change. The runtime model
        // is still correct; the warning is noise.
        optionsBuilder.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CatalogDbContext).Assembly);

        // Global tenant query filter. Reads via _tenant (captured by EF's filter
        // expression as a closure) so each query sees the current tenant. Outbox
        // / inbox tables are deliberately NOT filtered — operators need to see
        // every tenant's pending messages.
        modelBuilder.Entity<Product>().HasQueryFilter(p => p.TenantId == _tenant.TenantId);
    }
}
