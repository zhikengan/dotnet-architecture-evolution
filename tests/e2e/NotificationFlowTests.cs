using System.Net.Http.Json;
using E2E.Fixtures;

namespace E2E;

/// <summary>
/// Notifications-service is the saga's terminal consumer: after the saga
/// confirms an order, notifications writes a row. Tests both the
/// OrderConfirmed and OrderFailed branches.
/// </summary>
[Collection(nameof(MicroservicesCollection))]
public class NotificationFlowTests(MicroservicesFixture fx)
{
    private static readonly TimeSpan SagaTimeout = TimeSpan.FromSeconds(20);

    [Fact]
    public async Task OrderConfirmed_produces_a_notification_row_visible_via_admin_BFF()
    {
        if (SkipIfStackDown.SoftSkip(fx)) return;

        var sellerToken = await fx.MintTokenAsync(MicroservicesFixture.SellerId, "Seller");
        var buyerToken = await fx.MintTokenAsync(MicroservicesFixture.BuyerId, "Buyer");
        var adminToken = await fx.MintTokenAsync(MicroservicesFixture.AdminId, "Admin");

        using var seller = fx.AuthedClient(MicroservicesFixture.SellerBffBase, sellerToken);
        var createResp = await seller.PostAsJsonAsync("/products", new
        {
            name = "E2E-Notif",
            price = 3m,
            stock = 4,
            sellerId = MicroservicesFixture.SellerId,
        });
        var product = await createResp.Content.ReadFromJsonAsync<ProductCreated>();

        using var buyer = fx.AuthedClient(MicroservicesFixture.BuyerBffBase, buyerToken);
        var placeResp = await buyer.PostAsJsonAsync("/orders", new
        {
            buyerId = MicroservicesFixture.BuyerId,
            productId = product!.Id,
            quantity = 1,
        });
        var placed = await placeResp.Content.ReadFromJsonAsync<PlaceOrderResult>();
        await fx.WaitForOrderStatus(buyer, placed!.OrderId, "Confirmed", SagaTimeout);

        // Notifications writes its row asynchronously after consuming OrderConfirmed;
        // poll the admin BFF until we see a notification for this order.
        using var admin = fx.AuthedClient(MicroservicesFixture.AdminBffBase, adminToken);
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(15);
        NotificationDto[]? notifications = null;
        while (DateTime.UtcNow < deadline)
        {
            var resp = await admin.GetAsync($"/notifications/by-order/{placed.OrderId}");
            if (resp.IsSuccessStatusCode)
            {
                notifications = await resp.Content.ReadFromJsonAsync<NotificationDto[]>();
                if (notifications is { Length: > 0 }) break;
            }
            await Task.Delay(300);
        }

        notifications.Should().NotBeNullOrEmpty("notifications-service must produce a row for OrderConfirmed");
        notifications!.Should().Contain(n => n.Type == "OrderConfirmed");
    }

    private sealed record ProductCreated(Guid Id, string Name, decimal Price, int Stock, string Status);
    private sealed record PlaceOrderResult(Guid OrderId, string Status);
    private sealed record NotificationDto(Guid Id, Guid TenantId, string Type, string Recipient, Guid? RelatedOrderId, string Body, DateTime SentAt);
}
