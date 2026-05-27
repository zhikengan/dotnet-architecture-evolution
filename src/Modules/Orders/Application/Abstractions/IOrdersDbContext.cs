using BuildingBlocks.Infrastructure.Inbox;
using BuildingBlocks.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using Orders.Domain.Orders;

namespace Orders.Application.Abstractions;

public interface IOrdersDbContext
{
    DbSet<Order> Orders { get; }
    DbSet<OutboxMessage> OutboxMessages { get; }
    DbSet<InboxMessage> InboxMessages { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
