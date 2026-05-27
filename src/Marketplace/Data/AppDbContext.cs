using Microsoft.EntityFrameworkCore;

namespace Marketplace.Data;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }
}
