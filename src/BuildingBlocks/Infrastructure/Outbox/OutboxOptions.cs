namespace BuildingBlocks.Infrastructure.Outbox;

public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";
    public int PollIntervalMilliseconds { get; init; } = 500;
    public int BatchSize { get; init; } = 50;
    public int MaxRetries { get; init; } = 5;
}
