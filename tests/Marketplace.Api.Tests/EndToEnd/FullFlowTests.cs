using System.Net;
using System.Net.Http.Json;
using Marketplace.Api.Tests.Common;
using Marketplace.Domain.Orders;
using Marketplace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Marketplace.Api.Tests.EndToEnd;

public class FullFlowTests(ApiFixture fx) : IClassFixture<ApiFixture>, IAsyncLifetime
{
    public Task InitializeAsync() => fx.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // ---------- S1: Create product, valid data -> 201 + visible in admin list

    [Fact]
    public async Task S1_seller_creates_product_appears_in_admin_list()
    {
        var seller = fx.ClientFor("Seller", ApiFixture.SellerId);
        var resp = await seller.PostAsJsonAsync("/api/seller/products",
            new { name = "Test Widget", price = 12.50m, stock = 5 });

        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await resp.Content.ReadFromJsonAsync<CreateProductResponse>();
        created.Should().NotBeNull();

        var admin = fx.ClientFor("Admin", ApiFixture.AdminId);
        var list = await admin.GetFromJsonAsync<AdminProductResponse[]>("/api/admin/products");
        list.Should().Contain(p => p.Id == created!.Id && p.Name == "Test Widget");
    }

    // ---------- S2: Create product, name="" -> 400 with validation error

