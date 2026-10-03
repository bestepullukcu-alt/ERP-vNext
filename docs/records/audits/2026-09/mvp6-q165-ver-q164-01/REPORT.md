# Q165 — delta VER of Q164 (Pre-Mac UI checklist r2) — SOP §37

| Field | Value |
|---|---|
| WP | Q165 (CT-QUEUE `mvp6-process-pilot-01/CT-QUEUE.tsv:214`, READY at preflight; re-dispatch after the CT-5 start-gate stop) · read-only-auditor (`/read-only-audit`) + frontend-ui-ux perspective |
| Where | LANE 4, Cowork Linux VM (`uname -s` = `Linux`). Not the writer's chat: Q144/Q164 were LANE 3 (`mvp6-q164-ui-checklist-02/CHECKLIST.md:3`). This chat wrote no Q144/Q164 file |
| Start gate | `docs/records/audits/2026-09/mvp6-q164-ui-checklist-02/SHA256SUMS` exists (writer hand-off) |
| Repo | `feature/mvp6-logistics` @ `4a8d4d4b339528a88e6220fb8402e5a2c771136c`; `GIT_OPTIONAL_LOCKS=0`; `git status --porcelain` only (no `git diff`); no `.git/index.lock` at start or end |
| Baseline | 29 ` M` (19 known + 10 UC-01) + 2 untracked UC-01 + record folders; 535 porcelain lines before this folder was written |
| Time | 2026-09-28 ≈11:23 → 11:31 +03:00 (Istanbul; VM clock UTC 08:23 → 08:31) |
| Scratch | `/tmp/q165` in the VM only: preimage copies (`p/dry`, `p/r2`, `p/q144`), `fe-delta.diff`, fresh extracts of the four draft archives (`x/`) |
| Authority | OD-F-Q145-1 `mvp6-ct-verdicts-q157-q142-q145-2026-09-28.md:41` (`39075081…`); OD-Q164-09 + CT-4 `mvp6-ct-verdicts-q161-q164-sop-v25-signoff-2026-09-28.md:45, :48` (`aa6af3ba5478825e9ddb7547b589251c56165db2c346730776f9d504179b9722`) |

## 0. Verification report

```text
VERIFICATION REPORT

WP ID:               Q165 (delta VER of Q164)
Verifier:            LANE 4 (Cowork, Linux VM) — read-only-auditor; independent of the Q144/Q164 writer (LANE 3)
Verification date:   2026-09-28
Branch/HEAD:         feature/mvp6-logistics @ 4a8d4d4b… (unchanged start → end)

Agent Verdict:       Q164 "checklist r2 — patches only; Claims v4 12/12 PASS; draft FAILs 13"
Verification Verdict: PASS (7/7)
CT Status:           pending (Agent PASS ≠ CT ACCEPTED)

Evidence level achieved: E1 (file/hash, patch dry-run and apply on /tmp copies, independent diff, static re-run on the draft archives)
Required evidence level: E1

Checks:
- 1 integrity (5/5):                        PASS (report count "6/6" is a count error; already in CT-4)
- 2 dry-run + apply = r2 hashes:            PASS
- 3 delta vs Q144 = 07/09/10 + 11 cell:     PASS
- 4 rule texts vs OD-F-Q145-1 / OD-Q164-09: PASS (wording note O-1)
- 5 Claims v4 12/12 + opener trace:         PASS (citation note O-2)
- 6 re-run 07/09/10 on the drafts:          PASS (= CHECK-RUN; FAIL set 13; D4 still caught)
- 7 no live change / K4:                    PASS

Failed criteria:     none
Rework required:     no
Next gate:           CT disposition → owner sign-off of the r2 patches → exact-hash apply; Q158/Q159/Q160 fix the 13 FAILs
```

## 1. Checks

