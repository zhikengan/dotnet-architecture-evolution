# Marketplace — Tier 5: Microservices

The same marketplace as Tiers 1-4 — same business rules, same 13 scenarios from [`SHARED_SCOPE.md`](SHARED_SCOPE.md) — now decomposed into **5 independently deployable services** + **3 audience-specific BFFs**. Database per service, RabbitMQ for async messaging, gRPC for the rare sync lookup, distributed tracing across all spans.

## Topology (≈18 containers)

```
                          ┌──────────┐  ┌──────────┐  ┌──────────┐
   browser / curl ──────► │buyer-bff │  │seller-bff│  │admin-bff │   (YARP)
                          └────┬─────┘  └────┬─────┘  └────┬─────┘
                               │ Bearer JWT  │             │
            ┌──────────────────┴─────────────┴─────────────┴──────────┐
            ▼                ▼                ▼                ▼      ▼
       identity-api     catalog-api      orders-api    notifications  platform-api
       (JWT issuer       (Products,       (Orders,        (consumes    (FeatureFlags,
        JWKS, gRPC)       gRPC, saga       gRPC, saga      order        IdempotencyKeys,
                          consumer)        orchestrator)   events)      gRPC)
            │                │                │                │              │
       identity-db      catalog-db       orders-db     notifications-db   platform-db
       (5435)           (5433)           (5434)        (5436)             (5437)
                        catalog-worker   orders-worker notifications-worker

                          ┌───────────────────────────┐
                          │ RabbitMQ 4 (5672 / 15672) │   ← all integration events
                          └───────────────────────────┘
                          ┌───────────────────────────┐
                          │ Jaeger 1.66 (16686)       │   ← distributed traces
                          └───────────────────────────┘
```

## Services

| Service | Owns | Publishes | Consumes | gRPC |
|---|---|---|---|---|
| `identity-service` | Users, Tenants, JWT | `UserCreated`, `TenantCreated` | — | `GetUser`, `GetTenant` |
| `catalog-service` | Products, Stock | `ProductCreated`, `StockDecremented`, `StockDecrementFailed`, `StockReturned` | `OrderPlaced`, `OrderCancelled` | `GetProduct`, `ListProducts` |
| `orders-service` | Orders | `OrderPlaced`, `OrderConfirmed`, `OrderCancelled`, `OrderFailed` | `StockDecremented`, `StockDecrementFailed` | `GetOrder` |
| `notifications-service` | Notification records | `NotificationSent` | `OrderConfirmed`, `OrderCancelled`, `OrderFailed` | — |
| `platform-service` | FeatureFlags, IdempotencyKeys | `FeatureFlagToggled` | — | `IsFeatureEnabled` |

## The PlaceOrder saga (choreography)

```
1. POST /orders (buyer-bff → orders-api)
2. orders: create Order(Pending) + publish OrderPlaced            (outbox: tx with insert)
3. catalog: WhenOrderPlaced_DecrementStock
      success → publish StockDecremented
      failure → publish StockDecrementFailed
4. orders:
      WhenStockDecremented_ConfirmOrder    → publish OrderConfirmed
      WhenStockDecrementFailed_FailOrder   → publish OrderFailed
5. notifications:
      WhenOrderConfirmed_SendNotification  → persist + publish NotificationSent
      WhenOrderCancelled_SendNotification
      WhenOrderFailed_SendNotification
```

Client receives `Pending` immediately, then polls `GET /orders/{id}` to see `Confirmed` (typically <5s).

## Architecture choices

- **Per service: Domain / Application / Infrastructure / Api / Worker** — same Clean Architecture split as Tier 4's modular monolith, just physically separated. The Domain and Application layers are identical in shape (sealed-class aggregates, MediatR `IRequest<Result<T>>` handlers, `IAuthorizationRequirement`, `When{Event}_{Action}` consumers in `EventHandlers/Integration/`).
- **gRPC is just another input adapter.** gRPC service implementations delegate to MediatR queries in the Application layer — never bypass to `DbContext` directly. HTTP and gRPC paths produce identical results because they go through the same handler.
- **One database per service, on its own port.** Service boundary is *physical*, not just logical. No cross-service FKs.
- **Async by default, sync only when async won't do.** gRPC reserved for request-time lookups (BFF needs user info, catalog needs feature-flag check). Everything else is RabbitMQ events.
- **MassTransit owns the outbox + inbox.** `AddEntityFrameworkOutbox<TDbContext> + UseBusOutbox()` makes `IPublishEndpoint.Publish` transactional with the same `SaveChangesAsync`. Consumer-side dedup is built in.
- **One Contracts project per producing service.** Cross-service references are *only* to `*.Contracts` — never to another service's main project.
- **YARP BFFs per audience.** `buyer-bff` (5010), `seller-bff` (5020), `admin-bff` (5030). Routes are config-only; transforms forward the JWT to the downstream service so authz is enforced at the service.
- **OpenTelemetry instruments ASP.NET, HttpClient, gRPC, MassTransit, EF Core.** Trace context propagates across HTTP headers, gRPC metadata, and RabbitMQ message headers — a single PlaceOrder shows up in Jaeger as one trace spanning every service.
- **Polly retries on MassTransit message handlers** (exponential, 3 attempts, capped at 30s). Failures land in the dead-letter exchange.

