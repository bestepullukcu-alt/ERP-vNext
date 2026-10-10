# MVP6 effort update 09a (Q96) — split of the pack / Phase 1.5 / dispatch-closure rows

Status: **analysis package, agent output** (not CT-accepted). Base `feature/mvp6-logistics` @ `4a8d4d4b339528a88e6220fb8402e5a2c771136c`.
Method of updates 08/09 unchanged: 8 h = 1 person-day; O/M/P in person-hours; figures are M unless O/M/P is shown.
Owner decision **C-1 = A** (from the Q96 dispatch): split the unsplit rows and credit only verified pack + Phase 1.5 work.
No ledger, record, pack or source was edited. Assumptions and inputs: `README.md`.

## 1. Headline (before = update 09, after = 09a)

| Measure | Frozen before | Frozen after | Δ | Forecast before | Forecast after | Δ |
|---|---:|---:|---:|---:|---:|---:|
| Total | 2,842 | 2,842 | 0 | 2,952 | 2,952 | 0 |
| Delivered | 1,464 (51.5 %) | **1,478.6 (52.0 %)** | +14.6 | 1,469 (49.8 %) | **1,483.6 (50.3 %)** | +14.6 |
| Accepted | 1,140 (40.1 %) | **1,154.6 (40.6 %)** | +14.6 | 1,140 (38.6 %) | **1,154.6 (39.1 %)** | +14.6 |
| Remaining | 1,378 | **1,363.4** | −14.6 | 1,483 | **1,468.4** | −14.6 |
| Pack category share | 66.1 % | **72.3 %** | +6.2 pt | 65.7 % | **71.7 %** | +6.0 pt |

Delivered O/M/P (frozen): 1,069 / 1,464 / 1,916.6 → **1,077.8 / 1,478.6 / 1,940.0** (+8.8 / +14.6 / +23.4).
Credited: part (a) of 0190-1 (+5.3), 0192-1 (+5.3) and 0187-1 (+4.0). 0186-1 is not credited: the PH15-UI-186 record did not exist at start.
For comparison, 09 C-1 (whole rows) would have added +22 h; this split adds +14.6 h and leaves 7.4 h of those rows open as dispatch closure.

## 2. Split table (each original row → (a) + (b))

Ratio (a):(b) = 2:1 on O, M and P (ASSUMPTION A2); (a) is rounded to one decimal and (b) = row − (a), so each pair adds up exactly.

| Row (frozen 08 ledger) | Original O/M/P | (a) pack text + Phase 1.5 table | (b) dispatch closure | Check (a)+(b) | (a) credited? |
|---|---:|---:|---:|---|---|
| 0186-1-REMAINING (Returns) | 3.6 / 6 / 9.6 | 2.4 / 4.0 / 6.4 | 1.2 / 2.0 / 3.2 | 3.6 / 6 / 9.6 ✓ | **No**: PH15-UI-186 not recorded (CL9a-04) |
| 0187-1-REMAINING (Claims) | 3.6 / 6 / 9.6 | 2.4 / 4.0 / 6.4 | 1.2 / 2.0 / 3.2 | 3.6 / 6 / 9.6 ✓ | **Yes** (CL9a-03) |
| 0190-1-REMAINING (S&OP) | 4.8 / 8 / 12.8 | 3.2 / 5.3 / 8.5 | 1.6 / 2.7 / 4.3 | 4.8 / 8 / 12.8 ✓ | **Yes** (CL9a-01) |
| 0192-1-REMAINING (Capacity) | 4.8 / 8 / 12.8 | 3.2 / 5.3 / 8.5 | 1.6 / 2.7 / 4.3 | 4.8 / 8 / 12.8 ✓ | **Yes** (CL9a-02) |
| **Sum of the four rows** | **16.8 / 28 / 44.8** | **11.2 / 18.6 / 29.8** | **5.6 / 9.4 / 15.0** | **16.8 / 28 / 44.8 ✓** | credited **8.8 / 14.6 / 23.4** |

