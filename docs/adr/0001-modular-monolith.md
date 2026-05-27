# ADR-0001: Modular monolith with enforced module boundaries

**Status:** Accepted (Tier 3)

## Context

Tier 2 ships a single bounded context in four projects (Domain/Application/Infrastructure/Api) — correct for a small team, painful once features start crossing modules. At 5+ devs the symptoms are: PRs touching the same handler files, build slowdowns, and "I just need to add one field" turning into a five-file refactor. The next correct step is *not* microservices — that buys distributed-system pain for org-scale problems most teams don't have. The correct step is **modular monolith**: break the codebase into modules with explicit contracts, deploy as one process.

## Decision

Three modules: **Catalog**, **Orders**, **Platform** (cross-cutting feature flags + idempotency). The build enforces the boundaries:

1. Each module is its own `.csproj` with its own `Domain/Application/Infrastructure` folder structure (Tier 2 layering preserved *inside* each module).
2. Each module owns its own database schema (`catalog`, `orders`, `platform`) and its own `__EFMigrationsHistory_{Module}` table. No cross-schema foreign keys, no cross-schema joins.
3. Modules talk through **integration events** published on an in-process `IEventBus`. Direct method calls across modules are forbidden — a module reaching another module's domain types violates an architecture test that fails the build.
4. Each module publishes a `*.Contracts` assembly containing only integration-event records and (optionally) thin query interfaces (e.g., `IFeatureFlagQuery`). Other modules reference Contracts, never impl.

The Api host project remains a single deployable that wires all modules together.

## Consequences

**Positive.**
- The dependency graph is a tree. Catalog can't accidentally call into Orders even with autocomplete trying to help.
- Each module has its own migration history, schema, and lifecycle. Tier 5's database-per-service split is mechanical.
- Architecture tests catch boundary violations at PR time, not three sprints later when a tangled refactor becomes necessary.
- The outbox-and-event-bus indirection is the *same shape* the team will use when moving to a real broker at Tier 5. Code that ships at Tier 3 keeps working with minor adapter changes.

**Negative.**
- ~17 projects in the solution at startup (vs. 8 at Tier 2). Build times go up; navigation gets noisier.
- `PlaceOrder` is now async — buyers see a `Pending` status that flips after ~1s. UI/clients must handle that. Tests must wait for eventual consistency.
- Integration events are versioned by reference. Schema-evolving an integration event is non-trivial; we use append-only fields and keep the type's full name stable in the outbox payload.

## Alternatives considered

- **Stay at Tier 2 with discipline.** Doesn't survive contact with a growing team. The boundary needs to be enforced by the build, not by reviewers.
- **Jump to microservices.** Would buy distributed transactions, network failure modes, and a deploy story we don't yet need. Tier 3 is the right *cost-of-architecture* for a team that doesn't yet have a platform engineering function.
- **Two modules instead of three.** Platform is cross-cutting (every other module consumes feature flags + idempotency) and was already coupling Catalog/Orders before this split. Treating it as a third module with its own schema isolates the cross-cutting tables.
