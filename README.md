# Marketplace Evolution

> **Same business logic. Five architectures. Pick the one that fits.**

One marketplace — sellers list products, buyers place orders, admins moderate — implemented five times across five branches. Each branch is a complete, runnable .NET 10 solution. Each one demonstrates the architecture appropriate to a specific stage of a product's life.

This is the reference you want next to your screen the next time someone says *"we need microservices"* — or, more usefully, the next time you find yourself doing too much for too little reason.

---

## Why this repo exists

Architecture advice on the internet is mostly written by people solving problems you don't have, for companies you don't work at, with budgets you don't get. The pattern is real, but the **context is missing**. So teams cargo-cult. They adopt microservices for 200 users, or hand-roll EF queries for 200 million.

Two failure modes, both fatal:

| Over-engineering | Under-engineering |
|---|---|
| 6 months to MVP | 6 weeks to MVP |
| Architecture book ticked off | Spaghetti by month 4 |
| Product never validates | Product validates, can't scale |
| Senior teams over-correcting | Junior teams shipping fast |

The right architecture is the smallest one that supports *today's reality* plus *the next 6–12 months of plausible growth*. Nothing more. Nothing less.

This repo shows you what that looks like at five concrete points on the curve.

---

## What's in each branch

| Branch | Stage | Footprint | What it teaches |
|---|---|---|---|
| [`tier-1-mvp`](#tier-1) | Prototype | 1 project, ~800 LOC | Ship the idea this week. Minimal API + EF + SQLite. One smoke test. |
| [`tier-2-clean-arch`](#tier-2) | Small product | 4 projects + test pyramid | Clean Architecture, MediatR vertical slices, domain invariants, Result<T>, FluentValidation. |
| [`tier-3-modular-monolith`](#tier-3) | Growing product | 9+ projects | Bounded-context modules, outbox/inbox, integration events, feature flags, OpenTelemetry, architecture tests. |
| [`tier-4-platform`](#tier-4) | Scaled monolith | + Worker host | Multi-tenancy, JWT, S3/MinIO, Quartz + Hangfire, rate limiting, separated worker process. |
| [`tier-5-microservices`](#tier-5) | Distributed | 5 services + 3 BFFs | Database-per-service, RabbitMQ via MassTransit, gRPC where unavoidable, YARP gateways, Pact contract tests, distributed tracing. |

Same business rules across all five. Same 13 test scenarios. Same `demo.http` flow. What changes is the architecture — and what each architecture earns you.

See [`SHARED_SCOPE.md`](SHARED_SCOPE.md) for the canonical business rules.

---

## Try it in 60 seconds

```bash
git clone <this-repo> && cd marketplace-evolution
git switch tier-1-mvp        # or any other tier
docker compose -f deploy/docker-compose.yml up --build
# open demo.http in VS Code (REST Client) and walk through
```

Each branch boots end-to-end with one command. Tier 1 is up in 10 seconds. Tier 5 spins ~18 containers and shows distributed tracing in Jaeger at `http://localhost:16686`.

---

## How to pick your tier

Score yourself. Count the YES answers.

- [ ] Team is 3+ devs, or will be in 12 months
- [ ] You have 3+ distinct bounded contexts you can name
- [ ] You serve 2+ genuinely different audiences (buyer, seller, admin…)
- [ ] Codebase will live 3+ years
- [ ] Downtime has measurable cost (revenue, SLA, compliance)
- [ ] You need to scale parts independently
- [ ] You expect to extract modules to services later
- [ ] You have regulatory requirements (GDPR, HIPAA, PCI, SOC2)
- [ ] You ship multiple times per week
- [ ] Your domain has real complexity (not just CRUD)

| Score | Where to start |
|---|---|
| **0–1** | Tier 1 — prototype, validate the idea |
| **2–3** | Tier 2 — Clean Architecture, ship it well |
| **4–6** | Tier 3 — modular monolith, the sweet spot for most growing products |
| **7+** | Tier 4 — platform-grade ops; Tier 5 only if scale or org boundary forces it |

**Use the highest matching row in any column.** Compliance forces you up a tier regardless of team size; pre-PMF status forces you down a tier regardless of ambition. There's no shame in being at Tier 1.

---

## The tiers, at a glance

<a id="tier-1"></a>
### Tier 1 — Prototype / MVP

**Profile.** Solo dev or pair. Weeks-to-months of life. Goal: *learn whether the idea works at all*. Throwaway-quality code is **correct** at this tier — that's the point.

**Stack.** One ASP.NET Core project. Minimal APIs. EF Core + SQLite. Built-in `ILogger`. One smoke test. One Dockerfile.

**Forbids.** MediatR, separate projects, Result<T>, domain events, outbox, caching, Polly, OpenTelemetry, FluentValidation, Serilog. None of it earns its cost yet.

**Win condition.** Real users (or stakeholders) interact with a working version, within weeks.

<a id="tier-2"></a>
### Tier 2 — Small product

**Profile.** 1–3 devs, single team, single bounded context, paying users exist, < 50K of them.

**What you add over Tier 1.** Clean Architecture (Domain / Application / Infrastructure / Api). MediatR vertical slices. FluentValidation per slice. Pipeline behaviors for validation + logging. Result<T>. Strongly-typed IDs + value objects. Domain events in-memory. Full test pyramid (unit → integration via Testcontainers → API end-to-end). Serilog. Conventional commits. GitHub Actions CI.

**What you still don't add.** Multiple DbContexts, outbox, integration events, feature flag infrastructure, Polly, OpenTelemetry, idempotency, multi-tenancy, JWT, separate worker. All Tier 3+.

**Why 4 projects, not 1?** Because the dependency direction is enforced by project references, tests can target Domain without spinning up EF, and refactoring to a modular monolith later becomes mechanical instead of architectural. The cost is one weekend; the payoff is years.

<a id="tier-3"></a>
### Tier 3 — Growing product (the sweet spot)

**Profile.** 3–10 devs, multi-audience (buyer/seller/admin), real revenue, multi-year roadmap. You feel friction: features take longer, devs step on each other, bugs come from unexpected module interactions.

**What changes.** The single bounded context splits into modules — **Catalog**, **Orders**, **Platform** — each with its own project, DbContext, schema, and migrations. Modules **never reference each other's main projects**; they talk through `Contracts` and integration events on an outbox/inbox. Cross-module flows become explicit sagas. Architecture tests enforce the boundaries so the build breaks the moment someone violates them.

**Operational scaffolding.** Idempotency keys on mutation endpoints. DB-backed feature flags with sticky bucketing. Audience-specific read DTOs (CQRS). OpenTelemetry traces, metrics, and structured logs. Runbooks for the top alerts.

**Why this is the sweet spot.** Most products that survive belong here for years. Tier 3 absorbs everything from team growth to compliance to multi-region read replicas — without forcing the operational overhead of microservices.

<a id="tier-4"></a>
### Tier 4 — Scaled monolith / platform

**Profile.** 10+ devs across teams, enterprise or regulated context, formal SLAs.

**What you add.** Multi-tenancy as a first-class concern (every aggregate carries `TenantId`; EF query filters enforce isolation; tests verify cross-tenant queries return empty). Real JWT (RS256) with a demo issuer. S3-compatible storage (MinIO locally) for files. Worker host **extracted from API host** with its own Dockerfile. Quartz for scheduled jobs, Hangfire for fire-and-forget. Rate limiting + security headers. Health checks split into `/health/live` and `/health/ready`. Custom OpenTelemetry metrics.

**What you still don't do.** Split modules into services. Tier 4 is the *modular monolith pushed to its limits* — and for most companies, this is the ceiling.

<a id="tier-5"></a>
### Tier 5 — True microservices

**Profile.** Org-scale, multiple teams, modules with different scaling profiles or ownership, real reason to deploy independently.

**What changes.** Each module becomes its own service with its own database (separate PostgreSQL instances on separate ports — the boundary is **physical**, not logical). Communication via **RabbitMQ + MassTransit** (events, default) or **gRPC** (sync calls, exception). **YARP BFFs** in front of each audience. Consumer-driven **Pact contracts** between every producer/consumer pair. Distributed tracing through HTTP, gRPC, and message bus headers.

**The trap to avoid.** *Distributed monolith*: services that synchronously call each other for every operation, share databases, or stitch joins across service boundaries. If your services look like that, you're paying microservices costs for monolith coupling. Stay at Tier 4.

---

## When to evolve

| Move up because… | …never move up because |
|---|---|
| Team crossed 3 devs and PRs are colliding | "We want to be enterprise-ready" |
| You can name 3+ bounded contexts | "Big companies do it this way" |
| A second team is forming around different code | "Microservices are the future" |
| Outages routinely affect unrelated features | "We might need it someday" |
| Compliance just landed on your roadmap | A new architect wants to demonstrate value |
| Specific module has a different scaling profile | A conference talk made it sound cool |
| Test suite is slow because everything's tangled | "What if a senior leaves?" |

The left column are *legitimate forcing functions*. The right column are *aspirations dressed as requirements*. Every pattern adopted before it's needed slows you down: more files per feature, more concepts to learn, more places to make mistakes. Architecture you don't need today is technical debt you took on voluntarily.

---

## Decide early vs decide late

The single highest-leverage architectural skill is knowing which decisions are *cheap to retrofit* and which are *expensive*.

**Decide early — these get a fortune more expensive over time:**

| Decision | Why early matters |
|---|---|
| Bounded context boundaries | Untangling intertwined modules is a multi-month effort |
| Intent-based command names (`ShipOrder`, not `UpdateOrderStatus`) | Renaming hundreds of commands is risky |
| Domain layer free of EF dependencies | Decoupling domain from EF later is rewrite-scale |
| Vertical-slice file organization | Reorganizing later changes every import |
| Multi-tenancy isolation model | Retrofitting tenant isolation is a security risk |
| Domain events | Adding them late means historical data is missing |
| Structured logging | Unstructured logs can't be queried; the data is lost |

**Decide late — let evidence force the move:**

| Decision | When to make it |
|---|---|
| Redis caching | When `MemoryCache` becomes a bottleneck |
| Extracted worker host | When background work affects API responsiveness |
| Feature flag infrastructure | When you ship multiple times per week |
| OpenTelemetry full stack | When debugging routinely takes >1 hour |
| Multi-region deployment | When a region has actual customer presence |
| Splitting a module into a service | When scaling or team boundary forces it |
| Idempotency keys | When external systems start calling you |
| Polly resilience policies | When you depend on something flaky |

Get the early ones right from day one — they cost almost nothing upfront. Defer the late ones until reality forces them. This combination is what separates teams that move fast for years from teams that ship for six months and refactor for two.

---

## Resource reality check

| Tier | Initial setup | Per-feature overhead | Operational cost |
|---|---|---|---|
| 1 | 2–5 days | Minimal | Negligible — shared hosting |
| 2 | 1–2 weeks | Low | PaaS or single VM, ~$50–200/mo |
| 3 | 4–8 weeks | Moderate | Multi-service infra, ~$500–5K/mo |
| 4 | Months + ongoing | Substantial | Dedicated SRE, $10K+/mo + people |
| 5 | Quarters | High | Platform team, $30K+/mo + people |

**Team-stage matrix:**

|  | 1–3 devs | 3–10 devs | 10+ devs |
|---|---|---|---|
| Solo / pair time | Tier 1–2 | Tier 1 only | — |
| Pre-PMF startup | Tier 1–2 | Tier 2 | Skip — focus PMF |
| Post-PMF growth | Tier 2 | Tier 3 | Tier 3–4 |
| Enterprise / regulated | Tier 3 (forced) | Tier 3–4 | Tier 4–5 |

Compliance is the asymmetric force. It will pull you up regardless of your team size — and it should, because the cost of getting it wrong dwarfs the cost of architectural overhead.

---

## Common traps, by tier

**Tier 1.** Building "the right architecture" for 3 months instead of shipping in 3 weeks. Kubernetes for a single container. Unit tests for code that may not survive the week.

**Tier 2.** Premature module boundaries — guessing at bounded contexts before knowing the domain. Building feature-flag infrastructure when an `appsettings.json` bool would do. Hiring DevOps before anything needs operating.

**Tier 3.** Staying lean too long, then refactoring under fire. Doing the migration in one big-bang sprint. Adopting microservices because "modular monolith is too simple". Forming teams that don't align with module boundaries. Skipping ADRs and re-litigating the same decisions every quarter.

**Tier 4.** Architecture review board becomes a bottleneck. Compliance becomes a checkbox. Building an internal platform nobody adopts.

**Tier 5.** *Distributed monolith.* Services tightly coupled via synchronous calls. Shared databases. Cross-service joins in the DB. Microservice-per-aggregate — over-decomposition costs you everywhere.

---

## How to read this repo

You can read it three ways depending on what you need:

**As a developer choosing your next stack.** Start at the [How to pick your tier](#how-to-pick-your-tier) scorecard. Score honestly. Check out that branch. Run it. Walk `demo.http`. Read the branch's `CLAUDE.md`. Now you know what to copy.

**As an architect reviewing a decision.** Read [`docs/decision-guide.md`](docs/decision-guide.md) end-to-end — it's the deep-dive version of this README, with drivers, anti-drivers, and the one-page card. Then sit in the design meeting with it open.

**As an engineer about to build Tier 3+.** Read [`docs/playbook.md`](docs/playbook.md) — the modular-monolith reference covering principles, patterns, data ownership, operations, and the AI-augmented workflow that ties it all together.

---

## Further reading in this repo

- [`SHARED_SCOPE.md`](SHARED_SCOPE.md) — the business rules + 13 universal scenarios every tier must satisfy
- [`docs/decision-guide.md`](docs/decision-guide.md) — long-form architecture decision guide
- [`docs/playbook.md`](docs/playbook.md) — modular-monolith playbook (Tier 3+ reference)
- [`docs/evolution-path.md`](docs/evolution-path.md) — how to migrate between tiers without rewriting everything
- Each tier branch has its own `CLAUDE.md` documenting tier-specific conventions and forbids, and ADRs in `docs/adr/` capturing the *why* of each architectural choice

---

## The one rule

> Right-size to the problem in front of you.
> Evolve when reality forces you, not when aspiration tells you.

That's the whole repo. The five branches are just the proof.
