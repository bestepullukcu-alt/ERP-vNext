# MVP6-SHIPMENT-ACCEPTANCE-RECONCILE-01 — SOP §22

- Work package: `MVP6-SHIPMENT-ACCEPTANCE-RECONCILE-01` (pilot queue Q05, Agent Lane-2)
- Role: acceptance-matrix reconciliation. Read-only except for this folder.
- Process: `docs/guides/operations/mvp6-development-process-v1.0.md` §4 (single acceptance matrix)
- Branch / HEAD: `feature/mvp6-logistics` / `4a8d4d4b339528a88e6220fb8402e5a2c771136c`. The working tree was already dirty (343 status rows at start, 344 rows outside this folder at end). It was not touched.
- Start: `2026-09-25T22:29:40+03:00` (Europe/Istanbul)
- End: `2026-09-25T22:37:44+03:00` (Europe/Istanbul)
- Verdict: **RECONCILED — ONE MATRIX, 25 ROWS; 18 READY / 7 BLOCKED; 35-FAIL CLASSIFICATION CONFIRMED 23 / 8 / 4; FOUR DELTA DECISIONS MISSING**

## Result

`ACCEPTANCE-MATRIX.tsv` is the single acceptance matrix for UI183-A01..A16 plus PRES-183-01..04. Every row lists the expected HTTP, browser and DB result, the current state, the evidence pointer with its SHA-256, the owner, the dependency, READY/BLOCKED, and the final-VER positive/negative/security runs. The records keep separate verdicts for UI183-A03, A09 and A12, so those criteria are split into sub-rows. The generic DataTable result is its own OUT row (`UI183-A14-GENERIC-GATE`) and is linked to change record `SCR-00`.

The controlling criterion text is `docs/roadmap/plans/mvp6-shipment-pod-ui-scope-01/ACCEPTANCE.md`. The MOD-0183 pack §11 states that the frontend is not part of the first slice, and pack §16 holds only backend criteria. The pack was used for boundaries only.

### Counts by current_state

| State | Count | Rows |
|---|---:|---|
| CLOSED_EXACT | 3 | A08, A09-409, A09-422 |
| ACCEPTED_BOUNDED | 11 | A01, A02, A03-ADAPTER, A04, A05, A06, A09-SUCCESS, A12-DATA-API, A15, PRES-183-01, PRES-183-02 |
| STATIC_ONLY | 1 | A12-SAFE404-DOM |
| PARTIAL | 4 | A03-MISSING-READ, A07, A11, A13 |
| UNAUTHORIZED | 1 | A10 (DN-01) |
| POLICY_OPEN | 3 | A14, A14-GENERIC-GATE, PRES-183-03 |
| OPEN | 2 | A16, PRES-183-04 (durable PNG) |
| **Total** | **25** | |

Readiness: **18 READY / 7 BLOCKED**. The BLOCKED rows are A10, A13, A14, A14-GENERIC-GATE, A16, PRES-183-03 and PRES-183-04. Every CLOSED_EXACT and ACCEPTED_BOUNDED row is READY only for its regression; it is not reopened.

### Reconciliation against the known facts

- Confirmed: A08, A09-409 and A09-422 are CLOSED_EXACT (`mvp6-shipment-a08-a09-a12-ct-disposition-01`, 25 Sep). They still need a targeted regression in Lane-1 because `EVIDENCE-REUSE.tsv` marks them for rerun after the change to `details.js` `load()`.
- Confirmed: A03 and PRES-183-02 are ACCEPTED_BOUNDED (shared-UI CT). **Refinement:** that CT accepted only the unauthenticated JSON-adapter 401 part of A03. The authenticated missing-read UAS-001 part stays PARTIAL in the functional consolidation, so it has its own row.
- Confirmed: PRES-183-01 was ACCEPTED_BOUNDED earlier (line-accessibility CT, SOP `7453b4fc…2f61`).
- Confirmed: A12 data/API isolation is PASS_RETAINED (shown as ACCEPTED_BOUNDED) and the safe-not-found DOM is STATIC_ONLY (`a12-safe404-independent-ver-01`: conditional static pass, runtime not run). The runtime VER belongs to Lane-1 (Q04).
- Confirmed: A10 is UNAUTHORIZED (DN-01), A13 is PARTIAL (durable PNG open) and A14 is POLICY_OPEN (DN-02, PC-02/03/04/28).
- **Difference:** A07 has an independent VER PASS (policy-error VER, 24 Sep) but no CT disposition. It stays PARTIAL (MD-01).
- **Difference:** A01, A02, A04, A05, A06, A07 and A15 were accepted on the 354-source manifest `e6551f…`. Later changes touched the controller, `Program.cs`, `dt-defaults.js`, the shared resx files and `details.js`. `EVIDENCE-REUSE.tsv` has no inherit/rerun row for these criteria (MD-02).

