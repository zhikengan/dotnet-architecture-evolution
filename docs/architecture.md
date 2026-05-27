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
