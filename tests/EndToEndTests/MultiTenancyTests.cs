using System.Net;
using System.Net.Http.Json;
using EndToEndTests.Fixtures;

namespace EndToEndTests;

/// <summary>
/// Verifies the EF Core global query filter actually isolates tenants —
/// both for list endpoints and for direct-id lookups. The Catalog seeder
/// puts "Widget"/"Gizmo"/"Doohickey" under Acme and "Globex Gadget" under
/// Globex; these tests cross-check the visibility from both sides.
/// </summary>
[Collection(nameof(ApiCollection))]
public class MultiTenancyTests(ApiFixture fx) : IAsyncLifetime
{
    public Task InitializeAsync() => fx.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Acme_buyer_does_not_see_Globex_products_in_list()
    {
        var buyer = fx.ClientFor("Buyer", ApiFixture.BuyerId, ApiFixture.AcmeTenantId);
        var products = await buyer.GetFromJsonAsync<BuyerProduct[]>("/api/buyer/products");

        products.Should().NotBeNull();
        products!.Should().Contain(p => p.Name == "Widget");
        products.Should().Contain(p => p.Name == "Gizmo");
        products.Should().NotContain(p => p.Name == "Globex Gadget");
    }

    [Fact]
    public async Task Globex_buyer_does_not_see_Acme_products_in_list()
    {
        var buyer = fx.ClientFor("Buyer", ApiFixture.BuyerId, ApiFixture.GlobexTenantId);
        var products = await buyer.GetFromJsonAsync<BuyerProduct[]>("/api/buyer/products");

        products.Should().NotBeNull();
        products!.Should().ContainSingle(p => p.Name == "Globex Gadget");
        products.Should().NotContain(p => p.Name == "Widget");
    }

    [Fact]
    public async Task Acme_admin_does_not_see_Globex_products_in_admin_list()
    {
        var admin = fx.ClientFor("Admin", ApiFixture.AdminId, ApiFixture.AcmeTenantId);
        var resp = await admin.GetAsync("/api/admin/products");
        resp.EnsureSuccessStatusCode();
        var products = await resp.Content.ReadFromJsonAsync<AdminProduct[]>();

        products.Should().NotBeNull();
        products!.Should().NotContain(p => p.Name == "Globex Gadget");
    }

    [Fact]
    public async Task Order_placed_by_Acme_buyer_is_invisible_to_Globex_buyer()
    {
        var acme = fx.ClientFor("Buyer", ApiFixture.BuyerId, ApiFixture.AcmeTenantId);
        var place = await acme.PostAsJsonAsync("/api/buyer/orders", new
        {
            productId = fx.WidgetId,
            quantity = 1,
        });
        place.EnsureSuccessStatusCode();
        var placed = await place.Content.ReadFromJsonAsync<PlaceOrderResponse>();
        placed.Should().NotBeNull();

        // Globex buyer with the same userId reads — but their tenant query
        // filter excludes Acme rows, so the order is invisible.
        var globex = fx.ClientFor("Buyer", ApiFixture.BuyerId, ApiFixture.GlobexTenantId);
        var get = await globex.GetAsync($"/api/buyer/orders/{placed!.OrderId}");
        get.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private sealed record BuyerProduct(Guid Id, string Name, decimal Price, bool InStock, bool IsPremium);
    private sealed record AdminProduct(Guid Id, string Name, decimal Price, int Stock, string Status);
    private sealed record PlaceOrderResponse(Guid OrderId, string Status);
}
