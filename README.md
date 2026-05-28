# Marketplace — Tier 4 (Platform)

Tier 4 takes the Tier 3 modular monolith and adds the operational patterns of a serious production system: extracted Worker host, multi-tenancy with EF query filters, RS256 JWT with a demo issuer, S3-compatible storage via MinIO, Quartz for scheduled jobs, Hangfire for fire-and-forget background work, built-in rate limiting, custom OpenTelemetry metrics, expanded health checks. The architecture stays a modular monolith — Tier 5 distributes the modules.

> Looking for the big picture? Check out [`main`](../../tree/main) for the cross-tier showcase.

## Stack (additions over Tier 3)

- All of Tier 3 (.NET 10 / C# 14, EF Core 10 + PostgreSQL 17, MediatR 13, OpenTelemetry, modular monolith)
- **Multi-tenancy** via `IMultiTenant` + EF global query filters
- **JWT RS256** with PEM-encoded keypair + OIDC-shaped discovery (`/.well-known/openid-configuration` + `jwks.json`)
- **MinIO** (S3-compatible) for product images via presigned URLs
- **Quartz.NET 3** for cron-style scheduled jobs
- **Hangfire 1.8** + Postgres storage for fire-and-forget jobs (dashboard at `/hangfire`)
- **Rate limiting** (.NET built-in) per-user partitioning
- **Security headers** (CSP, HSTS, X-Frame-Options, etc.)
- Extracted **Worker host** (separate process from the API)
- `AspNetCore.HealthChecks.NpgSql` + custom MinIO + outbox-lag checks

## Project structure (19 projects total)

```
src/
├── BuildingBlocks/                — domain primitives, behaviors, outbox/inbox infra,
│                                    multi-tenancy primitives, storage abstraction,
│                                    JWT issuer/validator, rate limiting, security
│                                    headers, health checks, custom OTel meter
├── Contracts/
│   ├── Catalog.Contracts/         — integration events Catalog publishes (with TenantId)
│   ├── Orders.Contracts/          — integration events Orders publishes (with TenantId)
│   └── Platform.Contracts/        — IFeatureFlagQuery, IIdempotencyStore
├── Modules/
│   ├── Catalog/                   — Product aggregate (TenantId + ImageKey); owns "catalog" schema
│   ├── Orders/                    — Order aggregate (TenantId); owns "orders" schema
│   └── Platform/                  — Tenants, FeatureFlags, IdempotencyKeys, DailyReports,
│                                    SentEmails; owns "platform" schema; hosts Hangfire client
└── Hosts/
    ├── Api/                       — HTTP only (JWT auth, rate limiting, security headers,
    │                                 demo token issuer, OIDC discovery, image upload,
    │                                 buyer/seller/admin endpoints, /health/live + ready)
    └── Worker/                    — Background only (OutboxProcessor, Quartz jobs,
                                       Hangfire server + dashboard, /health)

tests/
├── ArchitectureTests/             — NetArchTest rules (module boundaries, IMultiTenant, Worker ⊥ MVC)
├── {Catalog,Orders,Platform}.UnitTests/        — domain logic, no I/O
├── {Catalog,Orders,Platform}.IntegrationTests/ — Testcontainers Postgres
├── EndToEndTests/                 — WebApplicationFactory + Testcontainers (Postgres + MinIO)
└── Worker.Tests/                  — IHost test host + Testcontainers (OutboxProcessor, Quartz jobs)

deploy/docker-compose.yml          — postgres + jaeger + minio + minio-init + api + worker
Dockerfile.api                     — multi-stage build for the API host
Dockerfile.worker                  — multi-stage build for the Worker host
```

## Run with Docker Compose

```bash
docker compose -f deploy/docker-compose.yml up --build
# postgres on :5432
# jaeger UI on http://localhost:16686
# minio API on :9000, console on http://localhost:9001 (minioadmin/minioadmin)
# api on http://localhost:5000
# worker /health on http://localhost:5001, hangfire dashboard on http://localhost:5001/hangfire
```

The API applies migrations + seeds the `acme` and `globex` tenants + catalog data on Development startup. The Worker starts the OutboxProcessor + Quartz scheduler + Hangfire server.

## Walk through the demo

[`demo.http`](demo.http) chains REST Client requests through:

1. `GET /.well-known/openid-configuration` + `jwks.json` — discovery
2. Mint `Seller` / `Buyer` / `Admin` tokens for `acme`, plus a separate buyer token for `globex`
3. Place an order under `acme`; watch the async saga complete via Worker outbox dispatch
4. Switch to the `globex` buyer; verify they see ONLY Globex products (multi-tenancy isolation)
5. Seller uploads a product image via presigned URL; buyer list shows `imageUrl`
6. Admin sets `EnablePremiumBadge` rollout to 100% → buyer's `isPremium` flips
7. Try the unauthenticated and wrong-role paths (S12, S13)

## Authentication (RS256 JWT + demo issuer)

Tier 4 graduates the Tier 3 HS256 mechanism to **RS256 with a real demo issuer**:

- `GET /demo/token?role=Buyer&tenant=acme&userId={guid}` (Development only) → returns an RS256 JWT signed with the configured RSA private key. Claims: `sub`, `NameIdentifier`, `Role`, `tenant_id`, `jti`.
- `GET /.well-known/openid-configuration` + `GET /.well-known/jwks.json` publish the public key — relying parties (Worker, future SDKs) validate without ever holding the signing material.
- `JwtTokenIssuer` (BuildingBlocks) signs; `JwtPublicKeyProvider` exposes the public side for both JwtBearer middleware and the JWKS endpoint.

The dev keypair sits in `appsettings.json` deliberately — operators override via secret store in prod and rotate via `KeyId`.

## Multi-tenancy

Every aggregate root in every module implements `IMultiTenant`. Each module's DbContext applies an EF global query filter (`e => e.TenantId == _tenant.TenantId`). The API host's `TenantMiddleware` reads `tenant_id` off the JWT and writes the scoped `ITenantContext`; the `InMemoryEventBus` does the same per integration-event dispatch in the Worker. An architecture test fails the build if any new aggregate forgets `IMultiTenant`.

Seeded tenants: `acme` (`aaaaaaaa-…`) + `globex` (`bbbbbbbb-…`).

## File uploads (presigned URLs to MinIO/S3)

Seller's two-step flow:

1. `POST /api/seller/products/{id}/image-upload-url` body `{ contentType }` → returns `{ uploadUrl, publicUrl, key, expiresAt }`. Key is `{tenantId}/{productId}/{guid}`.
2. Client PUTs bytes directly to `uploadUrl` (API host never sees the bytes).
3. `POST /api/seller/products/{id}/image` body `{ key }` → server verifies the object actually landed, then stamps `Product.ImageKey`.
4. Buyer's product DTO populates `ImageUrl` from `IFileStorage.GeneratePublicUrl(product.ImageKey)`.

`Storage:Provider = S3` uses MinIO/AWS SDK; tests use `LocalFileStorage` or `Testcontainers.Minio`.

## Worker host

The Worker (`src/Hosts/Worker/`) hosts:

- `OutboxProcessor` — moved here from the API at Tier 4. Polls each module's outbox, dispatches via `IEventBus` to subscriber integration handlers.
- **Quartz scheduled jobs** — `ExpireStaleOrdersJob` (cron) cancels `Pending` orders > 30 min old. `DailyReportingJob` (02:00 UTC) writes per-tenant summaries to `platform.daily_reports`.
- **Hangfire server + dashboard** — `WhenOrderConfirmed_SendEmail` enqueues `SendOrderEmailService` jobs that log + insert rows into `platform.sent_emails`. Dashboard at `/hangfire`.

The Worker is a `WebApplication` only because Hangfire's dashboard needs HTTP routing. An architecture test asserts the Worker assembly does not depend on MVC controllers.

## Rate limiting + security headers

- **`per-user-writes`** — token bucket, 10 req/min (default), partitioned on `ICurrentUser.UserId`. Applied to POST/PUT/DELETE.
- **`per-user-reads`** — fixed window, 100 req/min, partitioned by user. Applied to GET.
- 429 responses carry `Retry-After`. Limits configurable via `RateLimit:Writes` / `RateLimit:Reads` (tests crank them up to avoid spurious rejections).
- **Security headers** on every response: CSP `default-src 'self'`, HSTS 1 year, `X-Frame-Options: DENY`, `X-Content-Type-Options: nosniff`, `Referrer-Policy: strict-origin-when-cross-origin`.

## Observability

Custom OpenTelemetry instruments (registered on both hosts via `MarketplaceMeter.Name`):

- `marketplace.orders.placed.total` — counter, tagged with `tenant_id`
- `marketplace.orders.cancelled.total` — counter, tagged with `tenant_id` + `reason`
- `marketplace.stock.decrements.total` — counter, tagged with `tenant_id`
- `marketplace.outbox.processed.total` — counter, tagged with `module` + `outcome`
- `marketplace.outbox.lag.seconds` — histogram of `now - OccurredAt` per dispatched message

Plus the existing Tier 3 traces span both processes (API → outbox → Worker → outbox → API consumer).

## Health checks

- `GET /health/live` — process up, no downstream checks. Liveness only.
- `GET /health/ready` — DB connectivity + MinIO bucket reachable + outbox lag under threshold. Degraded if `MinioHealthCheck` fails; degraded/unhealthy if outbox lag > 30s / 5min.
- Worker exposes a tiny `/health` reporting `IHostApplicationLifetime.ApplicationStarted` state.

## Endpoints

| Method | Path | Audience |
|---|---|---|
| GET | `/demo/token?role=...&tenant=...&userId=...` | Dev only (no auth) |
| GET | `/.well-known/openid-configuration` | Anonymous |
| GET | `/.well-known/jwks.json` | Anonymous |
| POST | `/api/seller/products` | Seller |
| POST | `/api/seller/products/{id}/image-upload-url` | Seller |
| POST | `/api/seller/products/{id}/image` | Seller |
| GET  | `/api/seller/products` | Seller |
| GET  | `/api/buyer/products` (carries `isPremium` + `imageUrl`) | Buyer |
| POST | `/api/buyer/orders` (idempotency-key) | Buyer |
| POST | `/api/buyer/orders/{id}/cancel` | Buyer |
| GET  | `/api/buyer/orders[/{id}]` | Buyer |
| GET  | `/api/admin/products` | Admin |
| GET  | `/api/admin/orders` | Admin |
| POST | `/api/admin/orders/{id}/cancel` | Admin |
| GET/PUT/POST | `/api/admin/feature-flags/...` | Admin |
| GET | `/health/live`, `/health/ready` | — |

## Run the tests

```bash
dotnet test
# Catalog/Orders/Platform unit + integration suites (Testcontainers Postgres)
# Worker.Tests (IHost + Testcontainers Postgres)
# EndToEndTests (WebApplicationFactory + Testcontainers Postgres + MinIO)
# ArchitectureTests (10 facts: module isolation, IMultiTenant rule, Worker ⊥ MVC, ...)
```

## How to add a new module

1. Scaffold `src/Modules/X/X.csproj` + `src/Contracts/X.Contracts/X.Contracts.csproj`
2. **Make every aggregate root implement `IMultiTenant`** — the architecture test will fail otherwise
3. Define aggregates in `X/Domain/`, slices in `X/Application/`, EF config + module-specific DbContext (with `HasDefaultSchema("x")` + tenant query filter) in `X/Infrastructure/`
4. Add an `XModule.AddXModule(IServiceCollection, IConfiguration)` extension that wires MediatR, validators, DbContext, IOutboxStore, integration event handlers
5. Register integration event handlers as `IIntegrationEventHandler<TEvent>` — `InMemoryEventBus` finds them via DI and lifts `TenantId` onto the ambient context before invoking them
6. Wire `services.AddXModule(configuration)` in BOTH `Hosts/Api/Program.cs` AND `Hosts/Worker/Program.cs`
7. Add architecture tests for the new module's boundaries

## What's intentionally missing at Tier 4

| Missing | Earned at |
|---|---|
| Splitting modules into separate services | Tier 5 (microservices) |
| External message broker (still in-process `IEventBus`) | Tier 5 |
| Per-service database | Tier 5 |
| API Gateway / BFFs | Tier 5 |
| gRPC for cross-service calls | Tier 5 |
| Quartz persistent job store enabled by default | Production override |
| Real IdP federation (vs the demo issuer) | Production |

The next branch (`tier-5-microservices`) splits the modules into independently-deployable services with a real message broker, per-service databases, and BFFs.

## Architecture Decision Records

- [`docs/adr/0001-modular-monolith.md`](docs/adr/0001-modular-monolith.md) — three modules with enforced boundaries
- [`docs/adr/0002-outbox-inbox-pattern.md`](docs/adr/0002-outbox-inbox-pattern.md) — at-least-once + consumer dedup
- [`docs/adr/0003-cross-module-saga-via-events.md`](docs/adr/0003-cross-module-saga-via-events.md) — async PlaceOrder choreography
- [`docs/adr/0004-cqrs-audience-projections.md`](docs/adr/0004-cqrs-audience-projections.md)
- [`docs/adr/0005-db-backed-feature-flags.md`](docs/adr/0005-db-backed-feature-flags.md)
- [`docs/adr/0006-architecture-tests.md`](docs/adr/0006-architecture-tests.md)
- [`docs/adr/0007-jwt-bearer-with-dev-mint.md`](docs/adr/0007-jwt-bearer-with-dev-mint.md) — Tier 3's HS256 retrofit
- [`docs/adr/0008-extract-worker-host.md`](docs/adr/0008-extract-worker-host.md) — Worker process split
- [`docs/adr/0009-multi-tenancy-shared-db-strategy.md`](docs/adr/0009-multi-tenancy-shared-db-strategy.md) — `IMultiTenant` + query filters
- [`docs/adr/0010-rs256-jwt-and-demo-issuer.md`](docs/adr/0010-rs256-jwt-and-demo-issuer.md) — RS256 + OIDC-shaped discovery
- [`docs/adr/0011-s3-compatible-storage-with-presigned-urls.md`](docs/adr/0011-s3-compatible-storage-with-presigned-urls.md)
- [`docs/adr/0012-quartz-and-hangfire-split.md`](docs/adr/0012-quartz-and-hangfire-split.md)

## Runbooks

- [`docs/runbooks/outbox-stuck.md`](docs/runbooks/outbox-stuck.md)
- [`docs/runbooks/inbox-message-poisoned.md`](docs/runbooks/inbox-message-poisoned.md)
- [`docs/runbooks/tenant-leakage-investigation.md`](docs/runbooks/tenant-leakage-investigation.md) — **NEW Tier 4**
