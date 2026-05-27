# ADR 0017 — Consumer-driven contract tests (Pact)

**Status**: Accepted (Tier 5) — placeholder pending implementation

## Decision

Cross-service event contracts will be verified via consumer-driven contract tests (Pact.NET). Each consumer publishes a Pact file describing what it expects from the producer's events; the producer's CI verifies it produces messages matching every consumer's contract.

## Why not just shared types?

The `*.Contracts` projects give us *compile-time* alignment within the repo — but in real systems those would be published as NuGet packages, and consumers may be on older versions. Contract tests give us *runtime* alignment: if catalog-service changes `StockDecrementedIntegrationEvent` in a breaking way, the consumer-driven test in orders-service catches it before deployment.

## Status

Test projects are scaffolded (`{Service}.ContractTests` per service) but no Pact assertions are wired yet. This ADR records the intent; first implementation lands when the team is ready to set up a Pact broker.
