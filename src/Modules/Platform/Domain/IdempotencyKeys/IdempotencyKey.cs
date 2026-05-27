namespace Platform.Domain.IdempotencyKeys;

public sealed class IdempotencyKey
{
    public string Key { get; set; } = string.Empty;
    public string ResponseJson { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
