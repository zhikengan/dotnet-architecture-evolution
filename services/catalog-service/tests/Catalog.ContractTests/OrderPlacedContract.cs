using System.Text.Json;
using Orders.Contracts.IntegrationEvents;

namespace Catalog.ContractTests;

/// <summary>Catalog consumes <c>OrderPlacedIntegrationEvent</c> from Orders.</summary>
public class OrderPlacedContract
{
    public const string ExpectedPact = """
    {
      "MessageId": "aaaaaaaa-1111-2222-3333-444444444444",
      "OccurredAt": "2026-01-01T00:00:00Z",
      "TenantId": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
      "OrderId": "22222222-2222-2222-2222-222222222222",
      "BuyerId": "22222222-2222-2222-2222-222222222222",
      "ProductId": "33333333-3333-3333-3333-333333333333",
      "Quantity": 3
    }
    """;

    [Fact]
    public void Pact_JSON_deserializes_into_the_consumer_record()
    {
        var evt = JsonSerializer.Deserialize<OrderPlacedIntegrationEvent>(
            ExpectedPact, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        evt.Should().NotBeNull();
        evt!.OrderId.Should().Be(Guid.Parse("22222222-2222-2222-2222-222222222222"));
        evt.ProductId.Should().Be(Guid.Parse("33333333-3333-3333-3333-333333333333"));
        evt.Quantity.Should().Be(3);
    }
}
