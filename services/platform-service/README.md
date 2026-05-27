# platform-service

Owns feature flags + idempotency keys. Used synchronously by other services via gRPC for feature checks.

## Endpoints

| Method | Path | Purpose |
|---|---|---|
| `GET` | `/api/admin/feature-flags` | List all flags (Admin only) |
| gRPC | `IsFeatureEnabled` | Synchronous feature check used by catalog/orders during request handling |
| `GET` | `/health` | Liveness |

Database on port 5437 in compose. JWT validation against identity-service's JWKS.
