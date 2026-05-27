using System.Net;
using System.Net.Http.Json;
using Marketplace.Data;
using Marketplace.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Marketplace.SmokeTests;

public sealed class HappyPathTests : IClassFixture<TestFixture>, IAsyncLifetime
{
    private readonly TestFixture _fixture;

    private static readonly Guid JohnBuyerId = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid RootAdminId = new("33333333-3333-3333-3333-333333333333");

    public HappyPathTests(TestFixture fixture) => _fixture = fixture;

    public Task InitializeAsync()
    {
        _fixture.ResetDatabase();
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private HttpClient ClientFor(string role, Guid userId)
    {
        var client = _fixture.CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Role", role);
        client.DefaultRequestHeaders.Add("X-User-Id", userId.ToString());
        return client;
    }

    [Fact]
    public async Task S1_seller_creates_product_appears_in_admin_list()
    {
        var seller = ClientFor("Seller", DataSeeder.AcmeSellerId);
        var resp = await seller.PostAsJsonAsync("/api/seller/products",
            new { name = "Test Widget", price = 12.50m, stock = 5 });
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var created = await resp.Content.ReadFromJsonAsync<Product>();
        Assert.NotNull(created);

        var admin = ClientFor("Admin", RootAdminId);
        var listResp = await admin.GetAsync("/api/admin/products");
        listResp.EnsureSuccessStatusCode();
        var products = await listResp.Content.ReadFromJsonAsync<List<Product>>();
        Assert.NotNull(products);
        Assert.Contains(products!, p => p.Id == created!.Id && p.Name == "Test Widget");
    }

    [Fact]
    public async Task S4_buyer_places_order_stock_decrements()
    {
        var buyer = ClientFor("Buyer", JohnBuyerId);
        var resp = await buyer.PostAsJsonAsync("/api/buyer/orders",
            new { productId = DataSeeder.WidgetId, quantity = 3 });
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var order = await resp.Content.ReadFromJsonAsync<Order>();
        Assert.NotNull(order);
        Assert.Equal(OrderStatus.Confirmed, order!.Status);

        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var widget = await db.Products.AsNoTracking().FirstAsync(p => p.Id == DataSeeder.WidgetId);
        Assert.Equal(100 - 3, widget.Stock);
    }

    [Fact]
    public async Task S5_insufficient_stock_returns_422()
    {
        var buyer = ClientFor("Buyer", JohnBuyerId);
        var resp = await buyer.PostAsJsonAsync("/api/buyer/orders",
            new { productId = DataSeeder.DoohickeyId, quantity = 1 });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, resp.StatusCode);

        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var doohickey = await db.Products.AsNoTracking().FirstAsync(p => p.Id == DataSeeder.DoohickeyId);
        Assert.Equal(0, doohickey.Stock);

        var failedOrder = await db.Orders.AsNoTracking()
            .FirstAsync(o => o.ProductId == DataSeeder.DoohickeyId);
        Assert.Equal(OrderStatus.Failed, failedOrder.Status);
    }

    [Fact]
    public async Task S7_buyer_cancels_own_order_stock_returns()
    {
        var buyer = ClientFor("Buyer", JohnBuyerId);
        var place = await buyer.PostAsJsonAsync("/api/buyer/orders",
            new { productId = DataSeeder.GizmoId, quantity = 4 });
        place.EnsureSuccessStatusCode();
        var order = await place.Content.ReadFromJsonAsync<Order>();
        Assert.NotNull(order);

        var cancel = await buyer.PostAsync($"/api/buyer/orders/{order!.Id}/cancel", content: null);
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        var cancelled = await cancel.Content.ReadFromJsonAsync<Order>();
        Assert.NotNull(cancelled);
        Assert.Equal(OrderStatus.Cancelled, cancelled!.Status);

        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var gizmo = await db.Products.AsNoTracking().FirstAsync(p => p.Id == DataSeeder.GizmoId);
        Assert.Equal(50, gizmo.Stock);
    }

    [Fact]
    public async Task S9_admin_force_cancels_returns_stock()
    {
        var buyer = ClientFor("Buyer", JohnBuyerId);
        var place = await buyer.PostAsJsonAsync("/api/buyer/orders",
            new { productId = DataSeeder.WidgetId, quantity = 7 });
        place.EnsureSuccessStatusCode();
        var order = await place.Content.ReadFromJsonAsync<Order>();
        Assert.NotNull(order);

        var admin = ClientFor("Admin", RootAdminId);
        var cancel = await admin.PostAsync($"/api/admin/orders/{order!.Id}/cancel", content: null);
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);

        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var widget = await db.Products.AsNoTracking().FirstAsync(p => p.Id == DataSeeder.WidgetId);
        Assert.Equal(100, widget.Stock);
        var cancelledOrder = await db.Orders.AsNoTracking().FirstAsync(o => o.Id == order.Id);
        Assert.Equal(OrderStatus.Cancelled, cancelledOrder.Status);
    }
}
