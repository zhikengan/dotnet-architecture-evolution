# ADR-0006: Architecture tests as build-gating safety net

**Status:** Accepted (Tier 3)

## Context

The whole point of the modular monolith is that modules don't reference each other's impl. The whole point of Clean Architecture inside each module is that Domain doesn't know about EF or HTTP. Without tooling, these rules survive only as long as the most disciplined reviewer is paying attention. The first PR that adds `using Orders.Domain.Orders;` from `Catalog.Application.*` slips through, and a year later the codebase has organic spaghetti and a rewrite-or-rewrite-anyway dilemma.

## Decision

`ArchitectureTests` is a regular xUnit project that uses **NetArchTest** to assert the dependency graph as code. Each rule is a `[Fact]`:

**Module isolation.**
- `Catalog` must not depend on `Orders.{Domain,Application,Infrastructure}` namespaces (`Orders.Contracts` allowed).
- `Orders` must not depend on `Catalog.{Domain,Application,Infrastructure}` namespaces (`Catalog.Contracts` allowed).
- `Catalog` / `Orders` must not depend on `Platform.{Domain,Application,Infrastructure}` namespaces (`Platform.Contracts` allowed).
- `Platform` must not depend on `Catalog` or `Orders` impl at all.

**Layering.**
- Modules must not reference the `Marketplace.Api` host.
- Each module's `Domain` namespace must not depend on `Microsoft.EntityFrameworkCore`.
- Each module's `Domain` namespace must not depend on `FluentValidation`.

The tests run in the unit-test stage of CI (no Docker), so they fail fast. A boundary violation surfaces in seconds, not after the change has been merged.

## Consequences

**Positive.**
- A PR that takes an unintended shortcut breaks the build at the architecture-test step. Reviewers don't have to remember the rule.
- The rules are *documentation*: a new team member can read `ModuleBoundaryTests.cs` to learn the boundaries without trawling READMEs.
- Adding a new module is a checklist item: write the boundary tests for it as part of the same PR.

**Negative.**
- NetArchTest matches namespaces by prefix. `Catalog.Contracts` starts with `Catalog`, so a naive `NotHaveDependencyOn("Catalog")` rule would flag legitimate cross-module event consumers. We solved this by enumerating the IMPL namespaces explicitly (`Catalog.Domain`, `Catalog.Application`, `Catalog.Infrastructure`) and asserting `NotHaveDependencyOnAny(...)`.
- A check on "Domain doesn't depend on MediatR runtime" was floated and dropped: `IDomainEvent : INotification` puts `MediatR` (Contracts) in the Domain assembly's reference graph by design (see ADR-0001's "pragmatic dependency leak" note). The architecture test would fail by intent.
- NetArchTest works on compiled assemblies. Adding it to the build pipeline costs a few seconds. Worth it.

## Alternatives considered

- **PR-review discipline.** Doesn't scale beyond ~3 reviewers and quickly drifts.
- **`InternalsVisibleTo` / `[InternalsVisibleToAttribute]` gating.** Solves the public API surface but not the "reference graph" question. NetArchTest is the right tool here.
- **Roslyn analyzer.** Heavier to author and maintain; reserve for rules that need to inspect syntax trees, not just type metadata.
