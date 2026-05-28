using System.Text.Json;
using Orders.Contracts.IntegrationEvents;

namespace Notifications.ContractTests;

/// <summary>Notifications consumes <c>OrderConfirmedIntegrationEvent</c> from Orders.</summary>
public class OrderConfirmedContract
{
    public const string ExpectedPact = """
    {
      "MessageId": "cccccccc-1111-2222-3333-444444444444",
      "OccurredAt": "2026-01-01T00:00:00Z",
      "TenantId": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
      "OrderId": "22222222-2222-2222-2222-222222222222",
      "BuyerId": "22222222-2222-2222-2222-222222222222",
      "ProductId": "33333333-3333-3333-3333-333333333333",
      "Quantity": 1
    }
    """;

    [Fact]
    public void Pact_JSON_deserializes_into_the_consumer_record()
    {
        var evt = JsonSerializer.Deserialize<OrderConfirmedIntegrationEvent>(
            ExpectedPact, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        evt.Should().NotBeNull();
        evt!.BuyerId.Should().Be(Guid.Parse("22222222-2222-2222-2222-222222222222"));
        evt.ProductId.Should().Be(Guid.Parse("33333333-3333-3333-3333-333333333333"));
        evt.Quantity.Should().Be(1);
    }
}
