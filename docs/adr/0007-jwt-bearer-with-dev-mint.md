# ADR-0007: JWT Bearer auth with a Development-only token mint

**Status:** Accepted (Tier 3)

## Context

Tiers 1–2 (until recently) used header-stuffing for "auth" — the request carried `X-User-Role` and `X-User-Id`, a hand-rolled filter parsed them, and an `ICurrentUser` impl read them straight off the request. That bypassed the entire ASP.NET Core auth pipeline: no `UseAuthentication`, no `UseAuthorization`, no `[Authorize]`, no `ClaimsPrincipal`, no token validation. The middleware ordering that bites people in production was never exercised.

Tier 3 needs to fix this for two reasons:

1. The architectural lesson at this tier is the **modular monolith** — modules talk through outbox events, each owns its schema, the host is the integration point. If the host's auth model is "trust HTTP headers" then the lesson is undermined the moment someone bolts on a real frontend.
2. The e2e suite (`EndToEndTests/`) needs to fail the way real clients fail. Missing bearer → 401, wrong policy → 403, expired token → 401 — the middleware's behavior, not our home-baked filter.

But Tier 3 is still pre-platform — we don't want to run Keycloak or hand-build an OIDC issuer. We need JWT *validation* without JWT *issuance overhead*.

## Decision

JWT Bearer authentication with **a symmetric (HS256) key from configuration**, plus a **Development-only token-mint endpoint** at `POST /api/dev/token`.

### Placement (shared kernel vs. host)

The modular monolith has BuildingBlocks (shared kernel) + per-module projects + the API host. Auth pieces land where their equivalents do for other cross-cutting concerns:

| File | Project | Why |
|---|---|---|
| `BuildingBlocks/Infrastructure/Authentication/JwtOptions.cs` | Shared kernel infra | Config DTO. Sibling to `OutboxOptions` and other infra options. |
| `BuildingBlocks/Infrastructure/Authentication/JwtTokenIssuer.cs` | Shared kernel infra | Crypto + `IClock`. Sibling to `InMemoryEventBus`, `OutboxProcessor`, `MarketplaceActivitySource` — all "real-world adapters". Any host that consumes BuildingBlocks gets the same issuer. |
| `BuildingBlocks/Api/AuthDependencyInjection.cs` | Shared kernel host helpers | `AddMarketplaceAuthentication` composition — sibling to `CorrelationMiddleware` and `ResultExtensions`. Lets any host wire JwtBearer + role policies in one call. |
| `Hosts/Api/Authentication/HttpCurrentUser.cs` | Host | `ICurrentUser` over `HttpContext.User`. Bound to HTTP context, which is a host concept — not something the shared kernel should depend on. |
| `Hosts/Api/Endpoints/Dev/DevTokenEndpoints.cs` | Host | HTTP endpoint. The `Endpoints/Dev/` subfolder mirrors the `Endpoints/Buyer/`/`Seller/`/`Admin/` layout — dev surface is its own audience. |

`BuildingBlocks.Api/RoleAuthorizationFilter.cs` (the hand-rolled header check from before this ADR) is deleted — built-in `RequireAuthorization` policies replace it.

### Validation parameters

`TokenValidationParameters` validates issuer, audience, lifetime, and signing key with a 5-second clock skew. Read from `Jwt:Key` / `Jwt:Issuer` / `Jwt:Audience` / `Jwt:LifetimeMinutes`.

The `AddJwtBearer` configure delegate reads options **inside the lambda** (not closure-captured at registration time) — same lazy-config pattern modules use for the connection string. This makes `WebApplicationFactory`-driven test overrides (in-memory config sources) visible by the time `JwtBearerOptions` is materialized.

### Policies

```csharp
options.AddPolicy("Buyer",  p => p.RequireAuthenticatedUser().RequireRole("Buyer"));
options.AddPolicy("Seller", p => p.RequireAuthenticatedUser().RequireRole("Seller"));
options.AddPolicy("Admin",  p => p.RequireAuthenticatedUser().RequireRole("Admin"));
```

Endpoint groups in `Hosts/Api/Endpoints/{Buyer,Seller,Admin}Endpoints.cs` gate on the policies via `.RequireAuthorization("Buyer")` etc.

### Dev token endpoint

```
POST /api/dev/token
Content-Type: application/json
{ "userId": "<guid>", "role": "Buyer" | "Seller" | "Admin" }
→ 200 { "access_token": "<jwt>", "token_type": "Bearer", "expires_at": "...", ... }
```

No login. No password. The caller asserts (userId, role) and gets a real JWT signed by the same `JwtTokenIssuer` the JwtBearer middleware validates against.

The endpoint is mapped **only when `app.Environment.IsDevelopment()`** (see `Program.cs` — the call to `app.MapDevTokenEndpoints()` is inside the dev guard) so the unauth mint path cannot reach production.

`EndToEndTests/Fixtures/ApiFixture.ClientFor(role, userId)` resolves `JwtTokenIssuer` from DI and mints directly — no HTTP round-trip. `demo.http` mints via the endpoint up top, then references `{{tokenName.response.body.$.access_token}}` in subsequent requests.

## Consequences

**Positive.**
- E2E tests exercise the real middleware pipeline, not a header shortcut. Bugs in middleware ordering, claim mapping, policy evaluation surface in tests.
- `ICurrentUser` matches the shape Tier 4's real IdP will produce — handler code doesn't move when the issuer changes.
- 401 vs 403 distinction is the framework's standard one — 401 on missing/invalid token (JwtBearer default), 403 on policy denial (AuthorizationMiddleware default) — instead of our home-baked filter.
- Cross-module event handlers and the OutboxProcessor are unaffected — they don't know auth exists, which is the right outcome.
- Architecture tests pass unchanged: modules still don't depend on auth packages because BuildingBlocks (which owns auth) is allowed.

**Negative.**
- HS256 + shared key is fine for dev/CI but unsuitable for production. `dev-only-symmetric-key-replace-in-production-32+chars` in `appsettings.json` is a deliberate cue. Tier 4 moves to RS256 + asymmetric key + an issuer service with discovery + key rotation.
- The dev mint endpoint is one if-statement away from being a public unauth issuer. The architectural guard is the `IsDevelopment()` check; the operational guard is the e2e test that asserts 401 on missing Authorization.
- `BuildingBlocks` now depends on `Microsoft.AspNetCore.Authentication.JwtBearer` and the IdentityModel packages. Worth it — the cost is one centralized location instead of multiple hosts each wiring their own.

## Alternatives considered

- **Keep header auth at Tier 3.** Rejected — the modular monolith deserves real middleware. Header-stuffing is a Tier 1 artifact.
- **Microsoft.AspNetCore.Authentication.OpenIdConnect against a containerized IdP (e.g., Keycloak).** Reasonable but heavier — adds a container, a discovery doc, a login flow. The Tier-3 lesson is module boundaries, not auth realism — that's Tier 4's chapter.
- **RS256 now.** Premature — key management overhead for a tier whose audience is "3–10-dev growing product". Tier 4's multi-tenancy + external clients are the right forcing function for asymmetric keys.
- **Put `JwtTokenIssuer` in a module rather than BuildingBlocks.** Considered briefly — Platform module could host it. Rejected because auth is genuinely cross-cutting (every module's endpoints sit behind it) and forcing modules to know about JWT muddies the boundary.
