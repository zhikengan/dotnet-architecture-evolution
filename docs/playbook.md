# Modular Monolith Playbook

> CQRS · DDD · Vertical Slices — from foundation to production for large-scale multi-audience .NET systems.

This is the reference for **Tier 3 and Tier 4**. If you're at Tier 1 or Tier 2, read [`decision-guide.md`](decision-guide.md) first and resist most of what's here.

The seven principles below are the *cheap-early, expensive-late* decisions from the decision guide. The rest of this document is mechanical: how to organize the code, how to handle data, how to operate the system, how to evolve it without rewriting.

---

## Part I — Foundations

### 1. The seven principles

Get these right first. They are expensive to reverse.

```
┌─────────────────────────────────────────────────────────────┐
│ 1. Modules = bounded contexts, NOT technical layers          │
│ 2. Audiences belong in endpoints + read models, NOT domain   │
│ 3. Commands express intent, not state mutation               │
│ 4. Vertical slices over technical layers                     │
│ 5. Domain is actor-agnostic; auth lives outside              │
│ 6. Everything flows through the Application layer            │
│ 7. Modules own their data; talk via Contracts only           │
└─────────────────────────────────────────────────────────────┘
```

What each violation looks like:

| Principle | Violation looks like |
|---|---|
| 1. Bounded contexts | Modules named `ApiModule`, `ServicesModule`, `DataModule` |
| 2. Audiences ≠ domain | `Catalog.Buyer`, `Catalog.Seller`, `Catalog.Admin` modules |
| 3. Intent commands | `UpdateOrderStatus(status)` instead of `ShipOrder` / `CancelOrder` |
| 4. Vertical slices | Global `Commands/`, `Queries/`, `Validators/` folders |
| 5. Actor-agnostic | `if (user.Role == "Admin")` inside an aggregate |
| 6. App layer rule | Background job calling `_repository.Add()` directly |
| 7. Module ownership | Foreign keys crossing schema boundaries |

### 2. System view

**Runtime topology.** Two hosts (API + Worker) sharing the same modules. The API serves HTTP; the Worker drains the outbox and runs scheduled work. Both register the same module DLLs — the difference is which services boot.

```
┌────────────────────────────────────────────────────────────────────┐
│                     RUNTIME PROCESSES                              │
│   ┌──────────────────┐         ┌──────────────────────┐            │
│   │   API Host       │         │   Worker Host        │            │
│   │  /api/buyer/*    │         │  Outbox processor    │            │
│   │  /api/seller/*   │         │  Inbox processor     │            │
│   │  /api/admin/*    │         │  Scheduled jobs      │            │
│   │  /api/internal/* │         │  Bus consumers       │            │
│   └────────┬─────────┘         └──────────┬───────────┘            │
│            └──────────────┬───────────────┘                        │
│                           │                                        │
│   ┌───────────────────────┴────────────────────────────────────┐   │
│   │                      MODULES                               │   │
│   │   ┌────────┐  ┌────────┐  ┌────────┐  ┌────────┐           │   │
│   │   │Catalog │  │Orders  │  │Payments│  │Identity│           │   │
│   │   └───┬────┘  └───┬────┘  └───┬────┘  └───┬────┘           │   │
│   │       └──────Contracts (integration events + DTOs)──────   │   │
│   └────────────────────────────────────────────────────────────┘   │
│   ┌─────────────────────────────────────────────────────────┐      │
│   │ BUILDING BLOCKS · CROSS-CUTTING · OBSERVABILITY         │      │
│   └─────────────────────────────────────────────────────────┘      │
└────────────────────────────────────────────────────────────────────┘
                                │
       ┌────────────────────────┴────────────────────────┐
       ▼            ▼              ▼            ▼        ▼
   PostgreSQL    Redis          S3/Blob    Key Vault   App Config
```

**The universal request flow.** Every trigger — HTTP, scheduler, webhook, bus event, domain event — uses the same path: thin adapter → command/query → pipeline behaviors → handler → aggregate → repository (+ outbox written atomically).

