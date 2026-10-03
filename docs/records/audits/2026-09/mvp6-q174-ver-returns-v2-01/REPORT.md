# Q174 — Independent static VER of Returns UI overlay v2 (Q160) — SOP §37

```text
VERIFICATION REPORT
WP:                    Q174 · Prompt Q174 v1 · Template T3 v1 (SOP v2.5 §36.2)
CT-QUEUE row:          line 245 — Q174 · Independent static VER of Returns overlay v2 · READY · LANE 2 (read-only-auditor; not LANE 4) · Q175
Verifies:              Q160 (writer LANE 4; CT-QUEUE line 242 "DONE (writer; VER Q174)")
Lane / independence:   Cowork LANE 2 (Linux VM; `uname -s` = Linux). This chat did not write Q160 (LANE 4).
Agent / entry:         read-only-auditor (/read-only-audit, worktree-read-only) + frontend-ui-ux perspective
Module:                MOD-0186 Returns · Risk low · Base Stack n/a (static)
Branch / HEAD:         feature/mvp6-logistics @ 4a8d4d4b339528a88e6220fb8402e5a2c771136c (matched)
Git:                   GIT_OPTIONAL_LOCKS=0; only `git status --porcelain`; no git diff; no .git/index.lock (start and end)
Baseline:              32 ` M` + 517 `??` at preflight (known: 32 M + 2 UC-01 + dispatch-wp.md + record folders)
Timeline:              start 2026-09-29T07:26:02Z; device link lost ~07:30Z; resumed 10:50:41Z; inputs re-verified; v1/v2 re-extracted fresh
Evidence level:        E1 (static inspection) + textual emulation of the C# test assertions; no build, no test run

Agent Verdict:         PASS (7/7 checks PASS)
Verification Verdict:  PASS — overlay v2 fixes the 7 checklist FAILs, 12/12 PASS on v2, no scope creep
Failed criteria:       none
Rework required:       no (observations O-1…O-2 are INFO, outside the checked criteria)
```

## Inputs (all verified with `sha256sum -c`, again after the resume)

| Input | Value | Result |
|---|---|---|
| `docs/records/audits/2026-09/mvp6-returns-ui-draft-02/` SHA256SUMS | `569f1cc995c3f6e21aaba70cbe0ed381263b82373377872fb335d1d1a1c8fd9f` | 4/4 OK |
| `returns-ui-draft-overlay-v2.tar.gz` | `8862b46e09a9da5283876cf5f0e9d34f1eb9ea11745c4436f2755fcab682ecfd` | = prompt; 36 files; 0 AppleDouble; owner 0/0 |
| `docs/records/audits/2026-09/mvp6-returns-ui-draft-01/` SHA256SUMS | `a80047b50696c3a88a4512b1405ee0c0d9e3458ef09870d4f15c3862ef755905` | 44/44 OK |
| v1 archive `returns-ui-draft-overlay.tar.gz` | `35dd1489d293e881894ae63590f394543c33c886dfe4a969602c062523f1a014` | = prompt |
| PH15-UI-186 record `docs/records/decisions/2026-09/mvp6-returns-ui-ph15-owner-decision-01.md` | `e142b74e00d8a8b0ea7758923b0a0a828a21223aa89df05f845217faafc61929` | = prompt; table `docs/roadmap/plans/mvp6-decision-prep-01/PH15-UI-186.md` `08bc9d86…` (= record line 11) |
| live checklist `.antigravity/agents/frontend-ui-ux.md` | `90247ddc689b1e06ab02ba33c9dd379ded45881f1c58fade0971c7ec3a78f044` | = prompt; UI-PM-01…12 at lines 102–113 |
| Claims v4 archive | `2b34741af91dc640862cd6e460809e860040e21747a66849aa5b61bfa099293f` | = prompt (pattern reference) |

## Checks

