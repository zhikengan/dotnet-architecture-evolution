using Polly;
using Polly.Retry;

namespace BuildingBlocks.Infrastructure.Resilience;

public static class OutboxResiliencePipeline
{
    public static ResiliencePipeline Build(int maxRetries) =>
        new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder().Handle<Exception>(),
                MaxRetryAttempts = maxRetries,
                Delay = TimeSpan.FromMilliseconds(200),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
            })
            .Build();
}
