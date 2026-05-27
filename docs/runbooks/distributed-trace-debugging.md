# Runbook — Debugging with distributed traces

## Where

http://localhost:16686 (Jaeger UI). In prod, your team's Jaeger / Tempo / Honeycomb instance.

## A successful PlaceOrder

Search by `service: buyer-bff` and look for `POST /orders`. The trace tree should show:

```
buyer-bff (HTTP POST /orders)
└── orders-api (HTTP POST /api/buyer/orders)
    ├── orders-api → MediatR PlaceOrderHandler
    ├── orders-api → MassTransit Publish OrderPlacedIntegrationEvent (outbox)
    └── catalog-worker (MassTransit Consume OrderPlacedIntegrationEvent)
        ├── catalog-worker → MediatR DecrementStock
        ├── catalog-worker → MassTransit Publish StockDecrementedIntegrationEvent
        └── orders-worker (MassTransit Consume StockDecrementedIntegrationEvent)
            ├── orders-worker → MediatR ConfirmOrder
            ├── orders-worker → MassTransit Publish OrderConfirmedIntegrationEvent
            └── notifications-worker (MassTransit Consume OrderConfirmedIntegrationEvent)
                ├── notifications-worker → Persist Notification
                └── notifications-worker → Publish NotificationSent
```

If any span shows red, click into it for the exception + tags.

## Common patterns

- **Trace stops at orders-api with no consumer span** → MassTransit outbox dispatcher isn't running, or RabbitMQ is unreachable. Check API container logs and `docker compose ps rabbitmq`.
- **Consumer span shows a long gap before SaveChanges** → DB contention. Check the slow query log on the service's DB.
- **No trace at all** → check the service's `Otel__Endpoint` env var resolves to the Jaeger OTLP gRPC port (4317).
