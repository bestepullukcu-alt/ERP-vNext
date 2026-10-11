# mvp6-effort-update-09a (Q96) — README

Effort analyst package. Status: **agent output, not CT-accepted.** Package only: no ledger, record, pack or source edit, and no git write.

- Base: `feature/mvp6-logistics` @ `4a8d4d4b339528a88e6220fb8402e5a2c771136c`, `GIT_OPTIONAL_LOCKS=0`, no `.git/index.lock` at start or end.
- Lane: **Linux VM chat lane** (device bridge; `uname -s` = Linux, not Darwin). The dispatch said to stop if not Darwin; the owner answered
  "Allow Linux VM lane (Recommended)" through the question tool, with the same rules and output folder (ASSUMPTION A1).
- Start 2026-09-26T17:25Z (20:25 +03:00, git-status snapshot) · end: see `SHA256SUMS` time in the Q96 report.
- Scratch only in VM `/tmp/q96` (calculation scripts `calc.py`, `credit.py` and `tables.py`; they are not part of the package).

## Files

| File | Content |
|---|---|
| `EFFORT-UPDATE-09a.md` | Headline before → after, split table with arithmetic check, per-module and per-category tables (frozen and forecast), findings |
| `CREDIT-LINES.tsv` | 10 lines: 3 credited (a) parts, 1 (a) not credited (0186), 4 (b) parts remaining, text patches and drafts at 0 h; evidence path + sha256 per line (multiple paths `; `-separated in the same order as their hashes) |
| `BOARD-FIGURES.json` | Progress Board fields (`summary`, `modules[code]` with accepted, delivered, remaining, total and `categories{pack, contract, backend, frontend, integration, test}`), **frozen 2,842 h baseline** as in 09 (A4 of 09) |
| `README.md` | This file |
| `SHA256SUMS` | sha256 of the four files above |

## Inputs (read-only; sha256 at start)

