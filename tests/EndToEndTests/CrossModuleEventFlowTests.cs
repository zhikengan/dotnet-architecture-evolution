using System.Net.Http.Json;
using EndToEndTests.Fixtures;

namespace EndToEndTests;

[Collection(nameof(ApiCollection))]
public class CrossModuleEventFlowTests(ApiFixture fx) : IAsyncLifetime
{
    private static readonly TimeSpan SagaTimeout = TimeSpan.FromSeconds(10);

    public Task InitializeAsync() => fx.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task PlaceOrder_flows_through_outbox_to_Catalog_and_back_to_Orders()
    {
        var buyer = fx.ClientFor("Buyer", ApiFixture.BuyerId);
        var resp = await buyer.PostAsJsonAsync("/api/buyer/orders", new { productId = fx.WidgetId, quantity = 7 });
        var order = await resp.Content.ReadFromJsonAsync<PlacedOrder>();

        // Initial response is Pending (saga hasn't fired yet)
        order!.Status.Should().Be("Pending");

        // Saga: Orders.outbox -> Catalog handler decrements stock -> Catalog.outbox -> Orders handler confirms
        var status = await fx.WaitForOrderStatusAsync(order.OrderId, ApiFixture.BuyerId, "Confirmed", SagaTimeout);
        status.Should().Be("Confirmed");
        (await fx.GetProductStockAsync(fx.WidgetId)).Should().Be(93);
    }

    [Fact]
    public async Task Cancellation_returns_stock_via_OrderCancelled_integration_event()
    {
        var buyer = fx.ClientFor("Buyer", ApiFixture.BuyerId);
        var placed = await buyer.PostAsJsonAsync("/api/buyer/orders", new { productId = fx.GizmoId, quantity = 5 });
        var order = await placed.Content.ReadFromJsonAsync<PlacedOrder>();
        await fx.WaitForOrderStatusAsync(order!.OrderId, ApiFixture.BuyerId, "Confirmed", SagaTimeout);
        (await fx.GetProductStockAsync(fx.GizmoId)).Should().Be(45);

        // Buyer cancels -> Orders.outbox publishes OrderCancelled with StockWasDecremented=true ->
        // Catalog handler returns the stock.
        await buyer.PostAsync($"/api/buyer/orders/{order.OrderId}/cancel", content: null);

        var deadline = DateTime.UtcNow + SagaTimeout;
        while (DateTime.UtcNow < deadline)
        {
            if (await fx.GetProductStockAsync(fx.GizmoId) == 50) return;
            await Task.Delay(150);
        }
        (await fx.GetProductStockAsync(fx.GizmoId)).Should().Be(50, "stock should be returned via cross-module saga");
    }

    [Fact(Skip = "IdempotencyBehavior is wired but JSON round-trip of Result<T> needs a custom JsonConverter; deferred to Tier 4")]
    public Task Idempotency_key_de_dupes_PlaceOrder()
    {
        // Behavior is registered in the MediatR pipeline; PlaceOrderCommand implements
        // IIdempotentCommand; the endpoint reads the Idempotency-Key header and threads
        // it into the command. Round-tripping the cached Result<PlaceOrderResult> via
        // System.Text.Json requires a custom JsonConverter because Result<T> has no
        // parameterless constructor and accessing Value on a failure throws. Tracked
        // for Tier 4.
        return Task.CompletedTask;
    }

    private sealed record PlacedOrder(Guid OrderId, string Status);
}
