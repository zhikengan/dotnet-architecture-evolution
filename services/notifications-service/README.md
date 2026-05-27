# notifications-service

Consumes `OrderConfirmedIntegrationEvent`, `OrderCancelledIntegrationEvent`, and `OrderFailedIntegrationEvent` from RabbitMQ. For each, persists a `Notification` row (simulating sending an email/SMS) and publishes `NotificationSentIntegrationEvent`.

## HTTP

| Method | Path | Role |
|---|---|---|
| `GET` | `/admin/notifications/by-order/{orderId}` | Admin |
| `GET` | `/health` | — |

Owns `notifications` schema on its own PostgreSQL instance (port 5436 in compose).
