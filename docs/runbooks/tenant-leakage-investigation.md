# Runbook: tenant leakage investigation

**Severity:** S1 — data isolation breach across tenants is the most expensive thing that can happen to a multi-tenant platform.

## Symptoms

One or more of:

- Customer support reports a tenant seeing another tenant's product/order/feature-flag.
- An automated alert (TBD: implement a sampling job that asserts `EVERY product.TenantId = expected_tenant` per request trace) fires.
- The `MultiTenancyTests` suite turns red on a deploy that shouldn't have touched tenancy.

## First 5 minutes — contain

1. **Don't redeploy yet.** A bad fix here makes audit harder.
2. **Identify the scope** — single tenant pair, or systemic?
   ```sql
   -- How many products show a TenantId that doesn't match any expected tenant?
   SELECT tenant_id, COUNT(*) FROM catalog.products
     WHERE tenant_id NOT IN (SELECT id FROM platform.tenants) GROUP BY tenant_id;
   ```
3. **If systemic** (cross-tenant rows everywhere) — disable the affected endpoint(s) via feature flag. The application can run in degraded mode while the bug is found.
4. **Capture an evidence snapshot** — `pg_dump --schema=catalog --schema=orders --schema=platform > leakage-snapshot.sql` to a secure location.

## Next 30 minutes — root-cause

The bug is almost always one of these four. Walk them in order — they're sorted by frequency in practice.

### 1. Missing `IMultiTenant` on a new aggregate

If a recent PR added an aggregate that doesn't implement `IMultiTenant`, its query filter is missing and `db.NewThings.Where(...)` returns rows for ALL tenants.

Check:
```bash
dotnet test tests/ArchitectureTests --filter "FullyQualifiedName~IMultiTenant"
```
That test catches this case on PR. If it's green and you still leaked, the test isn't covering the assembly (look for missing project references in `ArchitectureTests.csproj`).

### 2. `IgnoreQueryFilters()` called from the wrong place

Grep for it:
```bash
grep -rn "IgnoreQueryFilters" src/ tests/
```
Expected callers: seeders, ResetAsync helpers, the OutboxProcessor's scope (indirectly via no-filter outbox table), the `tenant-leakage-investigation` queries above. Any other caller in production code is suspect.

### 3. `ITenantContext.TenantId == Guid.Empty` leaking through

The query filter is `e.TenantId == _tenant.TenantId`. If `_tenant.TenantId` is `Guid.Empty` (no JWT claim, anonymous request, background work without a set tenant), the filter matches only rows with `TenantId == Guid.Empty` — i.e., none if data is well-formed. The risk is if the OPPOSITE happened: code created a `TenantId.Empty` row, and a query with empty context now sees it.

Check the API logs for requests with no `tenant_id` claim that still reached a tenant-filtered query:
```bash
docker compose logs api | grep -E "TenantId.*00000000-0000-0000-0000-000000000000"
```

### 4. Hangfire job ran without setting tenant context

`SendOrderEmailService.SendAsync(tenantId, ...)` takes the tenant explicitly. But Quartz jobs that run "for all tenants" must explicitly call `ITenantContextSetter.SetTenant(tenantId)` per tenant before touching the DbContext. A job that forgets sees empty context = no rows in dev, but if data is mis-tenanted, sees other tenants' rows.

Audit each `IJob` implementation: search for `SetTenant` to ensure every DB-touching code path sets it.

## Recovery

1. **Confirm the data did or didn't actually leak.** A bug that returns wrong data to a request but doesn't write wrong data is recoverable by fixing the query. A bug that wrote rows with the wrong `TenantId` is a data-correction job.
2. **Patch the offending code.** Add `IMultiTenant`, remove the `IgnoreQueryFilters`, or add the missing `SetTenant` call. Add a regression test in `MultiTenancyTests`.
3. **Run the architecture test locally + on CI** to make sure it would have caught the variant.
4. **Re-enable disabled endpoints** once the test suite is green.

## Post-incident

- Write a short note in the engineering log: what aggregate/code path, who deployed it, did the architecture test miss it (and if so, extend it).
- If the architecture test missed this category of bug, extend it. The IMultiTenant test was created exactly because someone forgot once.
