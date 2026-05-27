namespace BuildingBlocks.Infrastructure.Outbox;

public interface IOutboxStore
{
    string ModuleName { get; }
    Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(int batchSize, CancellationToken ct);
    Task MarkProcessedAsync(Guid messageId, CancellationToken ct);
    Task MarkFailedAsync(Guid messageId, string error, CancellationToken ct);
}