Arithmetic check: 2.4+1.2 = 3.6, 4.0+2.0 = 6, 6.4+3.2 = 9.6; 3.2+1.6 = 4.8, 5.3+2.7 = 8, 8.5+4.3 = 12.8.
Credited = 2.4+3.2+3.2 = 8.8 (O), 4.0+5.3+5.3 = 14.6 (M), 6.4+8.5+8.5 = 23.4 (P). Frozen ledger rows 106 → 110; total 2,842 unchanged.

**Forecast view.** The forecast rows are deltas on these ledger rows (08 FC-0186-1 and FC-0187-1 +0.4/2/6.4; 09 FC9-0190-1 and FC9-0192-1 −0.8/0/+3.2).
The deltas stay whole in part (b) (ASSUMPTION A4), so the credit is the same in both views:

| Row | Forecast row O/M/P | (a) | (b) incl. forecast delta | Check |
|---|---:|---:|---:|---|
| 0186-1 | 4 / 8 / 16 | 2.4 / 4.0 / 6.4 (not credited) | 1.6 / 4.0 / 9.6 | 4 / 8 / 16 ✓ |
| 0187-1 | 4 / 8 / 16 | 2.4 / 4.0 / 6.4 | 1.6 / 4.0 / 9.6 | 4 / 8 / 16 ✓ |
| 0190-1 | 4 / 8 / 16 | 3.2 / 5.3 / 8.5 | 0.8 / 2.7 / 7.5 | 4 / 8 / 16 ✓ |
| 0192-1 | 4 / 8 / 16 | 3.2 / 5.3 / 8.5 | 0.8 / 2.7 / 7.5 | 4 / 8 / 16 ✓ |

## 3. Credited lines (detail in `CREDIT-LINES.tsv`)

| Line | Row part | Delivered / accepted O/M/P | Evidence |
|---|---|---:|---|
| CL9a-01 | 0190-1 (a) | 3.2 / 5.3 / 8.5 | CT verdicts Q81/Q82 `f7577a53…c6a5`; VER Q82 PASS2 `35f35438…6af646`; owner decision Q80 (pack + PH15-UI-190) `37f3ff0d…0bea` |
| CL9a-02 | 0192-1 (a) | 3.2 / 5.3 / 8.5 | same three records (PH15-UI-192) |
| CL9a-03 | 0187-1 (a) | 2.4 / 4.0 / 6.4 | pack sign-off `365de5be…827b`; apply Q38 `dd8936d9…2fd`; VER Q49 `26af6011…c13`; CT disposition q46-q47-q49 `798d3c78…96b47`; PH15-UI-187 `95a5c4f4…f74e`; Q87 PASS `335c7ab3…e915` (supporting) |
| CL9a-04 | 0186-1 (a) | 0 (not credited) | PH15-UI-186 record absent at start and end |
| CL9a-05…08 | (b) of each row | 0 (remaining) | original estimate sources |
| CL9a-09 | Q86/Q87 text patches | 0 (no own row) | supporting only |
| CL9a-10 | draft overlays Q64a/Q84/Q88/Q91 | 0 | drafts earn 0 h |

## 4. Per module — frozen baseline (Board view)

Accepted per module = the 09 Board split plus the credited (a) M (ASSUMPTION A5). Totals are unchanged.

