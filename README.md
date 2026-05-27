# Marketplace — Tier 2 (Clean Architecture + Vertical Slices)

The architecture a 1–3-dev product team should ship once a prototype starts paying its way. Same business rules as Tier 1, but with the cheap foundations a future Tier 3 migration depends on: Domain/Application/Infrastructure/Api boundaries, MediatR vertical slices, `Result<T>` instead of business exceptions, strongly-typed IDs + value objects, domain events raised by aggregates and dispatched in `SaveChanges`, FluentValidation per slice, Serilog structured logs, EF Core 10 + Postgres, full test pyramid up to API end-to-end against Testcontainers.

> Looking for the big picture? Check out [`main`](../../tree/main) for the cross-tier showcase.

## Stack

- .NET 10 (LTS), C# 14
- ASP.NET Core Minimal APIs
- EF Core 10 + PostgreSQL 17 (Docker Compose for local dev)
- MediatR + FluentValidation per slice
- Serilog (structured, console sink)
- xUnit + FluentAssertions + NSubstitute + Testcontainers.PostgreSql
- GitHub Actions CI

## Project structure

```
src/
├── Marketplace.Domain/         — aggregates, value objects, errors, events
├── Marketplace.Application/    — MediatR slices, abstractions, pipeline behaviors
├── Marketplace.Infrastructure/ — EF Core, configurations, interceptor, seeder
└── Marketplace.Api/            — composition root + endpoints + auth filter

tests/
├── Marketplace.Domain.Tests/         — pure unit tests, no I/O
├── Marketplace.Application.Tests/    — EF InMemory + builders + NSubstitute
├── Marketplace.Infrastructure.Tests/ — Testcontainers Postgres
└── Marketplace.Api.Tests/            — Testcontainers + WebApplicationFactory
```

The dependency direction is enforced by project references: Domain has no project references, Application → Domain only, Infrastructure → Application, Api → Application + Infrastructure.

## Run with Docker Compose

```bash
docker compose -f deploy/docker-compose.yml up --build
# postgres on :5432, app on :5000 (waits for postgres healthcheck before starting)
```

The app applies migrations and seeds the three demo products on first boot in `Development`.

## Run locally

```bash
docker compose -f deploy/docker-compose.test.yml up -d        # postgres on :5433
ConnectionStrings__Default="Host=localhost;Port=5433;Database=marketplace_test;Username=marketplace;Password=marketplace" \
    dotnet run --project src/Marketplace.Api
```

## Walk through the demo

Open [`demo.http`](demo.http) in VS Code with the [REST Client](https://marketplace.visualstudio.com/items?itemName=humao.rest-client) extension. The script chains named REST Client requests so dynamic seed IDs are picked up at runtime, and walks scenarios **S1–S13** end-to-end against the running container.

## Run the tests

```bash
dotnet test
# ~80 tests across 4 projects:
#   Marketplace.Domain.Tests         (~50 unit tests, no Docker)
#   Marketplace.Application.Tests    (~13 handler tests, EF InMemory)
#   Marketplace.Infrastructure.Tests (~3 tests, Testcontainers Postgres)
#   Marketplace.Api.Tests            (13 facts, one per SHARED_SCOPE scenario)
```

CI splits these into a fast Domain/Application stage and a slower Infrastructure/Api stage that needs Docker on the runner.

## Auth (still header-based at Tier 2)

The same `X-User-Role` + `X-User-Id` headers as Tier 1. A custom `RoleAuthorizationFilter` endpoint filter checks them: missing → 401, wrong role → 403. No JWT until Tier 5.

## Endpoints

| Method | Path                                  | Role   | MediatR command/query           |
|--------|---------------------------------------|--------|----------------------------------|
| POST   | `/api/seller/products`                | Seller | `CreateProductCommand`           |
| GET    | `/api/buyer/products`                 | Buyer  | `ListProductsForBuyerQuery`      |
| POST   | `/api/buyer/orders`                   | Buyer  | `PlaceOrderCommand`              |
| POST   | `/api/buyer/orders/{id}/cancel`       | Buyer  | `CancelOwnOrderCommand`          |
| GET    | `/api/admin/products`                 | Admin  | `ListProductsForAdminQuery`      |
| POST   | `/api/admin/orders/{id}/cancel`       | Admin  | `ForceCancelOrderCommand`        |

## What's intentionally missing at Tier 2

| Missing                                  | Earned at                              |
|------------------------------------------|----------------------------------------|
| Multiple bounded-context modules         | Tier 3 (modular monolith)             |
| Multiple `DbContext`s                    | Tier 3                                |
| Outbox / inbox / integration events      | Tier 3                                |
| Architecture tests                       | Tier 3                                |
| Audience-specific read models (full CQRS)| Tier 3                                |
| Caching layer                            | Tier 4 (platform)                     |
| Polly resilience policies                | Tier 4                                |
| OpenTelemetry traces / metrics           | Tier 4                                |
| Feature flag infrastructure              | Tier 4                                |
| Idempotency keys                         | Tier 4                                |
| Multi-tenancy                            | Tier 4                                |
| Separate Worker host (Hangfire/Quartz)   | Tier 4                                |
| Real JWT-based authentication            | Tier 5                                |
| Distributed services + BFFs              | Tier 5 (microservices)                |
| Async event-driven order placement saga  | Tier 5                                |

Look at the next branch (`tier-3-modular-monolith`) to see what gets added — and what *doesn't*.

## Migration path to Tier 3

Because the dependency direction is already enforced by project references, the Tier 3 migration is mechanical rather than architectural:

1. Split `Marketplace.Application` into per-module slices (`Catalog`, `Orders`, `Platform`). Each module gets its own `Module` project, its own `Contracts` project, and a public-API surface.
2. Replace cross-aggregate inline orchestration in `PlaceOrderHandler` with an integration event flow: `Catalog` publishes `OrderPlaced` on its outbox; `Orders` consumes it from its inbox; stock decrement and order confirmation become an explicit saga.
3. One `DbContext` per module, separate schemas, separate migration histories.
4. Architecture tests fail the build the moment something crosses a module boundary it shouldn't.

The Tier 2 code that doesn't change: the aggregates, value objects, errors, events, validators, pipeline behaviors, and the Result-to-HTTP mapping. The Tier 2 code that does change: handler orchestration, DI wiring, and EF configuration scope.

## Architecture Decision Records

- [`docs/adr/0001-clean-architecture-four-projects.md`](docs/adr/0001-clean-architecture-four-projects.md)
- [`docs/adr/0002-mediatr-vertical-slices.md`](docs/adr/0002-mediatr-vertical-slices.md)
- [`docs/adr/0003-result-pattern-over-exceptions.md`](docs/adr/0003-result-pattern-over-exceptions.md)
- [`docs/adr/0004-strongly-typed-ids-and-value-objects.md`](docs/adr/0004-strongly-typed-ids-and-value-objects.md)
