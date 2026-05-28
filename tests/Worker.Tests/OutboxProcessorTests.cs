using BuildingBlocks.Application.MultiTenancy;
using BuildingBlocks.Domain;
using BuildingBlocks.Infrastructure.EventBus;
using BuildingBlocks.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Orders.Infrastructure.Persistence;
using Worker.Tests.Fixtures;

namespace Worker.Tests;

[Collection(nameof(WorkerCollection))]
public class OutboxProcessorTests(WorkerFixture fx)
{
    [Fact]
    public async Task Processor_dispatches_pending_messages_and_marks_them_processed()
    {
        // Arrange — write a message directly to the Orders outbox.
        var evt = new TestPing(Guid.NewGuid(), DateTime.UtcNow, WorkerFixture.AcmeTenantId, "ping");
        using (var seedScope = fx.CreateTenantScope(WorkerFixture.AcmeTenantId))
        {
            var db = seedScope.ServiceProvider.GetRequiredService<OrdersDbContext>();
            db.OutboxMessages.Enqueue(evt);
            await db.SaveChangesAsync();
        }

        // Act — run ONE processor pass directly (no need to start a hosted-service loop).
        var processor = ResolveProcessor();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var task = processor.StartAsync(cts.Token);
        // Wait until the row is marked processed or timeout.
        await WaitForProcessedAsync(evt.MessageId, TimeSpan.FromSeconds(8));
        await processor.StopAsync(CancellationToken.None);

        // Assert
        using var scope = fx.CreateTenantScope(WorkerFixture.AcmeTenantId);
        var ordersDb = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        var row = await ordersDb.OutboxMessages.AsNoTracking().FirstAsync(m => m.Id == evt.MessageId);
        row.ProcessedAt.Should().NotBeNull("the processor's resilience pipeline runs handlers even when none are registered for the event type");
    }

    [Fact]
    public void Worker_resolves_OutboxProcessor_from_DI()
    {
        var processor = ResolveProcessor();
        processor.Should().NotBeNull();
    }

    private OutboxProcessor ResolveProcessor()
    {
        // The host registers the processor as IHostedService; resolve it by type.
        var hostedServices = fx.Host.Services.GetServices<Microsoft.Extensions.Hosting.IHostedService>();
        return hostedServices.OfType<OutboxProcessor>().FirstOrDefault()
            ?? throw new InvalidOperationException("OutboxProcessor not registered in the Worker host");
    }

    private async Task WaitForProcessedAsync(Guid messageId, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            using var scope = fx.CreateTenantScope(WorkerFixture.AcmeTenantId);
            var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
            var row = await db.OutboxMessages.AsNoTracking().FirstOrDefaultAsync(m => m.Id == messageId);
            if (row?.ProcessedAt is not null) return;
            await Task.Delay(150);
        }
    }

    /// <summary>
    /// Test-only event with no registered handler. The bus logs "no handlers"
    /// and returns; the processor then marks the row processed. Exercises the
    /// full dispatch path without depending on a specific module's events.
    /// </summary>
    private sealed record TestPing(Guid MessageId, DateTime OccurredAt, Guid TenantId, string Note) : IIntegrationEvent;
}
