# MVP6 — efor baseline R1 / Shipment UI uzlaştırması

**Cut-off:** 2026-09-24. **Verdict:** R0 korunarak Shipment UI tahmini çifte sayım olmadan bağlandı. Bu bir E1
raporlama çalışmasıdır; ürün geliştirmesi veya test yapılmadı.

## Modül ve kategori yüzdeleri

| Modül | Pack/tasarım | Contract | Backend | Frontend | Entegrasyon | Test/VER | Genel | Kalan beklenen saat |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| 0183 Shipment/POD | 67% | 73% | 80% | 0% | 33% | 47% | **47.6%** | 154 |
| 0184 Carrier | 80% | 80% | 92% | 77% | 67% | 60% | **76.9%** | 48 |
| 0185 Loads | 73% | 67% | 80% | 0% | 20% | 57% | **50.4%** | 134 |
| 0186 Returns | 77% | 83% | 90% | 0% | 50% | 67% | **61.9%** | 106 |
| 0187 Claims | 77% | 86% | 92% | 0% | 50% | 71% | **63.7%** | 114 |
| 0190 S&OP | 71% | 83% | 84% | 0% | 27% | 57% | **52.1%** | 136 |
| 0192 Capacity | 75% | 86% | 90% | 0% | 38% | 67% | **60.4%** | 144 |
| 0147 Supplier Performance | 43% | 33% | 0% | 0% | 0% | 0% | **7.5%** | 248 |
| 0148 Supplier Portal | 43% | 33% | 0% | 0% | 0% | 0% | **7.9%** | 232 |
| Ortak işler | — | 0% | 78% | 0% | 0% | 24% | **27.9%** | 196 |
| MVP6 | 67% | 61% | 70% | 8% | 25% | 47% | **46.1%** | 1512 |

Delivered equivalent effort: **1292 h**. Remaining most likely: **1512 h**. Total: **2804 h**. Effort-weighted
completion: **46.1%**. Remaining sensitivity: **904–2816 h**. Completion sensitivity: **26–64%**; istatistiksel
güven aralığı değildir.

## Shipment uzlaştırması

| Kalem | R0 O/M/P saat | R1 O/M/P saat | Most-likely fark | Disposition |
|---|---:|---:|---:|---|
| Pack/tasarım remaining | 3.6 / 6 / 9.6 | 4 / 8 / 16 | +2 remaining | Exact owner decision/application/Phase 1.5 closure |
| Frontend remaining | 33.6 / 56 / 112 | 40 / 64 / 104 | +8 | Exact bounded UI replaces unknown reserve |
| Entegrasyon remaining | 19.2 / 32 / 51.2 | 12 / 24 / 40 | -8 | Shared handoff narrowed; Auth remediation excluded |
| Test/VER remaining | 14.4 / 24 / 38.4 | 20 / 36 / 64 | +12 | Composed runtime/browser boundary now explicit |

Contract and Backend rows are unchanged. The four remaining UI-related rows total **76/132/224 h**, exactly the
source package's **9.5/16.5/28 person-days** under the stated 8 h/day assumption. They replace earlier reserves.

## Change attribution

- **Completed work:** UI scope preparation is complete at E1, but receives **0 new numeric hours** because no measured or
  separately elicited preparation effort exists.
- **Estimate correction:** Shipment remaining and total denominator increase by **14 h**.
- **Scope change:** 0 h. The same first usable bounded UI slice remains; no upload, lookup, edit/delete/bulk, live producer,
  E5/G5 or rollout was added.

Shipment completion changes from 50.0% to 47.6%, and portfolio completion from 46.3% to 46.1%. These are denominator
corrections, not development regression. All other module and SHARED estimates are unchanged.

## Separate readiness metrics

The readiness successor remains **69.8% development checklist** (`44/63`) and **31.1% release gates** (`14/45`).
Those scores are not effort percentages and are not combined with 46.1%.

## Open uncertainty and reserves

- The 8 h/day conversion is an explicit assumption; no measured labor dataset exists.
- Gateway/module registration/navigation preimages remain unbound until a durable target is selected.
- Auth is a dependency for browser VER, but Auth product rework is excluded to prevent Carrier/SHARED double counting.
- Browser artifact export, response-loss evidence and accessible seven-language/RTL verification drive the wide VER range.
- Any future lookup/upload/live producer/E5/G5/rollout scope requires a row-level successor change rather than consuming
  these reserves silently.

## Repository effect

Only `docs/roadmap/plans/mvp6-effort-baseline-reconcile-02/` was added. R0, packs, source, contracts, board and Git state
were not changed. No tests or runtime processes were started.
