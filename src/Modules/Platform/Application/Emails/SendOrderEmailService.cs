using BuildingBlocks.Application;
using Microsoft.Extensions.Logging;
using Platform.Application.Abstractions;
using Platform.Domain.Emails;

namespace Platform.Application.Emails;

/// <summary>
/// Hangfire job target. The fire-and-forget invocation enqueues an
/// <c>Enqueue&lt;SendOrderEmailService&gt;(s =&gt; s.SendAsync(...))</c> call;
/// Hangfire serializes the args, picks the job up from storage, and a
/// Hangfire worker instantiates this class from DI and invokes <c>SendAsync</c>.
/// At Tier 4 we just log + write a row — a real email provider is a
/// drop-in for the SendAsync body.
/// </summary>
public sealed class SendOrderEmailService(
    IPlatformDbContext db,
    IClock clock,
    ILogger<SendOrderEmailService> logger)
{
    public async Task SendAsync(Guid tenantId, Guid orderId, Guid buyerId, CancellationToken ct = default)
    {
        var email = SentEmail.Create(
            tenantId,
            template: "OrderConfirmed",
            recipient: $"buyer-{buyerId:N}@example.com",
            relatedEntityId: orderId,
            clock.UtcNow);
        db.SentEmails.Add(email);
        await db.SaveChangesAsync(ct);
        logger.LogInformation(
            "Order confirmation email sent: tenant={TenantId} order={OrderId} buyer={BuyerId}",
            tenantId, orderId, buyerId);
    }
}