| # | Check | Verdict | Evidence |
|---|---|---|---|
| 1 | `sha256sum -c SHA256SUMS`; SHA256SUMS = `a9024ac5…` | **PASS** | `mvp6-q164-ui-checklist-02/SHA256SUMS` = `a9024ac55daa4b45ca929eabf13474a82bade3ab96d7a3bda5ae78c34980ea0e`, **5/5 OK**: `frontend-ui-ux.patch` `4ce84c427996246fbf7b5cd867c7777d52327e5648f7ebcdc0f65bb62e0ffcd6`, `test-workflow.patch` `1c062665549879f4c14911295f3143f87c57ab56d42530bdcdbbdd843072122b`, `CHECKLIST.md` `0052695d818c857709d29e87ec566e73823d54e441443a2b2d8112e516dd2356`, `CHECK-RUN.tsv` `c86604794eee133847d91d7fc8d6bb20a0e56b551c5a3556afa819d2791d4f98`, `CHANGE-NOTES.md` `005e3441f1becdcaeb01de2567074738065d04fa35179d999e541226f40f56ef`. The writer's report said "6/6"; the folder holds 5 hashed files + SHA256SUMS (count error only; CT-4 already records it) |
| 2 | Both patches: `--dry-run` exit 0 on /tmp copies of the preimages; real apply = r2 hashes; no `.orig`/`.rej` | **PASS** | Preimages = live `.antigravity/agents/frontend-ui-ux.md` `a8ba2cb5…` and `.antigravity/workflows/test.md` `db207a9a…`. Dry-run exit 0 for both. Apply on a 2nd copy: `frontend-ui-ux.md` = `90247ddc689b1e06ab02ba33c9dd379ded45881f1c58fade0971c7ec3a78f044`, `test.md` = `0273cb3071b9a4fa10266edf0eb30d30d40313fc9fbc73d5a320b3064ae2b238`. 0 `.orig`/`.rej` |
| 3 | Delta vs Q144: only UI-PM-07/09/10 rows + UI-PM-11 defect cell; `test-workflow.patch` byte-identical | **PASS** | Q144 folder 4/4 OK; Q144 `frontend-ui-ux.patch` on a 3rd copy → `328e54a5b01c0ec8f988c86a74e7ae2944aa132645ce76643c7db2c7c6f0dac3`. `diff -u` Q144 postimage vs r2 postimage: **1 hunk** (`@@ -105,9 +105,9 @@`), 4 lines changed: UI-PM-07 (rule text), UI-PM-09 (rule text), UI-PM-10 (exemption + `grep -n "showConfirm"` + scope tag, insertions only), UI-PM-11 (Defect cell only). `test-workflow.patch` = `1c062665…` in both folders |
| 4 | Rule texts = OD-F-Q145-1; UI-PM-11 preventive; F-Q145-3 citations fixed | **PASS** | **07:** "opened from a **row menu** (dropdown item) or with **no focused opener** … Exempt: surfaces opened by a focused button (toolbar, page or row button)" = OD-F-Q145-1 (:41). **09:** "Every surface opened from a **stable opener** (a toolbar or page control that stays in the DOM …) stores that opener and returns focus to it on `hidden.bs.*` (fallback: the Add button). Exempt: surfaces opened from a row control" = OD-F-Q145-1 under the owner reading OD-Q164-09, now recorded by CT (`…q161-q164…md:48`). **10:** "Exempt: submits reached through `window.showConfirm`" = OD-F-Q145-1. **11:** "preventive (no defect instance; CU-24/CU-25 ar PASS)" in the patch, `CHECKLIST.md:30` and every CHECK-RUN UI-PM-11 row (F-Q145-2 closed). **F-Q145-3:** `CHECKLIST.md:25` UI-PM-06 → "`index.js:275-281` (comment :275, code :278-281) and `:1008`"; CHECK-RUN S&OP UI-PM-07 → generic listener `details.js:695-701` (list :695-696, `shown` :701) — both closed |
| 5 | Claims v4 (`2b34741a…`) 12/12 PASS with opener/exemption trace for 07/09/10 | **PASS** | Archive hash matches; fresh extract identical to the Q145 extract (`diff -rq`). CHECK-RUN: 12 PASS / 0 / 0. Trace (`js/SupplyChain/Claims/index.js`): **07** transition opened from row `dropdown-item js-transition` (:365 → click :1041-1042 → `openTransition` :604-633) → `shown.bs.offcanvas` → `focusTransitionField` (:1093, :167) = PASS; create (focused `.add-new` :1002) and quick view (row button `js-quick-view` :360, :1039-1040) exempt. **09** create from stable `.add-new`: opener stored :451-452; `hidden.bs.offcanvas` → `restoreCreateOpener` (:1085-1091 → :170-175, fallback `.add-new` :172) = PASS; quick view and transition exempt (row controls). **10** create submit disabled :550 → `finally` :570-574 re-enables and calls `restoreCreateFocus` (:573; fn :180-188) = PASS; transition submit :688 reached through `window.showConfirm` :667 = exempt. Items 01-06, 08, 11, 12 as verified in Q145 (unchanged archive) |
| 6 | Re-run 07/09/10 on S&OP / Capacity / Returns = CHECK-RUN; FAIL set 13; UI-PM-09 still catches Returns create (D4) | **PASS** | Archives `fcf52d82…`, `dd290891…`, `35dd1489…` match; extracts identical to Q145. All 9 re-run results equal CHECK-RUN (`RECHECK.tsv` group 6): **S&OP** 07 N/A (3 surfaces, all focused page buttons), 09 PASS, 10 FAIL (create `index.js:206`/`:230-232`, capture `details.js:485`/`:501-503`; sign-off via `showConfirm` `details.js:650` exempt). **Capacity** 07 N/A, 09 PASS, 10 FAIL (0 `showConfirm`; `index.js:212`/`:236-239`, `details.js:583`/`:626-629`). **Returns** 07 FAIL (row `dropdown-item` → `openTransition` :600, no focus, no `shown` listener :1019-1029), 09 FAIL (create from stable `.add-new` :939 → `openCreate` :439-449, no opener stored, `hidden` :1024-1028 returns no focus — **D4 still caught**), 10 FAIL (`:553`, `finally` :572-575; transition via `showConfirm` :670 exempt). Totals from CHECK-RUN: S&OP 6/3/3, Capacity 6/3/3, Returns 5/7/0, Claims v4 12/0/0 → **13 FAILs** |
| 7 | `.antigravity` checklist files unchanged; Q144/Q145 folders unchanged | **PASS** | `frontend-ui-ux.md` `a8ba2cb5a6893a4dd82fdecd136be78e15e6d73c5a05fd2f1e1a7a291503d1fa`, `test.md` `db207a9a510f8bf3bad61127a116fba533995289c90ef73961a8797b6a759177` (= preimages). Q144 `SHA256SUMS` `18edd238…` 4/4 OK. Q145 `SHA256SUMS` `032aba85…` 2/2 OK (`REPORT.md` `4cefb057…`) |

