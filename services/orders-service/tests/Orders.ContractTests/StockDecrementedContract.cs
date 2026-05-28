using System.Text.Json;
using Catalog.Contracts.IntegrationEvents;

namespace Orders.ContractTests;

/// <summary>
/// Consumer-driven contract — Orders service consumes <c>StockDecrementedIntegrationEvent</c>
/// from Catalog. This test PINS the JSON shape Orders is willing to parse;
/// Catalog's producer-side contract test asserts the same shape is emitted.
/// In a real CDCT pipeline this JSON would be the pact file the consumer
/// publishes to a Pact broker — here we keep it inline for showcase clarity.
/// </summary>
public class StockDecrementedContract
{
    /// <summary>The shape Orders relies on. Adding a required field here is a breaking change.</summary>
    public const string ExpectedPact = """
    {
      "MessageId": "11111111-1111-1111-1111-111111111111",
      "OccurredAt": "2026-01-01T00:00:00Z",
      "TenantId": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
      "OrderId": "22222222-2222-2222-2222-222222222222",
      "ProductId": "33333333-3333-3333-3333-333333333333",
      "Quantity": 2,
      "RemainingStock": 48
    }
    """;

    [Fact]
    public void Pact_JSON_deserializes_into_the_consumer_record()
    {
        var evt = JsonSerializer.Deserialize<StockDecrementedIntegrationEvent>(
            ExpectedPact,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        evt.Should().NotBeNull();
        evt!.MessageId.Should().Be(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        evt.TenantId.Should().Be(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
        evt.OrderId.Should().Be(Guid.Parse("22222222-2222-2222-2222-222222222222"));
        evt.ProductId.Should().Be(Guid.Parse("33333333-3333-3333-3333-333333333333"));
        evt.Quantity.Should().Be(2);
        evt.RemainingStock.Should().Be(48);
    }

    [Fact]
    public void Round_trip_preserves_all_fields()
    {
        var original = JsonSerializer.Deserialize<StockDecrementedIntegrationEvent>(
            ExpectedPact, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        var roundTrip = JsonSerializer.Deserialize<StockDecrementedIntegrationEvent>(
            JsonSerializer.Serialize(original), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        roundTrip.Should().BeEquivalentTo(original);
    }
}
