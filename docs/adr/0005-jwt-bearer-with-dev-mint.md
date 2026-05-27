# ADR-0005: JWT Bearer auth with a Development-only token mint

**Status:** Accepted (Tier 2)

## Context

Tier 1 used header-based pseudo-auth (`X-User-Role` + `X-User-Id`) because shipping in days mattered more than auth realism. We need to graduate at Tier 2:

- The team has to exercise the **real ASP.NET Core auth pipeline** — `UseAuthentication`/`UseAuthorization`, `[Authorize]` / `RequireAuthorization`, policies, claims principals. Header-stuffing learns none of that.
- E2E tests should fail the way real clients fail — missing/expired/tampered token → 401, wrong policy → 403.
- The `ICurrentUser` abstraction should be backed by `ClaimsPrincipal`, not raw headers, so it matches what Tier 4's real IdP will produce.

But Tier 2 is a 1–3-dev product team — running a real OIDC issuer (Keycloak, Auth0 sandbox) is overkill. We want the middleware behavior of JWT without the operational weight of an issuer.

## Decision

JWT Bearer authentication with **a symmetric (HS256) key from configuration**, plus a **Development-only token-mint endpoint** at `POST /api/dev/token`.

### Layering

| Concern | Project | Type |
|---|---|---|
| Options class | `Marketplace.Infrastructure/Authentication/JwtOptions.cs` | DTO (config DataAnnotations) |
| Token signing service | `Marketplace.Infrastructure/Authentication/JwtTokenIssuer.cs` | Uses `JsonWebTokenHandler` + `SymmetricSecurityKey` + `IClock` |
| Middleware wiring | `Marketplace.Api/Authentication/AuthDependencyInjection.cs` | `AddJwtBearer` + `AddAuthorization` policies |
| Claims reader | `Marketplace.Api/Authentication/HttpCurrentUser.cs` | `ICurrentUser` over `HttpContext.User` |
| Dev mint endpoint | `Marketplace.Api/Endpoints/DevTokenEndpoints.cs` | Only mapped when `IsDevelopment()` |

The token-signing service belongs in Infrastructure (it's a real-world adapter — crypto + `IClock` + serialization, just like `AppDbContext` is an EF adapter and `SystemClock` is a clock adapter). The middleware wiring and the claims reader live in the Api host because they're HTTP-bound concepts.

### Validation parameters

Standard `TokenValidationParameters`: issuer, audience, lifetime, signing key, 5-second clock skew. Read from `Jwt:Key`/`Jwt:Issuer`/`Jwt:Audience`/`Jwt:LifetimeMinutes` in configuration.

The `AddJwtBearer` configure delegate reads the options *inside the lambda* (not closure-captured) so that `WebApplicationFactory`-style test overrides applied via `ConfigureAppConfiguration` are visible when `JwtBearerOptions` is materialized — same lazy-config pattern as `AddInfrastructureServices` uses for the connection string.

### Policies

Three role policies, declared once:

```csharp
options.AddPolicy("Buyer",  p => p.RequireAuthenticatedUser().RequireRole("Buyer"));
options.AddPolicy("Seller", p => p.RequireAuthenticatedUser().RequireRole("Seller"));
options.AddPolicy("Admin",  p => p.RequireAuthenticatedUser().RequireRole("Admin"));
```

Endpoint groups gate on these via `.RequireAuthorization("Buyer")` etc. — replacing the Tier-1/early-Tier-2 hand-rolled `RoleAuthorizationFilter`.

### Dev token endpoint

```
POST /api/dev/token
Content-Type: application/json
{ "userId": "<guid>", "role": "Buyer" | "Seller" | "Admin" }
→ 200 { "access_token": "<jwt>", "token_type": "Bearer", "expires_at": "...", ... }
```

No login. No password. The caller asserts a (userId, role) and gets a real JWT signed by the same `JwtTokenIssuer` that the middleware validates against. The endpoint is registered behind `if (app.Environment.IsDevelopment())` and tagged `[AllowAnonymous]` — it can't ship.

Test fixtures use the same `JwtTokenIssuer` directly (no HTTP round-trip) to mint tokens and attach them as `Authorization: Bearer <jwt>`. `demo.http` uses the HTTP endpoint via REST Client named requests.

## Consequences

**Positive.**
- E2E tests exercise the real auth middleware, not a header shortcut. Bugs in middleware ordering, claim mapping, policy evaluation surface in tests.
- `ICurrentUser` matches the shape Tier 4 will produce, so handler code doesn't need rewriting when the issuer changes.
- The 401/403 distinction is the framework's standard one — 401 on missing/invalid token, 403 on policy denial — instead of our home-baked filter.
- `JwtTokenIssuer` is testable in isolation and reusable in tests without WebApplicationFactory.

**Negative.**
- HS256 with a shared key is fine for dev/CI but unsuitable for production. The config key is marked `dev-only-symmetric-key-replace-in-production-32+chars` to make this obvious. Tier 4 moves to RS256 + an asymmetric key, where clients hold the public key and the issuer holds the private key.
- The dev token endpoint is a kill switch for unauthenticated mint — bound by the Development environment check. Forget that guard and you ship an open issuer. The integration test that asserts 401 on missing Authorization header is what catches this regression.
- The Api host now depends on `Microsoft.AspNetCore.Authentication.JwtBearer` and Infrastructure on `Microsoft.IdentityModel.Tokens`/`Microsoft.IdentityModel.JsonWebTokens`. Worth it.

## Alternatives considered

- **Keep header auth at Tier 2.** Rejected — it bypasses the auth middleware and teaches the wrong shape. The cost of JWT plumbing at Tier 2 is one dependency injection extension plus an issuer class; the upside is "the rest of the codebase looks like production".
- **Use `Microsoft.AspNetCore.Authentication.OpenIdConnect` against a sandbox IdP (e.g., Keycloak in compose).** Reasonable but heavier — adds a container, a discovery doc, and a login flow we don't need at this tier. Reserve for Tier 4.
- **RS256 now.** Possible but premature — key management overhead for a tier whose audience is "1–3 devs shipping a paying product". Tier 4's multi-tenancy and external clients are the right forcing function for asymmetric keys.
