# Runbook: Outbox messages are stuck (not being processed)

## Symptoms

- Orders that are placed never transition out of `Pending`.
- Buyer "cancel my order" returns 200 but stock isn't returned (Catalog never gets the `OrderCancelled` integration event).
- `outbox_messages_processed_total` counter has stopped incrementing in OpenTelemetry / Jaeger.
- `outbox_messages_failed_total` counter is climbing.

## What's happening

The `OutboxProcessor` background service in the Api host polls each module's `OutboxMessages` table every `Outbox:PollIntervalMilliseconds` (default 500ms). For each pending row it deserializes the payload, calls `IEventBus.PublishAsync`, and on success marks `ProcessedAt`. On failure, Polly retries up to `Outbox:MaxRetries` (5) with exponential backoff + jitter; if all retries fail, the row gets `RetryCount` incremented and `LastError` populated, then is skipped on future polls (per the `RetryCount < 5` filter in `IOutboxStore.GetPendingAsync`).

## Investigate

1. **Is OutboxProcessor running?** Check the Api host logs:
   ```
   OutboxProcessor starting; poll=500ms
   ```
   If you don't see it, the hosted service didn't start — `dotnet ef migrations` may have left the host in a half-initialized state, or `app.Run()` is being blocked elsewhere. Restart the Api process.

2. **Are there parked messages?** Query each module's outbox:
   ```sql
   SELECT id, type, retry_count, last_error, occurred_at
   FROM catalog.outbox_messages
   WHERE processed_at IS NULL
   ORDER BY occurred_at ASC
   LIMIT 50;

   SELECT id, type, retry_count, last_error, occurred_at
   FROM orders.outbox_messages
   WHERE processed_at IS NULL
   ORDER BY occurred_at ASC
   LIMIT 50;
   ```
   Look at `last_error` — that's the exception message from the last failed publish attempt.

3. **Is `IEventBus` resolving handlers?** If `last_error` mentions "No handlers for integration event" with a Debug-level log, the consumer module didn't register its `IIntegrationEventHandler<TEvent>`. Check the module's `Add{Module}Module` DI extension.

4. **Is the inbox blocking the consumer?** A consumer handler might be throwing because of a stale `(MessageId, ConsumerName)` row from a previous failed save. Inspect:
   ```sql
   SELECT * FROM catalog.inbox_messages WHERE message_id = '<id>';
   SELECT * FROM orders.inbox_messages WHERE message_id = '<id>';
   ```

## Recover

- **Resume retry for a parked row** (use sparingly — make sure the underlying bug is fixed first):
  ```sql
  UPDATE catalog.outbox_messages
  SET retry_count = 0, last_error = NULL
  WHERE id = '<message-id>';
  ```
  The next OutboxProcessor tick will pick it up.

- **Skip a poison message** (last resort — you're acknowledging the event will never be delivered):
  ```sql
  UPDATE catalog.outbox_messages
  SET processed_at = now(), last_error = 'manually skipped: <reason>'
  WHERE id = '<message-id>';
  ```
  Document the skip in the incident log; the saga that depended on it will need compensation.

- **Drain a backlog after a bug fix**: deploy the fix, reset `retry_count` on all parked rows in the affected outbox:
  ```sql
  UPDATE catalog.outbox_messages
  SET retry_count = 0, last_error = NULL
  WHERE processed_at IS NULL;
  ```
  Watch `outbox_messages_processed_total` climb.

## Prevent

- Add a real broker (Tier 5) with proper dead-letter handling.
- Alert on `outbox_messages_failed_total` rate > 0 for more than 5 minutes.
- Alert on max `occurred_at` of unprocessed rows getting older than 1 minute.
