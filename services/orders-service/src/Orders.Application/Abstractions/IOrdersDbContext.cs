using Microsoft.EntityFrameworkCore;
using Orders.Domain.Orders;

namespace Orders.Application.Abstractions;

public interface IOrdersDbContext
{
    DbSet<Order> Orders { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
