using Catalog.Domain.Products;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Application.Abstractions;

public interface ICatalogDbContext
{
    DbSet<Product> Products { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
