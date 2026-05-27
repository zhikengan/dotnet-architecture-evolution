using System.Text.Json;
using BuildingBlocks.Domain;
using Microsoft.EntityFrameworkCore;

namespace BuildingBlocks.Infrastructure.Outbox;

public static class OutboxHelpers
{
    public static OutboxMessage ToOutboxMessage<T>(T integrationEvent) where T : IIntegrationEvent =>
        new()
        {
            Id = integrationEvent.MessageId == Guid.Empty ? Guid.NewGuid() : integrationEvent.MessageId,
            Type = integrationEvent.GetType().AssemblyQualifiedName!,
            Payload = JsonSerializer.Serialize(integrationEvent, integrationEvent.GetType()),
            TenantId = integrationEvent.TenantId,
            OccurredAt = integrationEvent.OccurredAt == default ? DateTime.UtcNow : integrationEvent.OccurredAt,
        };

    public static void Enqueue<T>(this DbSet<OutboxMessage> outbox, T integrationEvent) where T : IIntegrationEvent =>
        outbox.Add(ToOutboxMessage(integrationEvent));
}
