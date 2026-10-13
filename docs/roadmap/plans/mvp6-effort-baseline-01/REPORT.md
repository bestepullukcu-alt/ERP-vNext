# MVP6 — efor ağırlıklı geliştirme tahmini

2026-09-24. Provisional E1; estimated specialist-hours, NOT recorded work time. See METHOD.md.

| Modül | Pack/tasarım | Contract | Backend | Frontend | Entegrasyon | Test/VER | Genel | Kalan beklenen saat |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| 0183 Shipment/POD | 73% | 73% | 80% | 0% | 27% | 57% | **50.0%** | 140 |
| 0184 Carrier | 80% | 80% | 92% | 77% | 67% | 60% | **76.9%** | 48 |
| 0185 Loads | 73% | 67% | 80% | 0% | 20% | 57% | **50.4%** | 134 |
| 0186 Returns | 77% | 83% | 90% | 0% | 50% | 67% | **61.9%** | 106 |
| 0187 Claims | 77% | 86% | 92% | 0% | 50% | 71% | **63.7%** | 114 |
| 0190 S&OP | 71% | 83% | 84% | 0% | 27% | 57% | **52.1%** | 136 |
| 0192 Capacity | 75% | 86% | 90% | 0% | 38% | 67% | **60.4%** | 144 |
| 0147 Supplier Performance | 43% | 33% | 0% | 0% | 0% | 0% | **7.5%** | 248 |
| 0148 Supplier Portal | 43% | 33% | 0% | 0% | 0% | 0% | **7.9%** | 232 |
| Ortak işler | — | 0% | 78% | 0% | 0% | 24% | **27.9%** | 196 |
| MVP6 | 67% | 61% | 70% | 8% | 25% | 48% | **46.3%** | 1498 |

Delivered equivalent effort: 1292 h. Remaining most likely: 1498 h. Total: 2790 h. Completion: 46.3%.
Remaining sensitivity: 899–2803 h. Completion sensitivity: 26–64%. Not a statistical confidence interval.

## Critical path and continuation
1. NumericDate exact candidate binding/authority → application → independent Auth VER → Carrier E2E.
2. In parallel: scope/estimate the next authorized UI slice; do not open a code lane from this planning estimate.
3. Supplier DC decisions → shared producer artifacts → disjoint Supplier core and UI work.
4. Module closures → shared golden-flow/source reconciliation → operational release. Parallel hours are not calendar days.

No numeric release-readiness score is inferred from effort. Full G5/rollout acceptance remains unproven by reviewed inputs.

## Validation
EFFORT.tsv row arithmetic and source/output hashes checked. No source/pack/contract/board/Git mutation; only this report package was added.

## R1 reconciliation distinction
The supplied R1 result (44/63 = 69.8% development checklist; 14/45 = 31.1% release gates) measures checklist closure, not effort. It does not replace this effort estimate. The latest NumericDate candidate remains unapplied and awaits independent VER; F-02 and Carrier real-Auth E2E remain open. No additional earned effort is credited merely for another candidate report.
