using BuildingBlocks.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace BuildingBlocks.Infrastructure.Persistence;

public sealed class DomainEventDispatchInterceptor(IPublisher publisher) : SaveChangesInterceptor
{
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken ct = default)
    {
        var context = eventData.Context;
        if (context is null) return result;

        // Loop: handlers (e.g., domain -> outbox translators) may add more
        // entities/events to the change tracker. Drain until no events remain.
        while (true)
        {
            var aggregates = context.ChangeTracker.Entries()
                .Select(e => e.Entity)
                .OfType<IHasDomainEvents>()
                .Where(a => a.DomainEvents.Count > 0)
                .ToList();

            if (aggregates.Count == 0) break;

            var events = aggregates.SelectMany(a => a.DomainEvents).ToList();
            foreach (var a in aggregates) a.ClearDomainEvents();

            foreach (var evt in events)
            {
                await publisher.Publish((object)evt, ct);
            }
        }

        return result;
    }
}
