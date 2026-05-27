namespace BuildingBlocks.Application;

public interface IIdempotencyStore
{
    Task<string?> TryGetAsync(string key, CancellationToken ct);
    Task SaveAsync(string key, string responseJson, CancellationToken ct);
}
