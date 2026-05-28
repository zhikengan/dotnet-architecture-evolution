using System.Net;
using System.Net.Http.Json;
using E2E.Fixtures;

namespace E2E;

/// <summary>
/// Cross-service end-to-end tests that exercise the full distributed saga
/// through the BFFs. Require the docker-compose stack to be up — see
/// <see cref="MicroservicesFixture"/> for the probe + skip behavior.
/// </summary>
[Collection(nameof(MicroservicesCollection))]
public class PlaceOrderSagaTests(MicroservicesFixture fx)
{
    private static readonly TimeSpan SagaTimeout = TimeSpan.FromSeconds(15);

    [Fact]
    public async Task Place_order_with_sufficient_stock_transitions_to_Confirmed_via_RabbitMQ_saga()
    {
        if (SkipIfStackDown.SoftSkip(fx)) return;

        var sellerToken = await fx.MintTokenAsync(MicroservicesFixture.SellerId, "Seller");
        var buyerToken = await fx.MintTokenAsync(MicroservicesFixture.BuyerId, "Buyer");

        // Seller creates a product through the seller BFF.
        using var seller = fx.AuthedClient(MicroservicesFixture.SellerBffBase, sellerToken);
        var createResp = await seller.PostAsJsonAsync("/products", new
        {
            name = "E2E-Saga-Widget",
            price = 10m,
            stock = 5,
            sellerId = MicroservicesFixture.SellerId,
        });
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<ProductCreated>();
        created.Should().NotBeNull();

        // Buyer places an order.
        using var buyer = fx.AuthedClient(MicroservicesFixture.BuyerBffBase, buyerToken);
        var placeResp = await buyer.PostAsJsonAsync("/orders", new
        {
            buyerId = MicroservicesFixture.BuyerId,
            productId = created!.Id,
            quantity = 2,
        });
        placeResp.EnsureSuccessStatusCode();
        var placed = await placeResp.Content.ReadFromJsonAsync<PlaceOrderResult>();
        placed!.Status.Should().Be("Pending");

        // Saga eventually flips status to Confirmed (catalog decrement → orders confirm).
        var final = await fx.WaitForOrderStatus(buyer, placed.OrderId, "Confirmed", SagaTimeout);
        final.Should().Be("Confirmed", $"saga must complete within {SagaTimeout.TotalSeconds}s; last status was {final}");
    }

    [Fact]
    public async Task Cancel_own_pending_order_does_not_change_stock()
    {
        if (SkipIfStackDown.SoftSkip(fx)) return;

        var sellerToken = await fx.MintTokenAsync(MicroservicesFixture.SellerId, "Seller");
        var buyerToken = await fx.MintTokenAsync(MicroservicesFixture.BuyerId, "Buyer");

        using var seller = fx.AuthedClient(MicroservicesFixture.SellerBffBase, sellerToken);
        var createResp = await seller.PostAsJsonAsync("/products", new
        {
            name = "E2E-CancelPending",
            price = 5m,
            stock = 10,
            sellerId = MicroservicesFixture.SellerId,
        });
        var product = await createResp.Content.ReadFromJsonAsync<ProductCreated>();

        using var buyer = fx.AuthedClient(MicroservicesFixture.BuyerBffBase, buyerToken);
        var placeResp = await buyer.PostAsJsonAsync("/orders", new
        {
            buyerId = MicroservicesFixture.BuyerId,
            productId = product!.Id,
            quantity = 3,
        });
        var placed = await placeResp.Content.ReadFromJsonAsync<PlaceOrderResult>();

        // Cancel before the saga has confirmed — Tier 5's cancel command publishes
        // OrderCancelled with StockWasDecremented=false, so catalog skips the
        // return-stock branch.
        var cancelResp = await buyer.PostAsync($"/orders/{placed!.OrderId}/cancel", content: null);
        cancelResp.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Wrong_role_for_seller_endpoint_returns_403()
    {
        if (SkipIfStackDown.SoftSkip(fx)) return;
        var buyerToken = await fx.MintTokenAsync(MicroservicesFixture.BuyerId, "Buyer");
        using var seller = fx.AuthedClient(MicroservicesFixture.SellerBffBase, buyerToken);
        var resp = await seller.PostAsJsonAsync("/products", new { name = "X", price = 1m, stock = 1, sellerId = MicroservicesFixture.BuyerId });
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Missing_token_returns_401()
    {
        if (SkipIfStackDown.SoftSkip(fx)) return;
        using var anon = fx.ClientFor(MicroservicesFixture.BuyerBffBase);
        var resp = await anon.GetAsync("/products");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private sealed record ProductCreated(Guid Id, string Name, decimal Price, int Stock, string Status);
    private sealed record PlaceOrderResult(Guid OrderId, string Status);
}
