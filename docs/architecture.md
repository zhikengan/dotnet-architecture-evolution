# Tier 5 — Architecture

## Service map

| Service | Port (HTTP / gRPC) | Owns | Database |
|---|---|---|---|
| `identity-service` | 5300 / 5301 | Users, Tenants, JWT signing | `identity` on 5435 |
| `catalog-service`  | 5100 / 5101 | Products, Stock | `catalog` on 5433 |
| `orders-service`   | 5200 / 5201 | Orders, saga orchestration | `orders` on 5434 |
| `notifications-service` | 5400 / — | Notification records | `notifications` on 5436 |
| `platform-service` | 5500 / 5501 | FeatureFlags, IdempotencyKeys | `platform` on 5437 |
| `buyer-bff` (YARP) | 5010 | — | — |
| `seller-bff` (YARP) | 5020 | — | — |
| `admin-bff` (YARP) | 5030 | — | — |
| RabbitMQ 4 | 5672 / 15672 | Integration bus | — |
| Jaeger 1.66 | 16686 (UI), 4317 (OTLP) | Trace storage | — |

## Per-service internal structure

Each service mirrors Tier 4's Clean Architecture split:

```
services/{name}-service/
  src/
    {Name}.Domain/             aggregates + value objects + domain events + errors
    {Name}.Application/        commands, queries, MediatR handlers, MassTransit consumers
      Abstractions/            I{Name}DbContext
      {Aggregate}/             one folder per command/query group
        {Operation}/           one folder per command (command + validator + handler)
        Queries/               grouped query handlers
      EventHandlers/Integration/   When{Event}_{Action}.cs consumers
    {Name}.Infrastructure/     DbContext, EF Core configs, MassTransit wiring, seeders
    {Name}.Api/                ASP.NET Core minimal API endpoints + gRPC services + Program.cs
    {Name}.Worker/             headless background host that runs the same MassTransit pipeline
  proto/
    {name}.proto               gRPC contract
  tests/
    {Name}.UnitTests/          (placeholder — domain rules)
    {Name}.IntegrationTests/   (placeholder — Testcontainers Postgres + RabbitMQ)
    {Name}.ContractTests/      (placeholder — Pact consumer/producer verification)
  Dockerfile.api
  Dockerfile.worker            (where consumers exist)
  {Name}.sln
  README.md
```

## Why API + Worker

The API hosts HTTP/gRPC endpoints AND runs MassTransit consumers (so a single container can serve traffic end-to-end during local dev). The Worker is an identical messaging pipeline minus the HTTP/gRPC surface — in production you'd scale Workers independently of APIs (consumer throughput vs request throughput).

Both API and Worker register the same consumers on the same RabbitMQ queue; the broker load-balances. Idempotency at the consumer (MassTransit inbox) keeps this safe.

## Outbox / inbox

- **Outbox** — `AddEntityFrameworkOutbox<TDbContext>` + `UseBusOutbox()` installs MassTransit's own outbox tables in each service's DbContext. `IPublishEndpoint.Publish(...)` inside a transaction writes the message to the outbox table, not directly to RabbitMQ. The publish becomes part of the same SQL transaction as the aggregate insert. A background MassTransit dispatcher polls the outbox and delivers to RabbitMQ.
- **Inbox** — `ConfigureEndpoints` enables MassTransit's inbox per consumer; dedup by `MessageId`.

## Tenant resolution

- **HTTP requests** — `TenantMiddleware` (in `BuildingBlocks.Api`) reads `tenant_id` claim from the validated JWT principal and calls `ITenantContext.Set(tenantId)`.
- **MassTransit consumers** — each consumer reads `evt.TenantId` from the integration event and calls `tenant.Set(...)` itself before any DbContext query, so the EF Core query filter applies.
- **gRPC** — currently the gRPC adapters delegate to MediatR queries that explicitly `IgnoreQueryFilters()` (gRPC is used for cross-tenant admin/BFF lookups). When introducing tenant-scoped gRPC calls, propagate `tenant_id` via gRPC metadata.

## Distributed tracing

`BuildingBlocks.Infrastructure.Telemetry.ObservabilityExtensions.AddMarketplaceObservability` registers OpenTelemetry with:

- ASP.NET Core (inbound HTTP)
- HttpClient (outbound)
- Grpc.Net.Client (outbound gRPC)
- MassTransit (publish + consume)
- EF Core
- A shared `MarketplaceActivitySource` for manual spans

