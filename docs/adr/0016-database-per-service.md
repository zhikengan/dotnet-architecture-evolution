# ADR 0016 — Database per service

**Status**: Accepted (Tier 5)

## Decision

Each service runs its own PostgreSQL 17 instance on its own port. No shared schemas, no cross-service foreign keys, no service ever connects to another's database.

| Service | DB host:port |
|---|---|
| `identity-service` | `identity-db:5432` (5435 on host) |
| `catalog-service` | `catalog-db:5432` (5433 on host) |
| `orders-service` | `orders-db:5432` (5434 on host) |
| `notifications-service` | `notifications-db:5432` (5436 on host) |
| `platform-service` | `platform-db:5432` (5437 on host) |

## Why

- The service boundary becomes *physical*, not just logical — accidental coupling becomes impossible (you can't `JOIN` across DBs)
- Each service can independently choose schema migration strategies and downtime windows
- Different services can pick different DB technologies later if needed (a search service might want Elasticsearch; an analytics service might want ClickHouse)

## Implications

- Joins across services are forbidden — replace with cross-service queries (gRPC) or denormalize via events
- Reporting/analytics that needs cross-service data lives in a downstream warehouse fed by integration events
- Operationally: 5 DBs to back up, monitor, patch
