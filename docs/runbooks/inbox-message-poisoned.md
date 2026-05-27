# Runbook: An inbox message is "poisoned" (consumer keeps failing)

## Symptoms

- A specific integration event fails to be consumed: e.g. `StockDecrementedIntegrationEvent` for order X keeps throwing in `Orders.WhenStockDecremented_ConfirmOrder`.
- The producer's outbox row for that message is parked (`retry_count = 5`, `last_error` populated).
- Order X is stuck in `Pending` even though stock was decremented in Catalog (or vice versa).

## What's happening

Inbox dedup works on `(MessageId, ConsumerName)`. If a consumer crashes *before* `inbox.MarkProcessed` is reached, the message will be redelivered. If it crashes for the *same reason* every retry — bad payload, missing FK target, unhandled domain error — the outbox eventually parks it after 5 attempts and stops retrying.

The order's state is now divergent across modules: Catalog decremented stock; Orders never confirmed; the buyer sees `Pending` forever and the seller sees stock missing.

## Investigate

1. **Identify the message and consumer.**
   ```sql
   SELECT id, type, retry_count, last_error, occurred_at
   FROM catalog.outbox_messages
   WHERE processed_at IS NULL AND retry_count >= 5;
   ```
   (And the same for `orders.outbox_messages`.) The `type` column tells you which event; `last_error` includes the exception + stack.

2. **Did the consumer already mark inbox?**
   ```sql
   SELECT * FROM orders.inbox_messages WHERE message_id = '<id>';
   ```
   If the row exists, the consumer ran successfully *at least once* — the parked outbox row is a duplicate that the dedup absorbed silently. You can safely mark the outbox row processed.

3. **What aggregate state is divergent?**
   ```sql
   -- For a stuck PlaceOrder saga:
   SELECT id, status, failure_reason FROM orders.orders WHERE id = '<order-id>';
   SELECT id, stock, status FROM catalog.products WHERE id = '<product-id>';
   ```
   Compare to what the saga "should" have produced based on the original `OrderPlacedIntegrationEvent`.

## Recover

- **If the consumer's logic was buggy and you've deployed a fix:** clear the inbox row (so the consumer re-processes), reset the outbox row, and let the saga re-run.
  ```sql
  DELETE FROM orders.inbox_messages WHERE message_id = '<id>' AND consumer_name = '<name>';
  UPDATE catalog.outbox_messages SET retry_count = 0, last_error = NULL WHERE id = '<id>';
  ```

- **If the message is genuinely undeliverable** (e.g., it references an entity that no longer exists): mark inbox + outbox as processed and manually reconcile the aggregate state.
  ```sql
  -- Acknowledge the inbox so future redeliveries are silent
  INSERT INTO orders.inbox_messages (message_id, consumer_name, processed_at)
  VALUES ('<id>', '<consumer>', now())
  ON CONFLICT DO NOTHING;
  -- Mark outbox processed
  UPDATE catalog.outbox_messages SET processed_at = now(), last_error = 'manually skipped: <reason>'
  WHERE id = '<id>';
  -- Reconcile aggregate state, e.g. mark order Failed with reason
  UPDATE orders.orders SET status = 3, failure_reason = '<reason>' WHERE id = '<order-id>';
  ```

- **If state is divergent across modules**, decide which module is canonical for the affected entity and compensate:
  - Order is `Pending` but Catalog already decremented stock → mark Order `Failed` and emit a synthetic `OrderCancelled` (StockWasDecremented=true) so Catalog returns the stock.
  - Order is `Confirmed` but Catalog never decremented (rare; race that needs investigation) → manually decrement Catalog and log the compensation.

## Prevent

- Avoid unhandled exceptions in consumers. Wrap any non-deterministic logic (HTTP calls, third parties) in defensive code that either succeeds idempotently or logs and lets the message be redelivered.
- Add a unique constraint or "expected state" check inside the consumer to catch divergence early.
- Alert on outbox rows with `retry_count >= 5`.
- A formal saga orchestrator (Tier 4+) with explicit compensation steps would replace these manual interventions.
