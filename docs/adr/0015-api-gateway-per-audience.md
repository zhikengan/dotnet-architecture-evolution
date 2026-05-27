# ADR 0015 — API gateway per audience (BFFs)

**Status**: Accepted (Tier 5)

## Decision

Each audience (buyer, seller, admin) gets a dedicated YARP-based BFF rather than a single shared gateway. Routes and authorization policies are config-only (no custom code unless aggregating multiple services in one response).

| Gateway | Port | Audience |
|---|---|---|
| `buyer-bff` | 5010 | Mobile/web buyer client |
| `seller-bff` | 5020 | Seller dashboard |
| `admin-bff` | 5030 | Internal ops/admin |

## Why per-audience

- Authorization shape differs per audience (admin policies are stricter, buyer routes don't need them)
- Different aggregation needs (admin BFF would fan out across services; buyer BFF stays narrow)
- Independent rate limiting and caching policies
- Independent deployment if buyer traffic patterns change

## YARP config approach

Routes and clusters live in each BFF's `appsettings.json` under `ReverseProxy`. The BFF validates the JWT against identity-service's JWKS and forwards `Authorization` to the downstream service so the service applies its own role check (defense in depth — the BFF can't be the only gate).
