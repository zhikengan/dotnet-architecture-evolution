# Architecture Decision Guide

> From MVP to production — right-sizing engineering decisions based on tier, team, and timeline.

The [README](../README.md) summarized the rules. This document is the long-form version: how to score a tier, which patterns earn their cost at which stage, what should move you up, what should NOT, and the trap waiting at every level.

If you're picking architecture today and you're not sure whether you're already late or already overcommitted, read this end-to-end before the next design meeting.

---

## 1. The core principle

Good architecture is what solves your problem **without imposing costs your problem does not justify**.

That's the whole game. Everything below is corollary.

The right architecture is the smallest one that supports today's reality *plus the next 6–12 months of plausible growth*. Not the problem you wish you had. Not the problem someone famous wrote about. Not the problem you might have in 5 years.

### The two failure modes

| Over-engineering | Under-engineering |
|---|---|
| 6 months to MVP | 6 weeks to MVP |
| Architecture book ticked off | Spaghetti by month 4 |
| Product never validates | Product validates → can't scale |
| Team burns out | Team burns out |
| **Common at:** senior teams over-correcting, greenfield rewrites | **Common at:** junior teams, inherited legacy, aggressive deadlines |

Both kill products. One feels professional and ends in irrelevance. The other feels fast and ends in rewrite.

---

## 2. The four tiers (at a glance)

| Tier | Profile | Risk |
|---|---|---|
| **1** Prototype / MVP | 1 dev, weeks-months, validating an idea | Building the wrong thing |
| **2** Small product | 1–3 devs, single team, single audience, < 50K users | Shipping too slowly to iterate |
| **3** Growing product | 3–10 devs, multi-audience, real revenue, multi-year | Tech debt outpacing feature work |
| **4** Large-scale platform | 10+ devs, multi-team, enterprise / regulated | Coordination overhead, partial outages |

### What each tier optimizes for

| Tier | Primary goal | Optimizes for | Accepts cost of |
|---|---|---|---|
| 1 | Validate the idea | Speed to deployable demo | Throwaway code, manual ops |
| 2 | Find product-market fit | Iteration speed | Some tech debt, limited scale |
| 3 | Sustainable growth | Maintainability + velocity | Upfront architecture investment |
| 4 | Reliability + team scale | Independent team productivity | Operational complexity |

Tier 5 in this repo (true microservices) is a special case of Tier 4 — the modular monolith forced apart by organizational or scaling pressure. You shouldn't be there unless something *physically forced you*.

---

## 3. How to decide your tier

Score yourself honestly across dimensions. **Use the highest matching row in any column.**

| Dimension | Tier 1–2 signals | Tier 3 signals | Tier 4 signals |
|---|---|---|---|
| Team size | 1–3 devs | 3–10 devs | 10+ devs, multiple teams |
| Audiences | One user type | 2–3 distinct audiences | Multiple + tenants |
| Lifespan | < 1 year | 1–3 years | 3+ years |
| Bounded contexts | 1 | 3–5 | 5+ |
| Deploy frequency | Manual / weekly | Daily / on-demand | Multiple per day |
| Downtime cost | Annoyance | Lost revenue | Contractual SLA / regulatory |
| Compliance | None | Light (GDPR) | Heavy (HIPAA, PCI, SOC2) |
| User count | < 1K | 1K–100K | 100K+ |
| Revenue | Pre-revenue | Real revenue | Material to business |

If you have 2 devs (Tier 1–2 by team) but heavy compliance (Tier 4 by regulation), you operate at Tier 4. Compliance forces operational maturity regardless of team size. Conversely, a Tier 4 company can run a small internal tool at Tier 2 — there's no obligation to apply enterprise patterns to every codebase you own.

---

## 4. Tier 1 — Prototype / MVP

> Solo dev or pair. Weeks to a few months. Goal: validate whether the idea works *at all*.

**Stack.**

```
src/
└── MyApp/                          ← one project, ASP.NET Core
    ├── Program.cs                  ← Minimal API inline
    ├── Models/                     ← entity classes
    ├── Services/                   ← business logic
    ├── Data/AppDbContext.cs        ← single DbContext
    └── appsettings.json
```

EF Core code-first migrations. SQLite locally, Postgres in prod. Serilog console + file. One Docker container, deployed manually. User Secrets for dev.