```
HTTP      Scheduler     Webhook       Bus event    Domain event
 │            │            │             │             │
 └────────────┴────────────┴─────────────┴─────────────┘
                           ▼
              ADAPTERS (thin: receive → build command → dispatch)
                           ▼
                 IMediator.Send / Publish
                           ▼
   PIPELINE BEHAVIORS (registered once, run for everything):
   Correlation → Logging → Validation → Authorization →
   Idempotency → Transaction → UnitOfWork
                           ▼
                  Command/Query Handler
                           ▼
                  Aggregate methods
                  (invariants + domain events)
                           ▼
                  Repository / DbContext
                  (+ outbox write atomically)
```

### 3. Project structure

**One project per module.** Folders inside, not sub-projects. Architecture tests enforce Domain → Application → Infrastructure direction.

```
MyMarketplace.sln
│
├── src/
│   ├── Modules/
│   │   ├── Catalog/         Catalog.csproj         ◆ one project per module
│   │   ├── Orders/          Orders.csproj
│   │   ├── Payments/        Payments.csproj
│   │   └── Identity/        Identity.csproj
│   │
│   ├── Contracts/                                  ◆ cross-module only
│   │   ├── Catalog.Contracts/
│   │   ├── Orders.Contracts/
│   │   ├── Payments.Contracts/
│   │   └── Identity.Contracts/
│   │
│   ├── BuildingBlocks/      BuildingBlocks.csproj  ◆ small shared kernel
│   │
│   └── Hosts/
│       ├── Api/             Api.csproj             ◆ /buyer /seller /admin
│       └── Worker/          Worker.csproj          ◆ outbox + jobs
│
├── tests/
│   ├── ArchitectureTests/
│   ├── {Module}.UnitTests/
│   ├── {Module}.IntegrationTests/
│   └── EndToEndTests/
│
├── deploy/    docker-compose.yml · helm · terraform
├── docs/      adr · runbooks · api
├── scripts/   seed · migrate · loadtest
├── .github/   workflows
│
├── Directory.Packages.props      ◆ central package versions
├── Directory.Build.props          ◆ solution-wide MSBuild
├── global.json                    ◆ .NET SDK pin
└── CLAUDE.md                      ◆ AI agent instructions
```

**Reference rules (non-negotiable, enforced with architecture tests):**

```
BuildingBlocks      →  nothing
{Module}.Contracts  →  BuildingBlocks (at most)
{Module}            →  BuildingBlocks, {Module}.Contracts
                       + OTHER MODULES' Contracts (never their main project)
Api, Worker         →  all modules + BuildingBlocks
```

If your modules reference each other's main projects, you have a modular monolith only in name. Add an architecture test that breaks the build the moment someone tries.

### 4. Inside a module

```
Modules/Catalog/Catalog.csproj
│
├── Domain/                                    ◆ no I/O, no EF
│   ├── Products/
│   │   ├── Product.cs                          aggregate root
│   │   ├── ProductId.cs                        strongly-typed ID
│   │   ├── ProductStatus.cs / ProductName.cs   value objects
│   │   ├── Events/                             ProductApproved.cs ...
│   │   └── Errors/                             ProductErrors.cs
│   ├── Categories/
│   └── SharedKernel/                           VOs across aggregates
│
├── Application/                               ◆ orchestration only
│   ├── Abstractions/
│   │   ├── IProductRepository.cs
│   │   ├── ICatalogDbContext.cs
│   │   ├── IFileStorage.cs
│   │   └── IClock.cs / ICurrentUser.cs
│   ├── Products/                              VERTICAL SLICES
│   │   ├── CreateProduct/
│   │   ├── ApproveProduct/
│   │   ├── ForceUnpublishProduct/
│   │   └── Queries/
│   │       ├── GetProductForBuyer/
│   │       ├── GetProductForSeller/
│   │       └── GetProductForAdmin/
│   ├── EventHandlers/
│   │   ├── Domain/                             in-module reactions
│   │   └── Integration/                        cross-module reactions
│   └── BackgroundJobs/                         scheduled commands
│
├── Infrastructure/                            ◆ EF, external services
│   ├── Persistence/
│   │   ├── CatalogDbContext.cs
│   │   ├── Configurations/                     EF fluent mappings
│   │   ├── Repositories/
│   │   ├── Migrations/                         EF-generated
│   │   ├── Interceptors/
│   │   │   ├── AuditInterceptor.cs
│   │   │   └── OutboxInterceptor.cs
│   │   └── ReadModels/                         denormalized projections
│   ├── Integrations/                           external service adapters
│   ├── Seeding/                                ICatalogReferenceDataSeeder
│   └── Configuration/                          CatalogOptions.cs
│
└── CatalogModule.cs                           ◆ public IServiceCollection ext
```