## Generic DataTable 49 PASS / 35 FAIL

Source: `docs/records/audits/2026-09/mvp6-shipment-ui-presentation-ver-01/raw/datatable-quality-gate.log`. The run used the 354-source manifest `e6551f…` and is recorded as finding PRES-183-03. A recount of the log gives 49 `[PASS]` and 35 `[FAIL]` lines. The result stays **historical FAIL**, with no waiver.

Each FAIL line was matched in order to `DATATABLE-35-DISPOSITION.tsv` (35/35 aligned), and the two prior disposition files agree on every row (0 mismatches). Each row was then re-checked against UI183-A01..A16 and the verifier source `.antigravity/scripts/verify_datatable_page.py`, which was read only.

| Class | Prior record | This reconciliation | Rows |
|---|---:|---:|---|
| Out-of-scope generic expectation | 23 | **23** | 01, 05, 06, 07, 09, 11, 12, 13, 14, 17, 18, 22, 23, 24, 26, 27, 29, 30, 31, 32, 33, 34, 35 |
| Evidence gap (stays IN) | 8 | **8** | 08, 10, 15, 16, 19, 20, 21, 25 |
| Policy conflict | 4 | **4** | 02 (PC-02), 03 (PC-03), 04 (PC-04), 28 (PC-28) |

The counts are confirmed. Two row-level reasons differ from the prior record:
- **SCR-15 `Cancel`.** The verifier groups `Cancel` with the confirm-dialog keys (lines 370-374), so the key means the dialog Cancel button, not only the Cancelled lifecycle action. The class is still evidence gap, but the required evidence must include the localized SweetAlert Cancel button.
- **SCR-07 `Unknown` and SCR-22 `ShowAll`.** Both are correctly OUT, but the DN-02 authorization text does not name them (MD-03).

`SCOPE-CHANGE-RECORD.tsv` holds `SCR-00` (the A14 "repository quality gate" clause, proposed OUT and replaced by the bounded profile) and one row for each of the 35 FAIL lines. Owner decision needed: yes for the 23 OUT rows, the 4 policy rows and SCR-00; no for the 8 evidence-gap rows. These are proposals only; there is no silent waiver.

## Method

1. Preflight: branch, HEAD and dirty-row count checked. The output folder did not exist before the run.
2. Read every listed input. Recomputed the SHA-256 values in `mvp6-shipment-remaining-acceptance-disposition-01/SOURCES.tsv` (9/9 match, no drift).
3. Took each row state from the latest CT disposition. Where no CT record exists, the latest independent VER was used and the row is marked PARTIAL, never accepted.
4. Recounted the raw 49/35 log and matched it line by line to both 35-row disposition files. Reviewed each classification against the criterion text and the verifier source.
5. Computed the evidence SHA-256 values with Python `hashlib` over the exact repository files.

## Inputs (SHA-256 at read time)

