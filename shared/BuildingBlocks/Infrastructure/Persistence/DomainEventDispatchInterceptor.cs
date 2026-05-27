using BuildingBlocks.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace BuildingBlocks.Infrastructure.Persistence;

/// <summary>
/// Dispatches domain events raised by tracked aggregates via MediatR after
/// SaveChanges succeeds. Translation from domain → integration event happens
/// inside domain event handlers (which call <c>IPublishEndpoint.Publish</c>
/// — MassTransit's EF Core bus outbox keeps the publish transactional with
/// the original save).
/// </summary>
public sealed class DomainEventDispatchInterceptor(IPublisher publisher) : SaveChangesInterceptor
{
    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is null) return result;

        var aggregates = eventData.Context.ChangeTracker
            .Entries<IHasDomainEvents>()
            .Where(e => e.Entity.DomainEvents.Count > 0)
            .Select(e => e.Entity)
            .ToList();

        var events = aggregates.SelectMany(a => a.DomainEvents).ToList();
        foreach (var agg in aggregates) agg.ClearDomainEvents();

        foreach (var domainEvent in events)
        {
            await publisher.Publish(domainEvent, cancellationToken);
        }

        return result;
    }
}
