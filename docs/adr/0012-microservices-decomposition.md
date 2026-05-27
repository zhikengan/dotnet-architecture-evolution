# ADR 0012 — Microservices decomposition

**Status**: Accepted (Tier 5)
**Date**: 2026-05-28

## Context

Tier 4 ships a modular monolith — clean module boundaries, but one solution, one process, one database. The team is at the point where independent deployment per module would pay off (catalog and orders churn at different rates, identity needs its own SLA, notifications is bursty).

## Decision

Decompose along the Tier 4 module boundaries — Catalog, Orders, Identity, Notifications, Platform — into 5 services. Each owns its codebase, its database, and its deployment pipeline. Same domain rules, same 13 scenarios, same per-service Domain/Application/Infrastructure split as Tier 4 — physically separated instead of logically separated.

The cut lines come directly from Tier 4's modules because those boundaries are already validated:

- `Catalog.Module` → `catalog-service`
- `Orders.Module` → `orders-service`
- `Identity.Module` → `identity-service`
- `Notifications.Module` → `notifications-service`
- `Platform.Module` → `platform-service`

## Consequences

- Independent deploys, independent scaling, independent CI per service
- Mandatory async-by-default communication via RabbitMQ (see ADR 0013)
- Database per service (see ADR 0016)
- Distributed traces become essential (Jaeger)
- Operational complexity goes up — runbooks and dashboards needed (Jaeger, RabbitMQ Management UI)

## Alternatives considered

- **Microservice-per-aggregate** — explicitly avoided; would balloon to 8-10 services with no operational benefit
- **Stay modular monolith** — viable until the team is large enough that independent deploys matter; Tier 4 remains a valid endpoint
