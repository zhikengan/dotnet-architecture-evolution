# ADR 0014 — gRPC reserved for sync-only calls

**Status**: Accepted (Tier 5)

## Decision

Default communication between services is **async via RabbitMQ events**. gRPC is reserved for the narrow set of synchronous lookups where async wouldn't work:

- `identity-service.GetUser` / `GetTenant` — needed at request validation time by BFFs and other services
- `platform-service.IsFeatureEnabled` — needed inside request handling; the answer can't lag
- `catalog-service.GetProduct` / `ListProducts` — admin views that need fresh, transactional reads
- `orders-service.GetOrder` — admin/BFF lookups

## Rule

If a piece of information *could* be eventually consistent without surprising the user, use events. Only fall back to gRPC when the freshness or latency requirement makes async untenable.

## Implementation note

gRPC service implementations are *thin adapters* — they translate wire messages into MediatR queries against the Application layer and back. They never hit the `DbContext` directly. This keeps the HTTP and gRPC paths consistent (same query filters, same DTOs, same tenant scoping, same authz).
