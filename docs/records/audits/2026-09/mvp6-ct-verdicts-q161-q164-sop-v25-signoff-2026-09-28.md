# CT verdicts Q161 / Q162 / Q163 / Q164 + O-1 decision, SOP v2.5 r2 sign-off, OD-Q164-09, Q165 stop — 2026-09-28

Recorded by the Q166 LANE (Cowork LANE 1, Linux VM — placement gate `uname -s` = Linux; repo via bridge; single ledger writer;
@orchestrator + `/reconcile-records`, record written in the documentation-writer role) at 2026-09-28T11:23+03:00 on CT instruction
(CT writes no files). CT-1…CT-5, OD-O1, OD-SIGN-v2.5 and OD-Q164-09 are copied as given in the Q166 dispatch. Q166 preflight 11:22:06 +03:00.

## 1. Metadata (SOP §17.1)

| Field | Value |
|---|---|
| Work Package ID / Prompt | Q166 (as dispatched; no WP-ID suffix or prompt version stated) |
| Capability Block / Module | MVP6 process pilot — governance records; no module code |
| Agent Lane ID / Type | Cowork LANE 1 (Linux) / DEV (records + ledgers only) |
| Target Agent / Entry Point | `@orchestrator` + `/reconcile-records`; record by documentation-writer |
| Risk Class | not stated in the dispatch |
| Target Branch / Expected Base HEAD | `feature/mvp6-logistics` / `4a8d4d4b339528a88e6220fb8402e5a2c771136c` (matched) |
| Dirty-worktree baseline | `git status --porcelain` only (no `git diff`): 29 tracked ` M` (= Q161 set) + 2 untracked UC-01 files + untracked record folders; new since Q161: the three evidence folders below |
| Depends On | Q161, Q162, Q163, Q164 |
| Authority Sources | SOP `docs/guides/operations/control-tower-sop.md` v2.4 §17.1, §20, §25, §37 |
| Allowed Paths | this record; `docs/roadmap/plans/mvp6-process-pilot-01/CT-QUEUE.tsv`; `…/MILESTONE-EVENTS.tsv` |
| Protected Paths | everything else — UC-01 files, code, SOP and `.antigravity` untouched (the apply is Q167) |
| Ledgers before (matched) | CT-QUEUE `34442f7948949bec6ac5cf59a9d9da33782523d742a867fb8ea6efb2034c47f5` (217 rows) · MILESTONE-EVENTS `5de27e695748497da4e9da91e9cfcde4d40317e00fd9d9fcb419bec826c6858e` (212 lines) |

## 2. Evidence (verified with `sha256sum -c` before writing)

| Item (`docs/records/audits/2026-09/…`) | SHA-256 | Result |
|---|---|---|
| `mvp6-q162-sop-v25-r2-01/SHA256SUMS` | `00c03274acf6f8a7c2533b89d7e6d7be6a5218692c68c025e12df59f1d07bfa0` | 4/4 OK |
| `…/SOP-v2.5-r2.patch` | `3e8a0dba32dfe620500229b6494106443a19629032f4a558d579120239aea5f6` | = dispatch |
| `…/dispatch-wp.md` | `af0a080e85664dff29cb52206beccd8acd9971988f8b95b85f972b9a3cea3e50` | = dispatch |
| r2 postimage (`CHANGE-NOTES-r2.md:19, :68`) | `c1afe981c8f432087c5220035e49a0fc4ff9369df29f7c60a4c77badbcccf032` | = dispatch |
| `mvp6-q163-ver-q162-01/SHA256SUMS` · `REPORT.md` | `0ce07c709b5b1e8603632003fba53593d1962b9d54cad266835620a95621ab89` · `4f11fa30ffc66df5926b3f479dae30bd0b750a021bdf6bdf9f24eb999efe4597` | 1/1 OK; PASS 6/6 (`REPORT.md:24, :54`); O-1…O-3 at :60ff |
| `mvp6-q164-ui-checklist-02/SHA256SUMS` | `a9024ac55daa4b45ca929eabf13474a82bade3ab96d7a3bda5ae78c34980ea0e` | 5/5 OK |
| `…/frontend-ui-ux.patch` · `…/test-workflow.patch` | `4ce84c427996246fbf7b5cd867c7777d52327e5648f7ebcdc0f65bb62e0ffcd6` · `1c062665549879f4c14911295f3143f87c57ab56d42530bdcdbbdd843072122b` (= Q144 file) | listed |
| `…/CHECKLIST.md` (r2; postimage `90247ddc…` at :13; A2 at :48) | `0052695d818c857709d29e87ec566e73823d54e441443a2b2d8112e516dd2356` | listed |
| Q161 record `mvp6-ct-verdicts-q157-q142-q145-2026-09-28.md` | `39075081db78ba5c54a568ce9e8ebe37dcf3f5f0125cd445dc14b006c491f7a1` | = dispatch |
| Live `docs/guides/operations/control-tower-sop.md` at Q166 preflight | `e85854bd1ecb4895406d8e5a1276adbc82a2af250dc304abad796a2ae7a4de36` | = OD-SIGN "before" value |
| `.antigravity/workflows/dispatch-wp.md` at Q166 preflight | absent | not yet applied |

## 3. CT dispositions and owner decisions (verbatim)

- **CT-1** Q161: CT ACCEPTED.
- **CT-2** Q162: CT ACCEPTED (writer; r2 patch 3e8a0dba…, postimage c1afe981…).
- **CT-3** Q163: CT ACCEPTED (delta VER PASS 6/6). O-2 and O-3 are informational.
- **CT-4** Q164: writer complete (checklist r2; frontend-ui-ux.patch 4ce84c42…, postimage 90247ddc…; test-workflow.patch 1c062665… unchanged vs Q144). Claims v4 12/12 PASS; draft FAILs unchanged (13). Observation: the report says "6/6" but the folder holds 5 files, 5/5 OK (count error only). VER is Q165.
- **OD-O1** (owner, question tool, 28 Sep): O-1 accepted as is — the T2 NEREDE omission of "; Darwin evidence unchanged" is acceptable; the placement gate carries the Darwin requirement.
- **OD-SIGN-v2.5** (owner, question tool, 28 Sep): APPROVED — apply SOP-v2.5-r2.patch (3e8a0dba…) to docs/guides/operations/control-tower-sop.md only if before = e85854bd1ecb4895406d8e5a1276adbc82a2af250dc304abad796a2ae7a4de36 and after = c1afe981…; copy dispatch-wp.md (af0a080e…) to .antigravity/workflows/dispatch-wp.md. The Q141 r1 patch is NOT applied. One SOP writer (LANE 2); independent VER (LANE 4).
- **OD-Q164-09** (owner, question tool in the Q164 chat, 28 Sep): UI-PM-09 reading = "Stable opener" — a panel opened by a stable control (Add, Open…) counts as having an opener even if the code stores none (Q164 CHECKLIST.md A2).
- **CT-5** Q165: the first dispatch was NOT RUN — it stopped correctly at its start gate (the Q164 hand-off did not exist then). It stays READY; re-dispatched now.

## 4. Ledger rows appended (CT-QUEUE, append-only; since 2026-09-28; record = this file)

Q161 DONE (CT ACCEPTED) · Q162 DONE (CT ACCEPTED; writer) · Q163 DONE (CT ACCEPTED) · Q164 DONE (writer; VER Q165) ·
Q141 DONE (superseded by r2; not applied) · Q166 DONE · Q167 READY (depends Q166) · Q168 READY (depends Q167).
Q165 stays READY (no row appended). MILESTONE-EVENTS: one hand-off line. No hours.
