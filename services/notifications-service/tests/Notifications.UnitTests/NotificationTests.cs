using Notifications.Domain.Notifications;

namespace Notifications.UnitTests;

public class NotificationTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Tenant = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public void Create_captures_all_fields_and_assigns_id()
    {
        var orderId = Guid.NewGuid();
        var n = Notification.Create(Tenant, "OrderConfirmed", "buyer@example.com", orderId, "Body", Now);

        n.Id.Value.Should().NotBe(Guid.Empty);
        n.TenantId.Should().Be(Tenant);
        n.Type.Should().Be("OrderConfirmed");
        n.Recipient.Should().Be("buyer@example.com");
        n.RelatedOrderId.Should().Be(orderId);
        n.Body.Should().Be("Body");
        n.SentAt.Should().Be(Now);
    }

    [Fact]
    public void Create_allows_null_order_id() =>
        Notification.Create(Tenant, "TenantCreated", "admin@example.com", null, "Welcome", Now)
            .RelatedOrderId.Should().BeNull();
}
