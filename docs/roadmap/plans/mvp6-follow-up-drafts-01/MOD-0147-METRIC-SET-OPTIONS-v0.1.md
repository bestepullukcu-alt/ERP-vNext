# MOD-0147 v1 fixed metric set — options for Natig's decision (v0.1)

    created : 2026-10-21 · status: OPTIONS, not a decision · approved in principle: the four metrics (Natig 2026-10-21)
    basis   : plan v10.0 §3a; pack `execution/domains/supply-chain-execution/module-packs/MOD-0147-supplier-performance-risk.md`
              §0.1 (unit PERCENT, 0..100, HIGHER_IS_BETTER, weights > 0, total = policy weight, round-half-even,
              every policy metric required today)
    timing  : MOD-0147 starts after MOD-0216 TPRM (Natig 2026-10-21) — these choices can be fixed now and put into the
              pack amendment later.

The four metrics: `OTD` on-time delivery · `QTY_ACC` quantity accuracy · `QUAL_ACC` quality acceptance (= 100 − reject
rate) · `INV_MATCH` invoice match (= 100 − mismatch rate). Scope: per Tenant + LE + supplier + period.

## 1 · Weights

| option | OTD | QTY_ACC | QUAL_ACC | INV_MATCH | note |
|---|---|---|---|---|---|
| **A — equal (recommended for v1)** | 25 | 25 | 25 | 25 | simplest to explain; no hidden priority; change later as a new policy revision (old scorecards stay as published) |
| B — quality and delivery first | 30 | 20 | 30 | 20 | common in manufacturing / pharma purchasing, where late or rejected material stops production |
| C — per supplier category | set per category | | | | needs a category master the destination does not have; not for v1 |

## 2 · Period

| option | note |
|---|---|
| **A — calendar month (recommended)** | matches monthly purchasing reviews; enough receipts for most suppliers |
| B — calendar quarter | steadier numbers for low-volume suppliers; slower feedback |
| C — rolling 90 days | always current, but every scorecard covers a different window — harder to compare and to correct |

## 3 · Tolerances

| metric | option A (recommended) | option B |
|---|---|---|
| OTD | **on time = received on or before the promised date; up to 3 days early also on time** | a window of ±N days set by the owner |
| QTY_ACC | **within ±2 % of the ordered quantity** (per receipt line) | exact match only |

Both tolerances are part of the policy revision, so changing them later does not rewrite published scorecards.

## 4 · A metric with no data in the period

The pack today requires every metric. Real cases: no invoices yet for this month's receipts; no QC inspection for a
service supplier.

| option | effect | pack change |
|---|---|---|
| A — leave it out and re-scale the others to 100 | always a score; a metric can silently disappear | yes (missing-metric rule) |
| **B — leave it out, re-scale, but publish only if at least 3 of 4 metrics have data; mark the missing one on the scorecard (recommended)** | no score from thin data; the reader sees what was missing | yes |
| C — no scorecard for the period | strict; many suppliers get none in early months | no |
| D — count missing as 100 | hides problems | not recommended |

## 5 · Also to settle (small)

- Minimum volume: below **N receipt lines** in the period (proposal N = 5) the scorecard says "insufficient data".
- Rounding: the pack's round-half-even, 2 decimals — no change proposed.
- Sources are Procurement modules (MOD-0141 PO, MOD-0142 GRN, MOD-0143 invoice match) and a QC source not yet
  identified — reading them needs XMC-001 consent and read contracts with the Procurement owner.

## Recommended set in one line

Equal weights 25/25/25/25 · calendar month · OTD on or before promised date (≤ 3 days early ok) · quantity ±2 % ·
publish when ≥ 3 of 4 metrics have data, missing one shown · minimum 5 receipt lines.
