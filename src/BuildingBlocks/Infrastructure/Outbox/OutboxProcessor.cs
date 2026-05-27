using System.Diagnostics.Metrics;
using System.Text.Json;
using BuildingBlocks.Infrastructure.EventBus;
using BuildingBlocks.Infrastructure.Resilience;
using BuildingBlocks.Infrastructure.Telemetry;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BuildingBlocks.Infrastructure.Outbox;

public sealed class OutboxProcessor(
    IEnumerable<IOutboxStore> stores,
    IEventBus bus,
    IOptions<OutboxOptions> options,
    ILogger<OutboxProcessor> logger) : BackgroundService
{
    private static readonly Meter Meter = new(MarketplaceActivitySource.Name);
    private static readonly Counter<long> Processed = Meter.CreateCounter<long>(
        "outbox_messages_processed_total", "messages", "Outbox messages processed");
    private static readonly Counter<long> Failed = Meter.CreateCounter<long>(
        "outbox_messages_failed_total", "messages", "Outbox messages that failed dispatch");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var poll = TimeSpan.FromMilliseconds(options.Value.PollIntervalMilliseconds);
        var pipeline = OutboxResiliencePipeline.Build(options.Value.MaxRetries);
        var storeList = stores.ToList();
        logger.LogInformation("OutboxProcessor starting; {Count} stores registered", storeList.Count);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                foreach (var store in storeList)
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
                            Processed.Add(1, new KeyValuePair<string, object?>("module", store.ModuleName));
                        }
                        catch (Exception ex)
                        {
                            await store.MarkFailedAsync(msg.Id, ex.ToString(), stoppingToken);
                            Failed.Add(1, new KeyValuePair<string, object?>("module", store.ModuleName));
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