---

## Part II — Patterns

### 5. Vertical slices

**One folder = one use case.** Pass the *delete-a-feature* test: removing a feature touches exactly one folder.

```
Application/Products/CreateProduct/
├── CreateProductCommand.cs       ◆ public command record (intent)
├── CreateProductHandler.cs       ◆ orchestrates domain
├── CreateProductValidator.cs     ◆ input shape only (FluentValidation)
└── CreateProductResult.cs        ◆ shape returned to caller
```

Naming: **intent first**; actor only when invariants differ.

| ✓ Good | ✗ Avoid |
|---|---|
| `CreateProduct` | `BuyerCreateProduct`, `SellerCreateProduct` |
| `ApproveProduct` | `AdminApproveProduct` |
| `CancelOrder`, `ForceCancelOrder` | Buyer/Admin variants of the same logic |
| `GetProductForBuyer` (query) | `GetProduct` with role-based field hiding |

**The test:** if only the caller differs, you have *one* command with different policies. If invariants, fields, or rules differ, separate slices are correct.

### 6. Multi-audience

Two seams, not three: **endpoints** and **read models**. The domain stays actor-agnostic.

```
            ┌──────────────────────────────────────────┐
            │  THE DOMAIN — actor-agnostic              │
            │  Product, Order, Payment, Inventory, ... │
            └──────────────────────────────────────────┘
                              ▲
            ┌─────────────────┼─────────────────┐
     ┌──────┴──────┐   ┌──────┴──────┐   ┌──────┴──────┐
     │ Endpoints   │   │ Endpoints   │   │ Endpoints   │
     │ /buyer/...  │   │ /seller/... │   │ /admin/...  │
     └─────────────┘   └─────────────┘   └─────────────┘
     ┌─────────────┐   ┌─────────────┐   ┌─────────────┐
     │ Read model  │   │ Read model  │   │ Read model  │
     │ Buyer view  │   │ Seller view │   │ Admin view  │
     └─────────────┘   └─────────────┘   └─────────────┘
```

```
Hosts/Api/Endpoints/
├── Buyer/
│   ├── Catalog/   SearchProductsEndpoint.cs    GET /api/buyer/products
│   └── Orders/    PlaceOrderEndpoint.cs        POST /api/buyer/orders
├── Seller/
│   ├── Catalog/   CreateProductEndpoint.cs     POST /api/seller/products
│   └── Orders/    ShipOrderEndpoint.cs         POST /api/seller/orders/{id}/ship
├── Admin/
│   ├── Catalog/   ApproveProductEndpoint.cs    POST /api/admin/products/{id}/approve
│   └── Orders/    ForceCancelEndpoint.cs       POST /api/admin/orders/{id}/force-cancel
└── Internal/
    └── Webhooks/  StripeWebhookEndpoint.cs     POST /api/internal/webhooks/stripe
```

| ✓ Per-audience DTOs | ✗ One unified DTO |
|---|---|
| `BuyerProductDto`: Id, Name, Price, ImageUrl, Rating | Conditional field hiding based on roles |
| `SellerProductDto`: + Stock, Sales, Drafts | Role-based serialization filters |
| `AdminProductDto`: + Audit, Moderation, History | Constant DTO churn when roles change |

### 7. Communication — five scenarios, five patterns

| Scenario | Pattern | Cost |
|---|---|---|
| Within an aggregate | Direct method calls | free |
| Within a module | Domain events (in-memory, sync) | free |
| Across modules | Integration events (outbox) | cheap |
| Sync cross-module read | Contracts interface (rare) | avoid |
| External triggers | Adapters → MediatR commands | free |

**Decision tree:**

```
Does the listener live in the same aggregate?
                  │
        ┌─────────┴─────────┐
       Yes                  No
        │                    │
   Call method               ▼
       directly      Same module as raiser?
                            │
                  ┌─────────┴─────────┐
                 Yes                  No
                  │                    │
          Domain event            Integration event
          (in-memory)             (via outbox table)
          Same transaction        Eventually consistent
          Rolls back together     At-least-once delivery
```

### 8. Domain events vs integration events

