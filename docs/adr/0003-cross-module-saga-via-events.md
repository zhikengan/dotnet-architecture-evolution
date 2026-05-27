# ADR-0003: Cross-module PlaceOrder saga via integration events

**Status:** Accepted (Tier 3)

## Context

At Tier 2, `PlaceOrderHandler` orchestrated across both aggregates in one transaction: load product, create order, decrement stock, confirm order, save. Synchronous, simple, atomic — and a hard coupling between `Catalog` and `Orders`. Tier 3 splits these into separate modules with separate schemas. We can't write across schemas in one transaction (and don't want to — that's the whole point of the split). The order placement flow has to become a **choreography**.

## Decision

PlaceOrder is an asynchronous **saga** that completes in five steps via four integration events:

```
1. POST /api/buyer/orders ──▶ Orders.PlaceOrderCommand
       Order.Create(...) -> Pending
       db.Orders.Add(order)
       Domain event OrderPlaced → outbox: OrderPlacedIntegrationEvent
       SaveChanges  ▶ 201 Created { status: "Pending" }

2. OutboxProcessor (orders) ──▶ IEventBus.Publish(OrderPlacedIntegrationEvent)

3. Catalog.WhenOrderPlaced_DecrementStock
       inbox check (message id + consumer name)
       Load Product. product.Decrement(qty, orderId)
         ✓ raises StockDecremented → outbox: StockDecrementedIntegrationEvent
         ✗ raises StockDecrementFailed → outbox: StockDecrementFailedIntegrationEvent
       inbox.MarkProcessed
       SaveChanges (single catalog-schema txn)

4. OutboxProcessor (catalog) ──▶ IEventBus.Publish

5a. Orders.WhenStockDecremented_ConfirmOrder
       inbox check
       Load Order. order.Confirm() raises OrderConfirmed
       SaveChanges  ▶ status flips to Confirmed
5b. OR Orders.WhenStockDecrementFailed_FailOrder
       inbox check
       order.Fail(reason) raises OrderFailed
       SaveChanges  ▶ status flips to Failed
```

End-to-end latency at default settings (500ms outbox poll, 2 hops): ~1s typical.

Cancellation follows the inverse: Order publishes `OrderCancelledIntegrationEvent` (carrying `StockWasDecremented: bool`). Catalog's `WhenOrderCancelled_ReturnStock` returns stock only when the flag is set, so a Pending or Failed order's cancel doesn't double-spend the return.

## Consequences

**Positive.**
- The two aggregates live in their own schemas with their own consistency boundaries. No cross-schema transactions; no two-phase commit.
- Failure modes are explicit and recoverable. A poison message stops at the outbox after `MaxRetries`; the inbox's idempotency means redeliveries are safe.
- The shape of the saga is the same shape we'd use with a real broker — moving to RabbitMQ at Tier 5 doesn't rewrite the handlers, just the bus.

**Negative.**
- `PlaceOrder` returns `Pending`. Clients must poll or be told the order is asynchronous. Tests have to wait for eventual consistency (we provide `WaitForOrderStatus(...)` helpers — todo).
- Compensation for partial failures is more complex than a synchronous handler. E.g., if `Catalog.WhenOrderPlaced` decrements stock but the subsequent `Orders.ConfirmOrder` fails repeatedly, the order is stuck in `Pending` with stock missing. The runbook is to inspect both modules' outbox + inbox, decide manually, and either re-run the consumer or compensate.
- Debugging is harder because the call stack is split across the OutboxProcessor tick. OpenTelemetry traces help (`MarketplaceActivitySource` + `propagation` via correlation id), but reading a trace is a different skill than reading a stack.

## Alternatives considered

- **Synchronous cross-module orchestrator.** Defeats the whole modular-monolith point — modules would directly invoke each other's domain methods, which is a single search-and-replace away from being a distributed monolith later.
- **Single-aggregate "OrderWithStock" combining Order + Product.** Forces unrelated concerns into one aggregate (sellers managing catalog vs. buyers placing orders) and undoes the Tier-3 separation we paid for.
- **Two-phase commit / sagas with explicit compensating commands.** Heavier than we need at Tier 3 — the current flow is choreography, not orchestration. If the order pipeline gets more steps (payment, fulfillment, notification) we'd revisit with an explicit saga orchestrator at Tier 4.
