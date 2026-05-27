using Marketplace.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Marketplace.Infrastructure.Persistence.Interceptors;

public sealed class DomainEventDispatchInterceptor(IPublisher publisher) : SaveChangesInterceptor
{
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken ct = default)
    {
        var context = eventData.Context;
        if (context is null) return result;

        var aggregates = context.ChangeTracker.Entries()
            .Select(e => e.Entity)
            .OfType<IHasDomainEvents>()
            .Where(a => a.DomainEvents.Count > 0)
            .ToList();

        if (aggregates.Count == 0) return result;

        var events = aggregates.SelectMany(a => a.DomainEvents).ToList();
        foreach (var a in aggregates) a.ClearDomainEvents();

        foreach (var evt in events)
        {
            // Cast to object so the dynamic-dispatch overload of IPublisher.Publish
            // routes to concrete INotificationHandler<TEvent> handlers based on
            // runtime type, not the IDomainEvent compile-time type.
            await publisher.Publish((object)evt, ct);
        }

        return result;
    }
}
