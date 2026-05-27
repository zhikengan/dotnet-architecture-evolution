# catalog-service

Owns `Product` aggregates and is the catalog side of the PlaceOrder saga:

1. Consumes `OrderPlacedIntegrationEvent` → tries to decrement stock
2. Publishes `StockDecrementedIntegrationEvent` on success, `StockDecrementFailedIntegrationEvent` on failure
3. Consumes `OrderCancelledIntegrationEvent` → returns stock if the order had been confirmed
4. Publishes `ProductCreatedIntegrationEvent` when a seller creates a product

## HTTP

| Method | Path | Role |
|---|---|---|
| `POST` | `/api/seller/products` | Seller |
| `GET` | `/api/seller/products` | Seller (own) |
| `GET` | `/api/buyer/products` | Buyer (published) |
| `GET` | `/api/admin/products` | Admin (all) |
| `POST` | `/api/admin/products/{id}/suspend` | Admin |
| `GET` | `/health` | — |

## gRPC

`catalog.proto`: `GetProduct`, `ListProducts` (rare sync — for admin views).

## Data

Owns `catalog` schema on its own PostgreSQL instance (port 5433 in compose).
Seeds three products in tenant `acme` on first run.
