# Marketplace — Tier 1 (MVP)

The simplest possible working version of the marketplace described in [`SHARED_SCOPE.md`](SHARED_SCOPE.md). One ASP.NET Core project, EF Core + SQLite, header-based auth, inline endpoint handlers, idempotent seeder. No abstractions, no patterns. This is what a working prototype looks like before any architecture.

> Looking for the big picture? Check out [`main`](../../tree/main) for the cross-tier showcase. This branch is just the Tier 1 stop on that tour.

## Stack

- .NET 10 (LTS), C# 14
- ASP.NET Core Minimal APIs
- EF Core 10 + SQLite
- xUnit + `Microsoft.AspNetCore.Mvc.Testing`

## Run locally

```bash
dotnet run --project src/Marketplace
# app on http://localhost:5000
```

`marketplace.db` is created in the working directory on first boot, and the seeder fills it with three demo products under one seller. The seeder is idempotent — restart and nothing duplicates.

## Run with Docker

```bash
docker compose -f deploy/docker-compose.yml up --build
# app on http://localhost:5000
# SQLite file persisted in volume `marketplace-data` at /app/data/marketplace.db
```

## Walk through the demo

Open [`demo.http`](demo.http) in VS Code with the [REST Client](https://marketplace.visualstudio.com/items?itemName=humao.rest-client) extension and click through the requests top-to-bottom. It covers all six use cases plus an insufficient-stock and a wrong-role example.

## Run the smoke tests

```bash
dotnet test
# 5 facts: S1, S4, S5, S7, S9 from SHARED_SCOPE
```

## Auth — Tier-1 style

There is no real auth. Every request must carry two headers:

| Header        | Values                       |
|---------------|------------------------------|
| `X-User-Role` | `Seller`, `Buyer`, `Admin`   |
| `X-User-Id`   | any GUID (caller identity)   |

Wrong role for an endpoint → 403. Missing/invalid `X-User-Id` on mutating endpoints → 400.

Tier 5 swaps this for JWTs issued by an IdP.

## Endpoints

| Method | Path                                  | Role   |
|--------|---------------------------------------|--------|
| POST   | `/api/seller/products`                | Seller |
| GET    | `/api/buyer/products`                 | Buyer  |
| POST   | `/api/buyer/orders`                   | Buyer  |
| POST   | `/api/buyer/orders/{id}/cancel`       | Buyer  |
| GET    | `/api/admin/products`                 | Admin  |
| POST   | `/api/admin/orders/{id}/cancel`       | Admin  |

## What's intentionally missing at Tier 1

Each missing piece is the budget a later tier spends to earn its complexity. Today's "crude" is tomorrow's "clean":

| Missing                                  | Earned at                              |
|------------------------------------------|----------------------------------------|
| Domain/Application/Infrastructure split  | Tier 2 (clean architecture)           |
| MediatR + handlers + pipeline behaviors  | Tier 2                                |
| Value objects / strongly-typed IDs       | Tier 2                                |
| FluentValidation, Result<T>              | Tier 2                                |
| Domain events                            | Tier 3 (modular monolith)             |
| Module boundaries, internal/public API   | Tier 3                                |
| EF Core migrations (vs. EnsureCreated)   | Tier 3                                |
| Outbox / inbox patterns                  | Tier 3                                |
| Caching, Polly, OpenTelemetry, Serilog   | Tier 4 (platform)                     |
| Background workers, feature flags        | Tier 4                                |
| Architecture tests                       | Tier 4                                |
| Distributed services + BFFs              | Tier 5 (microservices)                |
| Real JWT-based authentication            | Tier 5                                |
| Async event-driven order placement       | Tier 5                                |

Take a look at [`tier-2-clean-arch`](../../tree/tier-2-clean-arch) next to see what gets added — and what *doesn't*.

## Tier-1 commit map

| # | Commit | What changed |
|---|--------|--------------|
| 1 | `feat: initial scaffold and EF Core setup`        | csproj, sln, Program.cs, AppDbContext shell |
| 2 | `feat: product and order models with endpoints`   | POCO models, status enums, all 6 endpoints |
| 3 | `feat: data seeder for demo`                       | idempotent seeder + Program.cs wiring |
| 4 | `feat: docker compose for demo`                    | Dockerfile + compose with persistent volume |
| 5 | `test: happy path smoke tests`                     | 5 facts (S1, S4, S5, S7, S9) |
| 6 | `docs: README and demo.http`                       | this file + REST Client script |
