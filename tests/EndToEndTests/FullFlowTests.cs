using System.Net;
using System.Net.Http.Json;
using EndToEndTests.Fixtures;

namespace EndToEndTests;

[Collection(nameof(ApiCollection))]
public class FullFlowTests(ApiFixture fx) : IAsyncLifetime
{
    private static readonly TimeSpan SagaTimeout = TimeSpan.FromSeconds(10);

    public Task InitializeAsync() => fx.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // ---------- S1: Seller creates product -> 201, visible in admin list ----------

    [Fact]
    public async Task S1_seller_creates_product_appears_in_admin_list()
    {
        var seller = fx.ClientFor("Seller", ApiFixture.SellerId);
        var resp = await seller.PostAsJsonAsync("/api/seller/products", new { name = "Test Widget", price = 12.50m, stock = 5 });
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await resp.Content.ReadFromJsonAsync<CreatedProduct>();

        var admin = fx.ClientFor("Admin", ApiFixture.AdminId);
        var list = await admin.GetFromJsonAsync<AdminProduct[]>("/api/admin/products");
        list.Should().Contain(p => p.Id == created!.Id && p.Name == "Test Widget");
    }

    // ---------- S2/S3: invalid product create -> 400 ----------

    [Fact]
    public async Task S2_create_with_empty_name_returns_400()
    {
        var seller = fx.ClientFor("Seller", ApiFixture.SellerId);
        var resp = await seller.PostAsJsonAsync("/api/seller/products", new { name = "", price = 10m, stock = 5 });
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task S3_create_with_zero_price_returns_400()
    {
        var seller = fx.ClientFor("Seller", ApiFixture.SellerId);
        var resp = await seller.PostAsJsonAsync("/api/seller/products", new { name = "Free", price = 0m, stock = 5 });
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ---------- S4: place order, sufficient stock -> 201 Pending -> saga Confirmed + stock decremented ----------

    [Fact]
    public async Task S4_place_order_with_sufficient_stock_eventually_Confirmed_and_stock_decremented()
    {
        var buyer = fx.ClientFor("Buyer", ApiFixture.BuyerId);
        var resp = await buyer.PostAsJsonAsync("/api/buyer/orders", new { productId = fx.WidgetId, quantity = 3 });
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var order = await resp.Content.ReadFromJsonAsync<PlacedOrder>();
        order!.Status.Should().Be("Pending");

        var status = await fx.WaitForOrderStatusAsync(order.OrderId, ApiFixture.BuyerId, "Confirmed", SagaTimeout);
        status.Should().Be("Confirmed");
        (await fx.GetProductStockAsync(fx.WidgetId)).Should().Be(100 - 3);
    }

    // ---------- S5: insufficient stock -> 201 Pending -> saga Failed + stock unchanged ----------

    [Fact]
    public async Task S5_insufficient_stock_eventually_Failed_and_stock_unchanged()
    {
        var buyer = fx.ClientFor("Buyer", ApiFixture.BuyerId);
        var resp = await buyer.PostAsJsonAsync("/api/buyer/orders", new { productId = fx.DoohickeyId, quantity = 1 });
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var order = await resp.Content.ReadFromJsonAsync<PlacedOrder>();

        var status = await fx.WaitForOrderStatusAsync(order!.OrderId, ApiFixture.BuyerId, "Failed", SagaTimeout);
        status.Should().Be("Failed");
        (await fx.GetProductStockAsync(fx.DoohickeyId)).Should().Be(0);
    }

    // ---------- S6: non-published product -> 201 Pending -> saga Failed (NotPublished surfaced async) ----------

    [Fact]
    public async Task S6_place_order_on_non_published_product_eventually_Failed()
    {
        var suspendedId = await fx.AddSuspendedProductAsync("Hidden");
        var buyer = fx.ClientFor("Buyer", ApiFixture.BuyerId);
        var resp = await buyer.PostAsJsonAsync("/api/buyer/orders", new { productId = suspendedId, quantity = 1 });
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var order = await resp.Content.ReadFromJsonAsync<PlacedOrder>();
        var status = await fx.WaitForOrderStatusAsync(order!.OrderId, ApiFixture.BuyerId, "Failed", SagaTimeout);
        status.Should().Be("Failed");
    }

    // ---------- S7: buyer cancels own order -> stock returned ----------

    [Fact]
    public async Task S7_buyer_cancels_own_order_stock_returned()
    {
        var buyer = fx.ClientFor("Buyer", ApiFixture.BuyerId);
        var place = await buyer.PostAsJsonAsync("/api/buyer/orders", new { productId = fx.GizmoId, quantity = 4 });
        var order = await place.Content.ReadFromJsonAsync<PlacedOrder>();
        await fx.WaitForOrderStatusAsync(order!.OrderId, ApiFixture.BuyerId, "Confirmed", SagaTimeout);

        var cancel = await buyer.PostAsync($"/api/buyer/orders/{order.OrderId}/cancel", content: null);
        cancel.StatusCode.Should().Be(HttpStatusCode.OK);

        await WaitForStockAsync(fx.GizmoId, 50, SagaTimeout);
        (await fx.GetProductStockAsync(fx.GizmoId)).Should().Be(50);
    }

    // ---------- S8: other buyer's order -> 403 ----------

    [Fact]
    public async Task S8_other_buyer_cancel_returns_403()
    {
        var buyer = fx.ClientFor("Buyer", ApiFixture.BuyerId);
        var place = await buyer.PostAsJsonAsync("/api/buyer/orders", new { productId = fx.WidgetId, quantity = 2 });
        var order = await place.Content.ReadFromJsonAsync<PlacedOrder>();
        await fx.WaitForOrderStatusAsync(order!.OrderId, ApiFixture.BuyerId, "Confirmed", SagaTimeout);

        var otherBuyer = fx.ClientFor("Buyer", ApiFixture.OtherBuyerId);
        var cancel = await otherBuyer.PostAsync($"/api/buyer/orders/{order.OrderId}/cancel", content: null);
        cancel.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ---------- S9: admin force-cancel -> stock returned ----------

    [Fact]
    public async Task S9_admin_force_cancels_confirmed_order_stock_returned()
    {
        var buyer = fx.ClientFor("Buyer", ApiFixture.BuyerId);
        var place = await buyer.PostAsJsonAsync("/api/buyer/orders", new { productId = fx.WidgetId, quantity = 6 });
        var order = await place.Content.ReadFromJsonAsync<PlacedOrder>();
        await fx.WaitForOrderStatusAsync(order!.OrderId, ApiFixture.BuyerId, "Confirmed", SagaTimeout);

        var admin = fx.ClientFor("Admin", ApiFixture.AdminId);
        var cancel = await admin.PostAsync($"/api/admin/orders/{order.OrderId}/cancel", content: null);
        cancel.StatusCode.Should().Be(HttpStatusCode.OK);

        await WaitForStockAsync(fx.WidgetId, 100, SagaTimeout);
        (await fx.GetProductStockAsync(fx.WidgetId)).Should().Be(100);
    }

    // ---------- S10/S11: buyer excludes suspended, admin includes ----------

    [Fact]
    public async Task S10_buyer_list_excludes_non_published()
    {
        await fx.AddSuspendedProductAsync("Hidden");
        var buyer = fx.ClientFor("Buyer", ApiFixture.BuyerId);
        var products = await buyer.GetFromJsonAsync<BuyerProduct[]>("/api/buyer/products");
        products.Should().NotContain(p => p.Name == "Hidden");
        products!.Length.Should().Be(3);
    }

    [Fact]
    public async Task S11_admin_list_includes_suspended()
    {
        await fx.AddSuspendedProductAsync("Hidden");
        var admin = fx.ClientFor("Admin", ApiFixture.AdminId);
        var products = await admin.GetFromJsonAsync<AdminProduct[]>("/api/admin/products");
        products.Should().Contain(p => p.Name == "Hidden" && p.Status == "Suspended");
        products!.Length.Should().Be(4);
    }

    // ---------- S12/S13: auth ----------

    [Fact]
    public async Task S12_no_role_headers_returns_401()
    {
        var anonymous = fx.AnonymousClient();
        var resp = await anonymous.GetAsync("/api/buyer/products");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task S13_wrong_role_returns_403()
    {
        var buyerHitsSeller = fx.ClientFor("Buyer", ApiFixture.BuyerId);
        var resp = await buyerHitsSeller.PostAsJsonAsync("/api/seller/products", new { name = "x", price = 1m, stock = 1 });
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task WaitForStockAsync(Guid productId, int expected, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (await fx.GetProductStockAsync(productId) == expected) return;
            await Task.Delay(150);
        }
    }

    private sealed record CreatedProduct(Guid Id, string Name, decimal Price, int Stock, string Status);
    private sealed record PlacedOrder(Guid OrderId, string Status);
    private sealed record BuyerProduct(Guid Id, string Name, decimal Price, bool InStock, bool IsPremium);
    private sealed record AdminProduct(Guid Id, string Name, decimal Price, int Stock, string Status, Guid SellerId, DateTime CreatedAt);
}
