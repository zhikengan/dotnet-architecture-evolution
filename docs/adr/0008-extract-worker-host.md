# ADR-0008: Extract Worker host

**Status:** Accepted (Tier 4)

## Context

At Tier 3 the API host did three things: served HTTP, processed the outbox, and held all the modules' DbContexts/handlers/integration handlers. One process, one operational profile, one scale dimension. That conflates "request path" with "background path" — a saga that backs up doesn't drop HTTP requests in obvious ways, but it does steal CPU and connection-pool capacity from them, and you can't scale the two independently.

Tier 4's audience is a product team running real workloads. They want to:

- Scale HTTP horizontally on traffic; scale background work on queue depth.
- Restart the API for code changes without losing in-flight outbox dispatches.
- Run scheduled jobs and fire-and-forget tasks in a host that doesn't accept user requests at all.

## Decision

Split the API host into two processes that share BuildingBlocks + Modules + database:

- **`src/Hosts/Api/`** — HTTP only. JWT auth, rate limiting, security headers, the buyer/seller/admin endpoint groups, the demo token issuer, OIDC discovery, file upload coordination, OpenTelemetry traces/metrics for the request path, health checks (live + ready). Does NOT process the outbox.
- **`src/Hosts/Worker/`** — Background only. Hosts the `OutboxProcessor`, Quartz scheduled jobs (`ExpireStaleOrdersJob`, `DailyReportingJob`), and the Hangfire server for fire-and-forget work. Has a tiny `/health` endpoint (process is up) and the Hangfire dashboard at `/hangfire`. Does NOT accept business HTTP.

Both hosts:

- Reference the same module projects via `AddCatalogModule` / `AddOrdersModule` / `AddPlatformModule`. Modules are host-agnostic; the differences are which `MapXxxEndpoints` calls run and which hosted services are registered.
- Share the same Postgres database — one source of truth for outbox/inbox/business state.
- Wire OpenTelemetry identically against the same OTLP endpoint so traces span both processes (a request creates an outbox row in the API → Worker picks it up → dispatches → another module's handler runs).

Both ship in their own container: `Dockerfile.api` and `Dockerfile.worker`. CI builds them via a matrix.

## Consequences

**Positive.**
- Independent scale + restart of the two profiles.
- Worker tests can exercise the no-MVC shape — an architecture test asserts the Worker assembly does not depend on `Microsoft.AspNetCore.Mvc.Core` etc.
- Production deploys can route operator traffic (Hangfire dashboard, /health) to the Worker without exposing the API surface there.

**Negative.**
- Two containers to operate, two deployments to coordinate during cutover. Manifested cost: the Worker process needs the same secrets (DB connection, OTel endpoint) as the API — config drift between the two is now a real risk.
- The e2e test fixture has to co-locate the OutboxProcessor in the API test process (`ConfigureTestServices` adds it) because spinning up a real Worker host alongside `WebApplicationFactory<Program>` is overkill for the saga assertions. The production shape is enforced by the architecture test, not by the e2e fixture.
- The Worker IS a `WebApplication` (not a bare `IHost`) because Hangfire's dashboard needs HTTP routing. We mitigate by asserting "no MVC" architecturally — the dashboard renders via its own middleware, not Mvc.

## Alternatives considered

- **Single host with named scale-out flags.** Keeps one Dockerfile but multiplies role-aware branching across `Program.cs`. The host-versus-role conflation that bit us at Tier 3 doesn't go away.
- **Bare `IHost` for Worker.** Cleanest for "no HTTP at all", but Hangfire's dashboard is the operator's tool and shipping without it costs more than the narrow MVC-free contract we have now.
- **One project, two entry-points.** Doable via project-level `OutputType` tricks but obscures the API/Worker boundary at the file level. Tier 5 splits further; making the boundary explicit now is the cheap forcing function.
