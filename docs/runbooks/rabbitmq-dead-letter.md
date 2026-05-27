# Runbook — RabbitMQ dead-letter queues

When a consumer fails 3 times (exponential 1s → 30s ×2 multiplier), MassTransit moves the message to a dead-letter exchange.

## Triage

1. Open http://localhost:15672 (Management UI; in prod, the equivalent).
2. Look for any queue with `_skipped` or `_fault` suffix — those hold failed messages.
3. Click the queue → **Get message(s)** to inspect the payload and the `MT-Fault-Message` header for the exception.

## Common causes

- **Producer drift**: the integration event shape changed in a way that breaks the consumer's deserialization. Fix by updating contracts and republishing.
- **Downstream dependency down**: the consumer needs another service (gRPC) that's unreachable. Wait, then requeue.
- **Poison message**: a malformed message that will never succeed. Discard after recording.

## Requeue

Use the Management UI's **Move messages** feature to push from `_skipped` back onto the original queue once the underlying issue is fixed. MassTransit's inbox dedupes — replayed messages won't double-apply.
