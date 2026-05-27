# Evolution Path

> How to move from one tier to the next *without rewriting everything*.

The whole point of this repo is that the same business logic lives at every tier. Going from Tier 1 to Tier 5 is not a rewrite — it's a sequence of mechanical refactors, each one triggered by an actual problem.

Use this document **when reality forces you to move**. Read [`decision-guide.md`](decision-guide.md) first to make sure reality is actually forcing you, not aspiration.

---

## The cardinal rules

1. **Never refactor everything in one quarter.** Big-bang migrations are how teams end up shipping nothing for six months. Incremental wins.
2. **Migrate one pattern at a time.** Pick the one whose absence hurts most this month. Ship it. Measure. Move to the next.
3. **Every migration must keep the system shippable.** No "we'll fix it after the refactor" branches. Every commit on `main` deploys.
4. **Each pattern adopted gets an ADR.** Future you (or your replacement) will not remember why you did this. Record the trigger and the outcome.

---

## Tier 1 → Tier 2

> **Trigger:** A second developer joins. Code starts colliding. You've been at it 3+ months and the idea has validated.

**What changes.** One project becomes four. Inline endpoints become MediatR handlers. POCO models become aggregates with private setters. Inline validation becomes FluentValidation.

**Order of operations:**

1. **Split projects** without changing logic. `dotnet new classlib` for `Domain`, `Application`, `Infrastructure`. Move files; fix usings; verify the build still passes. One commit.
2. **Introduce MediatR.** Pick one endpoint; convert to a command + handler. Pipeline behaviors come *after* the first handler exists. Don't pre-build the pipeline.
3. **Convert one aggregate at a time.** Pick `Product` first (it has the most invariants). Add private setters, factory methods, `Result<T>`. Write failing tests for each invariant; make them pass.
4. **FluentValidation per slice.** One validator per command. Wire up `ValidationBehavior` to fail fast.
5. **Promote to Testcontainers.** Replace any in-memory provider tests with real PostgreSQL via Testcontainers.
6. **Add Serilog + a few ADRs.** Document the three biggest decisions you've made.

