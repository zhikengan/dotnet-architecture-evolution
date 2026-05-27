# Marketplace — Tier 3 (Modular Monolith)

The architecture a 3–10-dev product team should ship once Tier 2 starts to creak: features take longer, modules collide, debugging a bug in one area requires understanding five. The single bounded context splits into three modules — **Catalog**, **Orders**, **Platform** — communicating through **integration events on an outbox/inbox pattern**. Modules never reference each other's impl projects, only each other's `*.Contracts` assemblies. The build refuses to compile a shortcut.

> Looking for the big picture? Check out [`main`](../../tree/main) for the cross-tier showcase.

## Stack

- .NET 10 / C# 14, file-scoped namespaces, nullable enabled, warnings as errors
- ASP.NET Core Minimal APIs
- EF Core 10 + PostgreSQL 17 (one DbContext per module, separate schemas)
- MediatR 13 + FluentValidation 12 + pipeline behaviors per module
- Polly v8 resilience pipeline for outbox dispatch
- Serilog 9 + OpenTelemetry 1.10 (OTLP → Jaeger)
- xUnit + FluentAssertions + NetArchTest for module-boundary enforcement
- Docker Compose: postgres + jaeger + app

## Project structure (17 projects total)

```
src/
├── BuildingBlocks/                — domain primitives, behaviors, outbox/inbox infra
├── Contracts/
│   ├── Catalog.Contracts/         — integration events Catalog publishes
│   ├── Orders.Contracts/          — integration events Orders publishes
│   └── Platform.Contracts/        — IFeatureFlagQuery
├── Modules/
│   ├── Catalog/                   — product aggregate; owns "catalog" schema
│   ├── Orders/                    — order aggregate; owns "orders" schema
│   └── Platform/                  — feature flags + idempotency; owns "platform" schema
└── Hosts/Api/                     — composition root + audience-specific endpoints

tests/
└── ArchitectureTests/             — NetArchTest rules (module boundaries, layering)
```

**Reference graph (enforced by the build):**

```
BuildingBlocks
   ▲
   │
   ├── {Catalog,Orders,Platform}.Contracts
   │
   └── {Catalog,Orders,Platform} module projects
            │                │              │
            └─ owns its Contracts; references SIBLING Contracts where it subscribes;
               NEVER references another module's impl
            
            Hosts/Api ── references all modules + all Contracts + BuildingBlocks
```

## The cross-module saga (the headline feature)

`PlaceOrder` becomes **asynchronous**:

```
1. POST /api/buyer/orders             ─▶  Orders.PlaceOrderHandler
                                            Order.Create -> Pending
                                            db.Orders.Add + db.Outbox enqueue
                                            SaveChanges (single txn)
                                            (returns 201 with status=Pending)
2. OutboxProcessor tick ─▶ IEventBus.Publish(OrderPlacedIntegrationEvent)
3. Catalog.WhenOrderPlaced_DecrementStock (inbox-check, decrement, enqueue
   StockDecremented or StockDecrementFailed in catalog outbox, mark inbox)
4. OutboxProcessor tick (catalog) ─▶ IEventBus.Publish
5. Orders.WhenStockDecremented_ConfirmOrder OR _FailOrder
   (inbox-check, Confirm() or Fail(reason), mark inbox)
```

Saga completes in ~1s (500ms outbox poll × 2 hops). `GET /api/buyer/orders/{id}` shows `Confirmed` or `Failed` once the saga settles.

## Run with Docker Compose

```bash
docker compose -f deploy/docker-compose.yml up --build
# postgres on :5432
# jaeger UI on http://localhost:16686
# app on http://localhost:5000
```

The app applies all three modules' migrations and seeds reference data (3 products + EnablePremiumBadge flag) on Development startup.

## Walk through the demo

[`demo.http`](demo.http) chains named REST Client requests so dynamic seed IDs flow through. Covers S1–S13 from `SHARED_SCOPE.md`, the async saga (PlaceOrder returns Pending; re-GET shows Confirmed), and the feature-flag toggle (admin sets `EnablePremiumBadge` rollout to 100%; buyer's `isPremium` flips after cache TTL).

## Auth (header-based, same as Tier 2)

`X-User-Role` (Buyer/Seller/Admin) + `X-User-Id` (Guid). Missing → 401, wrong role → 403. JWT lands at Tier 5.

## Endpoints

| Method | Path | Audience |
|---|---|---|
| POST | `/api/seller/products` | Seller |
| GET  | `/api/seller/products` | Seller |
| GET  | `/api/buyer/products` (carries `isPremium` per feature flag) | Buyer |
| POST | `/api/buyer/orders` (accepts `Idempotency-Key` header) | Buyer |
| POST | `/api/buyer/orders/{id}/cancel` | Buyer |
| GET  | `/api/buyer/orders[/{id}]` | Buyer |
| GET  | `/api/admin/products` | Admin |
| GET  | `/api/admin/orders` | Admin |
| POST | `/api/admin/orders/{id}/cancel` | Admin |
| GET  | `/api/admin/feature-flags` | Admin |
| PUT  | `/api/admin/feature-flags/{name}/rollout` | Admin |
| PUT  | `/api/admin/feature-flags/{name}/users/{userId}` | Admin |
| POST | `/api/admin/feature-flags/{name}/toggle` | Admin |
| GET  | `/health/live`, `/health/ready` | — |

## Run the architecture tests

```bash
dotnet test tests/ArchitectureTests/ArchitectureTests.csproj
# 8 facts: module isolation (Catalog ⊥ Orders ⊥ Platform impl),
# modules ⊥ Api host, Domain ⊥ EF Core, Domain ⊥ FluentValidation
```

## How to add a new module

1. Scaffold `src/Modules/X/X.csproj` + `src/Contracts/X.Contracts/X.Contracts.csproj`
2. Define aggregates in `X/Domain/`, vertical slices in `X/Application/`, EF config + outbox/inbox stores + module-specific DbContext (with `HasDefaultSchema("x")`) in `X/Infrastructure/`
3. Add an `XModule.AddXModule(IServiceCollection, IConfiguration)` extension that wires MediatR, validators, DbContext, IOutboxStore, integration event handlers, and module-private services
4. Register integration event handlers as `IIntegrationEventHandler<TEvent>` — the in-memory event bus discovers them automatically via DI
5. Wire `services.AddXModule(configuration)` in `Hosts/Api/Program.cs`
6. Add architecture tests for the new module's boundaries

## What's intentionally missing at Tier 3

| Missing | Earned at |
|---|---|
| Real message broker (RabbitMQ / Kafka) — in-memory `IEventBus` only | Tier 5 (microservices) |
| Multi-tenancy with `TenantId` query filters | Tier 4 (platform) |
| JWT authentication | Tier 5 |
| Separate Worker host (OutboxProcessor is in the Api host) | Tier 4 |
| S3 / Blob storage for files | Tier 4 |
| Distributed services + BFFs | Tier 5 |
| Database-per-service physical isolation | Tier 5 |

The next branch (`tier-4-platform`) introduces multi-tenancy, JWT, S3, and a worker process; Tier 5 distributes the modules.

## Architecture Decision Records

- [`docs/adr/0001-modular-monolith.md`](docs/adr/0001-modular-monolith.md) — why three modules with enforced boundaries
- [`docs/adr/0002-outbox-inbox-pattern.md`](docs/adr/0002-outbox-inbox-pattern.md) — at-least-once + consumer dedup
- [`docs/adr/0003-cross-module-saga-via-events.md`](docs/adr/0003-cross-module-saga-via-events.md) — async PlaceOrder choreography
