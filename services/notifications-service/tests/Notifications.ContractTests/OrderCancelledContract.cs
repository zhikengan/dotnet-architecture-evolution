using System.Text.Json;
using Orders.Contracts.IntegrationEvents;

namespace Notifications.ContractTests;

/// <summary>Notifications consumes <c>OrderCancelledIntegrationEvent</c> from Orders.</summary>
public class OrderCancelledContract
{
    public const string ExpectedPact = """
    {
      "MessageId": "dddddddd-1111-2222-3333-444444444444",
      "OccurredAt": "2026-01-01T00:00:00Z",
      "TenantId": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
      "OrderId": "22222222-2222-2222-2222-222222222222",
      "ProductId": "33333333-3333-3333-3333-333333333333",
      "Quantity": 1,
      "StockWasDecremented": false
    }
    """;

    [Fact]
    public void Pact_JSON_deserializes_for_notifications_consumer()
    {
        var evt = JsonSerializer.Deserialize<OrderCancelledIntegrationEvent>(
            ExpectedPact, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        evt.Should().NotBeNull();
        evt!.OrderId.Should().Be(Guid.Parse("22222222-2222-2222-2222-222222222222"));
    }
}