    [Fact]
    public async Task S2_create_product_with_empty_name_returns_400()
    {
        var seller = fx.ClientFor("Seller", ApiFixture.SellerId);
        var resp = await seller.PostAsJsonAsync("/api/seller/products",
            new { name = "", price = 10m, stock = 5 });

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ---------- S3: Create product, price=0 -> 400

    [Fact]
    public async Task S3_create_product_with_zero_price_returns_400()
    {
        var seller = fx.ClientFor("Seller", ApiFixture.SellerId);
        var resp = await seller.PostAsJsonAsync("/api/seller/products",
            new { name = "Widget", price = 0m, stock = 5 });

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ---------- S4: Place order, sufficient stock -> 201, Confirmed, stock decremented

    [Fact]
    public async Task S4_buyer_places_order_with_sufficient_stock_decrements_stock()
    {
        var buyer = fx.ClientFor("Buyer", ApiFixture.BuyerId);
        var resp = await buyer.PostAsJsonAsync("/api/buyer/orders",
            new { productId = fx.WidgetId, quantity = 3 });

        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var order = await resp.Content.ReadFromJsonAsync<PlaceOrderResponse>();
        order!.Status.Should().Be(nameof(OrderStatus.Confirmed));

        var stock = await ReadProductStockAsync(fx.WidgetId);
        stock.Should().Be(100 - 3);
    }

    // ---------- S5: Place order, insufficient stock -> 422, Failed, stock unchanged

    [Fact]
    public async Task S5_insufficient_stock_returns_422_and_persists_failed_order()
    {
        var buyer = fx.ClientFor("Buyer", ApiFixture.BuyerId);
        var resp = await buyer.PostAsJsonAsync("/api/buyer/orders",
            new { productId = fx.DoohickeyId, quantity = 1 });

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var stock = await ReadProductStockAsync(fx.DoohickeyId);
        stock.Should().Be(0);

        using var scope = fx.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var failed = await db.Orders.AsNoTracking().SingleAsync();
        failed.Status.Should().Be(OrderStatus.Failed);
    }

    // ---------- S6: Place order, non-published product -> 404

    [Fact]
    public async Task S6_place_order_on_non_published_product_returns_404()
    {
        var suspendedId = await fx.AddSuspendedProductAsync("Suspended");

        var buyer = fx.ClientFor("Buyer", ApiFixture.BuyerId);
        var resp = await buyer.PostAsJsonAsync("/api/buyer/orders",
            new { productId = suspendedId, quantity = 1 });

        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------- S7: Cancel own pending order -> 200, Cancelled, stock returned

    [Fact]
    public async Task S7_buyer_cancels_own_order_stock_is_returned()
    {
        var buyer = fx.ClientFor("Buyer", ApiFixture.BuyerId);
        var place = await buyer.PostAsJsonAsync("/api/buyer/orders",
            new { productId = fx.GizmoId, quantity = 4 });
        var order = await place.Content.ReadFromJsonAsync<PlaceOrderResponse>();

        var cancel = await buyer.PostAsync($"/api/buyer/orders/{order!.OrderId}/cancel", content: null);
        cancel.StatusCode.Should().Be(HttpStatusCode.OK);

        var stock = await ReadProductStockAsync(fx.GizmoId);
        stock.Should().Be(50);
    }

    // ---------- S8: Cancel another buyer's order -> 403

    [Fact]
    public async Task S8_cancel_another_buyers_order_returns_403()
    {
        var buyer = fx.ClientFor("Buyer", ApiFixture.BuyerId);
        var place = await buyer.PostAsJsonAsync("/api/buyer/orders",
            new { productId = fx.WidgetId, quantity = 2 });
        var order = await place.Content.ReadFromJsonAsync<PlaceOrderResponse>();

        var otherBuyer = fx.ClientFor("Buyer", ApiFixture.OtherBuyerId);
        var cancel = await otherBuyer.PostAsync($"/api/buyer/orders/{order!.OrderId}/cancel", content: null);

        cancel.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ---------- S9: Admin force-cancels confirmed order -> 200 + stock returned

    [Fact]
    public async Task S9_admin_force_cancels_confirmed_order_stock_returned()
    {
        var buyer = fx.ClientFor("Buyer", ApiFixture.BuyerId);
        var place = await buyer.PostAsJsonAsync("/api/buyer/orders",
            new { productId = fx.WidgetId, quantity = 6 });
        var order = await place.Content.ReadFromJsonAsync<PlaceOrderResponse>();

        var admin = fx.ClientFor("Admin", ApiFixture.AdminId);
        var cancel = await admin.PostAsync($"/api/admin/orders/{order!.OrderId}/cancel", content: null);
        cancel.StatusCode.Should().Be(HttpStatusCode.OK);

        var stock = await ReadProductStockAsync(fx.WidgetId);
        stock.Should().Be(100);
    }

    // ---------- S10: Buyer list excludes drafts/suspended

    [Fact]
    public async Task S10_buyer_list_excludes_non_published_products()
    {
        await fx.AddSuspendedProductAsync("Hidden");

        var buyer = fx.ClientFor("Buyer", ApiFixture.BuyerId);
        var products = await buyer.GetFromJsonAsync<BuyerProductResponse[]>("/api/buyer/products");

        products.Should().NotContain(p => p.Name == "Hidden");
        products!.Length.Should().Be(3);
    }

    // ---------- S11: Admin list includes drafts + stock

    [Fact]
    public async Task S11_admin_list_includes_suspended_products()
    {
        await fx.AddSuspendedProductAsync("Hidden");

        var admin = fx.ClientFor("Admin", ApiFixture.AdminId);
        var products = await admin.GetFromJsonAsync<AdminProductResponse[]>("/api/admin/products");

        products.Should().Contain(p => p.Name == "Hidden" && p.Status == "Suspended");
        products!.Length.Should().Be(4);
    }

    // ---------- S12: No role header / no token -> 401

    [Fact]
    public async Task S12_request_without_role_headers_returns_401()
    {
        var anonymous = fx.AnonymousClient();
        var resp = await anonymous.GetAsync("/api/buyer/products");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---------- S13: Wrong role for endpoint -> 403

    [Fact]
    public async Task S13_wrong_role_returns_403()
    {
        var buyerHittingSeller = fx.ClientFor("Buyer", ApiFixture.BuyerId);
        var resp = await buyerHittingSeller.PostAsJsonAsync("/api/seller/products",
            new { name = "Naughty", price = 1m, stock = 1 });

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ---------- helpers

    private async Task<int> ReadProductStockAsync(Guid productId)
    {
        using var scope = fx.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var id = new Marketplace.Domain.Products.ProductId(productId);
        var p = await db.Products.AsNoTracking().SingleAsync(x => x.Id == id);
        return p.Stock.Value;
    }

    private sealed record CreateProductResponse(Guid Id, string Name, decimal Price, int Stock, string Status);
    private sealed record PlaceOrderResponse(Guid OrderId, string Status);
    private sealed record BuyerProductResponse(Guid Id, string Name, decimal Price, bool InStock);
    private sealed record AdminProductResponse(Guid Id, string Name, decimal Price, int Stock, string Status, Guid SellerId, DateTime CreatedAt);
}
