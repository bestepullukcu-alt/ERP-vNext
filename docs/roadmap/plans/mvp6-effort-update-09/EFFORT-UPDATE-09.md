# MVP6 Effort — Update 09 (events 26 Sep 13:01 → 19:50)

**Date:** 2026-09-26 (+03:00) · **Lane:** Q94 effort analyst (chat lane, Linux VM) · **Type:** reporting-only successor to `mvp6-effort-update-08` (not overwritten)
**Method:** update 08 unchanged — hours move only at writer complete, independent VER pass or CT decision, and only where the decision closes an
existing ledger row with an O/M/P split. 8 h = 1 person-day. Figures are most-likely (M) unless O/M/P is shown. Estimated scope index, not readiness.

## Headline (before → after)

| Measure | Update 08 | Update 09 | Change |
|---|---:|---:|---:|
| Frozen baseline total | 2,842 h | **2,842 h** | 0 |
| Delivered (frozen) | 1,464 h (51.5 %) | **1,464 h (51.5 %)** | 0 |
| CT-accepted (frozen) | 1,140 h (40.1 %) | **1,140 h (40.1 %)** | 0 |
| Remaining (frozen) | 1,378 h | **1,378 h** | 0 |
| Forecast total | 2,954 h | **2,952 h** | −2 h (remaining O/M/P −0.6 / −2 / −33.6) |
| Delivered (forecast) | 1,469 h (49.7 %) | 1,469 h (49.8 %) | 0 h |
| CT-accepted (forecast) | 1,140 h (38.6 %) | 1,140 h (38.6 %) | 0 h |
| Remaining (forecast) | 1,485 h | 1,483 h | −2 h |

**Why nothing is credited.** Every event since 08 is one of: a draft overlay (0 by rule), pack text (08 A5: no product credit), an analysis or
preparation package, or a Phase 1.5 approval. The only pack/Phase 1.5 work that is CT ACCEPTED after independent VER (MOD-0190/0192 UI pack
revision, Q81/Q82) and the owner-approved Phase 1.5 tables (PH15-UI-190/192, PH15-UI-187) all sit in rows that bundle "UI pack revision,
Phase 1.5, dispatch closure" with no O/M/P split, and the dispatch closure is still open. Update 08 treated the equivalent Claims/Returns rows
the same way ("no split, so no credit"). CT can override this (item C-1 below); the effect is quantified there.

The forecast moves by −2 h M: the Q78 option A estimates replace the 0190/0192 pack and frontend reserves (−12 h each), and the Loads
producer uptake (Q68, 19 h) plus F-1 (3 h) enter as new forecast rows (+22 h).

## Per-module figures

Category cells = delivered share of that category's hours (%), as on the Progress Board. Accepted per module = the split published on the
Progress Board (ASSUMPTION A3); 08 kept accepted at portfolio level only.

### Frozen baseline (2,842 h) — unchanged from 08

| Module | Accepted | Delivered | Remaining | Total | Pack | Contract | Backend | Frontend | Integration | Test/VER |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 0183 Shipment | 152 | 252 | 42 | 294 | 66.7 | 72.7 | 90.0 | 100.0 | 100.0 | 70.6 |
| 0184 Carrier | 104 | 180 | 28 | 208 | 80.0 | 80.0 | 92.3 | 100.0 | 100.0 | 60.0 |
| 0185 Loads | 144 | 144 | 164 | 308 | 66.7 | 100.0 | 80.0 | 0.0 | 18.2 | 47.1 |
| 0186 Returns | 172 | 172 | 106 | 278 | 76.9 | 83.3 | 90.0 | 0.0 | 50.0 | 66.7 |
| 0187 Claims | 200 | 200 | 114 | 314 | 76.9 | 85.7 | 91.7 | 0.0 | 50.0 | 70.6 |
| 0190 S&OP | 148 | 148 | 136 | 284 | 71.4 | 83.3 | 84.2 | 0.0 | 27.3 | 57.1 |
| 0192 Capacity | 220 | 220 | 144 | 364 | 75.0 | 85.7 | 89.7 | 0.0 | 38.5 | 66.7 |
| 0147 Supplier Perf. | 0 | 20 | 248 | 268 | 42.9 | 33.3 | 0.0 | 0.0 | 0.0 | 0.0 |
| 0148 Supplier Portal | 0 | 20 | 232 | 252 | 42.9 | 33.3 | 0.0 | 0.0 | 0.0 | 0.0 |
| Shared | 0 | 108 | 164 | 272 | – | 0.0 | 100.0 | 0.0 | 0.0 | 42.9 |
| **MVP6** | **1,140** | **1,464** | **1,378** | **2,842** | 66.1 | 64.0 | 73.4 | 21.8 | 32.4 | 51.7 |

