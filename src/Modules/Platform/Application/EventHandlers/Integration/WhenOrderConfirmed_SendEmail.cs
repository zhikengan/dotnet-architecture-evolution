using BuildingBlocks.Infrastructure.EventBus;
using Hangfire;
using Microsoft.Extensions.Logging;
using Orders.Contracts.IntegrationEvents;
using Platform.Application.Emails;

namespace Platform.Application.EventHandlers.Integration;

/// <summary>
/// Integration handler that hands work off to Hangfire. Note the difference
/// from Tier 3's inline integration handlers — here we *queue* a fire-and-
/// forget job rather than doing the work inline, because email sending is
/// (a) slow, (b) external, and (c) should retry independently of the saga.
/// Hangfire owns the retry/scheduling/state; this handler is a thin shim
/// that deliberately swallows enqueue failures so a transient Hangfire
/// hiccup never wedges the upstream saga.
/// </summary>
public sealed class WhenOrderConfirmed_SendEmail(
    IBackgroundJobClient jobs,
    ILogger<WhenOrderConfirmed_SendEmail> logger) : IIntegrationEventHandler<OrderConfirmedIntegrationEvent>
{
    public Task HandleAsync(OrderConfirmedIntegrationEvent evt, CancellationToken ct)
    {
        try
        {
            var jobId = jobs.Enqueue<SendOrderEmailService>(
                svc => svc.SendAsync(evt.TenantId, evt.OrderId, evt.BuyerId, CancellationToken.None));
            logger.LogInformation(
                "Queued OrderConfirmed email job={JobId} for order={OrderId}",
                jobId, evt.OrderId);
        }
        catch (Exception ex)
        {
            // Email is non-essential to the saga; log + drop so the OutboxProcessor
            // doesn't park the message and stall downstream consumers.
            logger.LogWarning(ex, "Hangfire enqueue failed for OrderConfirmed {OrderId}", evt.OrderId);
        }
        return Task.CompletedTask;
    }
}
