using System.Text.Json;
using BuildingBlocks.Infrastructure.EventBus;
using BuildingBlocks.Infrastructure.Resilience;
using BuildingBlocks.Infrastructure.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BuildingBlocks.Infrastructure.Outbox;

public sealed class OutboxProcessor(
    IServiceScopeFactory scopeFactory,
    IEventBus bus,
    IOptions<OutboxOptions> options,
    ILogger<OutboxProcessor> logger) : BackgroundService
{

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var poll = TimeSpan.FromMilliseconds(options.Value.PollIntervalMilliseconds);
        var pipeline = OutboxResiliencePipeline.Build(options.Value.MaxRetries);
        logger.LogInformation("OutboxProcessor starting; poll={PollMs}ms", options.Value.PollIntervalMilliseconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var stores = scope.ServiceProvider.GetServices<IOutboxStore>().ToList();

                foreach (var store in stores)
                {
                    var pending = await store.GetPendingAsync(options.Value.BatchSize, stoppingToken);
                    foreach (var msg in pending)
                    {
                        try
                        {
                            await pipeline.ExecuteAsync(async token =>
                            {
                                var type = Type.GetType(msg.Type, throwOnError: true)!;
                                var evt = JsonSerializer.Deserialize(msg.Payload, type)!;
                                await bus.PublishAsync(evt, token);
                            }, stoppingToken);

                            await store.MarkProcessedAsync(msg.Id, stoppingToken);
                            MarketplaceMeter.OutboxProcessed.Add(1,
                                new KeyValuePair<string, object?>("module", store.ModuleName),
                                new KeyValuePair<string, object?>("outcome", "success"));
                            // Lag = (now - OccurredAt). Surfaces "how long did
                            // this message sit before we dispatched it?" — a
                            // sentinel value for outbox-stuck alerts.
                            MarketplaceMeter.OutboxLagSeconds.Record(
                                (DateTime.UtcNow - msg.OccurredAt).TotalSeconds,
                                new KeyValuePair<string, object?>("module", store.ModuleName));
                        }
                        catch (Exception ex)
                        {
                            await store.MarkFailedAsync(msg.Id, ex.ToString(), stoppingToken);
                            MarketplaceMeter.OutboxProcessed.Add(1,
                                new KeyValuePair<string, object?>("module", store.ModuleName),
                                new KeyValuePair<string, object?>("outcome", "failed"));
                            logger.LogError(ex,
                                "Outbox dispatch failed for message {MessageId} (module={Module}, type={Type})",
                                msg.Id, store.ModuleName, msg.Type);
                        }
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "OutboxProcessor iteration failed");
            }

            try { await Task.Delay(poll, stoppingToken); } catch (OperationCanceledException) { break; }
        }

        logger.LogInformation("OutboxProcessor stopped");
    }
}
