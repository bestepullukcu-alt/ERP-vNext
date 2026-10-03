# Q172 — Independent static VER of the MOD-0190 S&OP UI draft overlay v2 (Q158) — SOP §37

```text
VERIFICATION REPORT

WP ID:                Q172 · Prompt Q172 v2 (re-dispatch; v1 stopped by the owner at 10:24, wrote nothing) · Template T3 v1
                      CT-QUEUE line 243: "Q172 · Independent static VER of S&OP overlay v2 · READY · LANE 3 (read-only-auditor; not LANE 2) · Q175"
Verifier:             LANE 3 (Cowork, Linux VM, repo via bridge) · read-only-auditor (/read-only-audit, worktree-read-only) + frontend-ui-ux
                      perspective. Not the Q158 writer chat (LANE 2).
Verification date:    2026-09-29, start 14:01:11 +03:00 · end 14:04:32 +03:00 (Istanbul)
Branch/HEAD:          feature/mvp6-logistics / 4a8d4d4b339528a88e6220fb8402e5a2c771136c (read from .git; no git diff)

Agent Verdict:        PASS — checks 1–8 PASS; checklist r2 on v2: 9 PASS, 3 N/A (valid), 0 FAIL
Verification Verdict: S&OP v2 fixes UI-PM-01/05/10 and Q165 O-3 as FILE-PLAN states; nothing else changed
CT Status:            not set by this lane (Agent PASS ≠ CT ACCEPTED)

Evidence level achieved:  static (hashes, plain diff -ru, source reading, node --check parse, emulated test assertions)
Required evidence level:  static (Mac build/runtime is Q84b)

Checks:
- scope:                  11 files changed = FILE-PLAN; 47 files in both archives
- build/tests/runtime:    not run (draft; Q84b)
- audit/evidence:         path:line per item in RECHECK.tsv
- console/security leakage: n/a (no new endpoint, key or permission)

Failed criteria:      none
Rework required:      no
Next gate:            CT disposition → Q84b Mac build/runtime of S&OP v2
```

Preflight: `uname -s` = Linux; this chat is not LANE 2; the Q172 row is present (line 243); no `.git/index.lock`; the output folder did not exist; `GIT_OPTIONAL_LOCKS=0`; only `git status --porcelain` (552 lines, 32 ` M`: the known baseline plus the record folders of other lanes); fresh scratch folder `/tmp/q172v2`.

## Inputs (hash-verified)

| Input | SHA-256 |
|---|---|
| v2 folder `mvp6-sop-ui-draft-02/` SHA256SUMS | `0bbb22a8efa2435f6eb1a3b51b36a6276b871a31029420afa7a09405f6ce6cc1` (4/4) |
| v2 archive `sop-ui-draft-overlay-v2.tar.gz` | `7a9666c7ee1dfab4f85c1324db38ab06ed87ed1c385de706629e60ed68e394e3` |
| v1 folder `mvp6-sop-ui-draft-01/` SHA256SUMS | `0aafc3402511f95fd8638a4325bb032dfbf2ca4bafb5bf0b3f3d05c03f568093` (55/55) |
| v1 archive | `fcf52d827914dc9c1d217b191a416821a024ba1e3314148f488ebbb2c81e5e30` |
| live checklist `.antigravity/agents/frontend-ui-ux.md` | `90247ddc689b1e06ab02ba33c9dd379ded45881f1c58fade0971c7ec3a78f044`; `mvp6-q164-ui-checklist-02/CHECKLIST.md` `0052695d…` |
| Claims v4 reference archive | `2b34741af91dc640862cd6e460809e860040e21747a66849aa5b61bfa099293f` |
| Q165 REPORT (O-3) | `732c576892de2aa355dc4edbd024abb473fc15a8a78aa6df2c45a9ef69dedbf3` |

## Checks

