# MVP6 effort update 09b (Q138) — Rev2 rules applied to the events since 09a

Status: **agent output, not CT-accepted.** Written by the Q138 LANE (Cowork LANE 1, Linux VM, repo via bridge; @orchestrator as effort owner,
product-manager estimate check, `/reconcile-records`; record in the documentation-writer role) on 2026-09-27, 17:14 → 17:23 +03:00.
Successor to `docs/roadmap/plans/mvp6-effort-update-09a/` (not overwritten). Scope = D-8 in
`docs/records/audits/2026-09/mvp6-ct-verdicts-q129-q101b-q121-2026-09-27.md` (`1ebacf02…`). Owner decision 27 Sep 16:25: 09b runs after Q129.

## 1. Metadata (SOP §17.1)

| Field | Value |
|---|---|
| Work Package / Prompt | Q138 (as dispatched; no version stated) · effort update 09b |
| Capability / Module | MVP6 effort accounting; MOD-0183…0192, 0147, 0148, SHARED |
| Lane / type / entry point | Cowork LANE 1 · DEV (records + ledgers only) · @orchestrator + product-manager, `/reconcile-records` |
| Risk class | not stated in the dispatch |
| Branch / base HEAD | `feature/mvp6-logistics` @ `4a8d4d4b339528a88e6220fb8402e5a2c771136c` (matched); 19 tracked diff paths |
| Authority | SOP v2.4 §7, §17.1, §17.4, §20, §25, §29.1, §37; `docs/guides/operations/mvp6-development-process-v1.0.md` §3.7, §9 (Rev2); owner decision C-1 = A + 2:1 (`docs/records/decisions/2026-09/mvp6-effort-c1-split-owner-decision-01.md` `41f44725…`) |
| Allowed paths | this folder; `CT-QUEUE.tsv`; `MILESTONE-EVENTS.tsv` |
| Previous effort | 09a `SHA256SUMS` `b595619ef555b92f3a1afed8dc01697d60472bc6a28ccb7af798d91e3a3855df` (4/4 OK); `EFFORT-UPDATE-09a.md` `8f2306c5…`; `BOARD-FIGURES.json` `54e72eaef88abdf6e838716a476c9a3f875ac48448457ee7f6e1bc3644d4de81` |

## 2. Method (09a's, unchanged)

- 8 h = 1 person-day; O/M/P in person-hours; figures are M unless O/M/P is shown. Estimated scope index, not readiness.
- Hours move only at **writer complete, independent VER pass or CT decision**, and only where the event closes an existing ledger row or
  row part that has its own O/M/P (08 A1, 09 method line, 09a). Drafts earn 0. Pack wording inside counted pack text earns no own hours (CL9a-09).
- Frozen baseline = 08 `EFFORT.tsv` (106 rows) with the 09a 2:1 split (110 rows); its denominator never changes (2,842 h).
  Scope outside the ledger enters the **forecast** as new rows (08 A7, 09 C-2). A forecast-only row can be DELIVERED in the forecast view only (08 FC-SR-PACK).
- Accepted hours are kept separately from delivered (Rev2 §9: "implementation index and accepted-milestone effort separately").
- `RECALCULATE.py` first rebuilds 09a from the 08 inputs and **asserts** it equals 09a `BOARD-FIGURES.json` and the 09a forecast table; only then are the 09b lines applied.

## 3. Headline (09a → 09b)

| Measure | Frozen 09a | Frozen 09b | Δ | Forecast 09a | Forecast 09b | Δ |
|---|---:|---:|---:|---:|---:|---:|
| Total | 2,842 | **2,842** | 0 | 2,952 | **3,097** | +145 (fix plan) |
| Delivered | 1,478.6 (52.0 %) | **1,540.6 (54.2 %)** | +62 | 1,483.6 (50.3 %) | **1,546.6 (49.9 %)** | +63 |
| Accepted | 1,154.6 (40.6 %) | **1,158.6 (40.8 %)** | +4 | 1,154.6 (39.1 %) | **1,158.6 (37.4 %)** | +4 |
| Remaining | 1,363.4 | **1,301.4** | −62 | 1,468.4 | **1,550.4** | +82 |

