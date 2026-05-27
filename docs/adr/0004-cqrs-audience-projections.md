# ADR-0004: Audience-specific read projections (lightweight CQRS)

**Status:** Accepted (Tier 3)

## Context

Three audiences read the catalog differently:

| Audience | What they need | What they must not see |
|---|---|---|
| Buyer  | Published products with "in stock or not" + premium-badge flag | Draft / Suspended products, exact stock numbers, seller identity, audit fields |
| Seller | Own products only, full detail (status, stock, created date) | Other sellers' products |
| Admin  | All products, full detail incl. seller id + status + timestamps | (no exclusions) |

At Tier 2 we had a single `Product` DTO and trimmed fields per audience inline in endpoints. At Tier 3 this collapses into three separate read slices.

## Decision

Three distinct read models, each living in its own vertical slice under `Catalog.Application.Products.Queries.*`:

- `ListProductsForBuyerQuery` → `BuyerProductDto(Id, Name, Price, InStock, IsPremium)` — filters `Status == Published`, enriches with `IFeatureFlagQuery.IsEnabledAsync("EnablePremiumBadge", buyerId)`.
- `ListProductsForSellerQuery(SellerId)` → `SellerProductDto(Id, Name, Price, Stock, Status, CreatedAt)` — filters by `SellerId`.
- `ListProductsForAdminQuery` → `AdminProductDto(Id, Name, Price, Stock, Status, SellerId, CreatedAt)` — no filter.

Each query handler reads the same `Products` table directly via `ICatalogDbContext` (no projection table). The discriminator is the *DTO shape*, not the source-of-truth.

## Consequences

**Positive.**
- A buyer can't see Draft/Suspended products even by guessing URLs — the query *excludes* them at the SQL level.
- Adding "premium badge" was a one-handler change in the buyer slice; no impact on seller/admin reads.
- Each slice can evolve independently: e.g., Tier 4 might add audience-specific endpoints that aggregate or paginate differently.

**Negative.**
- Three DTO types with overlapping fields. Schema changes touch all three (acceptable for the modest size).
- We do not (yet) materialize separate read tables. With high-cardinality reads (millions of products) we'd add a denormalized projection per audience populated by an integration-event-driven indexer. Tier 4's `ListProducts` will likely grow that. Today the same-table read is fine.

## Alternatives considered

- **Single DTO with audience-filtered fields at the API edge.** Brittle — every endpoint repeats the same field-stripping logic, and the SQL still over-fetches.
- **Per-audience read projection tables (full CQRS).** Premature at Tier 3. Reads aren't a bottleneck and the eventual-consistency cost would dominate the simplicity.
- **GraphQL field selection.** Out of scope for this tier; doesn't solve the *authorization* problem (a buyer must not be able to ask for `Stock` even with GraphQL).