**Skip everything in the playbook except:** vertical-slice folder organization (free), structured logging with Serilog (free), one health-check endpoint (5 lines), User Secrets (built in).

**Project management.**

| Practice | How to handle |
|---|---|
| Planning | Trello / GitHub Issues; week-by-week |
| Code review | Self-review or informal pair check |
| Testing | Test the critical happy path. Skip the rest. |
| CI/CD | GitHub Actions: build + deploy on main |
| Deployment | Single VPS or PaaS (Render, Railway, Fly.io) |
| Database changes | EF migrations on startup (yes, this is fine here) |
| Monitoring | Console logs + UptimeRobot |
| Documentation | README + a few inline comments |

**Win condition.** Real users (or stakeholders) interact with a working version within weeks. The code is throwaway-quality; that's correct. The point is to *learn*.

---

## 5. Tier 2 — Small product

> 1–3 devs, single team, single audience, < 50K users. The product is real, there are paying customers, but the team is small and there's only one bounded context.

**Stack — Clean Architecture, four projects, not one.**

```
src/
├── MyApp.Domain/          ← pure domain, no I/O, no EF
├── MyApp.Application/     ← MediatR handlers, vertical slices
├── MyApp.Infrastructure/  ← EF Core, external services
└── MyApp.Api/             ← endpoints
```

**Why four projects, not one?**

- Tests target Domain without spinning up EF
- Application has no idea Infrastructure exists
- Dependency direction is enforced by project references — the compiler is your architecture test
- Refactoring to a modular monolith later is *mechanical*, not architectural

The cost is one weekend. The payoff is years.

**Adopt from the playbook:**

| Pattern | Why it earns its cost here |
|---|---|
| MediatR + vertical slices | Free organization win; foundation for everything later |
| Intent-based command names | Costs nothing; pays forever |
| FluentValidation per slice | One validator per command; easy to find |
| Pipeline behaviors (validation, logging) | Set up once, apply to every handler |
| Domain events in-memory | Cheap; useful even at small scale |
| `Result<T>` for business errors | Better than exception-based control flow |
| Architecture tests (a few) | Cheap insurance against drift |
| Health checks (live + ready) | Standard ops hygiene |
| Serilog + basic OpenTelemetry traces | Saves you when something breaks at 2am |
| Feature flags via `appsettings.json` | Toggle in-progress work; ship code dark |

**Skip until Tier 3:**

| Pattern | Why to skip |
|---|---|
| Multiple DbContexts | You have one bounded context |
| Outbox pattern | No cross-module communication yet |
| Integration events | Nothing to integrate with |
| Multi-audience endpoint folders | You don't have multiple audiences |
| Separate worker host | `BackgroundService` inside the API is fine |
| Polly resilience | Until you call something flaky |
| Distributed caching (Redis) | `MemoryCache` works until you scale out |
| Idempotency keys | Until external systems call you |
| Multi-tenancy abstractions | Until you have multiple tenants |
| Cloud config / Key Vault | User Secrets + env vars are fine |

**Project management.**

| Practice | Tier 2 approach |
|---|---|
| Planning | Kanban board (Linear, Trello, GitHub Projects) |
| Sprint cadence | 1–2 week cycles; light ceremony |
| Code review | Required on every PR; one approver |
| Testing | Unit tests for domain, integration for critical flows |
| CI/CD | Build + test + deploy to staging automatically; prod manual |
| Database changes | Generate SQL scripts; apply via deployment script |
| Monitoring | Hosted Sentry / Logtail / Better Stack |
| On-call | Whoever shipped it owns it (informal) |
| Documentation | README + 3–5 ADRs for the biggest decisions |

---

## 6. Tier 3 — Growing product

> 3–10 devs, multi-audience (buyer/seller/admin), real revenue, multi-year roadmap. You feel friction: features take longer, devs step on each other, bugs come from unexpected module interactions.

This is the sweet spot for the full modular-monolith playbook. The cost of architecture investment is repaid by reduced friction *within months*.

### Adopt the full playbook — incrementally

