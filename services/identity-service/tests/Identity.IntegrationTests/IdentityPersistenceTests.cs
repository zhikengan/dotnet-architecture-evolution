using BuildingBlocks.Infrastructure.Time;
using Identity.Domain.Users;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Identity.IntegrationTests;

public sealed class IdentityDbFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .WithDatabase("identity_test")
        .WithUsername("identity")
        .WithPassword("identity")
        .Build();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var db = NewContext();
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    public IdentityDbContext NewContext()
    {
        var opt = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .Options;
        return new IdentityDbContext(opt);
    }
}

[CollectionDefinition(nameof(IdentityDbCollection))]
public class IdentityDbCollection : ICollectionFixture<IdentityDbFixture>;

[Collection(nameof(IdentityDbCollection))]
public class IdentityPersistenceTests(IdentityDbFixture fx)
{
    [Fact]
    public async Task Seeder_creates_the_known_users_and_tenants()
    {
        await using (var db = fx.NewContext())
        {
            await IdentityDataSeeder.SeedAsync(db, new SystemClock());
        }

        await using var read = fx.NewContext();
        (await read.Tenants.CountAsync()).Should().BeGreaterOrEqualTo(2);
        var users = await read.Users.AsNoTracking().ToListAsync();
        users.Should().Contain(u => u.Role == UserRole.Seller);
        users.Should().Contain(u => u.Role == UserRole.Buyer);
        users.Should().Contain(u => u.Role == UserRole.Admin);
    }

    [Fact]
    public async Task Seeder_is_idempotent()
    {
        await using (var db = fx.NewContext())
        {
            await IdentityDataSeeder.SeedAsync(db, new SystemClock());
            await IdentityDataSeeder.SeedAsync(db, new SystemClock());
        }

        await using var read = fx.NewContext();
        (await read.Users.CountAsync()).Should().Be(3); // Seller, Buyer, Admin — not duplicated.
        (await read.Tenants.CountAsync()).Should().Be(2);
    }
}