Delivered O/M/P frozen 1,077.8 / 1,478.6 / 1,940.0 → **1,115.0 / 1,540.6 / 2,061.6**; remaining frozen **776.0 / 1,301.4 / 2,426.2**.
Forecast delivered **1,117.8 / 1,546.6 / 2,057.0**; remaining **911.0 / 1,550.4 / 2,817.0**.
The forecast delivered % falls although hours rise, because the 145 h fix plan enters the denominator with 0 h closed.

## 4. Event table (credit lines; full rows with evidence path + sha256 in `CREDIT-LINES.tsv`)

| Line | Event (2a–2e) | Row | Delivered (frozen) | Accepted | Decision |
|---|---|---|---:|---:|---|
| CL9b-01 | 2d · 0186-1 (a): PH15-UI-186 now recorded (`e142b74e…`) | 0186-1 (a) Pack | +4.0 (2.4/4/6.4) | **+4.0** | credited (C-1 = A; same as CL9a-01…03) |
| CL9b-02 | 2a · Claims dispatch closure: writer v4 (Q64f) + VER Q122/Q129 + CT D-1/D-2 | 0187-1 (b) Pack | +2.0 (1.2/2/3.2) | 0 | delivered, not accepted (A2) |
| CL9b-03 | 2a · Claims UI v4 `2b34741a…` writer-complete, VER PASS | 0187-4 Frontend | +56 (33.6/56/112) | 0 | delivered, not accepted (A2) |
| CL9b-04 | 2a · Claims manifest provider + 9 tests | FC-SR-0187 Backend (forecast only) | 0 (forecast +5) | 0 | forecast delivered only |
| CL9b-05 | 2a · Claims integration (CU-28 BLOCKED → Q108; O-01) | 0187-5 Integration | 0 | 0 | not credited (prompt rule) |
| CL9b-06 | 2a · Claims Test/VER | 0187-6 Test/VER | 0 | 0 | **RULE-GAP RG-1** |
| CL9b-07 | 2a · Claims backend reserve | 0187-3 Backend | 0 | 0 | not closed |
| CL9b-08 | 2b · Q114 P1 applied (Q126), VER Q128 PASS | inside 0187-1 (a) | 0 | 0 | no own hours (CL9a-09) |
| CL9b-09 | 2c · Q117 F02 fix (VER Q119) | FP-F02 | 0 | 0 | F02 PARTIAL (Q101b) |
| CL9b-10 | 2c · Q117 N1/N2 | none | 0 | 0 | **RULE-GAP RG-2** |
| CL9b-11 | 2c · Q121 Mongo guard (Q121a/b/c) | none | 0 | 0 | **RULE-GAP RG-3** |
| CL9b-12 | 2c · Q101 fix plan F01…F16 | 16 new forecast lines | 0 (forecast +145 total) | 0 | new lines; only F16 CLOSED (0 h) |
| CL9b-13 | 2e · all other events since 09a | — | 0 | 0 | existing 0-h rules |

**2a answer (Rev2: which categories go to delivered and which to accepted).** Claims v4 moves the **Pack (b)** and **Frontend** rows to
*delivered* (writer complete + independent VER + CT disposition all exist). It moves nothing to *accepted*, because D-2 states the build is
INTEGRATION_READY, "Not ACCEPTED/DONE" (SOP §7, §29.1). **Integration** stays remaining (CU-28 → Q108, O-01). **Test/VER** is a RULE-GAP (RG-1).
**Backend**: only the forecast-only provider row moves, and only in the forecast view.

**2c:** Q117/Q121 are not in the 09a estimates, so the Q101 fix plan was added as 16 forecast lines (`FIX-PLAN-LINES.tsv`). Only closed parts
are credited: Q101b (CT ACCEPTED, D-6) rates F16 CLOSED (0/0/0) and the rest PARTIAL/OPEN → **0 h credited**.

