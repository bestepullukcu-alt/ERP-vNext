# MVP6 Effort — Update 08 (CT decisions of 26 Sep + new forecast)

**Date:** 2026-09-26 · **Lane:** AL-MVP6-EFFORT-08 (Q51) · **Type:** reporting-only successor to `mvp6-effort-shipment-ct-update-07`
**Primary metric:** estimated scope index (not readiness, elapsed time or production completion). 8 h = 1 person-day. All figures most-likely (M) unless O/M/P is shown.

## Headline

| Measure | Update 07 | Update 08 — frozen baseline | Update 08 — current forecast |
|---|---:|---:|---:|
| Total (denominator) | 2,842 h | 2,842 h | **2,954 h** |
| Delivered | 1,452 h (51.1 %) | **1,464 h (51.5 %)** | 1,469 h (49.7 %) |
| CT-accepted | 1,128 h (39.7 %) | **1,140 h (40.1 %)** | 1,140 h (38.6 %) |
| Remaining | 1,390 h | **1,378 h** | 1,485 h |
| Unestimated items | 3 | 11 (UNESTIMATED-SCOPE.tsv) | same |

The frozen baseline keeps the 2,842 h denominator. The forecast adds the three new estimates (+112 h M); its percentage is lower because the denominator grew, not because work was lost.

## Why today's work moved the index only 0.4 points

The rule (process v1.0 §9) credits hours only where a CT decision closes an **existing ledger row with an O/M/P split**. Most of today's accepted work either has no split or is pack text, which CT itself recorded as "no product credit".

| CT decision | Credit (O/M/P) | Why |
|---|---:|---|
| 1. A12 + A08/A09 regression + 16 PNG | **+2/+4/+8** (0183-R2-04) | The existing CT allocation for the root real-Auth browser regression was held open only for a browser lane and PNG; A12 VER-02 supplies both. A12 itself has no split → no further credit |
| 2. Loads 3.1.0 publication + guard binding | **+4.8/+8/+12.8** (0185-2) | The Loads estimate maps root seam disposition/candidate/repin work onto this contract reserve; publication accepted after independent VER |
| 3. Claims/Returns pack applies; self-registration sections | 0 | Pack text; CT records: no product credit. Self-registration sections are counted as delivered **only in the forecast** (3/5/8), not CT-credited |
| 4. PNG method decision | 0 | Method only; captures stay in module Test/VER reserves |
| 5. Backlog entries | 0 | Governance record |
| **Net (frozen)** | **+6.8/+12/+20.8 delivered; same off remaining** | Total unchanged |

## Per module (frozen baseline → forecast)

| Module | Delivered | Remaining | Total | Index frozen | Forecast total | Index forecast |
|---|---:|---:|---:|---:|---:|---:|
| 0183 Shipment | 252 | 42 | 294 | 85.7 % (was 84.4) | 300 | 84.0 % |
| 0184 Carrier | 180 | 28 | 208 | 86.5 % | 212 | 84.9 % |
| 0185 Loads | 144 | 164 | 308 | 46.8 % (was 44.2) | 311 | 46.3 % |
| 0186 Returns | 172 | 106 | 278 | 61.9 % | 309 | 55.7 % |
| 0187 Claims | 200 | 114 | 314 | 63.7 % | 335 | 59.7 % |
| 0190 S&OP | 148 | 136 | 284 | 52.1 % | 284 | 52.1 % |
| 0192 Capacity | 220 | 144 | 364 | 60.4 % | 364 | 60.4 % |
| 0147 Supplier Perf. | 20 | 248 | 268 | 7.5 % | 268 | 7.5 % |
| 0148 Supplier Portal | 20 | 232 | 252 | 7.9 % | 252 | 7.9 % |
| Shared | 108 | 164 | 272 | 39.7 % | 319 | 35.4 % |
| **MVP6** | **1,464** | **1,378** | **2,842** | **51.5 %** | **2,954** | **49.7 %** |

CT-accepted hours are not split per module in any record; they are shown at portfolio level only.

## Forecast rows (FORECAST.tsv, not in the frozen baseline)

| Estimate | O/M/P | Treatment |
|---|---:|---|
| Returns UI (Scope A) | net +13.6/+26/+34.4 | Replaces rows 0186-1/4/5/6 (new view 70/120/204); entered as per-row deltas so nothing is counted twice |
| Claims UI (Scope A) | net +6.8/+16/+14.4 | Replaces rows 0187-1/4/5/6 (new view 68/118/200) |
| Self-registration (D2=A, D5=A) | 38/70/127 | 12 rows by module/category; pack/DCP sections 3/5/8 delivered (not CT-credited) |
| Loads producer uptake (decision C, option A) | UNESTIMATED | No source estimate; uptake WP preflight must give O/M/P |
| Evidence kit v1.x | UNESTIMATED | No source estimate in the kit records |

## Open boundaries

Shipment rows A01, A02, A04–A07, A10, A13, A14, A15 and their PNG stay in the 0183 Test/VER reserve (12/20/36). Carrier PNG and final UI VER unchanged. Loads producer uptake, detail, lookup and multi-root grouping are unestimated. S&OP/Capacity UI scope and Supplier scope are reserve-only. Possible overlap between the self-registration nav-key row and the Q33 shared-integration rows is flagged, not netted (ASSUMPTIONS A7).

Reproduce: `ERP_ROOT=<repo> python3 RECALCULATE.py && python3 VERIFY.py` (VALIDATION.txt). No product, pack, ledger or Git state was changed.
