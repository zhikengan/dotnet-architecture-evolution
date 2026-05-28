using System.Text.Json;
using Orders.Contracts.IntegrationEvents;

namespace Notifications.ContractTests;

/// <summary>Notifications consumes <c>OrderFailedIntegrationEvent</c> from Orders.</summary>
public class OrderFailedContract
{
    public const string ExpectedPact = """
    {
      "MessageId": "eeeeeeee-1111-2222-3333-444444444444",
      "OccurredAt": "2026-01-01T00:00:00Z",
      "TenantId": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
      "OrderId": "22222222-2222-2222-2222-222222222222",
      "BuyerId": "22222222-2222-2222-2222-222222222222",
      "Reason": "Insufficient stock"
    }
    """;

    [Fact]
    public void Pact_JSON_deserializes_with_reason()
    {
        var evt = JsonSerializer.Deserialize<OrderFailedIntegrationEvent>(
            ExpectedPact, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        evt.Should().NotBeNull();
        evt!.Reason.Should().Be("Insufficient stock");
    }
}
