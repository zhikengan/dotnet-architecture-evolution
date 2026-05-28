using System.Text.Json;
using Catalog.Contracts.IntegrationEvents;

namespace Orders.ContractTests;

/// <summary>Orders consumes <c>StockDecrementFailedIntegrationEvent</c> from Catalog.</summary>
public class StockDecrementFailedContract
{
    public const string ExpectedPact = """
    {
      "MessageId": "55555555-5555-5555-5555-555555555555",
      "OccurredAt": "2026-01-01T00:00:00Z",
      "TenantId": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
      "OrderId": "22222222-2222-2222-2222-222222222222",
      "ProductId": "33333333-3333-3333-3333-333333333333",
      "Reason": "Insufficient stock"
    }
    """;

    [Fact]
    public void Pact_JSON_deserializes_into_the_consumer_record()
    {
        var evt = JsonSerializer.Deserialize<StockDecrementFailedIntegrationEvent>(
            ExpectedPact, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        evt.Should().NotBeNull();
        evt!.Reason.Should().Be("Insufficient stock");
        evt.OrderId.Should().Be(Guid.Parse("22222222-2222-2222-2222-222222222222"));
    }
}
