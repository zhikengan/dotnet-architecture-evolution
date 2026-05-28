# ADR-0012: Quartz for scheduled jobs, Hangfire for fire-and-forget

**Status:** Accepted (Tier 4)

## Context

Tier 4 adds two distinct kinds of background work:

1. **Scheduled jobs** — runs on a cron (e.g., expire stale orders every 5 minutes; compute daily reports at 02:00). Deterministic schedule, persistent state ("did this job's 02:00 fire actually run today?"), missed-fire policies.
2. **Fire-and-forget** — triggered by an integration event, runs ASAP, no schedule. The OrderConfirmed handler enqueues a "send email" job; the job runs once with retries, and we want a dashboard to see queue depth and failures.

Both are legitimately "background work" but the requirements are different — and the .NET ecosystem already has two well-trodden libraries that each excel at one shape.

## Decision

**Quartz.NET for scheduled jobs. Hangfire for fire-and-forget. Both run in the Worker host.**

**Quartz (`src/Hosts/Worker/Configuration/QuartzConfiguration.cs`)**:
- `Quartz.Extensions.Hosting` with the default in-memory job store. Production should flip to `UsePersistentStore` against the Postgres `quartz` schema using Quartz's bundled SQL bootstrap.
- Two jobs:
  - `ExpireStaleOrdersJob` — cron `0 */5 * * * ?` in dev (every 5 min for visibility) / `0 */1 * * * ?` in prod. Walks every tenant, finds `Pending` orders older than 30 minutes, calls `ForceCancelOrderCommand` via MediatR.
  - `DailyReportingJob` — cron `0 0 2 * * ?` (02:00 UTC daily). Computes a per-tenant `DailyReport` row.
- Jobs use `[DisallowConcurrentExecution]` so a slow run doesn't overlap its next firing.
- Jobs walk tenants explicitly because the Worker scope has no ambient tenant — they iterate `platform.Tenants`, set `ITenantContextSetter` per tenant, then touch the tenant-filtered DbContexts.

**Hangfire (`src/Hosts/Worker/Configuration/HangfireConfiguration.cs` + `Modules/Platform`)**:
- `Hangfire.Core` + `Hangfire.PostgreSql` against a `hangfire` schema in the same Postgres database.
- Client + storage registered in `PlatformModule.AddPlatformModule` so both API and Worker can enqueue. Server registered only in the Worker host. Dashboard mounted at `/hangfire` on the Worker.
- The connection string is resolved lazily via the `(sp, cfg) => ...` overload — at module-DI time the test-fixture override hasn't been applied yet, so eager resolution captures the wrong host. Lazy resolution matches the EF DbContext pattern.
- Trigger: `WhenOrderConfirmed_SendEmail` integration handler enqueues `SendOrderEmailService.SendAsync(tenantId, orderId, buyerId)`. The handler wraps the enqueue in try/catch so a transient Hangfire storage failure can never wedge the upstream saga.

## Consequences

**Positive.**
- Each library used at its sweet spot. Quartz's cron + missed-fire handling > a hand-rolled BackgroundService loop; Hangfire's dashboard + retry visualization > anything we'd build.
- Operators get two diagnostic surfaces: scheduled-job state (Quartz's persistent store) and queue depth (Hangfire dashboard).
- The split is the seam Tier 5 needs to extract one or both into their own services.

**Negative.**
- Two libraries means two operational stories. Operators must understand both.
- Hangfire's PostgreSql storage owns its own schema, separate from EF migrations — outside the per-module migration story. Documented in the runbook.
- Quartz at default in-memory store loses scheduled-job state across Worker restarts. Acceptable for the showcase; production deploys must enable persistent storage before they care about missed fires.

## Alternatives considered

- **One library doing both.** Hangfire can do schedules; Quartz can do fire-and-forget. Either choice trades against the other's strengths. Forcing one tool to do both costs more than running both.
- **Bespoke `IHostedService` for everything.** Doable, but rebuilds the cron parser, the missed-fire policy, the retry semantics, the dashboard. Wrong leverage.
- **External job scheduler (Temporal, Sidekiq-equivalents).** Heavier than the Tier-4 audience needs. Tier 5 may re-evaluate when distributed correlation becomes a forcing function.