| Path | SHA-256 |
|---|---|
| `docs/guides/operations/mvp6-development-process-v1.0.md` | `ad0350508823c44b0ef62ca9721ed710db9e217bdb39e6af9122bc08efe1dd18` |
| `AGENTS.md` | `51d92c75b761e5c7195eda102d08f2b7f472b979d9940bdf21ae72cb7cab7fcb` |
| `docs/roadmap/plans/mvp6-claude-development-handoff-01/MODULE-STATUS.md` | `622a429206772ca0e12b26c5cd075be93d2573136ad996768ff4077cfe314d50` |
| `docs/roadmap/plans/mvp6-claude-development-handoff-01/AUTHORITY-AND-GATES.md` | `4ca689673866ae9183078a7fe58311b337ac6ae1b2859b8b21073c2da1b6025e` |
| `docs/roadmap/plans/mvp6-claude-development-handoff-01/LANE-HANDOFFS.md` | `a664a1496f0ff6a00a92df01017d670d1df997432911820db3434dbbc9613110` |
| `docs/records/audits/2026-09/mvp6-shipment-remaining-acceptance-disposition-01/DECISION-NEEDS.md` | `486393a5c94ff470e86376b1a29146bedca6350564232de1de7c7397c02f5936` |
| `docs/records/audits/2026-09/mvp6-shipment-remaining-acceptance-disposition-01/CURRENT-SUCCESSOR-STATUS.md` | `e5001e921836c2ef56c9a855d71d967dba5f4faf8477e5ed5b4c4aea118d1078` |
| `docs/records/audits/2026-09/mvp6-shipment-remaining-acceptance-disposition-01/EXECUTION-PLAN.md` | `4454c092005a219fcdea25b6779d57747859b459d7852a8b095de5a939a0bb9b` |
| `docs/records/audits/2026-09/mvp6-shipment-remaining-acceptance-disposition-01/SCOPE-AWARE-VERIFIER-PROPOSAL.md` | `3197e1c3777353e688d02f6c2de9af4d0c8a76bff1873d67b3f2f0d0d004e736` |
| `docs/records/audits/2026-09/mvp6-shipment-remaining-acceptance-disposition-01/ACCEPTANCE-GAP-MATRIX.tsv` | `f0cc3fc9ee988477c69ce4db1312caa2c89d90bda7b871f9fd244e447d0ac5a7` |
| `docs/records/audits/2026-09/mvp6-shipment-remaining-acceptance-disposition-01/SOP-22.md` | `03dc7ea54229a111dbf38239cb9cf78907403740401dad94af147d57ada62fa8` |
| `docs/records/audits/2026-09/mvp6-shipment-remaining-acceptance-disposition-01/DATATABLE-35-DISPOSITION.tsv` | `80bfcab54619bfe7757d8c5f27c03c4c818374946c3e706efe130d8d5ff5b961` |
| `docs/records/audits/2026-09/mvp6-shipment-remaining-acceptance-disposition-01/DATATABLE-35-ACCEPTANCE-MAP.tsv` | `c14a0a249dd06ee53695a01a51ba12a139c5346fac4055c7f3b2d64fac8bfef4` |
| `docs/records/audits/2026-09/mvp6-shipment-remaining-acceptance-disposition-01/evidence/a08-a09-a12-01/SOP-22.md` | `a098f82ad05ad984a0ccd58fc88fca00ffd7f271eb29cd4f72eb378b131d5012` |
| `docs/records/audits/2026-09/mvp6-shipment-remaining-acceptance-disposition-01/evidence/a08-a09-a12-01/ACCEPTANCE.tsv` | `76d44272f8168574f3e8ce90d070856ca36edb9f15b4963092a10ab73c4b764f` |
| `docs/records/audits/2026-09/mvp6-shipment-a08-a09-a12-ct-disposition-01/SOP-22.md` | `d8162a78071a07def743089017cdeccdf345dd2a97bf10f2e4613b94f4fa7ef6` |
| `docs/records/audits/2026-09/mvp6-shipment-a08-a09-a12-ct-disposition-01/EVIDENCE-DISPOSITION.tsv` | `5198766d444912494aaeb484847e856279f5d332e25004bc131ffd182492e334` |
| `docs/records/audits/2026-09/mvp6-shipment-shared-ui-ct-disposition-01/SOP-22.md` | `79b6b62f0e26e0e08d985760850c4fd28a1bed157ea76c7eefda93446e696333` |
| `docs/records/audits/2026-09/mvp6-shipment-shared-ui-ct-disposition-01/ACCEPTANCE.tsv` | `a4b4e6c0bbce028ab74727ca6192b2d8b851c853c6f1e27b37a2c3733d6d3525` |
| `docs/records/audits/2026-09/mvp6-shipment-shared-ui-ct-disposition-01/OPEN-GATES.tsv` | `a28cf3af5038ca25d52b18c59bcdd80236401e038850712a6fe6cacfd172a810` |
| `docs/records/audits/2026-09/mvp6-shipment-line-accessibility-ct-disposition-01/SOP-22.md` | `7453b4fcdc0a264b0d1233771e65b41c9935f0cb25f982fe5fccd98556c52f61` |
| `docs/records/audits/2026-09/mvp6-shipment-line-accessibility-ct-disposition-01/ACCEPTANCE.tsv` | `12a795044b0f2bac598f22ecde8b3a0ca508540b39d1e8b3665e264fbe774700` |
| `docs/records/audits/2026-09/mvp6-shipment-ui-functional-ct-consolidation-01/SOP-22.md` | `c3fb837e2f5db2e3d466a14c51b5de440d688f628ca71b16b39f84df8b6fb97e` |
| `docs/records/audits/2026-09/mvp6-shipment-ui-functional-ct-consolidation-01/ACCEPTANCE.tsv` | `e8fccf13083a945bfda95f72a019767eaadd054018eb0bafbc1445be8fc03664` |
| `docs/records/audits/2026-09/mvp6-shipment-root-r2-ct-disposition-01/SOP-22.md` | `2f8a4167d9fb46cabde5a46fb348054ad377062db57b2f7c80698359a6ed434c` |
| `docs/records/audits/2026-09/mvp6-shipment-root-r2-ct-disposition-01/BOUNDED-ACCEPTANCE.tsv` | `eac8d43e4ab2539dd5311a833052ffb9a6d3c9db208e3fd9cbb05aa1cf9cf642` |
| `docs/records/audits/2026-09/mvp6-shipment-a12-safe404-rework-01/SOP-22.md` | `d0ca0c8346d4c4141bda9521c34980d9dc4d43cb22b8f103a6fa7c462a4f4aa6` |
| `docs/records/audits/2026-09/mvp6-shipment-a12-safe404-independent-ver-01/SOP-22.md` | `dfd3254f3e8d07fe342b49653935ba076c94b27a16212b1c79b611e678683ab3` |
| `docs/records/audits/2026-09/mvp6-shipment-ui-policy-error-ver-01/SOP-22.md` | `e348a295acdb31fc9f3f1d7afb86c75f8cec6db203bc383922082a528babe1a9` |
| `docs/records/audits/2026-09/mvp6-shipment-ui-policy-error-ver-01/ACCEPTANCE.tsv` | `091aa5c4f4a76ce6db19cf9415c41d537fb84b09aae3e6963786f6628acefe1f` |
| `docs/records/audits/2026-09/mvp6-shipment-ui-presentation-ver-01/ACCESSIBILITY-FINDINGS.tsv` | `47eb9657e772286e9cefebd7ff406494305767de56579da9db0041c98056c7c3` |
| `docs/records/audits/2026-09/mvp6-shipment-ui-presentation-ver-01/raw/datatable-quality-gate.log` | `f0e3cb065d668b4f5625d840645733a44507ddc7af52e1678fb01bf544860538` |
| `docs/roadmap/plans/mvp6-effort-shipment-ct-update-07/REPORT.md` | `343e67b75e60cf8a2b8a94adcf24e6fc4ee46b8c89c07ada4247d3706ee59b5c` |
| `docs/roadmap/plans/mvp6-effort-shipment-ct-update-07/DELIVERY-STATUS.tsv` | `4dd969f7ddaa4b0abc63c2db1f9f103518549b7978144e6600d5e80eecb3eb6d` |
| `docs/roadmap/plans/mvp6-effort-shipment-ct-update-07/EVIDENCE-ALLOCATION.tsv` | `246ec0a24c0f1437fb455735445b4c3a79947ace6052f3bd029d770664f06837` |
| `docs/roadmap/plans/mvp6-process-pilot-01/CT-QUEUE.tsv` | `b9e0be191a8382ca23ad92f7aa21766799b8c891f0c1e2964bd7af980c4ea1fb` |
| `docs/roadmap/plans/mvp6-process-pilot-01/EVIDENCE-REUSE.tsv` | `77e8d6cd7f3e524b1db78f0b45879ee13d3f9f2a68bce189b6b9ec4be1af8f44` |
| `docs/roadmap/plans/mvp6-shipment-pod-ui-scope-01/ACCEPTANCE.md` | `83d0db58eff28b0ac56af7c0299fd775aa575615fc1d80d8ad6f57d0630eefa1` |
| `execution/domains/supply-chain-execution/module-packs/MOD-0183-shipment-tracking-pod.md` | `32381634163a2acff440475f4582c05defc8261957386dfa43d5c8fc7eaf962e` |
| `.antigravity/scripts/verify_datatable_page.py` | `00148e13a259623ffa2df2ba1290cf327af4b8065c9b5c14c09a79e06ba2748c` |

## Limits

- No build, test, browser or runtime process was run. Every state comes from existing records.
- The generic gate was not rerun on the current 360-source or A12 successor sources. The 49/35 result belongs to manifest `e6551f…` only.
- The A12 runtime authority exists as an owner decision quoted in `LANE-PROMPTS.md`. This package does not verify any repository decision record for it.
- Expected HTTP/browser/DB values restate the controlling acceptance text and the execution plans. They are not new requirements.
- The counts (DataTable 49/35 and this matrix) are observations, not performance measures.

## Effort

No new effort credit. This is a reconciliation record with no O/M/P allocation.

## No-change statement

This lane created only `docs/records/audits/2026-09/mvp6-shipment-acceptance-reconcile-01/`, which contains `SOP-22.md`, `ACCEPTANCE-MATRIX.tsv`, `SCOPE-CHANGE-RECORD.tsv`, `MISSING-DECISIONS.md` and `ARTIFACTS.sha256`. It made no change to product source, packs, boards, contracts, guards, `.antigravity`, the gateway, existing records or Git state, and it did not stage, stash, commit or push.