| Input | sha256 |
|---|---|
| `docs/roadmap/plans/mvp6-effort-update-09/EFFORT-UPDATE-09.md` | `807f50b27b516bc9914041eb3d41a1077d6ed8f0ae7d534659659465eda2e840` |
| `docs/roadmap/plans/mvp6-effort-update-09/BOARD-FIGURES.json` | `941b42033ad8ea59ed6ae572941e80f142ff153962602f5f890ca743e50bc4b9` |
| `docs/roadmap/plans/mvp6-effort-update-09/CREDIT-LINES.tsv` | `cfc51167372733b121438b3559819be7fcd79571c1234f3bf634c6e090cf3fc4` |
| `docs/roadmap/plans/mvp6-effort-update-09/FORECAST-09.tsv` | `d8b217d3c34d88e14810754fde3321477de63d97d009e13f4d5cbc0fc77fa553` |
| `docs/roadmap/plans/mvp6-effort-update-09/MODULE-FIGURES.tsv` | `ec19f5f9676af23107302ae3c283ab9911bb298e56a2fec41bd6b35dc137a4de` |
| `docs/roadmap/plans/mvp6-effort-update-09/RECALCULATE.py` | `7e4748bf6dd040bddb5b3f9d43bf9ce384a69de5fc4749217befc111f786c629` |
| `docs/roadmap/plans/mvp6-effort-update-09/SHA256SUMS` (`sha256sum -c` 12/12 OK) | `5718307d9df693c332196308c24625d91d047ab3f47736f4cf92833da99eed1e` |
| `docs/roadmap/plans/mvp6-effort-update-08/EFFORT.tsv` (frozen ledger, 106 rows) | `6254365bd9cda042c1d283364965b026991d7449daf21100e6101782e5103400` |
| `docs/roadmap/plans/mvp6-effort-update-08/FORECAST.tsv` | `114be5a3035537ffa0adafafa3b8d54575ec2fbe0bd239230cfb8ae79965dd17` |
| `docs/roadmap/plans/mvp6-effort-update-08/ASSUMPTIONS.md` | `01722e8ac3757ca74abcd74607b9392111b3580a2d3bfe0117c43acffc4abf8e` |
| `docs/roadmap/plans/mvp6-effort-update-08/ROW-CHANGES.tsv` | `4dff219ab986c47e3d61d959b90e9d965283e4075943992c5535f75830d15c85` |
| `docs/roadmap/plans/mvp6-effort-update-08/EVIDENCE-ALLOCATION.tsv` | `e67944738b1a54b7294e63236cdf8284b59c7cfa63f660d26ddb2cdc23d82b37` |
| `docs/roadmap/plans/mvp6-effort-update-08/SHA256SUMS` (`sha256sum -c` 20/20 OK) | `3928b36d07206d7db2f2d6f8296ce3b9494d5dfb7252b8de57a417396400e5bb` |
| `docs/roadmap/plans/mvp6-ui-pack-drafts-01/returns/EFFORT.md` (0186-1 source) | `2a33ab5285c0fe11d982698298c2375bf1f6fc016cdccb9b1a20c046f3b320bb` |
| `docs/roadmap/plans/mvp6-ui-pack-drafts-01/claims/EFFORT.md` (0187-1 source) | `3fc7c2e160c15c2d16c1671b9084c38f12b16ea589005605fb2e7a85237e86e3` |
| `docs/roadmap/plans/mvp6-ui-scope-190-192-01/ESTIMATE.tsv` (0190-1 / 0192-1 source) | `03669f33abb67a6be2ebdd1ae4e17fa26a7566d9190dc5d551f4969076a109e3` |
| `docs/records/audits/2026-09/mvp6-ct-verdicts-q81-q82-2026-09-26.md` | `f7577a539050b066a0b8fac43c05d92d24d99a4f3663153d34304e339c8ac6a5` |
| `docs/records/audits/2026-09/mvp6-q82-ver-ui-pack-190-192/SOP-22-VER-PASS2.md` | `35f3543896511eef1ba462c4c0b6fddb76bd0ff781db64563faf50f3b48af646` |
| `docs/records/decisions/2026-09/mvp6-sop-capacity-ui-pack-signoff-owner-decision-01.md` | `37f3ff0d08f52391e2f8e605917874c41146b1eba91e928f4b65b2fb18f80bea` |
| `docs/records/decisions/2026-09/mvp6-claims-ui-ph15-owner-decision-01.md` | `95a5c4f40d1a912c29b95d82c5c83fc817ed9fef578d885ae064e480de30f74e` |
| `docs/records/decisions/2026-09/mvp6-claims-pack-signoff-owner-decision-01.md` | `365de5be6fc670a8522dd173f4fbc4d15d72324ece4856519ac9e0424928827b` |
| `docs/records/audits/2026-09/mvp6-claims-pack-apply-01/SOP-22.md` | `dd8936d9eea1822054dad544d0b95caf833968bacd327e835047d176e102c2fd` |
| `docs/records/audits/2026-09/mvp6-pack-apply-independent-ver-01/SOP-22-VER.md` | `26af6011d17168a4747b949f596d5e2f6913768a4f2feb0e06e6748b61f55c13` |
| `docs/records/audits/2026-09/mvp6-ct-disposition-q46-q47-q49-2026-09-26.md` | `798d3c78655c989162365a8349e649fc3e5649ed9b709939b6b5065ea7e96b47` |
| `docs/records/audits/2026-09/mvp6-q87-ver-text-patch-q83/REPORT.md` | `335c7ab390f4c33d0c0d251a6cdba93285f66955fa8c3f616e9f6278defbe915` |
| `docs/records/decisions/2026-09/mvp6-text-patch-q83-signoff-owner-decision-01.md` | `d27458e19cda13c7453bd3c5378c22286078b3551f081ae5d91f0db1aff381da` |
| `docs/records/audits/2026-09/mvp6-ct-verdicts-q83-q84-2026-09-26.md` | `06e054adf27cdc379b72228fa450b0284a3a9bd9ef8e128b2f2b94c4be913cd8` |
| `docs/records/decisions/2026-09/mvp6-returns-pack-signoff-owner-decision-01.md` | `649269c3bc9bed88fa3c5eb019c18849367f47cdc6bbdc771c7cfbbf45a54611` |
| `docs/records/audits/2026-09/mvp6-returns-ui-draft-01/README.md` (Q91 draft, 0 h) | `7b8112a9f5441c67e63f63d6d83497ea5f8559a7db5887cd52acf84f3c35960d` |
| `docs/records/decisions/2026-09/mvp6-returns-ui-ph15-owner-decision-01.md` | **absent at start** (checked 17:25Z) → 0186 not creditable |

