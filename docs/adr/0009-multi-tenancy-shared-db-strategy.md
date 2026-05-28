# ADR-0009: Multi-tenancy via shared DB + EF query filters

**Status:** Accepted (Tier 4)

## Context

Tier 4's stack supports multiple tenants (e.g., Acme and Globex use the same marketplace, but their data is isolated). The choices are well-known:

1. **DB per tenant** — strongest isolation, hardest to operate at scale, painful for cross-tenant analytics, expensive at 3–10 tenants.
2. **Schema per tenant** — middle ground; one DB but separate schemas. Migration story is N times the work.
3. **Shared schema, `TenantId` column + query filter** — cheapest to operate, smallest blast radius for "I forgot to filter".

Tier 4's audience is "a platform team with a handful of customers". Option 1 over-engineers; option 3 leaves the door open to a `WHERE TenantId = ?` bug leaking data across tenants.

## Decision

**Shared schema with `TenantId` on every aggregate, enforced by an architecture test and applied via EF Core global query filters.**

- `IMultiTenant` marker interface in `BuildingBlocks/Domain/MultiTenancy/`. Every aggregate root (`Product`, `Order`, `FeatureFlag`, `DailyReport`, `SentEmail`) implements it.
- `ITenantContext` (read) / `ITenantContextSetter` (write) — scoped service. The API host's `TenantMiddleware` writes it from the JWT `tenant_id` claim per request. The Worker's `InMemoryEventBus` writes it per integration-event dispatch from the message envelope.
- Each module's `DbContext` injects `ITenantContext` and configures `modelBuilder.Entity<T>().HasQueryFilter(e => e.TenantId == _tenant.TenantId)` for tenant-scoped entities. Outbox/inbox tables are intentionally **not** filtered — operators must see every tenant's pending messages.
- Aggregates take `tenantId` as a creation parameter so it can never be unset. Command handlers read it from `ITenantContext` and pass it to `Aggregate.Create`.
- Integration events carry `TenantId`; the bus lifts it onto the ambient context before invoking subscribers so consumer DbContexts apply the right filter without per-handler boilerplate.

The architecture test `All_aggregate_roots_must_implement_IMultiTenant` walks each module's assembly, finds types inheriting `AggregateRoot<TId>`, and fails the build if any one of them is missing `IMultiTenant`. That's the **load-bearing** check — query filters only work if every aggregate has the field.

## Consequences

**Positive.**
- One DB, one connection pool, one migration story. Operationally cheap at the assumed scale.
- The query filter is automatic — application code reads `db.Products.Where(...)` and never thinks about tenant scoping. `IgnoreQueryFilters()` is a deliberate, greppable escape hatch for migrations/seeding/operators.
- A cross-tenant leak now requires either bypassing the filter (greppable) or forgetting `IMultiTenant` on a new aggregate (caught by the architecture test on PR).
- Existing Tier 3 tests carry over by defaulting to a single tenant (Acme); the test fixture mints tokens with `tenant_id=Acme` by default.

**Negative.**
- One blast radius for tenant-related bugs — a stored-procedure or raw-SQL escape hatch that forgets to filter sees every tenant's data.
- Per-tenant performance tuning (indexes that ride a tenant-specific access pattern) becomes harder; an index that's hot for one tenant may not be for another.
- The composite key `(TenantId, Id)` on FeatureFlag is the only aggregate that needed it — every other table keys on `Id` alone and trusts the filter. The trade-off is intentional: most aggregates use a globally-unique surrogate key, so collisions across tenants can't happen.
- Outbox messages carry `TenantId` as a non-filtered column. That's load-bearing for the OutboxProcessor's tenant-aware dispatch. A migration that adds a new tenant-scoped table needs to remember the filter — flagged in the new-module how-to in README.

## Alternatives considered

- **EF Core's interceptor-based filter (rather than `HasQueryFilter`).** Possible but more code than what's already a documented EF pattern. We hit the documented "captured field" gotcha — see the `PendingModelChangesWarning` suppression in each DbContext.
- **Schema per tenant.** Adds N-times migration cost. Postpone to Tier 5 if any tenant ever requires hard data isolation (e.g., a customer with compliance constraints).
- **Multi-tenancy as a module rather than a cross-cutting concern.** Considered but rejected — every module owns tenant-scoped state, so a "Tenancy module" would have to grant every other module access to its primitives. The cross-cutting placement in BuildingBlocks is cheaper.