|  | Domain event | Integration event |
|---|---|---|
| Lives in | Domain layer | Contracts project |
| Dispatched | MediatR in-memory | Outbox → async processor |
| Transaction | Same as aggregate | Separate per subscriber |
| Failure | Rolls back everything | Retried by outbox |
| Create when | A domain fact exists | A real consumer materializes |
| Delete when | Never | The last consumer goes away |
| Naming | `ProductApproved` (past tense) | `ProductPublishedIntegrationEvent` |

**Creation rules.** Create domain events freely — they describe the domain, raise on every fact even if there's no handler. Create integration events **only with a concrete consumer in the same PR**; delete them in the same PR as the last consumer.

**The relay pattern.** Aggregate raises a domain event → in-module handler decides whether to publish an integration event. Domain events are facts; integration events are contracts. They are not the same thing in different clothing.

### 9. Outbox + Inbox

**The dual-write problem.**

```
✗ DUAL WRITE — broken
  UPDATE Products ...     ✓
  broker.Publish(event)   ✗  → event lost forever if this fails

✓ OUTBOX — single transaction guarantees both writes
  ┌─────────────────────────────────────┐
  │ UPDATE Products ...                  │
  │ INSERT INTO OutboxMessages ...       │  same tx
  └─────────────────────────────────────┘
  Separate processor reads the outbox and publishes.
  Publishing is at-least-once; consumer dedups via inbox.
```

**Outbox table (per module):**

```sql
CREATE TABLE catalog.OutboxMessages (
    Id              UUID PRIMARY KEY,
    OccurredAt      TIMESTAMPTZ NOT NULL,
    Type            VARCHAR(500) NOT NULL,
    Payload         JSONB NOT NULL,
    ProcessedAt     TIMESTAMPTZ NULL,
    Error           TEXT NULL,
    RetryCount      INT NOT NULL DEFAULT 0,
    CorrelationId   VARCHAR(100) NULL
);
CREATE INDEX IX_Outbox_Pending ON catalog.OutboxMessages (OccurredAt)
    WHERE ProcessedAt IS NULL;
```

**Inbox table (per consumer module):**

```sql
CREATE TABLE search.InboxMessages (
    MessageId     UUID NOT NULL,
    ConsumerName  VARCHAR(200) NOT NULL,
    ProcessedAt   TIMESTAMPTZ NOT NULL,
    PRIMARY KEY (MessageId, ConsumerName)
);
```

Consumer flow: `BEGIN; IF NOT EXISTS in inbox THEN process + insert inbox; COMMIT`.

**Outbox + Inbox = effectively exactly-once.** Outbox guarantees at-least-once delivery; inbox deduplicates on the consumer side. This is the standard pattern for reliable async messaging.

### 10. Pipeline behaviors

Cross-cutting concerns belong here, not in base classes.

```
Request
  │
  ▼  CorrelationBehavior     ◆ propagate trace/correlation IDs
  ▼  LoggingBehavior          ◆ entry, exit, exception, duration
  ▼  ValidationBehavior       ◆ FluentValidation; fail fast
  ▼  AuthorizationBehavior    ◆ ownership checks (data-level)
  ▼  IdempotencyBehavior      ◆ key check; return cached if duplicate
  ▼  TransactionBehavior      ◆ tx open + outbox + domain events
  ▼  UnitOfWorkBehavior       ◆ DbContext flush
  ▼
  Handler
```

| Concern | Location |
|---|---|
| Authentication | ASP.NET Core middleware at the edge |
| Role / policy authz | `[Authorize]` on endpoint |
| Input shape validation | `ValidationBehavior` + FluentValidation per slice |
| Ownership / data-level authz | `AuthorizationBehavior` |
| Structured logging | `LoggingBehavior` |
| DB transactions | `TransactionBehavior` |
| Domain event dispatch | `DbContext` `SaveChanges` interceptor |
| Outbox publishing | `OutboxInterceptor` (same tx as save) |
| Idempotency | `IdempotencyBehavior` + key in header |
| Caching | Decorator over query handler |
| Retry / circuit breaker | Polly at infrastructure layer |
| Audit log | `ChangeTracker` inspection in interceptor |

---

## Part III — Data

### 11. CQRS

