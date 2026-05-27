# Runbook — Deploying a single service

## Pre-flight

- CI green on the service's branch
- Contract tests pass against the current producer (see ADR 0017)
- Migration script reviewed (no destructive operations in prod path)

## Deploy

1. Build + push the service's API and Worker images (separate Dockerfiles):
   ```
   docker build -f services/catalog-service/Dockerfile.api -t registry/catalog-api:$SHA .
   docker build -f services/catalog-service/Dockerfile.worker -t registry/catalog-worker:$SHA .
   docker push registry/catalog-api:$SHA registry/catalog-worker:$SHA
   ```
2. Roll the API first (RabbitMQ load-balances consumers between API and Worker; rolling them sequentially keeps consumer capacity).
3. Roll the Worker.
4. Smoke-check: hit `/health` on the API, watch RabbitMQ Management UI for the service's queues to clear.
5. Watch the Jaeger error rate panel for the service for 10 minutes after deploy.

## Roll back

Per-service rollback is independent. Re-deploy the previous image tag; MassTransit's idempotent consumers mean replayed messages are safe.

## Database migrations

- Dev: `EnsureCreated` in each service's Program.cs creates the schema on first run.
- Prod: EF Core migration bundles per service, applied as a separate step before the new container starts. Forward-compatible only (add column, never drop in the same deploy).