### Current forecast (2,952 h) — deltas vs 08 forecast

| Module | Accepted | Delivered | Remaining | Total (08 → 09) | Δ total | Pack | Contract | Backend | Frontend | Integration | Test/VER |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 0183 | 152 | 252 | 48 | 300 → 300 | 0 | 66.7 | 72.7 | 83.7 | 100.0 | 100.0 | 70.6 |
| 0184 | 104 | 180 | 32 | 212 → 212 | 0 | 80.0 | 80.0 | 85.7 | 100.0 | 100.0 | 60.0 |
| 0185 | 144 | 144 | 189 | 311 → **333** | **+22** | 66.7 | 96.0 | 71.9 | 0.0 | 17.4 | 39.5 |
| 0186 | 172 | 172 | 137 | 309 → 309 | 0 | 71.4 | 83.3 | 84.7 | 0.0 | 43.5 | 54.1 |
| 0187 | 200 | 200 | 135 | 335 → 335 | 0 | 71.4 | 85.7 | 87.1 | 0.0 | 43.5 | 58.5 |
| 0190 | 148 | 148 | 124 | 284 → **272** | **−12** | 71.4 | 83.3 | 84.2 | 0.0 | 27.3 | 57.1 |
| 0192 | 220 | 220 | 132 | 364 → **352** | **−12** | 75.0 | 85.7 | 89.7 | 0.0 | 38.5 | 66.7 |
| 0147 | 0 | 20 | 248 | 268 → 268 | 0 | 42.9 | 33.3 | 0.0 | 0.0 | 0.0 | 0.0 |
| 0148 | 0 | 20 | 232 | 252 → 252 | 0 | 42.9 | 33.3 | 0.0 | 0.0 | 0.0 | 0.0 |
| Shared | 0 | 113 | 206 | 319 → 319 | 0 | 100.0 | 0.0 | 90.0 | 0.0 | 0.0 | 31.6 |
| **MVP6** | **1,140** | **1,469** | **1,483** | 2,954 → **2,952** | **−2** | 65.7 | 63.7 | 70.1 | 22.9 | 31.1 | 46.2 |

Forecast index per module: 0185 46.3 → 43.2 %, 0190 52.1 → 54.4 %, 0192 60.4 → 62.5 % (denominator changes only; no hours earned).

## Credited lines

**None.** `CREDIT-LINES.tsv` lists the 13 lines evaluated, each with module, category, the row it would close, O/M/P, evidence path and
sha256, and why it is not credited. Summary:

| Line | Event | Module · category | Row (O/M/P) | Decision |
|---|---|---|---|---|
| CL-01/02 | Q81/Q82 UI pack revision, VER PASS 10/10, CT ACCEPTED (`mvp6-ct-verdicts-q81-q82-2026-09-26.md` `f7577a53…c6a5`) | 0190 / 0192 · Pack | 0190-1 / 0192-1 REMAINING 4.8/8/12.8 | not credited — no split from the open dispatch closure |
| CL-03 | Q80 owner approval PH15-UI-190/192 (`…ui-pack-signoff-owner-decision-01.md` `37f3ff0d…0bea`) | 0190 / 0192 · Pack | same rows | not credited — Phase 1.5 has no own O/M/P |
| CL-04 | Claims PH15-UI-187 approval, recorded late (`mvp6-claims-ui-ph15-owner-decision-01.md` `95a5c4f4…e74e`) | 0187 · Pack | 0187-1 REMAINING 3.6/6/9.6 | not credited — not credited in 08 either (no double count), no split |
| CL-05 | Q78 scope + estimates, CT ACCEPTED as analysis, owner A/A | 0190 / 0192 | forecast | forecast only |
| CL-06…09 | Q64a, Q77a, Q84, Q88 drafts, CT ACCEPTED as DRAFT | 0187, 0185, 0190, 0192 | frontend / uptake rows | 0 — drafts |
| CL-10 | Q83/Q86 text patches P1–P5 | pack/DCP | none | not accepted at start (Q87 report appeared later); pack text in any case |
| CL-11/12 | Q68 uptake estimate; F-1 +1.5/3/5 | 0185 | forecast | forecast only |
| CL-13 | Q27/Q28 pack promotion apply + VER Q72; heading labels Q75/Q76 | 0190 / 0192 · Pack | none | pack text |

