# Shared Scope — Marketplace Business Rules

Every tier implements **the same business logic** with **the same rules**. Only the architecture differs. When a tier's requirements conflict with this file, file an ADR.

## Domain

Minimal marketplace: **sellers** list products, **buyers** place orders, **admins** moderate.

### Product invariants

- `Name` required, 1–200 chars
- `Price` > 0
- `Stock` ≥ 0 (non-negative integer)
- Has `SellerId` and `Status` ∈ { `Draft`, `Published`, `Suspended` }
- Only `Published` products are visible to buyers
- Stock decrements on order, returns on cancellation
- Stock can never go below zero — domain error
- Raises events: `ProductCreated`, `StockDecremented`, `StockReturned`

### Order invariants

- Has `BuyerId`, `ProductId`, `Quantity` (≥ 1), `Status` ∈ { `Pending`, `Confirmed`, `Cancelled`, `Failed` }
- Created in `Pending`
- Placement decrements stock atomically (or async via saga from Tier 3+); insufficient stock → `Failed`, no stock change
- Admin can `ForceCancel` any non-cancelled order — stock returned
- Buyer can `Cancel` only their own `Pending` orders
- Raises events: `OrderPlaced`, `OrderConfirmed`, `OrderCancelled`, `OrderFailed`

### Authorization

| Role | Capabilities |
|---|---|
| `Buyer` | List published products, place orders, view/cancel own orders |
| `Seller` | Create products, list own products |
| `Admin` | View all, force-cancel any order, suspend products |

## The 6 use cases

| # | Use case | Actor | Endpoint |
|---|---|---|---|
| 1 | `CreateProduct` | Seller | `POST /api/seller/products` |
| 2 | `PlaceOrder` (idempotent from Tier 3+) | Buyer | `POST /api/buyer/orders` |
| 3 | `CancelOrder` | Buyer | `POST /api/buyer/orders/{id}/cancel` |
| 4 | `ForceCancelOrder` | Admin | `POST /api/admin/orders/{id}/cancel` |
| 5 | `ListProductsForBuyer` | Buyer | `GET /api/buyer/products` |
| 6 | `ListProductsForAdmin` | Admin | `GET /api/admin/products` |

## The 13 test scenarios

| # | Scenario | Expected |
|---|---|---|
| S1 | Create product, valid data | 201, visible in admin list |
| S2 | Create product, name="" | 400 with validation error |
| S3 | Create product, price=0 | 400 |
| S4 | Place order, sufficient stock | 201, stock decremented, `Confirmed` |
| S5 | Place order, insufficient stock | 422, `Failed`, stock unchanged |
| S6 | Place order, non-published product | 404 |
| S7 | Cancel own pending order | 200, `Cancelled`, stock returned |
| S8 | Cancel another buyer's order | 403 |
| S9 | Admin force-cancels confirmed order | 200, stock returned |
| S10 | Buyer list excludes drafts | Drafts hidden |
| S11 | Admin list includes drafts + stock | All visible |
| S12 | No role header / no token | 401 |
| S13 | Wrong role for endpoint | 403 |

**Tier 1** covers S1, S4, S5, S7, S9 only (smoke). **Tier 2+** must pass all 13.

## Demo authentication

| Tier | Mechanism |
|---|---|
| 1–2 | `X-User-Role: Buyer\|Seller\|Admin` + `X-User-Id: {guid}` headers |
| 3 | Same headers, abstracted via `ICurrentUser` |
| 4–5 | RS256 JWT with demo issuer endpoint; claims `sub`, `role`, `tenant_id` |

## Seed data (idempotent, on first run)

| Type | Data |
|---|---|
| Seller | `acme-seller` (`11111111-1111-1111-1111-111111111111`) |
| Buyer | `john-buyer` (`22222222-2222-2222-2222-222222222222`) |
| Admin | `root-admin` (`33333333-3333-3333-3333-333333333333`) |
| Products | "Widget" ($10, stock 100), "Gizmo" ($25, stock 50), "Doohickey" ($5, stock 0) — all `Published` |

Tier 4–5 add tenants: `acme` (`aaaaaaaa-...`), `globex` (`bbbbbbbb-...`).