**Done when.** All Tier 2 conventions hold (see Tier 2's `CLAUDE.md`), the 13 shared-scope scenarios pass at the API level, CI is green.

**Expected duration.** 1–2 weeks for a 2-dev team. Longer if the original code is tangled — but in Tier 1, it usually isn't.

---

## Tier 2 → Tier 3

> **Trigger:** Multi-audience pressure ("can we get a seller portal?"), team growing past 3, or PRs in unrelated features starting to break each other.

**This is the biggest jump.** The phases below take 4–6 months for most teams. Do them in order. Resist the urge to do them in parallel.

### Phase 1 — Module split (month 1–2)

The hardest part. Get this wrong and everything downstream is harder.

1. **Identify bounded contexts.** Walk the use cases. Group them by *which language stakeholders use to talk about them*. If "stock" means something different to sellers than to warehouses, those are different contexts.
2. **Don't over-decompose.** Three modules is plenty to start. You can split further later; merging is harder.
3. **Move code into module folders within the same project first.** Just folders, no projects yet. Verify nothing crosses boundaries except through what will become Contracts.
4. **Promote folders to projects.** One module at a time. Add architecture tests in the same commit so violations don't slip back.
5. **Split the DbContext.** Per-module DbContext, per-module schema. Existing data: keep the same tables, add the schema prefix in EF mappings. Migrations: generate one per module; review carefully — the first cross-cutting migration is the dangerous one.

### Phase 2 — Pipeline behaviors and Result everywhere (month 2–3)

Cross-cutting in `BuildingBlocks`. Behaviors: Correlation, Logging, Validation, Authorization, Transaction, UnitOfWork. Idempotency comes later (Phase 5). `Result<T>` everywhere — finish the conversion you started at Tier 2.

### Phase 3 — Outbox + Inbox (month 3–4)

Cross-module communication is now async by default. Pick the most important cross-module flow first (in this repo: place-order → decrement-stock). Implement it end-to-end with outbox + inbox + integration events. Get the test for eventual consistency working before adding any other flow.

### Phase 4 — Multi-audience endpoints (month 4–5)

Split `Hosts/Api/Endpoints/` into `Buyer/`, `Seller/`, `Admin/`. Audience-specific DTOs (`BuyerProductDto`, etc.). Remove conditional field-hiding from any handler — fields should be on different DTOs, not optionally serialized.

### Phase 5 — Operations layer (month 5–6)

Feature flags (DB-backed or LaunchDarkly). Polly resilience on external calls. Idempotency keys on mutation endpoints. Extract the Worker host. OpenTelemetry traces and metrics.

### Phase 6 — Ongoing

Per-module dashboards. SLOs and alerting. Runbooks. ADR registry kept current.

**Expected duration.** 4–6 months. Most teams find that Phase 1 + Phase 2 deliver ~60% of the value. If you stop after those, you've still won.

---

## Tier 3 → Tier 4

> **Trigger:** Multi-tenancy lands. Compliance lands. Multiple teams form. Background work starts affecting API responsiveness. Auth needs to be real, not header-based.

This jump is *additive*. Tier 3 modules don't change; you add operational scaffolding on top.

1. **Multi-tenancy first.** Every aggregate gets `TenantId`. EF global query filter enforces isolation. Write the isolation-leak tests *first* — they're the most important ones in the entire suite. JWT contains `tenant_id`. Architecture test: every aggregate root implements `IMultiTenant`.
2. **JWT replaces header auth.** Set up the demo issuer in dev; swap in the real OIDC provider in prod. `ICurrentUser` extracts from `ClaimsPrincipal` instead of headers. Tier 3 tests need updating to issue tokens.
3. **Worker host extraction.** New `Worker.csproj`. Move outbox processor and any background services from `Api` to `Worker`. `Worker` has no HTTP — `IHost.CreateApplicationBuilder()`, not `WebApplication`. Health endpoint stays.
4. **File storage.** `IFileStorage` abstraction; S3 / MinIO implementation. Add the upload flow.
5. **Schedulers.** Quartz for cron jobs, Hangfire for fire-and-forget. Both in the Worker host.
6. **Rate limiting + security headers.** Built-in `AddRateLimiter`; per-user policies.
7. **Health checks split.** `/health/live` (process alive), `/health/ready` (deps reachable).
8. **Custom OpenTelemetry metrics.** `orders.placed.total`, `outbox.lag.seconds`, `stock.decrements.total`.

**Don't refactor the modules.** Tier 4 doesn't change module boundaries — it adds infrastructure. If a module needs restructuring, that's a separate effort.

**Expected duration.** 2–4 months. Multi-tenancy is the longest single piece; everything else is additive.

---

## Tier 4 → Tier 5

> **Trigger:** A module has a fundamentally different scaling profile (e.g., catalog read-heavy + cache-friendly vs orders write-heavy + transactional). Or organizationally: a separate team wants to own a module's deploy cadence. Or compliance demands physical data isolation.

**This is the move people get wrong.** Most "we should be microservices" conversations are actually "we have a Tier 3 problem we never solved." If your modular monolith is messy, extracting messy services makes everything worse.

### Pre-flight checks (don't skip these)

- [ ] Modules in the monolith have **zero** cross-references except via Contracts. Architecture tests prove it.
- [ ] Every cross-module flow already runs through outbox + inbox.
- [ ] Each module's DbContext is independent — no shared types, no cross-schema FKs.
- [ ] You can name *exactly* which module(s) will move first and why.
- [ ] You have distributed tracing working at Tier 4 so you'll be able to debug a distributed bug.

If any of these is "no", you have a Tier 3 problem to fix first.

### The extraction itself

1. **Pick one module.** Usually the one with the worst tier-3 friction (deploy cadence, scaling, ownership).
2. **Create the service folder.** Move the module's projects under `services/{name}/`. Add `Dockerfile.api`, `Dockerfile.worker`. Add its `.sln`.
3. **Switch communication to a real broker.** RabbitMQ via MassTransit. Same outbox/inbox concept; MassTransit handles both natively. The module's events become messages on the bus.
4. **Database extraction.** Stand up the service's own PostgreSQL instance. **Use Expand-Contract**: dual-write to old schema and new DB during transition; switch reads; stop dual-writing; drop old schema.
5. **Add the gateway path.** A BFF (YARP) routes traffic for that module to the new service.
6. **Contract tests.** Pact tests between this service and any consumer. Producer CI verifies its contract; consumer CI verifies it can consume the producer's current contract.
7. **Cutover.** Feature-flag the gateway routing. Switch 1% → 10% → 100% gated on error rate.
8. **Decommission.** Remove the module from the monolith only after the service has been stable for two weeks.

Repeat for the next module. **Never do two extractions in parallel.**

**Expected duration.** 6–12 weeks per service for a small team. Faster if you've done one before — much of the work becomes copy-and-modify.

---

## Common evolution failures

**Big-bang migration.** "We'll spend Q3 refactoring everything." Q3 ends with nothing shipped and a branch nobody can merge. Always do migrations as a sequence of small, individually shippable steps.

**Migrating without a trigger.** "We *might* need to scale catalog independently." If you don't have *measurable* scaling pressure, you're paying microservices cost for monolith problems. Stay at Tier 4 until reality forces you.

**Refactoring code while migrating architecture.** Each commit should change *either* architecture *or* code, not both. Mixed commits are unreviewable and undebuggable.

**Skipping the ADR.** Six months later, someone asks why a service was extracted, and nobody remembers the trigger. Three months after that, someone proposes merging it back. Always write the ADR at the time of the decision.

**Optimizing the migration plan instead of executing.** Plans that look elegant on paper often collapse on the first real obstacle. Pick a workable plan, start executing, adapt as you learn.

---

## How to know you migrated successfully

You migrated tier N → N+1 successfully when:

1. The 13 shared-scope scenarios still pass.
2. The new tier's `CLAUDE.md` accurately describes what you built.
3. The new tier's `verify-tier` audit passes (build green, tests green, architecture tests green, forbids clean).
4. You can name the specific problem that triggered the move, and you can show that the migration *measurably* reduced it.
5. The team is faster, not slower, three months after the migration completes.

If #5 isn't true after three months, the migration didn't fix what you thought it would. Go back to [`decision-guide.md`](decision-guide.md) and re-score.

---

> Evolution is mechanical when the foundation is right.
> Evolution is rewrite when the foundation isn't.
> Spend your effort on the foundation.