| # | Check | Verdict | Evidence |
|---|---|---|---|
| 1 | `sha256sum -c` 4/4; archive hash; v1 still 44/44 | **PASS** | table above |
| 2 | plain `diff -ru` v1 vs v2 → exactly the 8 FILE-PLAN files | **PASS** | 8 `diff` headers, 16 hunks, 0 `Only in`; the 8 = FILE-PLAN rows 1–8 (5 partials, `index.js`, `ReturnFormContractTests.cs`, `ReturnIndexBehaviorTests.cs`); FILE-PLAN v1/v2 sha256 columns = the extracted files 8/8; the other 28 files byte-identical |
| 3 | 12 checklist items on v2 | **PASS** — 12 PASS, 0 FAIL, 0 N/A | `RECHECK.tsv` (independent re-run against the live checklist text, frontend-ui-ux.md:102–113); v1→v2: 01, 03, 05, 06, 07, 09, 10 FAIL→PASS; 02, 04, 08, 11, 12 PASS unchanged |
| 4 | each fix at the lines CHANGES.md cites; transition submit only via `showConfirm`; UI-PM-09 opener = Add + fallback | **PASS** | all cited lines checked in the v2 archive: partials line 6; `index.js` :105, :172, :176–181, :187–195 (CHANGES says 183–195 incl. the comment), :276–287, :283–286, :487–488, :615–616, :671–672, :963–965, :991, :1073–1076, :1078; `submitTransition(` has one call site, inside `window.showConfirm?.(…)` at :713; `restoreCreateOpener` falls back to `.add-new` (:178) |
| 5 | error-code map unchanged, 21 codes = PH15-UI-186; no new L10n key / route / permission / endpoint | **PASS** | `const ERROR_KEY` block byte-identical v1 = v2; 21 codes; = the 22 codes in PH15-UI-186.md minus `SHIPMENT_ROOT_INVALID` (amendment, record lines 14–17); the diff's added lines contain no `t('…')`, `Localizer[`, `fetch(`, endpoint, route or permission token; resx, controller, view models, `Index.cshtml`, `index.l10n.js` unchanged |
| 6 | tests: 5 new static tests + changed shell test match the source; fail on v1, pass on v2; no other test weakened | **PASS** | textual emulation of every assertion (Slice/Contains/DoesNotContain/Matches as in the C# helpers): 5 new facts FAIL on v1, PASS on v2; changed `Every_returns_view_states_the_tenant_shell_and_has_no_inline_handler` (`ReturnFormContractTests.cs:179–190`) FAIL on v1, PASS on v2; the only removed test line is the v1 assertion that encoded the D-02 defect (every view states Layout) — replaced by the partial/page split; no other test line removed |
| 7 | `node --check` on `index.js` | **PASS** | v2 `index.js`: syntax OK (node v22.23.2; parse only) |

## Observations (INFO; not criteria of this VER)

- **O-1 (INFO)** After a create 403, `closeCreateAsForbidden` disables every `.add-new` (`index.js:494`), so the hidden handler's
  `restoreCreateOpener` fallback targets a disabled button and focus stays outside the page flow. UI-PM-09 covers openers that stay
  usable, so this is outside the item as written; the writer records the same point (CHANGES.md A3). Same shape as the S&OP O-3 note.
- **O-2 (INFO)** Pack MOD-0186 line 613 (`fa7bd61e…`) still says every Returns `.cshtml` states `Layout = "_LayoutTenantShell";`.
  v2 follows UI-PM-01 and the prompt; the pack line needs its own text patch (writer CHANGES.md A2).

UI-PM-10 order: v2 re-enables Save, then calls `restoreCreateFocus()` (:615–616). This matches the live checklist text
(frontend-ui-ux.md:111, "re-enables it **and then** focuses") and Claims v4; the writer's A1 is consistent with the rule.

## Deviations

- **D-1** The device link dropped at ~07:30Z during check 3 and came back at 10:50Z. Nothing had been written to the repo. On resume,
  all input hashes were re-verified and v1/v2 were extracted again into a new scratch folder; the new `diff -ru` output is
  byte-identical to the one taken at 07:27Z.
- **D-2** The first scratch folder (`/tmp/q174-l2-*`, VM only) contained two items this lane did not create (`st-resume.txt`,
  `x/`, both written 10:32Z while the link was down). They were not used; all results come from the fresh extraction. Nothing in the
  repo was affected.
- `git status --porcelain` vs preflight: see the §37 hand-off; this lane adds only `docs/records/audits/2026-09/mvp6-q174-ver-returns-v2-01/`.

Agent PASS ≠ CT ACCEPTED — return to CT.
