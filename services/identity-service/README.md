# identity-service

Issues JWTs, owns users + tenants, exposes a gRPC API for user/tenant lookup used by the BFFs and other services.

## HTTP

| Method | Path | Purpose |
|---|---|---|
| `GET` | `/demo/token?role=Buyer&userId={guid}` | Demo JWT minting for a seeded user |
| `GET` | `/.well-known/jwks.json` | Public key set used by services to validate JWTs |
| `GET` | `/.well-known/openid-configuration` | OIDC discovery doc |
| `GET` | `/health` | Liveness |

## gRPC

`identity.proto`:

| RPC | Purpose |
|---|---|
| `GetUser(user_id)` | Lookup a user (used by BFFs to enrich token validation) |
| `GetTenant(tenant_id)` | Lookup a tenant |

## Data

Owns `identity` schema on its own PostgreSQL instance (port 5435 in compose). Seeds two tenants (`acme`, `globex`) and three demo users on first run.

## Run locally

```bash
dotnet run --project src/Identity.Api
```

Requires PostgreSQL reachable at the connection string in `appsettings.json` (defaults to `localhost:5435`). RabbitMQ at `localhost:5672`.
