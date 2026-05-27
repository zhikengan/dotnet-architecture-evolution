# ADR 0013 — RabbitMQ for integration events

**Status**: Accepted (Tier 5)

## Decision

RabbitMQ 4 is the integration bus. MassTransit 8 owns producer outbox, consumer inbox, retry policy, and dead-lettering. Every cross-service event is published as an `IIntegrationEvent` record from the producer's `*.Contracts` project.

## Rationale

- MassTransit `AddEntityFrameworkOutbox + UseBusOutbox()` gives transactional publish without writing our own outbox processor
- MassTransit's per-consumer inbox handles dedup
- RabbitMQ Management UI gives operators visibility (queues, dead-letter, retry counts) without a separate tool
- Polly-style exponential retry built in (`UseMessageRetry`)

## Alternatives

- **Kafka** — better fit at much higher throughput; introduces its own ops complexity. Reconsider at scale.
- **AWS SQS/SNS** — cloud lock-in, fine if the org is committed; chose to stay portable for the reference repo.
- **In-process events only** — only works in monolith; explicitly excluded by Tier 5 decomposition.
