# ADR-0004: Strongly-typed IDs and value objects

**Status:** Accepted (Tier 2)

## Context

Tier 1 used `Guid` for `Product.Id`, `Order.Id`, `Product.SellerId`, `Order.BuyerId`. Compile-time, every `Guid` looks the same — you can pass an `OrderId` where the code expects a `ProductId` and the compiler is happy. Same with `decimal` for price, currency, percentage. The bug surfaces at runtime, often deep in the call stack, and unit tests don't catch it because the test data uses the same `Guid.NewGuid()`.

Value objects (`Money`, `Stock`, `Quantity`) are the same problem: an `int` is an `int`, and "stock" and "quantity" and "loyalty points" all share the type — and the invariants live in scattered `if` checks.

## Decision

**Strongly-typed IDs** as `readonly record struct`:

```csharp
public readonly record struct ProductId(Guid Value)
{
    public static ProductId New() => new(Guid.NewGuid());
}
```

EF Core maps these via `HasConversion(id => id.Value, v => new ProductId(v))` in the entity configuration. Zero runtime overhead vs. raw `Guid`, full type safety at call sites.

**Value objects** for domain primitives:

- `Money` — sealed class (USD-only at Tier 2; multi-currency is a Tier 4+ concern). Stored as OwnsOne (`price_amount`, `price_currency` columns on the same table).
- `Stock`, `Quantity` — `readonly struct` with private constructors and static `Create(int)` returning `Result<T>`. The invariants live on the value object: `Stock.Decrement(n)` returns `Result.Failure(InsufficientStock)` if `n > Value`. The aggregate doesn't repeat the check.

## Consequences

**Positive.**
- `Order.Create(buyer, productId, quantity, now)` cannot accidentally receive an `OrderId` where it wants a `ProductId`. Compile error, not runtime bug.
- Stock invariants live in *one* place. Adding "max stock per product" is a single change on `Stock`, not a grep through every handler that touches stock.
- Domain tests assert on `result.Value.Stock.Value.Should().Be(7)` — the test reads like the prose it's enforcing.

**Negative.**
- EF Core mapping costs: every strongly-typed ID needs `HasConversion`; every value object struct needs the converter to reconstruct it. The cost is one config line per property — manageable, but means EF model failures show up when the mapping is missed.
- Smart enum (`ProductStatus`) has the same friction: it's a class with private constructor and static instances, stored via `HasConversion<int>`. `OrderStatus` is a plain `enum` for variety; both patterns are demonstrated. Tier 3 may unify (probably toward smart enum for the named-error-on-illegal-transition richness).
- Newcomers occasionally try `product.Id = Guid.NewGuid()` and the compiler stops them. Brief learning curve, real long-term safety.

## Alternatives considered

- **Stick with raw `Guid` / `decimal` / `int`.** Tier 1's choice; correct for one-week MVPs. Wrong here — the type errors strongly-typed IDs prevent are exactly the bugs that survive into prod and embarrass teams.
- **Source-generated strongly-typed IDs (e.g., `Vogen`).** Less hand-rolled boilerplate, especially for serialization. Reconsider at Tier 3 when there are 10+ ID types and the conversion glue adds up. At Tier 2 with two ID types, hand-rolled is fine.