```
       WRITE SIDE                       READ SIDE
 ┌──────────────────────┐         ┌──────────────────────┐
 │ Aggregate-based      │         │ Projection-based     │
 │ Enforces invariants  │         │ Optimized for shape  │
 │ One per BC           │         │ Many per audience    │
 └──────────┬───────────┘         └──────────┬───────────┘
            │                                │
            ▼                                ▼
 ┌──────────────────────┐         ┌──────────────────────┐
 │ Commands             │         │ Queries              │
 │ ApproveProduct       │         │ GetProductForBuyer   │
 │ CancelOrder          │         │ GetProductForSeller  │
 │ ShipOrder            │         │ GetProductForAdmin   │
 └──────────────────────┘         └──────────────────────┘
```

**Read model freshness — pick per use case:**

| Pattern | Freshness | Use when |
|---|---|---|
| Live SQL view over write tables | Always fresh | Low volume, simple shape |
| Materialized table, domain event update | Same tx | Default for most cases |
| Replicated table, integration event update | Eventually consistent | Cross-module reads |
| External (Elastic, Redis) | Eventually consistent | Search, very high volume |

### 12. Database ownership

```
Database: marketplace
│
├── catalog schema             ◆ owned by Catalog module
│   ├── Products / Categories
│   ├── ProductReadModels       denormalized reads
│   ├── OutboxMessages
│   ├── InboxMessages
│   └── __EFMigrationsHistory_Catalog
│
├── orders schema              ◆ owned by Orders module
│   ├── Orders / OrderLines
│   ├── ProductSummaries        local replica of Catalog data
│   ├── OutboxMessages
│   ├── InboxMessages
│   └── __EFMigrationsHistory_Orders
│
├── identity schema
│   └── Users / Roles
│
└── platform schema            ◆ cross-cutting
    ├── IdempotencyKeys
    ├── FeatureFlags
    └── AuditLog
```

**Isolation evolves by maturity:**

| Stage | Setup | Why |
|---|---|---|
| Day 1 | One DB, separate schemas, no cross-FK | Fast, simple, modular logically |
| Year 2 | Same DB, read-only DB user per module | Schema isolation at connection level |
| Year 3+ | Split schema into own DB on extraction | Code unchanged; just connection string |

**Three things never happen:** (1) foreign keys across module schemas, (2) joins across module schemas, (3) one DbContext spanning modules.

### 13. Safe migrations — Expand-Contract

Rolling deployments mean old and new code run *simultaneously*. Every intermediate database state must be safe for **both**.

```
Phase 1: EXPAND       ADD COLUMN new_col NULL          ◆ old code unaffected
Phase 2: MIGRATE      Background job backfills new_col   in batches, resumable
Phase 3: DUAL WRITE   Deploy app writes BOTH, reads OLD
Phase 4: SWITCH READS Deploy app reads NEW, still writes both
Phase 5: STOP OLD     Deploy app writes NEW only
Phase 6: CONTRACT     DROP COLUMN old_col              ◆ after verification window
```

**Safety checklist:**

| ✗ Dangerous | ✓ Safe alternative |
|---|---|
| `RENAME COLUMN` | Add new + dual-write + drop old (5 deploys) |
| `DROP COLUMN` in same release as code change | Stop writing first, drop next release |
| `ALTER` to stricter type | Add new column with new type, backfill, switch |
| `NOT NULL` with default on large table | Add NULL, backfill in batches, add constraint later |
| `CREATE INDEX` on huge table | `CREATE INDEX CONCURRENTLY` (Postgres) |
| Heavy backfill inside migration | Idempotent background job, batched |
| `Database.Migrate()` in production | Generate idempotent SQL or bundle, apply in CI/CD |

### 14. EF migration workflow

**Day-to-day generation:**

```bash
# 1. Make your domain/entity change
# 2. Generate migration (uses the module's DbContext)
cd src/Modules/Catalog
dotnet ef migrations add AddProductRating \
    --context CatalogDbContext \
    --output-dir Infrastructure/Persistence/Migrations \
    --startup-project ../../Hosts/Api

# 3. ALWAYS review the generated file before applying
#    Look for: DropColumn, DropTable, risky defaults, long-running ops, missing indexes

# 4. Apply to local dev DB
dotnet ef database update --startup-project ../../Hosts/Api

# 5. If wrong (only before commit)
dotnet ef migrations remove --startup-project ../../Hosts/Api
```

**Naming:** descriptive + dated. `AddProductRating`, `Backfill_Products_Currency_DefaultUSD`, `ExpandPhase_AddNewProductSlug`. Never `Update1`, `Fix`, `NewFeature`.

