namespace BuildingBlocks.Infrastructure.Inbox;

public interface IInboxStore
{
    Task<bool> HasProcessedAsync(Guid messageId, string consumerName, CancellationToken ct);
    void MarkProcessed(Guid messageId, string consumerName);
}
