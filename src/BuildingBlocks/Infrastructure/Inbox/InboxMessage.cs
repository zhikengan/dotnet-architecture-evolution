namespace BuildingBlocks.Infrastructure.Inbox;

public sealed class InboxMessage
{
    public Guid MessageId { get; set; }
    public string ConsumerName { get; set; } = string.Empty;
    public DateTime ProcessedAt { get; set; }
}
