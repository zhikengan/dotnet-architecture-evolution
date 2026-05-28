# Migration runbook

EF Core migrations are applied **out-of-band in production**. Each service's
`Program.cs` only calls `MigrateAsync` when `ASPNETCORE_ENVIRONMENT=Development`.
This avoids a known anti-pattern: when N replicas race `MigrateAsync` during a
rolling deploy, EF's migration history insert can deadlock under Postgres, or
two replicas can both attempt the same DDL with one winning and the other
crashing — sometimes loudly enough to fail the new ReplicaSet.

## Development

Auto-migrate + auto-seed happens on first request. Nothing to do.

## CI

`{svc}-ci.yml` runs `dotnet test` against the worktree's migrations as part
of `IntegrationTests` (Testcontainers Postgres). Failures here block merge.

## Production rollout pattern

Each service ships a self-contained migration bundle. The deploy pipeline:

1. Builds the bundle:
   ```bash
   dotnet ef migrations bundle \
     --project services/catalog-service/src/Catalog.Infrastructure \
     --startup-project services/catalog-service/src/Catalog.Api \
     --self-contained -r linux-x64 \
     -o artifacts/catalog-migrate
   ```
2. Runs the bundle as a Job/pre-deploy step (e.g., Kubernetes `Job`,
   `argocd-syncwave`, or a one-shot ECS task) **before** new replicas roll out:
   ```bash
   ./artifacts/catalog-migrate --connection "$CATALOG_DB_CONN"
   ```
3. Verifies the readiness probe (`/health/ready`) goes green on the new pods.
4. Rolls the new replicas in.

Because migrations are forward-only and applied **before** new code lands, the
new code can assume the new schema exists; old code (briefly co-resident during
rollout) still works as long as migrations are backward-compatible (add columns
nullable, drop columns in a follow-up release, etc.).

## Rollback

Don't roll back schema. Roll forward with a compensating migration. If a
deploy needs to be reverted before the old code rolls out, the old code must be
able to read the new schema — see the backward-compat guidance above.

## Common pitfalls

- **`MigrateAsync` in `Program.cs` for production** — covered by the
  `IsDevelopment()` gate. Don't remove it.
- **Long-running locks** — `ALTER TABLE` on a large table can block writes.
  Apply DDL during a maintenance window or use Postgres-native online DDL
  (`CREATE INDEX CONCURRENTLY`, partitioned table swaps).
- **MassTransit outbox tables** — the `AddMassTransitOutbox` migration must
  run before the app starts publishing, or messages will fail to persist.
  Bundle it with the regular service migrations.
