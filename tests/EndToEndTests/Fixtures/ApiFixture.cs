using System.Net.Http.Json;
using Catalog.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Orders.Infrastructure.Persistence;
using Platform.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace EndToEndTests.Fixtures;

public sealed class ApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .WithDatabase("marketplace_e2e")
        .WithUsername("e2e")
        .WithPassword("e2e")
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
                ["ConnectionStrings:Marketplace"] = _container.GetConnectionString(),
                ["FeatureFlags:CacheSeconds"] = "1",
                ["Outbox:PollIntervalMilliseconds"] = "200",
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
        await base.DisposeAsync();
        await _container.DisposeAsync();
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
        var catalog = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var orders = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        var platform = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

        orders.OutboxMessages.RemoveRange(orders.OutboxMessages);
        orders.InboxMessages.RemoveRange(orders.InboxMessages);
        orders.Orders.RemoveRange(orders.Orders);
        await orders.SaveChangesAsync();

        catalog.OutboxMessages.RemoveRange(catalog.OutboxMessages);
        catalog.InboxMessages.RemoveRange(catalog.InboxMessages);
        catalog.Products.RemoveRange(catalog.Products);
        await catalog.SaveChangesAsync();

        // Reseed Catalog
        await CatalogDataSeeder.SeedAsync(catalog);

        // Reset feature flag rollout to 0% and clear user opt-ins
        var flag = await platform.FeatureFlags.FirstOrDefaultAsync(f => f.Id == "EnablePremiumBadge");
        if (flag is null)
        {
            await PlatformDataSeeder.SeedAsync(platform);
            flag = await platform.FeatureFlags.FirstAsync(f => f.Id == "EnablePremiumBadge");
        }
        flag.SetRolloutPercentage(0, DateTime.UtcNow);
        // Clear EnabledUserIds by replacing the list. EF tracks the change via ValueComparer.
        var collection = typeof(global::Platform.Domain.FeatureFlags.FeatureFlag)
            .GetProperty(nameof(global::Platform.Domain.FeatureFlags.FeatureFlag.EnabledUserIds))!;
        collection.SetValue(flag, new List<Guid>());
        await platform.SaveChangesAsync();

        // Clear the feature-flag cache so the new rollout takes effect immediately.
        var cache = scope.ServiceProvider.GetRequiredService<IMemoryCache>();
        if (cache is MemoryCache concrete) concrete.Clear();

        // Capture deterministic ids
        WidgetId = (await catalog.Products.AsNoTracking().FirstAsync(p => p.Name == "Widget")).Id.Value;
        GizmoId = (await catalog.Products.AsNoTracking().FirstAsync(p => p.Name == "Gizmo")).Id.Value;
        DoohickeyId = (await catalog.Products.AsNoTracking().FirstAsync(p => p.Name == "Doohickey")).Id.Value;
    }

    /// <summary>Adds a suspended product so we can hit the "non-published" path.</summary>
    public async Task<Guid> AddSuspendedProductAsync(string name)
    {
        using var scope = Services.CreateScope();
        var catalog = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var p = global::Catalog.Domain.Products.Product
            .Create(name, global::Catalog.Domain.Products.Money.Usd(10m), 5, SellerId, DateTime.UtcNow).Value;
        p.Suspend();
        p.ClearDomainEvents();
        catalog.Products.Add(p);
        await catalog.SaveChangesAsync();
        return p.Id.Value;
    }

    /// <summary>Polls the buyer order endpoint until status matches or timeout.</summary>
    public async Task<string> WaitForOrderStatusAsync(Guid orderId, Guid asBuyer, string expected, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        var client = ClientFor("Buyer", asBuyer);
        while (DateTime.UtcNow < deadline)
        {
            var resp = await client.GetAsync($"/api/buyer/orders/{orderId}");
            if (resp.IsSuccessStatusCode)
            {
                var dto = await resp.Content.ReadFromJsonAsync<OrderProbe>();
                if (dto?.Status == expected) return expected;
            }
            await Task.Delay(150);
        }
        // Final probe so the assertion message is informative
        var final = await client.GetAsync($"/api/buyer/orders/{orderId}");
        var lastStatus = final.IsSuccessStatusCode ? (await final.Content.ReadFromJsonAsync<OrderProbe>())?.Status : final.StatusCode.ToString();
        return $"<timeout, last status={lastStatus}>";
    }

    public async Task<int> GetProductStockAsync(Guid productId)
    {
        using var scope = Services.CreateScope();
        var catalog = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var id = new global::Catalog.Domain.Products.ProductId(productId);
        var p = await catalog.Products.AsNoTracking().FirstAsync(x => x.Id == id);
        return p.Stock.Value;
    }

    private sealed record OrderProbe(Guid Id, Guid BuyerId, Guid ProductId, int Quantity, string Status, DateTime CreatedAt);
}

[CollectionDefinition(nameof(ApiCollection))]
public class ApiCollection : ICollectionFixture<ApiFixture>;
