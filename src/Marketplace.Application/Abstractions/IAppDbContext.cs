using Marketplace.Domain.Orders;
using Marketplace.Domain.Products;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Application.Abstractions;

public interface IAppDbContext
{
    DbSet<Product> Products { get; }
    DbSet<Order> Orders { get; }
}
