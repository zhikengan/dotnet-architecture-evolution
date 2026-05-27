# ADR-0002: MediatR + vertical slices over service classes

**Status:** Accepted (Tier 2)

## Context

Once the codebase has more than one or two use cases, "service classes" (`ProductService`, `OrderService`) start collecting unrelated methods. A change to `PlaceOrder` shouldn't touch the same file as `ListProducts`. We also want a uniform place to plug in cross-cutting concerns (validation, structured logging) without sprinkling them through handlers.

## Decision

Organize Application by **use case**, not by entity. Every use case lives in its own folder under `Application/{Aggregate}/{UseCase}/`:

```
Application/Orders/PlaceOrder/
├── PlaceOrderCommand.cs
├── PlaceOrderHandler.cs
├── PlaceOrderValidator.cs
└── PlaceOrderResult.cs
```

The command and the handler are dispatched via **MediatR**. Two open generic pipeline behaviors are registered globally:

- `ValidationBehavior<TRequest, TResponse>` runs every `FluentValidation` validator registered for `TRequest`. Failures short-circuit into `Result.Failure(...)` without throwing.
- `LoggingBehavior<TRequest, TResponse>` emits structured `Information` logs at begin/end with elapsed milliseconds, and `Error` with the exception on uncaught throws.

## Consequences

**Positive.**
- Each use case is a self-contained folder. A new dev can grok one feature without reading neighbouring code.
- Adding cross-cutting behavior (audit logging, idempotency at Tier 4, distributed tracing at Tier 4+) is a single open-generic registration, not 15 handler edits.
- The pattern survives Tier 3's split: each module owns its slices, all registered via `AddMediatR(typeof(ThisModule).Assembly)`.

**Negative — licensing.**
MediatR became a commercial product after v12 (Lucky Penny Software). For this educational/demo repo, dev/test usage is permitted under the current license; a real product would need to budget for a license. Migration off MediatR is mechanical — the slice folder structure is independent of the dispatcher. Free-software alternatives worth knowing:

- `martinothamar/Mediator` — source-generated, no reflection
- Hand-rolled dispatcher — 50 lines, no external dependency

Either swap leaves slice code unchanged.

## Alternatives considered

- **Service classes by aggregate (`OrderService` with `PlaceOrder`, `Cancel`, `ForceCancel` methods).** Drifts to god classes as the project grows. No natural place for cross-cutting concerns. Hard to test in isolation.
- **Hand-rolled command dispatcher.** Lower dependency but more code to maintain and easier to get wrong (open-generic registration is fiddly). Reconsider only if MediatR's licensing changes the calculus.
