# ADR-0002: Outbox + Inbox for at-least-once delivery and consumer dedup

**Status:** Accepted (Tier 3)

## Context

Cross-module integration events need to be reliable. The two failure modes that bite production:

1. **Dual-write inconsistency.** Module mutates aggregate A *and* publishes event B. The DB save succeeds; the event publish fails (broker hiccup, process crash, network blip). Aggregate A now lives in a world where B never happened. Other modules wait for B forever.
2. **Duplicate delivery.** Broker retries deliver the same event twice. The consumer side-effects (decrement stock, send email, charge card) run twice unless explicitly guarded.

## Decision

**Outbox** on the producer side, **Inbox** on the consumer side.

### Outbox

Each module's `DbContext` has an `OutboxMessages` table in its own schema. When a domain event fires inside `SaveChangesAsync`:

1. `DomainEventDispatchInterceptor` re-scans the change tracker for aggregates with raised events, publishes via MediatR.
2. Module-local `INotificationHandler<TDomainEvent>` translates to an integration event and adds an `OutboxMessage` to the *same* `DbContext` (using `db.OutboxMessages.Enqueue(intEvent)` extension that serializes the event to JSON + assembly-qualified type name).
3. EF Core SaveChanges re-scans tracked entities and commits the aggregate change AND the outbox row in a single transaction.

A background `OutboxProcessor` polls each module's outbox every 500ms (`OutboxOptions.PollIntervalMilliseconds`), deserializes pending rows, publishes via `IEventBus.PublishAsync(object)`, marks `ProcessedAt` on success. Polly v8 wraps publishing with retry-with-jitter; after `MaxRetries` (5) the row is parked with `RetryCount` + `LastError` populated (see `docs/runbooks/outbox-stuck.md` — todo).

### Inbox

Each module's `DbContext` has an `InboxMessages` table keyed on `(MessageId, ConsumerName)`. Each `IIntegrationEventHandler<TEvent>` follows the pattern:

```csharp
if (await inbox.HasProcessedAsync(evt.MessageId, ConsumerName, ct)) return;
// ... handle ...
inbox.MarkProcessed(evt.MessageId, ConsumerName);
await db.SaveChangesAsync(ct);
```

Same-transaction insert of the inbox row + the aggregate mutation. A redelivered event finds the row and returns silently.

### Result

- Outbox guarantees that an event reaches the bus if and only if the local transaction committed (no dual-write race).
- Inbox guarantees that *each consumer* processes a given message at most once, even under at-least-once delivery.
- The combination is **effectively-once processing** without distributed transactions.

## Consequences

**Positive.**
- A process crash between "DB committed" and "event published" loses nothing — the next poll resumes from `ProcessedAt IS NULL`.
- The same `MessageId + ConsumerName` key works whether messages arrive in order or out of order, or are redelivered.
- Tier 5's migration to RabbitMQ swaps `InMemoryEventBus` for a broker-backed implementation; outbox + inbox tables stay the same shape.

**Negative.**
- Two extra tables per module schema. Every cross-module mutation does two writes (aggregate + outbox; or aggregate + inbox).
- The 500ms poll interval means ~1s end-to-end latency for the PlaceOrder saga. Acceptable for Tier 3; Tier 5 can swap to `LISTEN/NOTIFY` or a real broker for tighter latency.
- Tests must wait for eventual consistency — `PlaceOrder` returns `Pending` and the test polls until `Confirmed`.

## Alternatives considered

- **Direct `IMediator.Publish` across modules.** Loses transactional guarantees and couples modules tightly. The whole reason we have a `Contracts` boundary is so the publish target isn't a hot reference.
- **Distributed transaction (XA / TransactionScope).** Heavyweight, fragile, and pushes the same problem onto the DBA instead of solving it.
- **Real broker now (RabbitMQ).** Reasonable but pre-mature: Tier 3's value is module boundaries, not network. Defer to Tier 5 when the deploy story actually has multiple processes.