OTLP exporter ships to Jaeger at `Otel:Endpoint` (default `http://jaeger:4317` in compose). One PlaceOrder produces a single Jaeger trace spanning every span.

## Resilience

- MassTransit handler retries: exponential, 3 attempts, 1s→30s, ×2 multiplier
- Dead-letter exchanges per consumer (MassTransit default)
- Polly on outbound HTTP (consumers issuing HTTP — none currently, but pattern is there)

## Cross-service references

| Allowed | Forbidden |
|---|---|
| Service → its own Domain/Application/Infrastructure | ❌ Service → another service's main projects |
| Service → `*.Contracts` of any other service | ❌ Cross-service FK in the DB |
| Service → `BuildingBlocks` | ❌ Shared DbContext / schema |

## The PlaceOrder saga, hop by hop

```
buyer-bff ──HTTP──▶ orders-api
                       │ Order.Create() → Pending
                       │ SaveChangesAsync (atomic with bus outbox row)
                       │
                       │ OrderPlacedIntegrationEvent ── RabbitMQ ──▶ catalog-worker
                       │                                                  │ Product.Decrement
                       │                                                  │ SaveChangesAsync
                       │                                                  │
                       │     ◀── RabbitMQ ── StockDecrementedIntegrationEvent
                       │
                       │ Order.Confirm() → Confirmed
                       │ SaveChangesAsync
                       │
                       │ OrderConfirmedIntegrationEvent ── RabbitMQ ──▶ notifications-worker
                                                                              │ Notification.Create
                                                                              │ SaveChangesAsync
```

Failure paths:
- catalog stock decrement fails → `StockDecrementFailedIntegrationEvent` → orders flips to `Failed` → `OrderFailedIntegrationEvent` → notifications.
- Buyer cancels → orders publishes `OrderCancelledIntegrationEvent` with `StockWasDecremented` computed from pre-cancel order status. Catalog only returns stock when the flag is `true`.

## Tests

| Level | Where | What |
|---|---|---|
| Per-service unit | `services/{name}-service/tests/{Name}.UnitTests/` | Domain invariants + per-service architecture facts (Domain ⊥ EF / MassTransit) |
| Per-service integration | `services/{name}-service/tests/{Name}.IntegrationTests/` | EF round-trip against a Postgres Testcontainer |
| Per-service contract | `services/{name}-service/tests/{Name}.ContractTests/` | Consumer-driven pacts — JSON pinned per consumed event |
| Repo architecture | `tests/architecture/RepoArchitectureTests/` | No cross-service main project refs; BFFs ⊥ service impl; every aggregate root implements `IMultiTenant` (except `Identity.Tenant` itself) |
| Cross-service E2E | `tests/e2e/E2E/` | Full saga + key SHARED_SCOPE scenarios against a running compose stack |

Per-service tests run on every PR via the matching `.github/workflows/{service}-ci.yml`. The `e2e.yml` workflow brings the full compose stack up before running the e2e suite. Locally, e2e tests soft-skip with a clear message if `docker compose up -d --build --wait` hasn't been run.

## Develop a single service in isolation

You don't need the full 18-container stack to work on one service. Recipe:

```bash
# 1. Bring up just the infra dependencies you need:
docker compose -f deploy/docker-compose.yml up -d \
  rabbitmq jaeger identity-db identity-api catalog-db

# 2. Run the service of interest directly:
cd services/catalog-service/src/Catalog.Api
dotnet run

# 3. Exercise it via the per-service .http file (VS Code REST Client):
#    open services/catalog-service/catalog-service.http
```

Each service ships its own `{service}-service.http` that mints a token from identity-service and calls the service directly (bypassing BFFs). Useful for low-level debugging.

For full-stack work, the repo-root `demo.http` walks the saga end-to-end through the BFFs — the way a real client would. The corresponding Jaeger trace is the showcase: one click reveals the full request graph spanning HTTP → orders-api → RabbitMQ → catalog-worker → RabbitMQ → orders-worker → RabbitMQ → notifications-worker.

## ADRs

See `docs/adr/` for the per-decision rationale:

- `0012` — microservices decomposition
- `0013` — RabbitMQ for events
- `0014` — gRPC for sync calls only
- `0015` — API gateway per audience (YARP)
- `0016` — database per service
- `0017` — consumer-driven contracts
