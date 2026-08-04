# Management & Governance (MG)

**Branch short code:** `mg`
**Governance orchestration:** [DCP-006](../../portfolio/delivery-capability-packs/DCP-006-portfolio-delivery-process-core.md)

## Purpose

This domain packages governance for authoritative enterprise decisions, structural work decomposition and
business-process architecture. It is a governance boundary, not a production-service topology. No service,
port, Gateway route, runtime module or module pack is created or authorized by this scaffold.

## Governance-packaged canonical modules

- `MOD-0007 — Decision & Rationale Log`
- `MOD-0354 — Decomposition & Work Structuring Engine`
- `MOD-0355 — Business Process Architecture & Modeling`

`MOD-0007` retains its Blueprint logical route **Enterprise Control Points → Enterprise Governance Registry
Layer → Authoritative Registries** and institutional owner **Enterprise Governance Process Owner**. Repository
topology is fixed by the authoritative [MOD-0007 parent pack](module-packs/MOD-0007-decision-rationale-log.md):
the primary is an isolated `DecisionRegistry` inside `Diten.ManagementGovernanceService`. If any mandatory
isolation test fails, the primary is replaced fail-closed by the sole `Diten.EnterpriseGovernanceService`
fallback; the two topologies cannot coexist or operate as a hybrid. DecisionRegistry cannot share domain
types, repositories, collections, indexes, dependency-injection registrations or transactions with DWS/BPM,
and placement in `Diten.EnterpriseStrategyService` is forbidden. This topology decision creates no service
scaffold, port, route, credential or runtime authority.

MOD-0354 and MOD-0355 retain DCP-006 OD-02's initial conditional `Diten.ManagementGovernanceService`
modular-monolith placement, mandatory isolation architecture tests and fail-closed split fallback. This
scaffold does not create that service. MOD-0354 remains structural-only; MOD-0355 remains process-model-only.
Neither may become a task, execution, workflow or approval engine.

## Gate I routing

Gate I-A consumes an approved MOD-0007 decision-reference contract and, only when authoritative policy
requires approval, a separately amended MOD-0023 ApprovalOutcome contract. Producer approval, a later
MOD-0117 consumer amendment and explicit runtime authority are all required before a consumer adapter is
executable. Gate I-B and Gate I-C producers are packaged by
[Enterprise Strategy & Business Performance](../enterprise-strategy-business-performance/).

## Code-reality quarantine

Existing Enterprise Strategy DWS/task/approval code and Management Governance frontend surfaces are
prototype/legacy evidence only. TaskAggregate, task-like DWS status/date/progress/assignment/dependency,
local approval and approve/assign/escalate controls remain `QUARANTINE`; they are not authoritative runtime
or migration baselines.

## Documents

- [Domain config](domain-config.md)
- [Module-pack routing](module-packs/README.md)
- [Module ID Registry](../../registries/module-id-registry.md)