**Overall: PASS (7/7).** The r2 patches change exactly the four rows the CT decisions name, apply cleanly to the live preimages, keep Claims v4 at 12/12, and leave the draft FAIL set at 13.

## 2. Final FAIL list per draft (unchanged from Q144/Q145)

| Draft | Archive | PASS / FAIL / N/A | FAILs | Fix WP |
|---|---|---|---|---|
| MOD-0190 S&OP | `mvp6-sop-ui-draft-01/sop-ui-draft-overlay.tar.gz` `fcf52d827914dc9c1d217b191a416821a024ba1e3314148f488ebbb2c81e5e30` | 6 / 3 / 3 | UI-PM-01, UI-PM-05, UI-PM-10 | Q158 |
| MOD-0192 Capacity | `mvp6-capacity-ui-draft-01/capacity-ui-draft-overlay.tar.gz` `dd290891d67eb1ccb6943acd25f855902b86b00af6384d0ec43525e1ee162818` | 6 / 3 / 3 | UI-PM-01, UI-PM-05, UI-PM-10 | Q159 |
| MOD-0186 Returns | `mvp6-returns-ui-draft-01/returns-ui-draft-overlay.tar.gz` `35dd1489d293e881894ae63590f394543c33c886dfe4a969602c062523f1a014` | 5 / 7 / 0 | UI-PM-01, UI-PM-03, UI-PM-05, UI-PM-06, UI-PM-07, UI-PM-09, UI-PM-10 | Q160 |
| MOD-0187 Claims v4 (reference) | `mvp6-claims-ui-draft-04/claims-ui-draft-overlay-v4.tar.gz` `2b34741af91dc640862cd6e460809e860040e21747a66849aa5b61bfa099293f` | 12 / 0 / 0 | — | — |

