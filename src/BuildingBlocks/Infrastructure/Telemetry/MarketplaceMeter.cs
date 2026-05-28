using System.Diagnostics.Metrics;

namespace BuildingBlocks.Infrastructure.Telemetry;

/// <summary>
/// Central <see cref="System.Diagnostics.Metrics.Meter"/> for marketplace-
/// specific instruments. Sibling to <see cref="MarketplaceActivitySource"/>
/// (tracing). Counters are tagged with <c>tenant_id</c> so observability
/// dashboards can slice by tenant — the prompt called this out explicitly
/// for orders.placed and orders.cancelled.
/// </summary>
public static class MarketplaceMeter
{
    public const string Name = "Marketplace";

    private static readonly Meter Instance = new(Name);

    /// <summary>Incremented once per successful <c>PlaceOrder</c> command.</summary>
    public static readonly Counter<long> OrdersPlaced =
        Instance.CreateCounter<long>("marketplace.orders.placed.total", "orders", "Orders successfully placed");

    /// <summary>Incremented once per <c>OrderCancelled</c> integration event with a reason tag.</summary>
    public static readonly Counter<long> OrdersCancelled =
        Instance.CreateCounter<long>("marketplace.orders.cancelled.total", "orders", "Orders cancelled");

    /// <summary>Incremented once per successful stock decrement in Catalog.</summary>
    public static readonly Counter<long> StockDecrements =
        Instance.CreateCounter<long>("marketplace.stock.decrements.total", "events", "Stock decrement events");

    /// <summary>Incremented once per outbox row processed; tagged with <c>module</c>.</summary>
    public static readonly Counter<long> OutboxProcessed =
        Instance.CreateCounter<long>("marketplace.outbox.processed.total", "messages", "Outbox messages processed");

    /// <summary>Histogram of (now − OccurredAt) seconds when dispatching an outbox row.</summary>
    public static readonly Histogram<double> OutboxLagSeconds =
        Instance.CreateHistogram<double>("marketplace.outbox.lag.seconds", "s", "Seconds between row OccurredAt and dispatch");
}