## 5. Per module — 09a → 09b

### Frozen baseline (Board view, 2,842 h)

| Module | Total 09a → 09b | Delivered 09a → 09b | Δ | Accepted 09a → 09b | Δ | Remaining 09b | Delivered % | Accepted % |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| 0183 | 294 → 294 | 252 → 252 | 0 | 152 → 152 | 0 | 42 | 85.7 → 85.7 | 51.7 → 51.7 |
| 0184 | 208 → 208 | 180 → 180 | 0 | 104 → 104 | 0 | 28 | 86.5 → 86.5 | 50.0 → 50.0 |
| 0185 | 308 → 308 | 144 → 144 | 0 | 144 → 144 | 0 | 164 | 46.8 → 46.8 | 46.8 → 46.8 |
| 0186 | 278 → 278 | 172 → 176 | +4 | 172 → 176 | +4 | 102 | 61.9 → 63.3 | 61.9 → 63.3 |
| 0187 | 314 → 314 | 204 → 262 | +58 | 204 → 204 | 0 | 52 | 65.0 → 83.4 | 65.0 → 65.0 |
| 0190 | 284 → 284 | 153.3 → 153.3 | 0 | 153.3 → 153.3 | 0 | 130.7 | 54.0 → 54.0 | 54.0 → 54.0 |
| 0192 | 364 → 364 | 225.3 → 225.3 | 0 | 225.3 → 225.3 | 0 | 138.7 | 61.9 → 61.9 | 61.9 → 61.9 |
| 0147 | 268 → 268 | 20 → 20 | 0 | 0 → 0 | 0 | 248 | 7.5 → 7.5 | 0.0 → 0.0 |
| 0148 | 252 → 252 | 20 → 20 | 0 | 0 → 0 | 0 | 232 | 7.9 → 7.9 | 0.0 → 0.0 |
| SHARED | 272 → 272 | 108 → 108 | 0 | 0 → 0 | 0 | 164 | 39.7 → 39.7 | 0.0 → 0.0 |
| **MVP6** | 2,842 → 2,842 | 1,478.6 → **1,540.6** | +62 | 1,154.6 → **1,158.6** | +4 | 1,301.4 | 52.0 → 54.2 | 40.6 → 40.8 |

### Forecast (2,952 → 3,097 h)

| Module | Total 09a → 09b | Delivered 09a → 09b | Δ | Accepted 09a → 09b | Δ | Remaining 09b | Delivered % | Accepted % |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| 0183 | 300 → 300 | 252 → 252 | 0 | 152 → 152 | 0 | 48 | 84.0 → 84.0 | 50.7 → 50.7 |
| 0184 | 212 → 212 | 180 → 180 | 0 | 104 → 104 | 0 | 32 | 84.9 → 84.9 | 49.1 → 49.1 |
| 0185 | 333 → 344 | 144 → 144 | 0 | 144 → 144 | 0 | 200 | 43.2 → 41.9 | 43.2 → 41.9 |
| 0186 | 309 → 309 | 172 → 176 | +4 | 172 → 176 | +4 | 133 | 55.7 → 57.0 | 55.7 → 57.0 |
| 0187 | 335 → 335 | 204 → 263 | +59 | 204 → 204 | 0 | 72 | 60.9 → 78.5 | 60.9 → 60.9 |
| 0190 | 272 → 272 | 153.3 → 153.3 | 0 | 153.3 → 153.3 | 0 | 118.7 | 56.4 → 56.4 | 56.4 → 56.4 |
| 0192 | 352 → 352 | 225.3 → 225.3 | 0 | 225.3 → 225.3 | 0 | 126.7 | 64.0 → 64.0 | 64.0 → 64.0 |
| 0147 | 268 → 268 | 20 → 20 | 0 | 0 → 0 | 0 | 248 | 7.5 → 7.5 | 0.0 → 0.0 |
| 0148 | 252 → 252 | 20 → 20 | 0 | 0 → 0 | 0 | 232 | 7.9 → 7.9 | 0.0 → 0.0 |
| SHARED | 319 → 453 | 113 → 113 | 0 | 0 → 0 | 0 | 340 | 35.4 → 24.9 | 0.0 → 0.0 |
| **MVP6** | 2,952 → 3,097 | 1,483.6 → **1,546.6** | +63 | 1,154.6 → **1,158.6** | +4 | 1,550.4 | 50.3 → 49.9 | 39.1 → 37.4 |