Reproduction check: recomputing the 09 frozen figures from 08 `EFFORT.tsv` gives the 09 `BOARD-FIGURES.json` exactly (asserted), before the split is applied.

## ASSUMPTIONs

- **A1 (lane):** Linux VM chat lane instead of Darwin, per the owner answer "Allow Linux VM lane (Recommended)". This is an analysis-only package, so no runtime claim is affected.
- **A2 (split ratio 2:1):** no original source has sub-items for the bundle. The returns and claims `EFFORT.md` say "UI pack revision, Phase 1.5, dispatch closure" 4/8/16, and Q78 `ESTIMATE.tsv` says "UI pack revision (…), Phase 1.5 closure, dispatch" 4/8/16.
  The 08 Claims/Returns treatment (08 A5, ROW-CHANGES lines 5–6, FC-0186-1/FC-0187-1) names the same three parts but gives no numeric ratio.
  The only structure in the precedent is therefore the three named items. Each gets an equal share: (a) = pack revision + Phase 1.5 = 2/3; (b) = dispatch closure = 1/3. The same ratio applies to O, M and P.
- **A3 (rounding):** (a) = round(row × 2/3, 1 decimal), and (b) = row − (a). Each pair is exact to 0.1 h.
- **A4 (views):** the split applies to the frozen 08 ledger rows, which are the ledger of record (08 A1: credit closes existing ledger rows).
  The forecast deltas (FC-0186-1, FC-0187-1, FC9-0190-1, FC9-0192-1) are re-estimates of open work with no evidence of their own, so they stay whole in (b). The credit is therefore identical in the frozen and forecast views.
  `BOARD-FIGURES.json` carries the frozen baseline, as 09 did (09 A4).
- **A5 (accepted):** credited (a) counts as delivered and accepted (C-1 = A). Accepted per module = 09 Board split + credited (a) M. The portfolio figure is 1,140 + 14.6.
- **A6 (C-1 supersedes "no product credit"):** the CT disposition q46-q47-q49 line "Effort: no product credit (pack text)" and 08 A5 are read as superseded for the pack category by owner decision C-1 = A. That decision is taken from the Q96 dispatch; no decision record file exists (F-09a-2).
- **A7 (0187 evidence):** the 0187 (a) credit rests on:
  - the §32 UI pack revision: owner sign-off Q37, apply Q38, independent VER Q49 PASS, and the CT disposition ACCEPTED (which covers Q38 retroactively);
  - PH15-UI-187, which is owner-approved.

  The Q86/Q87 §32.14 wording patch is supporting evidence only. The dispatch statement "Q86 accepted via Q87 PASS" is taken as given; CT-QUEUE still shows Q87 READY.
- **A8 (0186):** the PH15-UI-186 decision record was absent at start (and was rechecked at the end). Per the dispatch, 0186-1 (a) is not credited even though the Returns pack revision itself was VER-passed and CT-accepted (Q46/Q49).
- **A9 (drafts):** the Q64a, Q84, Q88 and Q91 draft overlays stay at 0 h. Q91 is listed for completeness with the 09 treatment.

## To do

- CT: confirm or replace the 2:1 ratio (F-09a-1) and accept or reject the three credited lines.
- Ledger writer (Q93): record owner decision C-1 = A. After CT, carry the split into the ledger and the Board.
- When PH15-UI-186 is recorded: credit 0186-1 (a), +2.4/4.0/6.4 (0186 delivered 176, pack 92.3 %).
- Next estimate: give pack/Phase 1.5 and dispatch closure separate O/M/P (09 F-2).

Agent PASS ≠ CT ACCEPTED — returning to CT.