**Two ways to ship to production:**

| Option | Pros | Cons |
|---|---|---|
| **Idempotent SQL script** (`dotnet ef migrations script --idempotent`) | Reviewable by DBA, idempotent, audit artifact | Needs a runner (sqlcmd, psql, Flyway) |
| **Migration bundle** (`dotnet ef migrations bundle --self-contained`) | Self-contained executable, no .NET SDK needed, one artifact per module | Bigger artifact |

Prefer bundles for Tier 3+. Run them as a **separate step before deploying the app**, never on startup.

### 15. Data seeding — four categories, four rules

| Type | What | When | Where |
|---|---|---|---|
| Reference | Lookups domain needs (currencies, statuses, categories) | Every startup, idempotent | `ICatalogReferenceDataSeeder` |
| System | Platform records (admin user, system roles) | Once per env, idempotent | `ISystemDataSeeder` |
| Demo | Sample data for QA / stakeholder demos | On demand in dev/staging | `Demo/` folder, env-gated |
| Test | Per-test fixtures and scenarios | Per test run | Builders + scenarios |

---

## Part IV — Operations

### 16. Configuration

Bind `appsettings.json` → strongly-typed `IOptions<T>` per module. Validate on startup with `ValidateDataAnnotations` + `ValidateOnStart`. **Never inject `IConfiguration` into handlers.**

### 17. Secrets

| Stage | Mechanism |
|---|---|
| Local dev | `dotnet user-secrets` |
| CI | Encrypted environment variables |
| Staging / prod | Cloud secret manager (Key Vault, AWS Secrets Manager, Doppler) |

### 18. Feature flags with progressive rollout

DB-backed or third-party (LaunchDarkly, ConfigCat). Sticky bucketing — `SHA256(userId + flagName) mod 100` — so the same user always gets the same result. Admin endpoints for: list, toggle, rollout %, explicit user enable. Cache flag definitions for 30 seconds.

### 19. File storage

`IFileStorage` abstraction. `S3FileStorage` for AWS / MinIO / Cloudflare R2. Presigned URLs for upload — never proxy bytes through your API.

### 20. Background jobs

| Need | Tool |
|---|---|
| Scheduled (cron-like) | Quartz.NET with DB-backed job store |
| Fire-and-forget | Hangfire |
| Outbox processor | `BackgroundService` in the Worker host |
| Long-running streams | `IHostedService` |

### 21. Resilience

Polly v8 pipelines on every outbound call to a flaky dependency: retry with exponential backoff + jitter, timeout, circuit breaker. Never retry non-idempotent operations without an idempotency key.

### 22. Caching

- `MemoryCache` is fine until you scale out (2+ instances).
- Redis when you need shared state.
- Cache-aside, never write-through unless the domain demands it.
- Always pair with a sane TTL and an explicit invalidation path.

### 23. Observability

OpenTelemetry for traces, metrics, logs. Instrument ASP.NET Core, HttpClient, EF Core, MediatR (via pipeline behavior), MassTransit. Custom `ActivitySource` per module. Custom metrics for the domain (`orders.placed.total`, `outbox.lag.seconds`, `stock.decrements.total`). Export via OTLP to your collector of choice (Jaeger, Tempo, Datadog, Honeycomb).

Serilog enriches every log with `TraceId`, `SpanId`, `CorrelationId`, `UserId`, `MachineName`. Unstructured logs are write-only data; structured logs are queryable assets.

---

## Part V — API & security

### 24. API design

- Audience-prefixed routes: `/api/buyer/*`, `/api/seller/*`, `/api/admin/*`, `/api/internal/*`.
- Problem Details (RFC 7807) for errors. Never leak stack traces.
- Versioning at the URL or header — pick one and stick with it.
- Idempotency-Key header on every mutation that external systems can call.
- Pagination by cursor (not page-offset) for anything that grows.

### 25. Multi-tenancy

- Every aggregate has `TenantId`. No exceptions.
- EF Core global query filter enforces isolation: `entity.HasQueryFilter(p => p.TenantId == ctx.TenantId)`.
- Tenant comes from the JWT, not from a request body. Never trust the body for security boundaries.
- Architecture test: every aggregate root implements `IMultiTenant`.
- E2E test: tenant A's buyer cannot see tenant B's products, even when probing by direct ID.