| Module | Accepted | Δ | Delivered | Δ | Remaining | Δ | Total | Delivered % | Accepted % |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 0183 | 152 | +0.0 | 252 | +0.0 | 42 | +0.0 | 294 | 85.7 → 85.7 | 51.7 → 51.7 |
| 0184 | 104 | +0.0 | 180 | +0.0 | 28 | +0.0 | 208 | 86.5 → 86.5 | 50.0 → 50.0 |
| 0185 | 144 | +0.0 | 144 | +0.0 | 164 | +0.0 | 308 | 46.8 → 46.8 | 46.8 → 46.8 |
| 0186 | 172 | +0.0 | 172 | +0.0 | 106 | +0.0 | 278 | 61.9 → 61.9 | 61.9 → 61.9 |
| 0187 | 204 | +4.0 | 204 | +4.0 | 110 | −4.0 | 314 | 63.7 → 65.0 | 63.7 → 65.0 |
| 0190 | 153.3 | +5.3 | 153.3 | +5.3 | 130.7 | −5.3 | 284 | 52.1 → 54.0 | 52.1 → 54.0 |
| 0192 | 225.3 | +5.3 | 225.3 | +5.3 | 138.7 | −5.3 | 364 | 60.4 → 61.9 | 60.4 → 61.9 |
| 0147 | 0 | +0.0 | 20 | +0.0 | 248 | +0.0 | 268 | 7.5 → 7.5 | 0.0 → 0.0 |
| 0148 | 0 | +0.0 | 20 | +0.0 | 232 | +0.0 | 252 | 7.9 → 7.9 | 0.0 → 0.0 |
| SHARED | 0 | +0.0 | 108 | +0.0 | 164 | +0.0 | 272 | 39.7 → 39.7 | 0.0 → 0.0 |
| **MVP6** | 1,154.6 | +14.6 | 1,478.6 | +14.6 | 1,363.4 | −14.6 | 2,842 | 51.5 → 52.0 | 40.1 → 40.6 |

## 5. Per category — frozen baseline (delivered share %, as the Board shows)

Only the pack category moves (bold). Contract, Backend, Frontend, Integration and Test are identical to update 09.

| Module | Pack | Contract | Backend | Frontend | Integration | Test |
|---|---:|---:|---:|---:|---:|---:|
| 0183 | 66.7 | 72.7 | 90.0 | 100.0 | 100.0 | 70.6 |
| 0184 | 80.0 | 80.0 | 92.3 | 100.0 | 100.0 | 60.0 |
| 0185 | 66.7 | 100.0 | 80.0 | 0.0 | 18.2 | 47.1 |
| 0186 | 76.9 | 83.3 | 90.0 | 0.0 | 50.0 | 66.7 |
| 0187 | 76.9 → **92.3** | 85.7 | 91.7 | 0.0 | 50.0 | 70.6 |
| 0190 | 71.4 → **90.4** | 83.3 | 84.2 | 0.0 | 27.3 | 57.1 |
| 0192 | 75.0 → **91.6** | 85.7 | 89.7 | 0.0 | 38.5 | 66.7 |
| 0147 | 42.9 | 33.3 | 0.0 | 0.0 | 0.0 | 0.0 |
| 0148 | 42.9 | 33.3 | 0.0 | 0.0 | 0.0 | 0.0 |
| SHARED | – | 0.0 | 100.0 | 0.0 | 0.0 | 42.9 |
| **MVP6** | 66.1 → **72.3** | 64.0 | 73.4 | 21.8 | 32.4 | 51.7 |

## 6. Per module — forecast (2,952 h)

| Module | Accepted | Δ | Delivered | Δ | Remaining | Δ | Total | Delivered % | Accepted % |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 0183 | 152 | +0.0 | 252 | +0.0 | 48 | +0.0 | 300 | 84.0 → 84.0 | 50.7 → 50.7 |
| 0184 | 104 | +0.0 | 180 | +0.0 | 32 | +0.0 | 212 | 84.9 → 84.9 | 49.1 → 49.1 |
| 0185 | 144 | +0.0 | 144 | +0.0 | 189 | +0.0 | 333 | 43.2 → 43.2 | 43.2 → 43.2 |
| 0186 | 172 | +0.0 | 172 | +0.0 | 137 | +0.0 | 309 | 55.7 → 55.7 | 55.7 → 55.7 |
| 0187 | 204 | +4.0 | 204 | +4.0 | 131 | −4.0 | 335 | 59.7 → 60.9 | 59.7 → 60.9 |
| 0190 | 153.3 | +5.3 | 153.3 | +5.3 | 118.7 | −5.3 | 272 | 54.4 → 56.4 | 54.4 → 56.4 |
| 0192 | 225.3 | +5.3 | 225.3 | +5.3 | 126.7 | −5.3 | 352 | 62.5 → 64.0 | 62.5 → 64.0 |
| 0147 | 0 | +0.0 | 20 | +0.0 | 248 | +0.0 | 268 | 7.5 → 7.5 | 0.0 → 0.0 |
| 0148 | 0 | +0.0 | 20 | +0.0 | 232 | +0.0 | 252 | 7.9 → 7.9 | 0.0 → 0.0 |
| SHARED | 0 | +0.0 | 113 | +0.0 | 206 | +0.0 | 319 | 35.4 → 35.4 | 0.0 → 0.0 |
| **MVP6** | 1,154.6 | +14.6 | 1,483.6 | +14.6 | 1,468.4 | −14.6 | 2,952 | 49.8 → 50.3 | 38.6 → 39.1 |

