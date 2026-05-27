# ADR-0001: Clean Architecture, four projects, dependency direction enforced by references

**Status:** Accepted (Tier 2)

## Context

Tier 1 ships a single ASP.NET Core project with everything in one folder. That's correct for an MVP that may not survive a quarter. Tier 2's job is to introduce *just enough* structure that:

- The domain layer can be tested without spinning up EF or the web host.
- A future migration to a modular monolith (Tier 3) is mechanical, not architectural.
- Dependency direction is enforced by the build, not by team discipline.

## Decision

Split the codebase into four projects with a strictly one-way reference graph:

```
Marketplace.Domain        — pure C#, no EF/MediatR/ASP.NET
Marketplace.Application   — references Domain only
Marketplace.Infrastructure — references Application
Marketplace.Api           — references Application + Infrastructure
```

The build fails if anyone tries to introduce a back-reference (Domain referencing Infrastructure, etc.).

## Consequences

**Positive.**
- Domain tests run in milliseconds — no Postgres, no MediatR, no HTTP host.
- EF configurations, migrations, and Postgres-specific code stay isolated in Infrastructure. Swapping the persistence layer (or adding a second `DbContext` at Tier 3) doesn't touch Application or Domain.
- The next tier's split of Application into per-module slices is a folder-and-csproj reorg, not a rewrite.

**Negative.**
- One small dependency leak we accept: `Marketplace.Domain` references `MediatR` for `INotification`, so domain events can be dispatched via the same mediator as commands. The alternative — a custom `IDomainEvent` marker + reflection-based dispatch — costs more than it saves at Tier 2. Documented and revisited at Tier 3.
- `IAppDbContext` in `Application/Abstractions` exposes EF Core's `DbSet<T>`, so Application transitively depends on `Microsoft.EntityFrameworkCore` (the abstractions package, not the provider). Pragmatic Clean Arch — purer alternatives (repository per aggregate) buy nothing at Tier 2 and pessimize Tier 3 read-model work.

## Alternatives considered

- **Single project (Tier 1 approach).** Right for prototypes, wrong here — the test pyramid wants Domain unit tests independent of EF, and Tier 3 splits will be painful without enforced boundaries.
- **Six projects (Domain / Application / Infrastructure / Persistence / Api / Contracts).** Premature at Tier 2 with one bounded context. Tier 3 introduces `Contracts` projects per module.
