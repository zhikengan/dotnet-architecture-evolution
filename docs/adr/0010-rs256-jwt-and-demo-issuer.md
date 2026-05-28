# ADR-0010: RS256 JWT + demo issuer endpoint

**Status:** Accepted (Tier 4)

## Context

Tier 3 shipped HS256 JWT with a dev-only token mint at `POST /api/dev/token` (ADR-0007). That worked when a single API host both signed and validated tokens — the shared symmetric key never needed to leave the process.

Tier 4 introduces:

1. A **Worker host** as a second relying party that must validate tokens issued by the API. With HS256, both would need the signing material — a wider distribution of the secret than necessary.
2. **Multi-tenancy** with `tenant_id` claims that downstream services (Worker, future SDKs, future per-tenant integrations) must trust.

The Tier 3 ADR explicitly telegraphed: "Tier 4 moves to RS256 + asymmetric key + an issuer service with discovery + key rotation."

## Decision

**RS256 (asymmetric) JWT + OIDC-shaped demo issuer + JWKS discovery.**

- `JwtOptions` now carries `PrivateKeyPem`, `PublicKeyPem`, `KeyId` — PEM-encoded RSA keypair. The private key signs; the public key validates and is published.
- `JwtTokenIssuer` signs with `RsaSecurityKey(RSA.Create() + ImportFromPem(...))` and `SecurityAlgorithms.RsaSha256`. RSA is intentionally not disposed in the singleton — `Microsoft.IdentityModel` caches signature providers keyed off `SecurityKey`, and an eager `Dispose` invalidates cached providers held by sibling singletons across parallel test fixtures.
- `JwtPublicKeyProvider` materializes the matching public key as `RsaSecurityKey` and exposes it both to JwtBearer validation (via `IConfigureNamedOptions<JwtBearerOptions>`) and to the JWKS endpoint (as a `JsonWebKey`).
- **Endpoint surface**:
  - `GET /demo/token?role=Buyer&tenant=acme&userId={guid}` — Development-only token mint. Replaces Tier 3's POST `/api/dev/token`. The GET-with-query shape is what the demo flow expects.
  - `GET /.well-known/openid-configuration` — always mounted. Publishes issuer + `jwks_uri` + supported algorithms.
  - `GET /.well-known/jwks.json` — always mounted. Returns the public key as a JWK with the configured `kid`.
- Tokens include `tenant_id` as a custom claim alongside the standard `sub`, `role`, `jti`.

The dev key pair is committed to `appsettings.json` (deliberately leaked, like the HS256 key before it). Production deployments override via environment variables / secret store and rotate via `KeyId`.

## Consequences

**Positive.**
- Relying parties (Worker, future SDKs, external integrations) validate against the public key without ever holding signing material.
- JWKS discovery lets clients fetch the public key dynamically, supporting key rotation without redeploying clients.
- `tenant_id` claim is now first-class — the API's `TenantMiddleware` reads it once and writes the scoped `ITenantContext`; query filters cascade automatically.
- The Tier 3 retrofit story holds: Tier 3 → Tier 4 is an additive change to the same primitives (`JwtOptions`, `JwtTokenIssuer`, `ICurrentUser.TenantId`), not a redesign.

**Negative.**
- The dev RSA keypair in `appsettings.json` is a sharper footgun than the symmetric key was: anyone with the file can mint tokens. The `if (app.Environment.IsDevelopment())` guard on the mint endpoint and the OIDC convention of *only* publishing the public side limit the blast radius, but the production handoff (override `Jwt__PrivateKeyPem` via secret store; rotate `Jwt__KeyId`) is now part of the deployment runbook.
- `Microsoft.IdentityModel`'s signature-provider cache caused a real test-isolation bug: when the test factory's RSA was disposed, the cached `AsymmetricSignatureProvider` shared with other factories blew up. Documented in `JwtTokenIssuer.cs` — RSA is GC-owned, not eagerly disposed.

## Alternatives considered

- **Keep HS256 with a shared secret across API + Worker.** Reasonable for two-host deployments but defeats the whole point of asymmetric crypto — the secret has to be everywhere. Postpones the migration to a real IdP.
- **Use a containerized IdP (Keycloak, Hydra, Ory).** Realistic but heavier than the showcase warrants. Tier 5 introduces real federation with external clients; the demo issuer is the bridge between "no auth" and "real IdP".
- **Mint via per-tenant signing keys.** Multi-key support is feasible (JWKS supports multiple `kid`s) but adds rotation/key-management complexity Tier 5 will need to solve anyway. One key for the showcase is fine.