| Phase | Weeks | What |
|---|---|---|
| 1 | 1–8 | Split monolith into bounded-context modules. One DbContext per module, separate schemas. Architecture tests enforcing boundaries. |
| 2 | 8–12 | Cross-cutting in `BuildingBlocks`. Pipeline behaviors: auth, logging, validation, transaction. `Result<T>` everywhere. |
| 3 | 12–16 | Outbox + Inbox patterns. Integration events for cross-module communication. Domain events stay in-memory within modules. |
| 4 | 16–20 | Multi-audience endpoint split: `/api/buyer`, `/api/seller`, `/api/admin`. Audience-specific read models. |
| 5 | 20–24 | Operations: cloud feature flags or LaunchDarkly, Polly for external calls, idempotency keys on mutations, extract Worker host. |
| 6 | ongoing | Observability: OpenTelemetry traces and metrics, dashboards per module, SLOs and alerting. |

**Don't try to do it all at once.** Teams that attempt to refactor everything in one quarter typically fail. Pick the highest-pain area first. Most teams find that **module boundaries + pipeline behaviors deliver 60% of the value in 20% of the effort.** Add the rest as specific problems make their absence painful.

### Project management

| Practice | Tier 3 approach |
|---|---|
| Planning | 2-week sprints, quarterly OKRs, lightweight roadmap |
| Squad structure | Forming around modules; CODEOWNERS aligned |
| Code review | Required; module owners block PRs touching their module |
| Testing | Pyramid enforced; coverage tracked but not over-targeted |
| CI/CD | Full pipeline; canary deploys; feature flags for rollout |
| Database changes | Expand-Contract for breaking changes; migration bundles |
| Monitoring | OTel + Grafana / Datadog; per-module dashboards |
| On-call | Rotation; runbooks for top-10 alert types |
| Documentation | ADRs for architecture, runbooks for ops, READMEs per module |
| Technical debt | Quarterly review; allocate ~20% of sprint capacity |

---

## 7. Tier 4 — Large-scale platform

> 10+ devs across teams, enterprise or regulated context. Multi-region, formal SLAs, compliance, dedicated platform / SRE.

**Modular monolith is the floor** — you build on top of it.

| Additional concern | How it's handled |
|---|---|
| Service extraction | Modules with own scaling needs become services; same patterns apply |
| Multi-region | Active-passive or active-active; data residency rules |
| Cross-region consistency | Event sourcing + eventual consistency; CRDTs where needed |
| Authorization at scale | OPA, Cedar, or attribute-based access control |
| Tenant isolation | Database-per-tenant or schema-per-tenant |
| Compliance audits | Immutable audit log; data lineage; SOC2 / HIPAA controls |
| Change management | Architecture Review Board for cross-cutting changes |
| Internal developer platform | Golden paths, scaffolding, paved roads |
| Observability at scale | Sampling strategies; cost-aware ingestion |

### Project management

| Practice | Tier 4 approach |
|---|---|
| Planning | Quarterly + annual; portfolio-level prioritization |
| Team structure | Stream-aligned squads + platform team + enabling team |
| Code review | Multiple approvers; security / architecture review for sensitive changes |
| CI/CD | Progressive delivery; blue-green or canary; automated rollback |
| Database changes | DBA review; change windows for destructive ops |
| Incident response | Formal IM process; blameless postmortems |
| Architecture governance | RFC process for cross-team decisions; ADR registry |
| Onboarding | Multi-week structured program; documented golden paths |

---

## 8. Drivers — what should move you

If you can point at one of these signals and say "this is why we're adopting X," the investment will pay off. The pattern solves a problem you actually have.

**People drivers**
- Team size crossed 3 and PRs are colliding
- Team will grow to 6+ in the next year
- A second team is forming around a different part of the system
- Onboarding new devs takes >4 weeks because the codebase is unclear
- Senior devs spend >30% of time answering "where does X live?"

**Product drivers**
- Multiple genuinely distinct audiences with different needs
- Multiple bounded contexts have emerged (you can name 3+)
- Multi-year roadmap with strategic dependency on this codebase
- Compliance requirement (audit log, data residency, change control)
- Customer SLAs creating real cost for downtime

**Technical drivers**
- Deploy frequency wants to increase but each release is risky
- Specific module has a different scaling profile from the rest
- Outages routinely affect features unrelated to the failing code
- Database is becoming a bottleneck due to coupling
- Test suite is slow because everything is tangled

---

## 9. Drivers — what should NOT move you

Common but bad reasons to adopt more architecture.

