using BuildingBlocks.Infrastructure.Inbox;
using BuildingBlocks.Infrastructure.Outbox;
using Catalog.Domain.Products;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Application.Abstractions;

public interface ICatalogDbContext
{
    DbSet<Product> Products { get; }
    DbSet<OutboxMessage> OutboxMessages { get; }
    DbSet<InboxMessage> InboxMessages { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