## Forecast rows added (FORECAST-09.tsv; 08 FORECAST.tsv kept as is)

| Id | Module · category | O/M/P | Source | Treatment |
|---|---|---:|---|---|
| FC9-0190-1 | 0190 · Pack | −0.8 / 0 / +3.2 | Q78 ESTIMATE.tsv `03669f33…09e3` | 4/8/16 replaces 0190-1-REMAINING 4.8/8/12.8 |
| FC9-0190-4 | 0190 · Frontend | −5.6 / −12 / −40 | same | 28/44/72 replaces 0190-4-REMAINING 33.6/56/112 |
| FC9-0192-1 | 0192 · Pack | −0.8 / 0 / +3.2 | same | 4/8/16 replaces 0192-1-REMAINING 4.8/8/12.8 |
| FC9-0192-4 | 0192 · Frontend | −6.4 / −12 / −40 | same | 32/52/88 replaces 0192-4-REMAINING 38.4/64/128 |
| FC9-0185-UP-01…05 | 0185 · Backend 3/5/9, Test/VER 3/5/8 + 4/6/12, Contract 0.5/1/2, Integration 1/2/4 | 11.5 / 19 / 35 | Q68 ESTIMATE.tsv `0412ab19…`; CT verdict `a326202a…a7a9` | RS-05 was outside both ledgers; Q68 option 1 (new forecast rows) |
| FC9-0185-F1-BE / -T | 0185 · Backend 0.5/1/2, Test/VER 1/2/3 | 1.5 / 3 / 5 | Q77a README A-07 `e7329f59…`; CT verdict `356cd273…05f3` | on top of Q68 |
| **Net** | | **−0.6 / −2 / −33.6** | | 0185 +13/+22/+40; 0190 −6.4/−12/−36.8; 0192 −7.2/−12/−36.8 |

Rows 0190/0192-5 and -6 are unchanged: Q78 names its integration (8/14/24) and UI VER (0190 12/20/36, 0192 14/24/40) as **UI sub-items** of
those reserves, so they are contained, not added (A5).

## Drafts, not credited (DRAFTS-NOT-CREDITED.tsv)

| Draft (CT ACCEPTED as DRAFT) | Module | Future credit potential O/M/P by category | Released by |
|---|---|---|---|
| Q64a Claims UI (45/45, `80da27c9…`) | 0187 | Frontend 30/50/84; Backend (provider) 3/5/9; Integration part of 8/16/28 | Q64b Mac build+test, then UI VER + CT |
| Q77a Loads uptake + F-1 (19/19, `8c472c27…`) | 0185 | Backend 3.5/6/11; Test/VER 4/7/11 (runtime VER 4/6/12 separate) | Q77b |
| Q84 S&OP UI (55/55, `0aafc340…`) | 0190 | Frontend 28/44/72; Integration part of 8/14/24 | Q84b |
| Q88 Capacity UI (55/55, `f4e18ab9…`) | 0192 | Frontend 32/52/88; Integration part of 8/14/24 | Q88c (IDs in address) → Q88b |
| **Total potential** | | **Frontend 90/146/244; Backend 6.5/11/20; Test/VER 4/7/11** + unsplit integration parts | writer complete (Mac), then VER + CT |

## Pending (PENDING.tsv)

| Item | State at this run | Hours at stake |
|---|---|---|
| Q86 text patches P1–P5 | applied; **no Q87 report at this run's start** (absent at 19:47 and 19:50); a `REPORT.md` "PASS 9/9" appeared at ~19:50 during the run — per the prompt not used; CT disposition still open | 0 (pack text) |
| Q64b, Q77b, Q84b, Q88b (+ Q88c) | READY on the local Mac (Q88c HELD after Q93) | see drafts table |
| Q61 Shipment isolated final VER | READY (Mac) | 0183-6-REMAINING 12/20/36 |
| Q62 Carrier UI source archive | READY (Mac) | unestimated |
| Q64 / Q65 Claims / Returns UI DEV | READY (Mac) | frontend rows |
| Q24b evidence kit validation | READY (Mac) | unestimated |
| Q91 Returns UI draft | in progress (parallel lane) | 0 while a draft |
| Q92 / Q93 text patches R1–R3 | decision required / held | 0 (pack text) |

