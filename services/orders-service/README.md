# orders-service

Owns `Order` aggregates and drives the PlaceOrder saga across services:

1. Buyer `POST /api/buyer/orders` → create `Order(Pending)` + publish `OrderPlacedIntegrationEvent`
2. Consume `StockDecrementedIntegrationEvent` from catalog → `Order.Confirm()` + publish `OrderConfirmedIntegrationEvent`
3. Consume `StockDecrementFailedIntegrationEvent` from catalog → `Order.Fail()` + publish `OrderFailedIntegrationEvent`
4. Buyer can cancel own pending orders; admin can force-cancel any non-cancelled order → publish `OrderCancelledIntegrationEvent`

## HTTP

| Method | Path | Role |
|---|---|---|
| `POST` | `/api/buyer/orders` | Buyer |
| `GET` | `/api/buyer/orders` | Buyer (own) |
| `GET` | `/api/buyer/orders/{id}` | Buyer (own) |
| `POST` | `/api/buyer/orders/{id}/cancel` | Buyer (own pending) |
| `GET` | `/api/admin/orders` | Admin |
| `POST` | `/api/admin/orders/{id}/cancel` | Admin (force cancel) |

## gRPC

`orders.proto`: `GetOrder` (used by admin views).

## Data

Owns `orders` schema on its own PostgreSQL instance (port 5434 in compose).
