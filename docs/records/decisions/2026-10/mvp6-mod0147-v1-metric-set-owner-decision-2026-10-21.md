# MOD-0147 v1 — fixed metric set (owner decision, 2026-10-21)

- owner: **Natig** (`ny@gmgroup.ch`, management) — decided in chat 2026-10-21, recorded by the Control Tower
- status: **ACCEPTED**
- module: MOD-0147 Supplier Performance & Risk (`execution/domains/supply-chain-execution/module-packs/MOD-0147-supplier-performance-risk.md`, `status: draft`)
- options considered: `docs/roadmap/plans/mvp6-follow-up-drafts-01/MOD-0147-METRIC-SET-OPTIONS-v0.1.md`
- timing: MOD-0147 starts after MOD-0216 TPRM (Natig 2026-10-21). This decision goes into the pack amendment then; the
  risk part only reads TPRM's result and holds no risk itself; metrics move to the Metric Registry when it exists.

## Decision

Scope: per Tenant + LE + supplier + period. Every metric: unit `PERCENT`, `0..100`, `HIGHER_IS_BETTER` (pack §0.1).

| code | metric | formula (period) | weight |
|---|---|---|---|
| `OTD` | On-time delivery rate | receipt lines received **on or before the promised date** — **up to 3 days early also counts as on time** — ÷ receipt lines × 100 | 25 |
| `QTY_ACC` | Quantity accuracy | receipt lines whose received quantity is **within ±2 %** of the ordered quantity ÷ receipt lines × 100 | 25 |
| `QUAL_ACC` | Quality acceptance rate (= 100 − quality reject rate) | accepted quantity ÷ inspected quantity × 100 | 25 |
| `INV_MATCH` | Invoice match rate (= 100 − invoice mismatch rate) | invoices passing 3-way match without exception ÷ invoices × 100 | 25 |

- **Period:** calendar month.
- **Missing data:** a metric with no data in the period is left out and the others are re-scaled to 100; the scorecard
  is **published only when at least 3 of the 4 metrics have data**, and the missing metric is shown on it.
- **Minimum volume:** fewer than **5 receipt lines** in the period → "insufficient data", no score.
- Rounding stays as in the pack (2 decimals, round-half-even).

## Consequences

- The pack's current "every policy metric is required" rule changes (missing-metric rule above) — a pack amendment, with
  CT-APR-02 independent review and a new CT-APR-01 row, when MOD-0147 starts.
- Sources are Procurement modules (MOD-0141 PO, MOD-0142 GRN, MOD-0143 3-way match) and a QC source not yet identified:
  reading them needs XMC-001 consent and read contracts with the Procurement owner (Cihan, per Ali 2026-10-21).
- Weights, tolerances and the period are a policy revision: changing them later creates a new revision and never
  rewrites published scorecards.

This record authorizes no code and no pack change by itself.
