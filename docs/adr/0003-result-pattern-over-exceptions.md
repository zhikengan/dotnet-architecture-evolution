# ADR-0003: `Result<T>` for business outcomes, exceptions only for bugs

**Status:** Accepted (Tier 2)

## Context

Exceptions are an expensive way to model expected business outcomes. "Insufficient stock", "buyer doesn't own this order", "product not found" are not bugs — they're branches the caller knows how to handle. Throwing in domain code:

- forces every handler to wrap in `try/catch` or hope nothing leaks
- pollutes structured logs with stack traces for non-incidents
- makes the success/failure shape of a method invisible at the call site
- makes mapping to HTTP status codes a fragile `catch` ladder

## Decision

Introduce `Result` / `Result<T>` in `Marketplace.Domain.Common`. Every domain method that can fail returns one. Every Application handler returns `Result<T>` (or `Result` for void operations). Every error has a stable `Code` string (e.g., `"Product.NotPublished"`, `"Stock.Insufficient"`).

`Result<T>` does **not** carry exception information — only an `Error` value object with `Code` and `Message`. Bugs (`NullReferenceException`, `InvalidOperationException`, etc.) still throw, get caught by ASP.NET's exception middleware, and surface as 500s.

`ResultToHttp.Map(Result<T>, onSuccess)` in the API layer translates errors to HTTP responses by error-code prefix:

| Code prefix / value          | HTTP status |
|------------------------------|-------------|
| `*.NotFound`, `*.NotPublished` | 404 |
| `Order.NotOwner`             | 403 |
| `*.Insufficient`, `*.Already*`, `*.NotCancellable`, `*.NotPending` | 422 |
| `Validation`                 | 400 |
| Any other domain code        | 400 |
| (anything else)              | 500 |

## Consequences

**Positive.**
- A handler's signature tells you everything: `Task<Result<PlaceOrderResult>>` says "this might return a domain error". You can't accidentally ignore it.
- The validation pipeline can use the same `Result` shape — `ValidationBehavior` builds a `Result.Failure(new Error("Validation", ...))` instead of throwing.
- Mapping to HTTP is a single switch in `ResultToHttp`, not scattered try/catch blocks. Same mapping logic is testable in isolation.

**Negative.**
- Boilerplate: every fallible call is `var r = ...; if (r.IsFailure) return Result.Failure<T>(r.Error);`. Tools like `LanguageExt`, `CSharpFunctionalExtensions`, or hand-rolled `Bind`/`Map` extensions would reduce this. Not pulled in at Tier 2 — adds dependency, may distract from the pattern. Reconsider at Tier 3 if the boilerplate becomes painful.
- Some teams find this style alien (especially folks coming from Java/C# enterprise). The convention needs to be enforced in code review until it's reflexive.

## Alternatives considered

- **Throw domain exceptions, catch at the API.** Works but couples HTTP mapping to exception types, makes the handler signature lie, and stack traces are expensive at scale.
- **Validation throws but business outcomes return.** Inconsistent. The "expected failure" line is fuzzy and gets argued about in PR reviews.
- **OneOf / discriminated unions library.** More expressive but heavier dependency. `Result<T>` is the smallest thing that gets us the wins.
