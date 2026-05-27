using Marketplace.Domain.Products;
using Marketplace.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace Marketplace.Api.Tests.Common;

public sealed class ApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .WithDatabase("marketplace_api_test")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    public static readonly Guid SellerId = new("11111111-1111-1111-1111-111111111111");
    public static readonly Guid BuyerId = new("22222222-2222-2222-2222-222222222222");
    public static readonly Guid OtherBuyerId = new("44444444-4444-4444-4444-444444444444");
    public static readonly Guid AdminId = new("33333333-3333-3333-3333-333333333333");

    public Guid WidgetId { get; private set; }
    public Guid GizmoId { get; private set; }
    public Guid DoohickeyId { get; private set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = _container.GetConnectionString(),
                ["App:Name"] = "MarketplaceTest",
                ["App:SeedOnStartup"] = "false",
            });
        });
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        _ = Services;
        await ResetAsync();
    }

    public new async Task DisposeAsync()
    {
        await _container.DisposeAsync();
        await base.DisposeAsync();
    }

    public HttpClient ClientFor(string role, Guid userId)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Role", role);
        client.DefaultRequestHeaders.Add("X-User-Id", userId.ToString());
        return client;
    }

    public HttpClient AnonymousClient() => CreateClient();

    public async Task ResetAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Orders.RemoveRange(db.Orders);
        db.Products.RemoveRange(db.Products);
        await db.SaveChangesAsync();

        await DataSeeder.SeedAsync(db);

        WidgetId = (await db.Products.AsNoTracking().FirstAsync(p => p.Name == "Widget")).Id.Value;
        GizmoId = (await db.Products.AsNoTracking().FirstAsync(p => p.Name == "Gizmo")).Id.Value;
        DoohickeyId = (await db.Products.AsNoTracking().FirstAsync(p => p.Name == "Doohickey")).Id.Value;
    }

    public async Task<Guid> AddSuspendedProductAsync(string name)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var p = Product.Create(name, Money.Usd(10m), 5, SellerId, DateTime.UtcNow).Value;
        p.Suspend();
        p.ClearDomainEvents();
        db.Products.Add(p);
        await db.SaveChangesAsync();
        return p.Id.Value;
    }
}
