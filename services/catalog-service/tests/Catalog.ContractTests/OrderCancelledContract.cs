using System.Text.Json;
using Orders.Contracts.IntegrationEvents;

namespace Catalog.ContractTests;

/// <summary>Catalog consumes <c>OrderCancelledIntegrationEvent</c> from Orders.</summary>
public class OrderCancelledContract
{
    public const string ExpectedPact = """
    {
      "MessageId": "bbbbbbbb-1111-2222-3333-444444444444",
      "OccurredAt": "2026-01-01T00:00:00Z",
      "TenantId": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
      "OrderId": "22222222-2222-2222-2222-222222222222",
      "ProductId": "33333333-3333-3333-3333-333333333333",
      "Quantity": 2,
      "StockWasDecremented": true
    }
    """;

    [Fact]
    public void Pact_JSON_deserializes_with_StockWasDecremented_flag()
    {
        var evt = JsonSerializer.Deserialize<OrderCancelledIntegrationEvent>(
            ExpectedPact, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        evt.Should().NotBeNull();
        evt!.StockWasDecremented.Should().BeTrue();
        evt.Quantity.Should().Be(2);
    }

    [Fact]
    public void Pact_with_StockWasDecremented_false_round_trips()
    {
        var pact = ExpectedPact.Replace("\"StockWasDecremented\": true", "\"StockWasDecremented\": false");
        var evt = JsonSerializer.Deserialize<OrderCancelledIntegrationEvent>(
            pact, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        evt.StockWasDecremented.Should().BeFalse();
    }
}