**Aspiration drivers**
- "We want to be enterprise-ready" (without a concrete enterprise need)
- "Big companies do it this way" (you're not them)
- "Microservices are the future" (they're a trade-off, not a destination)
- "We might need it someday" (build for today + 12 months, not 5 years)

**Resume drivers**
- A new architect wants to demonstrate value
- Team wants to use a trendy pattern
- A conference talk made it sound cool
- Someone wrote a famous book about it

**Fear drivers**
- "What if we have to scale?" (you can refactor when you do)
- "What if a senior leaves?" (architecture doesn't solve that)
- "Other teams are doing it" (their context is different)

Every pattern adopted before it's needed slows you down: more files per feature, more concepts to learn, more places to make mistakes, more coordination overhead. **Architecture you don't need today is technical debt you took on voluntarily.**

---

## 10. The evolution path

The single highest-leverage skill is knowing which decisions get *expensive* over time.

### Decide early — fortune to fix later

| Decision | Why early matters |
|---|---|
| Bounded context boundaries | Untangling intertwined modules is multi-month work |
| Intent-based command names | Renaming hundreds of commands is risky |
| Domain layer free of EF | Decoupling later is rewrite-scale |
| Vertical-slice organization | Reorganizing later changes every import |
| Multi-tenancy isolation model | Retrofitting tenancy is a security risk |
| Audience-specific endpoints (when 3+) | Combining roles creates security bugs later |
| Domain events | Adding them late means historical data is gone |
| Structured logging | Unstructured logs cannot be queried |

### Decide late — cheap to retrofit

| Decision | When to make it |
|---|---|
| Redis caching | When MemoryCache becomes a bottleneck |
| Extracted worker host | When background work affects API responsiveness |
| Feature flag infrastructure | When you ship multiple times per week |
| OpenTelemetry traces | When debugging routinely takes >1 hour |
| Multi-region deployment | When a region has actual customer presence |
| Split a module into a service | When scaling or team boundary forces it |
| Idempotency keys | When external systems start calling you |
| Polly resilience | When you actually depend on flaky externals |

**The evolution rule.** Get the "decide early" decisions right from day one — they cost almost nothing upfront and a fortune to fix later. Defer the "decide late" decisions until you have evidence you need them. This combination is the highest-leverage architecture strategy.

---

## 11. Resource and timeline reality check

Honest cost estimates for each tier's initial setup:

| Tier | Initial setup | Per-feature overhead | Operational cost |
|---|---|---|---|
| 1 | 2–5 days | Minimal | Negligible — shared hosting |
| 2 | 1–2 weeks | Low | PaaS or single VM, ~$50–200 / mo |
| 3 | 4–8 weeks | Moderate | Multi-service infra, ~$500–5K / mo |
| 4 | Months + ongoing | Substantial | Dedicated SRE, $10K+ / mo + people |

### The team-stage matrix

|  | 1–3 devs | 3–10 devs | 10+ devs |
|---|---|---|---|
| Solo / pair time | Tier 1–2 | Tier 1 only | — |
| Pre-PMF startup | Tier 1–2 | Tier 2 | Skip — focus PMF |
| Post-PMF growth | Tier 2 | Tier 3 | Tier 3–4 |
| Enterprise / regulated | Tier 3 (forced) | Tier 3–4 | Tier 4 |

**Force-multipliers and force-dividers.** Compliance forces you up a tier (regulatory floor exists regardless of team size). Pre-PMF status forces you down a tier (Tier 3 ceremony kills a startup that hasn't validated the product). Always factor these in.

---

## 12. Project management implications

Cadence and ceremony scale with tier.

| Practice | Tier 1–2 | Tier 3 | Tier 4 |
|---|---|---|---|
| Planning | Weekly informal | 2-week sprints | Sprint + quarterly + annual |
| Standups | Optional | Daily | Daily + cross-team sync |
| Retros | Monthly | Per sprint | Per sprint + quarterly |
| Roadmap | 1–3 months out | 1–2 quarters | Annual + portfolio view |
| Estimates | T-shirt sizes | Story points | Points + capacity planning |
| Architecture review | Self / peer | Tech lead | ARB / RFC process |
| Postmortems | Slack thread | Doc per incident | Formal blameless process |

### Documentation overhead

| Tier | What to document |
|---|---|
| 1 | README. That's it. The code IS the spec. |
| 2 | README + 3–5 ADRs for the biggest decisions |
| 3 | READMEs per module + ADR registry + runbooks for top alerts |
| 4 | Full ADR registry + RFC process + golden path docs + onboarding program |

### Decision authority

| Decision type | Who decides |
|---|---|
| Code style, naming (T1–2) | Whoever's typing |
| Code style, naming (T3+) | Tech lead + linting |
| Library choice (T1–2) | Whoever's typing |
| Library choice (T3+) | Tech lead with PR review |
| Library choice (T4) | Architecture review for cross-cutting |
| Module boundary changes (T3+) | Module owners + tech lead |
| Cross-team architecture (T4) | RFC + ARB approval |

---

## 13. Common traps at each tier

**Tier 1**
- Building "the right architecture" for 3 months instead of shipping in 3 weeks
- Adopting microservices because it "feels professional"
- Setting up Kubernetes for a single container
- Writing unit tests for code that may not survive the week
- Buying a domain and design system before validating the idea

**Tier 2**
- Premature module boundaries — guessing at bounded contexts before knowing the domain
- Building feature-flag infrastructure when `appsettings.json` would do
- Setting up multi-region before having users in one region
- Over-investing in observability dashboards before the team can use them
- Hiring a DevOps engineer before having anything that needs operating

**Tier 3**
- Staying lean too long — refactoring under fire when patterns were obvious months earlier
- Doing the architecture migration in one big-bang sprint instead of incrementally
- Adopting microservices because "modular monolith is too simple"
- Forming teams that don't align with module boundaries
- Skipping ADRs — re-litigating the same decisions every quarter

**Tier 4**
- Over-extracting to microservices before the modular monolith is fully understood
- Distributed monolith — services tightly coupled via synchronous calls
- Building an internal platform that nobody adopts
- Architecture Review Board becomes a bottleneck instead of an enabler
- Compliance becomes a checkbox exercise rather than embedded in design

---

## 14. The one-page decision card

### Step 1 — Score your tier

Count your YES answers:

- [ ] Team is 3+ developers, or will be in 12 months
- [ ] You have 3+ distinct bounded contexts you can name
- [ ] You serve 2+ genuinely different audiences
- [ ] Codebase will live 3+ years
- [ ] Downtime has measurable cost (revenue, SLA, compliance)
- [ ] You need to scale parts independently
- [ ] You expect to extract some modules to services later
- [ ] You have regulatory requirements (GDPR, HIPAA, PCI, SOC2)
- [ ] You ship multiple times per week
- [ ] Your domain has real complexity (not just CRUD)

| Score | Tier |
|---|---|
| 0–1 | **Tier 1** — prototype |
| 2–3 | **Tier 2** — small product |
| 4–6 | **Tier 3** — growing, modular monolith |
| 7+ | **Tier 4** — large-scale platform |

### Step 2 — Match patterns to tier

| Pattern | T1 | T2 | T3+ |
|---|---|---|---|
| Single project + EF Core | ✓ | — | — |
| Clean Architecture (4 projects) | — | ✓ | — |
| Vertical slices | ○ | ✓ | ✓ |
| MediatR + pipeline behaviors | — | ✓ | ✓ |
| Domain events | — | ✓ | ✓ |
| Modular monolith (multiple modules) | — | — | ✓ |
| One DbContext per module | — | — | ✓ |
| Outbox + Inbox | — | — | ✓ |
| Integration events | — | — | ✓ |
| Multi-audience endpoints | — | ○ | ✓ |
| Feature flag infrastructure | — | ○ | ✓ |
| Polly resilience | — | ○ | ✓ |
| OpenTelemetry full stack | — | ○ | ✓ |
| Idempotency keys | — | — | ✓ |
| Architecture tests | — | ○ | ✓ |
| Multi-tenancy abstractions | — | — | if multi-tenant |
| Service extraction | — | — | when forced |

Legend: ✓ adopt · ○ optional (depends on context) · — skip

### Step 3 — Plan the evolution

Every 6 months, re-score your tier. If you crossed into a higher tier:

1. Identify the patterns you're missing
2. Pick the highest-pain pattern first
3. Allocate one sprint to it
4. Ship, measure, iterate
5. Add an ADR documenting why you adopted it

**Don't refactor everything in one quarter.** Architecture evolves incrementally; revolutions usually fail.

---

> **Right-size to the problem in front of you.**
> **Evolve when reality forces you, not when aspiration tells you.**

That's the entire guide. Everything else is detail.
