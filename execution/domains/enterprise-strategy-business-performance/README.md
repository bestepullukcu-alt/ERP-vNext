# Enterprise Strategy & Business Performance (ESBP)

**Branch short code:** `esbp`
**Governance orchestration:** [DCP-006](../../portfolio/delivery-capability-packs/DCP-006-portfolio-delivery-process-core.md)

## Purpose

This domain packages strategy/performance governance and the Gate I funding, scenario and realization producer
packs. Repository-domain packaging does not alter Blueprint logical lanes and does not select a production
service. This scaffold creates no module pack or implementation authority.

## Governance-packaged canonical modules

- `MOD-0352 — Enterprise Strategy Management`
- `MOD-0136 — Budgeting`
- `MOD-0138 — Scenario Planning`
- `MOD-0072 — Decision Logs & Outcome Tracking`

MOD-0136 and MOD-0138 retain the logical lane **Enterprise Application Ecosystem → ERP Suite → FP&A / EPM**.
MOD-0072 retains **Data, Knowledge & Intelligence → Decision Intelligence**. Their physical placement,
including any use of `Diten.EnterpriseStrategyService`, remains `TBD` for parent-pack architecture review.

## Gate I routing

- Gate I-A: MOD-0007 plus conditional MOD-0023; packaged outside this domain.
- Gate I-B: MOD-0136 and MOD-0138.
- Gate I-C: MOD-0072.

Producer contracts must be approved before a matching MOD-0117 consumer amendment or adapter becomes
executable.

## Prototype quarantine

Existing Enterprise Strategy decision, budget, scenario, actual-value and realization fields are
prototype/legacy evidence and migration candidates, not authoritative MOD-0007/0136/0138/0072 runtime
baselines. Approval/task/assign/escalate surfaces remain ownership hazards under Gate 2.

## Documents

- [Domain config](domain-config.md)
- [Module-pack routing](module-packs/README.md)
- [Module ID Registry](../../registries/module-id-registry.md)