| # | Check | Verdict | Evidence |
|---|---|---|---|
| 1 | `sha256sum -c` 4/4; archive hash; v1 55/55 | **PASS** | as above |
| 2 | `diff -ru` v1 vs v2: changed files = FILE-PLAN (11), nothing else | **PASS** | 11 files changed; set equal to `FILE-PLAN.tsv` rows; 0 "Only in"; every v1/v2 hash in FILE-PLAN matches the extracted file |
| 3 | 12 checklist items on v2: 0 FAIL; N/A valid | **PASS** | `RECHECK.tsv`: 9 PASS, 3 N/A. UI-PM-03: no `ajax` option. UI-PM-06: no `#skeleton-loader`. UI-PM-07: every surface is opened by a focused page button; no row menu, no code-opened surface. |
| 4 | UI-PM-01, UI-PM-05, UI-PM-10; the sign-off still goes through `showConfirm` | **PASS** | **UI-PM-01:** 0 `Layout =` in the 5 partials; page views `Index.cshtml:10`, `Details.cshtml:11`. **UI-PM-05:** `details.js:187–194` sets `skeleton.style.display`. **UI-PM-10:** create `index.js:231–234` → `restoreCreateFocus` (`:241–247`); capture `details.js:535–540` → `restorePanelFocus` (`:465–471`). Both run while the panel is open and not hiding, and target the first invalid field or the first field, never the (still disabled) submit. The sign-off is reached through `window.showConfirm` (`details.js:687`) and is excluded by `kind === 'capture'` (`:539`). |
| 5 | O-3: on a 403, focus goes to the page title (tabindex −1), not BODY; the opener is removed only after the hidden handler | **PASS** | titles `Index.cshtml:27` `id="sandopPlansTitle" tabindex="-1"`, `Details.cshtml:25` `id="planTitle" tabindex="-1"`. Create 403: `createDenied = true` + hide (`index.js:264–267`); the hidden handler focuses the title, then removes the CTA (`:329–339`). Capture/sign-off 403: `deniedOpeners.add` + hide (`details.js:478–489`); the hidden handler runs `focusStableIfLost` (`:456–460`), then `deniedOpeners.delete → remove` (`:739–750`). If the panel is not shown, focus moves before the removal (`index.js:268–271`, `details.js:486–488`). The Q165 O-3 case (`details.js:454` in v1) is closed. |
| 6 | Tests match the v2 source; no test weakened except the Layout assertion | **PASS** | Contract test: only the Layout assertion changed, to page-views-only (`SandopPlanFormContractTests.cs` theory `Page_views_state_the_tenant_shell_partials_do_not…`); the inline-handler and `/Platform` assertions are kept. Behaviour tests: 3 facts added (UI-PM-05, UI-PM-10, O-3), none removed. 33/33 new or changed assertions were emulated in Python against the v2 sources, including the `Slice` anchors. All v1 literal assertions of the 3 test files keep the same outcome on v2. |
| 7 | `node --check` on the changed JS | **PASS** | `index.js` and `details.js` parse (v1 and v2) |
| 8 | No new route, permission, L10n key or endpoint vs v1 | **PASS** | `t('…')` keys, `Localizer[…]` keys, fetch count (3 → 3), API paths, view links and `permissions.*` names are unchanged. Controllers, resx and the manifest provider are not in the changed set. The only view changes are the partial Layout removal and the `id` + `tabindex="-1"` on the two page titles. |

## Findings

- **O-1 (INFO):** UI-PM-10 order. v2 restores focus *before* re-enabling the submit, and targets only fields (never the disabled button). The live checklist r2 text describes "re-enables it and then focuses … else the submit". v2 meets the rule's aim (focus inside the open panel, Escape works) and the prompt's "never on a disabled control". The Capacity v2 draft (Q159) uses the checklist order. Both orders are valid; CT may want one wording in a later checklist revision.
- No other findings.

## Deviations

- None in this run. v1 of this prompt was stopped by the owner at 10:24 and wrote nothing (re-dispatch noted in the prompt). This run used a fresh scratch folder and redid every check.

## Repository state

- `git status --porcelain` end vs start: two added untracked entries: this folder `docs/records/audits/2026-09/mvp6-q172-ver-sop-v2-01/` and `docs/records/audits/2026-09/mvp6-q177-ver-q176-01/`, created by another lane (Q177) during this run and not touched here; no other change
- `.git/index.lock`: absent at start and end; HEAD unchanged.
- No edit outside this folder; no rm; no git write; no `git diff`; nothing fixed; no "CT ACCEPTED".

Agent PASS ≠ CT ACCEPTED — returning to CT.