## 7. Per category — forecast

| Module | Pack | Contract | Backend | Frontend | Integration | Test |
|---|---:|---:|---:|---:|---:|---:|
| 0183 | 66.7 | 72.7 | 83.7 | 100.0 | 100.0 | 70.6 |
| 0184 | 80.0 | 80.0 | 85.7 | 100.0 | 100.0 | 60.0 |
| 0185 | 66.7 | 96.0 | 71.9 | 0.0 | 17.4 | 39.5 |
| 0186 | 71.4 | 83.3 | 84.7 | 0.0 | 43.5 | 54.1 |
| 0187 | 71.4 → **85.7** | 85.7 | 87.1 | 0.0 | 43.5 | 58.5 |
| 0190 | 71.4 → **90.4** | 83.3 | 84.2 | 0.0 | 27.3 | 57.1 |
| 0192 | 75.0 → **91.6** | 85.7 | 89.7 | 0.0 | 38.5 | 66.7 |
| 0147 | 42.9 | 33.3 | 0.0 | 0.0 | 0.0 | 0.0 |
| 0148 | 42.9 | 33.3 | 0.0 | 0.0 | 0.0 | 0.0 |
| SHARED | 100.0 | 0.0 | 90.0 | 0.0 | 0.0 | 31.6 |
| **MVP6** | 65.7 → **71.7** | 63.7 | 70.1 | 22.9 | 31.1 | 46.2 |

## 8. What did not move

- Frozen baseline 2,842 h and forecast 2,952 h (every module total unchanged).
- Part (b) of all four rows (5.6 / 9.4 / 15.0 frozen) and all forecast deltas: remaining until the UI build is writer-complete and closed by VER and CT.
- 0186-1 part (a): remaining until the PH15-UI-186 owner decision is recorded (then +2.4 / 4.0 / 6.4; 0186 delivered 176, pack 76.9 → 92.3 %).
- Draft code overlays (Q64a, Q84, Q88, Q91): 0 h.
- Contract, Backend, Frontend, Integration and Test shares.

## 9. Findings

- **F-09a-1 (method):** no estimate source has a sub-split of "UI pack revision, Phase 1.5, dispatch closure", and the 08 Claims/Returns treatment gives no numeric ratio ("no split, so no credit").
  The 2:1 ratio is an agent ASSUMPTION (A2). CT confirms it or names another. Sensitivity (M): 1:1 → +11.0 h; 2:1 → +14.6 h; whole row (09 C-1) → +22 h.
- **F-09a-2:** the owner decision C-1 = A exists only in the Q96 dispatch. No record file exists under `docs/records/decisions/2026-09/`, so the ledger writer should record it.
  The CT disposition q46-q47-q49 line "Effort: no product credit (pack text)" is read as superseded for the pack category (A6); that record is not edited.
- **F-09a-3:** PH15-UI-186 has no record, so 0186-1 (a) is pending (Q93 is writing it).
- **F-09a-4 (info):** CT-QUEUE still shows Q87 "READY" and there is no CT verdict record for Q86/Q87. The 0187 credit does not depend on it (A7).
- **F-09a-5 (info):** the totals now carry one decimal (1,478.6, 1,154.6). The Board must accept non-integer hours.