## 6. Per category — delivered share %, 09a → 09b (changes in bold)

### Frozen

| Module | Pack | Contract | Backend | Frontend | Integration | Test |
|---|---:|---:|---:|---:|---:|---:|
| 0183 | 66.7 | 72.7 | 90.0 | 100.0 | 100.0 | 70.6 |
| 0184 | 80.0 | 80.0 | 92.3 | 100.0 | 100.0 | 60.0 |
| 0185 | 66.7 | 100.0 | 80.0 | 0.0 | 18.2 | 47.1 |
| 0186 | 76.9 → **92.3** | 83.3 | 90.0 | 0.0 | 50.0 | 66.7 |
| 0187 | 92.3 → **100.0** | 85.7 | 91.7 | 0.0 → **100.0** | 50.0 | 70.6 |
| 0190 | 90.4 | 83.3 | 84.2 | 0.0 | 27.3 | 57.1 |
| 0192 | 91.6 | 85.7 | 89.7 | 0.0 | 38.5 | 66.7 |
| 0147 | 42.9 | 33.3 | 0.0 | 0.0 | 0.0 | 0.0 |
| 0148 | 42.9 | 33.3 | 0.0 | 0.0 | 0.0 | 0.0 |
| SHARED | – | 0.0 | 100.0 | 0.0 | 0.0 | 42.9 |
| **MVP6** | 72.3 → **74.8** | 64.0 | 73.4 | 21.8 → **32.3** | 32.4 | 51.7 |

### Forecast

| Module | Pack | Contract | Backend | Frontend | Integration | Test |
|---|---:|---:|---:|---:|---:|---:|
| 0183 | 66.7 | 72.7 | 83.7 | 100.0 | 100.0 | 70.6 |
| 0184 | 80.0 | 80.0 | 85.7 | 100.0 | 100.0 | 60.0 |
| 0185 | 66.7 | 96.0 | 71.9 → **64.6** | 0.0 | 17.4 | 39.5 → **39.0** |
| 0186 | 71.4 → **85.7** | 83.3 | 84.7 | 0.0 | 43.5 | 54.1 |
| 0187 | 85.7 → **100.0** | 85.7 | 87.1 → **92.1** | 0.0 → **100.0** | 43.5 | 58.5 |
| 0190 | 90.4 | 83.3 | 84.2 | 0.0 | 27.3 | 57.1 |
| 0192 | 91.6 | 85.7 | 89.7 | 0.0 | 38.5 | 66.7 |
| 0147 | 42.9 | 33.3 | 0.0 | 0.0 | 0.0 | 0.0 |
| 0148 | 42.9 | 33.3 | 0.0 | 0.0 | 0.0 | 0.0 |
| SHARED | 100.0 → **55.6** | 0.0 | 90.0 → **85.7** | 0.0 | 0.0 | 31.6 → **16.3** |
| **MVP6** | 71.7 → **73.7** | 63.7 | 70.1 → **69.5** | 22.9 → **32.5** | 31.1 → **30.1** | 46.2 → **39.8** |

## 7. RULE-GAP list (hours left at 0)

| ID | Case | Why the method does not cover it | Hours at stake (not credited) |
|---|---|---|---|
| RG-1 | 0187-6 Test/VER: independent UI VER done (Q122, Q129; CT ACCEPTED as verification), module E2E on target (CU-28) BLOCKED | Frozen row 12/20/32 has no O/M/P split between UI VER and module E2E. The Claims estimate split (UI VER 14/24/40 + E2E 6/10/16) exists only as the forecast replacement of the whole row; no rule maps it onto the frozen row | UI VER part: up to 14/24/40 (forecast); frozen share undefined |
| RG-2 | Q117 N1 (serializer registration) and N2 ×3 (MDM token clock skew) | Q103 findings; no estimate line in the 08 ledger, 08/09 forecast or the Q101 fix plan | unestimated |
| RG-3 | Q121 Mongo test-DB guard (Q121a/b/c), CT ACCEPTED (D-5) | Q119 finding; no estimate line anywhere | unestimated |

