using BuildingBlocks.Infrastructure.Inbox;
using BuildingBlocks.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Infrastructure.Persistence;

public sealed class CatalogOutboxStore(CatalogDbContext db) : IOutboxStore
{
    public string ModuleName => "Catalog";

    public async Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(int batchSize, CancellationToken ct) =>
        await db.OutboxMessages.AsNoTracking()
            .Where(m => m.ProcessedAt == null && m.RetryCount < 5)
            .OrderBy(m => m.OccurredAt)
            .Take(batchSize)
            .ToListAsync(ct);

    public async Task MarkProcessedAsync(Guid messageId, CancellationToken ct)
    {
        await db.OutboxMessages.Where(m => m.Id == messageId)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.ProcessedAt, DateTime.UtcNow), ct);
    }

    public async Task MarkFailedAsync(Guid messageId, string error, CancellationToken ct)
    {
        var truncated = error.Length > 4000 ? error[..4000] : error;
        await db.OutboxMessages.Where(m => m.Id == messageId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(m => m.RetryCount, m => m.RetryCount + 1)
                .SetProperty(m => m.LastError, truncated), ct);
    }
}

public sealed class CatalogInboxStore(CatalogDbContext db) : IInboxStore
{
    public Task<bool> HasProcessedAsync(Guid messageId, string consumerName, CancellationToken ct) =>
        db.InboxMessages.AsNoTracking()
            .AnyAsync(m => m.MessageId == messageId && m.ConsumerName == consumerName, ct);

    public void MarkProcessed(Guid messageId, string consumerName) =>
        db.InboxMessages.Add(new InboxMessage
        {
            MessageId = messageId,
            ConsumerName = consumerName,
            ProcessedAt = DateTime.UtcNow,
        });
}