## For CT confirmation

- **C-1 (pack/Phase 1.5 rows):** under the 08 method nothing is credited. If CT instead closes the whole pack/Phase 1.5 row where the pack
  revision is VER-accepted and Phase 1.5 is owner-approved, the frozen effect is 0190-1 +4.8/8/12.8, 0192-1 +4.8/8/12.8 and 0187-1 +3.6/6/9.6
  = **+22 h M** → delivered 1,486 h (52.3 %), accepted 1,162 h (40.9 %). Not applied here.
- **C-2 (Loads uptake in forecast):** Q68 option 1 is used (the package's recommendation; CT accepted the package "as preparation" without
  choosing). Without it the forecast total would be 2,930 h.
- **C-3:** A7 of 08 (self-registration nav-key overlap) is still carried; the 0190/0192 nav keys are inside the Q78 integration sub-item, not in FC-SR-NAV.

## ASSUMPTIONs

1. **Method:** 08 unchanged. Frozen ledger = 08 `EFFORT.tsv` (106 rows, `6254365b…`) with no row change; 08 forecast rows kept; new rows in `FORECAST-09.tsv`.
2. **No-split rule** (08 FC-0186-1/FC-0187-1) applied to the 0190/0192 and 0187 pack/Phase 1.5 rows; the dispatch closure is taken as open because no versioned UI dispatch exists (the Q84/Q88 drafts ran under the draft-overlays decision).
3. **Accepted per module** = the split published on the MVP6 Progress Board (module docs as read 2026-09-26 ~19:52; sum 1,140). 08 has no per-module split; no record gives one.
4. **BOARD-FIGURES.json** carries the **frozen baseline** (the Board's denominator is 2,842 h); category values are delivered-share percentages as on the Board; `null` = no hours in that category (Shared · pack). Forecast figures are in `MODULE-FIGURES.tsv`.
5. **Q78 integration and UI VER** are "UI sub-items" of 0190/0192-5 and -6 → contained; the reserves are not changed. For 0192-6 the UI VER M 24 equals the whole reserve M 24 (finding F-3).
6. **Self-registration for 0190/0192** (08 UNESTIMATED SR-0190-0192) is taken as estimated inside the Q78 integration row, which names "provider + 2 nav keys + tests".
7. **Loads uptake + F-1** enter the forecast as new rows (Q68 option 1; F-1 "on top of the Q68 total" per A-07).
8. **Events window:** 08 sealed 13:01; records with a later modification time were reviewed (list in `SOURCES.tsv`); the ledgers were read while Q90 was writing them — their hashes are "at read".
9. Q86 is treated as not accepted: no Q87 report existed at the start (checked 19:47 and 19:50:40). `docs/records/audits/2026-09/mvp6-q87-ver-text-patch-q83/REPORT.md` (Result: PASS 9/9) appeared at ~19:50 while this run was working; per the prompt it is not used. It would not change any number (pack text; 08 A5).

## Findings

- **F-1:** nothing earned hours since 08; the Board figures stay 1,464 delivered / 1,140 accepted on 2,842 h (`BOARD-FIGURES.json` equals the published Board values).
- **F-2:** the "UI pack revision, Phase 1.5, dispatch closure" rows (0186-1, 0187-1, 0190-1, 0192-1) have no split, so pack and Phase 1.5 work that is VER-accepted or owner-approved can never be credited until the dispatch also closes. A split (e.g. pack/Phase 1.5 vs dispatch) in the next estimate would remove the ambiguity.
- **F-3:** 0192-6-REMAINING M 24 is fully consumed by the Q78 UI VER sub-item (P 40 > reserve 38.4); module E2E and defect closure have no hours left in that row.
- **F-4:** new unestimated item CAP-IDS (Capacity IDs in the address, owner decision F-Q79-05 = A; Q88c).
- **F-5:** the Board method note ("three unestimated Loads items … producer root read") is now partly outdated: the producer root read is estimated (forecast).