## 8. Assumptions (for CT)

- **A1 (09a reproduced):** frozen 09a figures are rebuilt from the 08 inputs and equal 09a `BOARD-FIGURES.json` (asserted); the 09a forecast
  table (§6 of 09a) is asserted at portfolio and module level.
- **A2 (delivered vs accepted):** INTEGRATION_READY work counts as delivered (implementation index), not accepted (Rev2 §9; D-2; §29.1).
  Sensitivity: if CT reads D-1 as accepting CL9b-02/03, frozen accepted becomes 1,216.6 h (42.8 %).
- **A3 (whole-row closure):** 0187-4 closes in full, as 08 transferred 0185-2 in full. In the forecast view its delta FC-0187-4 (−3.6/−6/−28)
  closes with it, so the forecast credit is 30/50/84. Likewise FC-0187-1 closes with 0187-1 (b).
- **A4 (fix plan in the forecast):** "add to the total" is applied to the forecast total, because the frozen denominator is fixed by the method
  (08/09/09a). If CT puts it in the frozen baseline instead: frozen total 2,987 h, delivered 51.6 %, accepted 38.8 %.
- **A5 (fix-plan mapping):** module = the single module a finding names (F02, F03, F08 → 0185), otherwise SHARED. Category = the artefact the
  fix changes: F01/F07/F14 Integration; F03/F08/F09 Backend; F06/F13 Frontend; F05/F16 Pack; F02/F04/F10/F11/F12/F15 Test/VER.
- **A6 (overlaps, not netted, as 08 A7):** FP-F03 ↔ FC-SR-0185 (2/3/6); FP-F07 ↔ 0186/0187/0190/0192-5 and FC-SR-NAV; FP-F04 ↔ module Test/VER
  reserves; FP-F10 ↔ the evidence kit (08 UNESTIMATED); FP-F01 ↔ the integration reserves. CT decides any netting.
- **A7 (Q99):** 0186-1 (a) was queued as Q99 ("09b: credit 0186-1 (a)"); CL9b-01 delivers it. The Q99 row is not changed here.

## 9. Checks (DOĞRULA)

- Sum of lines = total: frozen 110 lines = **2,842** M; forecast 157 lines (110 + 08 FORECAST 20 + 09 FORECAST-09 11 + fix plan 16) = **3,097** M
  (2,842 + 112 − 2 + 145). Fix plan 16 lines = 80 / 145 / 244 exactly.
- Delivered ≥ accepted in every module and in total, in both views.
- Per-module totals sum to the overall total in both views; frozen module totals are unchanged from 09a.
- Every credited line (CL9b-01…04) has evidence paths and sha256 values; every cited evidence folder passed `sha256sum -c` at the start
  (Q129 217/217, Q122 454/454, draft-04 4/4, Q126 1/1, Q128 1/1, Q114 3/3, Q117 12/12, Q119 15/15, Q121 3/3, Q121b 20/20, Q121c 18/18,
  Q101 4/4, Q101b 4/4, 08 20/20, 09 12/12, 09a 4/4).

## 10. Files

`EFFORT-UPDATE-09b.md` (this file) · `CREDIT-LINES.tsv` (13 lines) · `FIX-PLAN-LINES.tsv` (16 new forecast lines) · `MODULE-FIGURES.tsv`
(09a → 09b per module and category, both views) · `BOARD-FIGURES.json` (frozen view, Board fields as 09a) · `RECALCULATE.py` · `README.md` · `SHA256SUMS`.

Agent PASS ≠ CT ACCEPTED — returning to CT.