## 3. Observations (no rework required)

| # | Level | Observation | Evidence |
|---|---|---|---|
| O-1 | INFO | UI-PM-09 wording: the rule says a surface "stores that opener"; the CT copy of OD-Q164-09 says a stable control "counts as having an opener even if the code stores none". S&OP and Capacity return focus to a named opener id rather than a stored element; Q164 scores that PASS, which fits OD-Q164-09. The result does not change, but a later wording pass could say "stores or names that opener" so rule text and OD read the same | patch UI-PM-09 row; `…q161-q164…md:48`; S&OP `details.js:695-704`, Capacity `details.js:911-922` |
| O-2 | INFO | Small line drift in CHECK-RUN Claims v4 UI-PM-10: it cites `finally` at `:573-577` and `restoreCreateFocus` at `:179-188`; the `finally` block is `:570-574` (call at :573) and the function is `:180-188` (comment from :177). Substance correct | Claims v4 `index.js:170-188`, `:570-575` |
| O-3 | INFO | On a 403, S&OP removes the opener after hiding the panel (`details.js:454`), so the `hidden` handler focuses a removed id and focus falls to BODY. Outside UI-PM-09 as written ("control that stays in the DOM"); noted for the Q158 fix author only | S&OP `details.js:449-455`, `:702-704` |

## 4. ASSUMPTIONs

- **A1:** For check 6, "results = CHECK-RUN" is judged on the result column and the substance of the evidence (opener, listener, exemption). Line numbers were re-checked; drift of 1-3 lines that points at the same statement is not a mismatch (O-2).
- **A2:** OD-Q164-09 was only in the Q164 chat when Q164 was written; CT copied it into the Q166 record (`…q161-q164…md:48`) before this run. Check 4 uses that record.
- **A3:** Items 01-06, 08, 11 and 12 were not re-run on the drafts: the archives are unchanged (hash + `diff -rq` against the Q145 extracts) and Q164 changed no text in those rows (check 3). Their counts are taken from CHECK-RUN and agree with Q145.

## 5. No-change verification

- Branch, HEAD and the 2 untracked UC-01 files are unchanged; the 29 known ` M` paths are unchanged (30 ` M` at end = 29 + the other lane's SOP apply).
- `git status --porcelain` vs the pre-write snapshot (08:26:39 UTC): this lane added only `?? docs/records/audits/2026-09/mvp6-q165-ver-q164-01/`. Three other lines appeared from another lane during the run, not from this chat: ` M docs/guides/operations/control-tower-sop.md` (mtime 08:26:59 UTC, now `c1afe981…`), `?? .antigravity/workflows/dispatch-wp.md` (08:27:06 UTC) and `?? docs/records/audits/2026-09/mvp6-q167-sop-v25-apply-01/` (08:28:08 UTC) — the Q167 SOP v2.5 apply. They do not touch the checklist files: `frontend-ui-ux.md` `a8ba2cb5…` and `test.md` `db207a9a…` were re-hashed after them and are unchanged (check 7 holds). Not read or used here.
- No `.git/index.lock`, no git write, no `git diff`, no rm in the repo.
- No edit to `.antigravity`, the drafts, the Q144/Q145/Q164 folders or any ledger. This lane wrote no ledger.

## 6. Files

`REPORT.md` · `RECHECK.tsv` · `SHA256SUMS`

Agent PASS ≠ CT ACCEPTED — returning to CT.
