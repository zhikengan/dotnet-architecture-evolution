using BuildingBlocks.Domain;

namespace Platform.Domain.IdempotencyKeys;

public sealed class IdempotencyKey : Entity<string>, IMultiTenant
{
    public Guid TenantId { get; private set; }
    public string ResultJson { get; private set; } = string.Empty;
    public DateTime SeenAt { get; private set; }

    private IdempotencyKey() { }

    public static IdempotencyKey Create(string key, Guid tenantId, string resultJson, DateTime now) => new()
    {
        Id = key,
        TenantId = tenantId,
        ResultJson = resultJson,
        SeenAt = now,
    };
}