### 26. Security

- AuthN at the edge (middleware). AuthZ in two places: ASP.NET policies (role / claim) and `AuthorizationBehavior` (data-level ownership).
- HTTPS everywhere. HSTS, CSP, X-Frame-Options, no-cache on sensitive responses.
- Rate limit per-user-writes (token bucket, ~10/min) and per-user-reads (fixed window, ~100/min). Return 429 with `Retry-After`.
- Audit log for sensitive operations (force-cancel, force-unpublish, role change, tenant access).

---

## Part VI — AI-augmented workflow

### 27. Claude Code setup

A repo configured for AI-assisted delivery has:

- A small **`CLAUDE.md`** at the root listing conventions, forbids, and a pointer to deep docs.
- One CLAUDE.md per significant module if conventions diverge.
- `.claude/settings.json` with a pre-approved permission allowlist for `dotnet`, `docker`, `git` commands the team uses constantly.
- `.claude/skills/` for the team's repeating workflows (start a feature, run the verification suite, prepare a PR).

The goal is not "let the AI write code"; it's *load the AI with your conventions once* so it stops fighting them on every prompt.

### 28. New feature workflow

1. Pick the smallest vertical slice that delivers value.
2. Plan first (plan mode). Confirm the slice maps to *one* bounded context.
3. TDD the domain: write failing tests for every invariant, then minimal code.
4. Write the handler. Update the read model if needed. Add the endpoint.
5. End-to-end test the new slice. Verify the outbox if cross-module.
6. ADR if the decision is non-obvious.

### 29. Bug fix and refactor workflows

- Bug: reproduce in a failing test first. Only then fix.
- Refactor: green-to-green. Move in small commits. Tests stay passing on every commit.

### 30. PR review and merge

A PR should answer three questions for the reviewer:
1. What changed and why?
2. What did you test?
3. What risk did you take on?

If a PR can't be reviewed in 30 minutes, split it. If a PR needs a Zoom call to explain, the design isn't done yet.

---

## Part VII — Delivery

### 31. Module registration

Each module exposes one extension method — `services.AddCatalogModule(cfg)`. Composition roots (Api, Worker) call them all. No module touches the others' DI containers.

### 32. Testing

The pyramid:

| Layer | Tool | When it runs |
|---|---|---|
| Unit (domain) | xUnit + FluentAssertions | Every commit, sub-second |
| Application | xUnit + NSubstitute + builders | Every commit, < 30s total |
| Integration | xUnit + Testcontainers (Postgres) | Every commit, parallelizable |
| Architecture | NetArchTest + xUnit | Every commit, instant |
| End-to-end | `WebApplicationFactory` + Testcontainers | Every PR, < 5 min |
| Contract (Tier 5) | Pact.NET | Producer CI + consumer CI |

### 33. CI/CD pipeline

```
PR opened ─► build + unit + integration + architecture tests
         ─► generate migration SQL preview ─► post to PR comment
         ─► DBA / reviewer approves
Merge   ─► build module bundles, tag with SHA
        ─► deploy to staging, run smoke tests
        ─► manual approval gate
        ─► run migration bundles
        ─► deploy app (canary → full)
```

### 34. Gray-scale (canary) deployment

Deploy to 1% → 10% → 100% gated on error rate, latency, and a manual approval. Feature-flag the new behavior so you can disable code paths without redeploying. Always have a one-click rollback.

### 35. Quick reference

| If you need to... | Go to |
|---|---|
| Add a new use case | New folder under `Application/{Aggregate}/{UseCase}/` |
| Add a cross-module reaction | Integration event in `{Producer}.Contracts`, handler in `{Consumer}/Application/EventHandlers/Integration/` |
| Add a new entity | Aggregate root in `Domain/{Aggregate}/`, configuration in `Infrastructure/Persistence/Configurations/` |
| Add an audience | New endpoint folder + audience-specific DTO + query |
| Add a scheduled job | Quartz job in the Worker host, sends a MediatR command |
| Change DB schema | EF migration, reviewed in PR, applied via bundle pre-deploy |
| Promote a module to a service (Tier 5) | Same code; switch connection string + add MassTransit + add gRPC where unavoidable |

---

The principles at the top are non-negotiable. Everything below them is mechanical. Get the seven principles right, follow the patterns, and the rest of the system stays buildable for years.