## Layout

```
.
├── services/
│   ├── identity-service/        Identity.sln + src/{Domain,Application,Infrastructure,Api} + proto/identity.proto
│   ├── platform-service/        Platform.sln + ... + proto/platform.proto
│   ├── catalog-service/         Catalog.sln + ... + Catalog.Worker + proto/catalog.proto
│   ├── orders-service/          Orders.sln + ... + Orders.Worker + proto/orders.proto
│   └── notifications-service/   Notifications.sln + ... + Notifications.Worker
├── gateways/
│   ├── buyer-bff/  seller-bff/  admin-bff/   (YARP)
├── shared/
│   ├── BuildingBlocks/                       (Result, Entity, AggregateRoot, behaviors, telemetry)
│   └── contracts/
│       ├── Catalog.Contracts/  Orders.Contracts/  Notifications.Contracts/
│       ├── Identity.Contracts/ Platform.Contracts/
├── deploy/
│   ├── docker-compose.yml          orchestrates everything
│   ├── docker-compose.infra.yml    just infra (for single-service local dev)
│   └── rabbitmq/definitions.json   pre-declared users/permissions
├── docs/
│   ├── adr/ 0012 .. 0017            (microservices, rabbitmq, grpc-policy, gateway, db-per-service, contract-tests)
│   ├── runbooks/                   service-deployment, dead-letter, distributed-trace
│   └── architecture.md
├── .github/workflows/              per-service CI with path filters + e2e
├── demo.http
└── SHARED_SCOPE.md                 the 13 universal scenarios
```

## Run

```bash
cd deploy
docker compose up -d --build           # build + start ~18 containers
docker compose ps                       # all healthy
```

Then walk through [`demo.http`](demo.http) (VS Code REST Client, JetBrains HTTP client, or Insomnia). Watch the saga in:

- RabbitMQ: http://localhost:15672 (guest/guest)
- Jaeger: http://localhost:16686 — search `service:buyer-bff` and you'll see a single trace spanning `buyer-bff → orders-api → rabbitmq → catalog-worker → rabbitmq → orders-worker → rabbitmq → notifications-worker`

## Develop a single service in isolation

Bring up only the infra, then run the service from your IDE:

```bash
cd deploy && docker compose -f docker-compose.infra.yml up -d
cd ../services/catalog-service
dotnet run --project src/Catalog.Api
```

## Build everything

Each service has its own solution; build them individually:

```bash
for sln in services/*/*.sln; do dotnet build "$sln"; done
for csproj in gateways/*/*.csproj; do dotnet build "$csproj"; done
```

## Conventions (all services)

- .NET 10 / C# 14, file-scoped namespaces, nullable enabled, warnings-as-errors
- Records for commands/queries/DTOs/events; `sealed class` for handlers and aggregates
- `Async` suffix on async methods
- Aggregates inherit `AggregateRoot<TId>` + `IMultiTenant`
- IDs as `readonly record struct {X}Id(Guid Value)`
- Commands implement `IRequest<Result<T>>` + `IAuthorizationRequirement`
- Integration event consumers named `When{Event}_{Action}` in `EventHandlers/Integration/`
- gRPC adapters delegate to MediatR queries — *never* to `DbContext` directly
- Cross-service references *only* through `*.Contracts` projects

## Forbids (Tier 5 anti-patterns)

- ❌ Direct service-to-service main-project references → only `*.Contracts`
- ❌ Shared databases across services → one DB instance per service
- ❌ Synchronous service calls where async would work
- ❌ Skipping the outbox for cross-service events
- ❌ Skipping idempotency on event consumers (MassTransit inbox does this for you)
- ❌ Manual outbox implementation (use MassTransit's `AddEntityFrameworkOutbox`)
- ❌ gRPC handlers querying DbContext directly (route through Application/MediatR)
