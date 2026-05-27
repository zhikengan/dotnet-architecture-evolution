# ADR-0005: DB-backed feature flags with sticky-bucket rollout

**Status:** Accepted (Tier 3)

## Context

At Tier 3 we want to ship features behind flags so admins can roll them out gradually without a redeploy. Three constraints:

1. **No external SaaS** — flags must be controllable from the same DB that the rest of the app uses, no LaunchDarkly call at every request.
2. **Sticky per user** — a user bucketed into "premium" at 25% rollout must *stay* in premium when the rollout moves to 50%. Random per-call bucketing produces a confusing UX.
3. **Cross-module consumable** — Catalog's buyer query needs to know whether a flag is on for the requesting buyer, but it must not reach into Platform's tables directly.

## Decision

- `Platform.Contracts.IFeatureFlagQuery.IsEnabledAsync(flagName, userId, ct)` — the *only* surface other modules see.
- `platform.feature_flags` table: `Name (PK)`, `Enabled bool`, `RolloutPercentage int (0-100)`, `EnabledUserIds JSONB`, `UpdatedAt`.
- `DbFeatureManager : IFeatureFlagQuery` implements this with three short-circuits in order:
  1. If `userId` is in `EnabledUserIds`: **true** (explicit opt-in trumps everything).
  2. If `Enabled == false`: **false** (kill switch).
  3. Else: `bucket = SHA256(userId || ":" || flagName)[0..4] % 100`; return `bucket < RolloutPercentage`.
- `IMemoryCache` holds the flag definition for `FeatureFlags:CacheSeconds` (default 30) so we don't hit the DB on every read.
- Admin endpoints: `GET /api/admin/feature-flags`, `PUT /{name}/rollout`, `PUT /{name}/users/{userId}`, `POST /{name}/toggle`.

## Consequences

**Positive.**
- Sticky bucketing: same `(userId, flagName)` always yields the same bucket, so increasing the rollout monotonically grows the enabled population without re-bucketing anyone.
- Explicit opt-in for one user (e.g., the founder, support team, betas) cuts through the rollout math.
- Other modules (Catalog) consume `IFeatureFlagQuery` through `Platform.Contracts`, never touching Platform's tables. The architecture tests in `ArchitectureTests.ModuleBoundaryTests` enforce this.
- Cache reduces DB load to one query per flag per ~30s; admin changes propagate after the TTL elapses (or immediately if you bounce the process).

**Negative.**
- 30s cache means admin toggles take up to 30s to be observed. Tests use `FeatureFlags:CacheSeconds=1` to keep timing tight; in production it's a deliberate trade-off (fewer DB hits vs. snappier toggles).
- SHA256 bucketing is deterministic but doesn't allow re-bucketing strategies (e.g., "shuffle who's in the cohort weekly"). That's a Tier 4+ concern.
- We didn't pull in `Microsoft.FeatureManagement` after considering it — the package's `IFeatureManager` is opinionated about how flags are loaded, and our DB-backed strategy was simpler to express with a custom interface.

## Alternatives considered

- **Microsoft.FeatureManagement with custom `IFeatureDefinitionProvider`.** Considered; the wrapping ceremony added more lines than the direct DB lookup. May reconsider at Tier 4 when filter complexity grows.
- **Random per-call bucketing.** Rejected — same user gets inconsistent behavior across requests.
- **Per-tenant rollouts via subdomain mapping.** Out of scope (multi-tenancy lands at Tier 4).
