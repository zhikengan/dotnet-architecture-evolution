using BuildingBlocks.Application;
using Microsoft.EntityFrameworkCore;
using Platform.Domain.IdempotencyKeys;

namespace Platform.Infrastructure.Persistence;

public sealed class PlatformIdempotencyStore(PlatformDbContext db) : IIdempotencyStore
{
    public async Task<string?> TryGetAsync(string key, CancellationToken ct)
    {
        var existing = await db.IdempotencyKeys.AsNoTracking().FirstOrDefaultAsync(k => k.Key == key, ct);
        return existing?.ResponseJson;
    }

    public async Task SaveAsync(string key, string responseJson, CancellationToken ct)
    {
        db.IdempotencyKeys.Add(new IdempotencyKey
        {
            Key = key,
            ResponseJson = responseJson,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(ct);
    }
}
