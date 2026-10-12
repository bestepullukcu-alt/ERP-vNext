# MOD-0187 Claims — tenant UI DRAFT overlay v4 (Q64f) — SOP §22

> **DRAFT — not built, not tested, not run in this lane** (Linux VM, no dotnet). Build, tests and runtime are **Q129** on the Mac.
> Owner decision D6: archive only; no open `overlay/` folder in the repository. Agent PASS ≠ CT ACCEPTED.

| Field | Value |
|---|---|
| WP / prompt | WP-MVP6-187-UI-064f · Q64f v1 · DEV lane (Cowork LANE 2) · @frontend-ui-ux → @testing-agent |
| Pattern (§17.3) | table/bulk list + offcanvas (pack §32); unchanged |
| Authority | owner decision ~15:45: CU-25 / Q122-D5 = A (fix in v4) |
| Base | `feature/mvp6-logistics` @ `4a8d4d4b339528a88e6220fb8402e5a2c771136c`; 19 diff paths; no `index.lock` (start and end) |
| Pack | MOD-0187 `3d1a00e2f0a0e7e58d19f44b340de9ab0d6bf84e7e2997db451c65d2ddb74cae` §32 (read only) |
| Input | v3 `claims-ui-draft-overlay-v3.tar.gz` `66e72bf6af1ebb6fffbcedf5f8b7934cd9a5a0f95cb1abf558b92fa3b500ce0c` ✓; Q122 `REPORT.md` §6 Q122-D5 |
| Output | `claims-ui-draft-overlay-v4.tar.gz` `2b34741af91dc640862cd6e460809e860040e21747a66849aa5b61bfa099293f` (39 regular files, same list/order as v3; no link, no absolute or `..` path, no AppleDouble) |
| Window | 2026-09-27T12:57:20Z → end in the Q64f report (15:57 +03:00 start) |

## What changed (details and reasons: CHANGES.md; per-file hashes: FILE-PLAN.tsv)

**4 modified, 35 unchanged, 0 added or removed.**

| Change | What |
|---|---|
| F-01 `index.js` | New `restoreCreateFocus()`, called after Save is re-enabled in `submitCreate`'s `finally` |
| F-02 `ClaimIndexBehaviorTests.cs` | +1 fact `Create_surface_keeps_keyboard_focus_after_a_rejected_submit` |
| F-03 `claims-ui.spec.mjs` | +1 Playwright scenario (CU-25, Q122-D5) |
| F-04 runtime `README.md` | +1 table row |

- The fix follows Q122's proposal. It stays within frontend-js-standard and premium-modal-standard: no native dialog, no inline handler, no storage, no new text and no shared file.
- The transition surface is untouched.

## Static checks (this lane; no .NET)

| Check | Result |
|---|---|
| `node --check` (node v22.23.2) | index.js, index.l10n.js, claims-ui.spec.mjs: 3/3 OK |
| xmllint | 7/7 resx OK |
| resx parity | **7/7** (95 keys each) |
| resx vs v3 | byte-identical 7/7 |
| Error codes | **18 = 18 = 18**: UI `ERROR_KEY` / pack §32.8 / contract `claims-semantics-v3.0.0.md` §D187-05; identical sets, unchanged from v3 |
| Grep rules (views + JS) | native dialogs **0**, inline handlers **0**, browser storage **0**, token/ports **0**; new user-visible literals in index.js 0 |
| F-02 contract (Python replay of the C# assertions) | **v3 FAIL** (no `restoreCreateFocus`) · **v4 PASS**; the Q64e D1/D2/D4 contract strings are still present |
| jsdom 24.1.3 behaviour check of the extracted `restoreCreateFocus` | **6/6 PASS**: focus moves from BODY to Save; an `aria-invalid` field wins; with Save disabled it goes to Shipment ID; hiding or closed surface is left alone; focus already inside is kept |

The jsdom check does not prove Escape. Bootstrap's handler and the real focus loss happen only in a browser, so that is Q129's runtime row.

## Q129 — Mac plan (build, tests, sabotage, runtime)

1. **Compose.** Use `~/mvp6-env/q129/src`, cp only, the same layers as Q122 §2:
   - BASE a8a236de (14,566) → Q117 `83e6322c…` → [Q121 `93bf1c07…` if CT has accepted it] → Claims **v4** module files → `_shared-integration` inside the copy only.
   - Verify every archive hash before extracting.
2. **Build.**
   - `Diten.Web.Tests` (builds Web): 0 errors, 18 warnings as in Q122, 0 in Claims files.
   - SupplyChainService.sln: 0 errors.
3. **Web.Tests.** Expected **246/246** (Q122: 245 + 1). Claims 88/88; ClaimIndexBehaviorTests **16/16**.
4. **Sabotage proof.** In a copy, swap in the v3 `index.js` (`c8ef218e…`) with the v4 test file, then run the `ClaimIndexBehaviorTests` filter:
   - v3: **1 FAIL** (`Create_surface_keeps_keyboard_focus_after_a_rejected_submit`), 15 PASS;
   - v4: **16/16 PASS**.
   - Record both TRX files.
5. **Other suites.**
   - SupplyChain single-process: **416/417**, unchanged; the 1 is the by-design `ClaimReplayTests…DurableRecovery`. Use a lane Mongo.
   - Architecture: 17/18 without Q121, or 18/18 with it.
6. **Runtime.** Evidence kit, real Auth, lane ports.
   - Re-run **CU-25** in full: the new scenario "create 400 keeps focus; Escape closes without Tab; focus back on Add" in en and ar, with PNG `03b-create-400-escape`.
   - Record `document.activeElement` after the 400 (expected: inside `#offcanvasCreateEdit`, Save), then `closedByEscape: true` with no Tab.
7. **Regression** (runtime):
   - **D1** skeleton visible then table (CU-05);
   - **D2** transition surface focused on open, Escape closes;
   - **D4** focus back on Add after the create surface closes (also after the new Escape path);
   - CU-11 400 keeps the inputs.
8. **Hygiene.**
   - Stop all lane processes; leave no test databases or temp folders.
   - Repo unchanged (19 paths, no index.lock).

## ASSUMPTIONs

- **A1:** Linux lane per the dispatch. Nothing here claims build, test or runtime.
- **A2:** "First invalid field" means a field marked `aria-invalid="true"` or `.is-invalid`. v3 marks no field; errors are shown in `#formClaimAlert`. So today the fallback is Save (enabled for an eligible shipment), else Shipment ID. The selector stays in case field-level marking is added later.
- **A3:** Bootstrap is v5.3.3 (repo vendor `bootstrap.js`). During `hide()` the element keeps `show` plus `hiding` until the transition ends, so the helper skips a surface that is hiding: the 403/404 paths close the panel, and D4 then returns focus to Add.
- **A4:** The v4 archive uses owner/group 0/0 and a fixed mtime (v3 showed root/wheel). Content, list and order are identical to the v4 tree; only tar metadata differs.
- **A5:** `rm -rf` ran only on this lane's scratch folders under `/tmp/q64f/`. Nothing in the repo was removed.

## Files

`claims-ui-draft-overlay-v4.tar.gz`, `CHANGES.md`, `FILE-PLAN.tsv` (39 rows + header; v4_change/v4_reason/sha256), this README, `SHA256SUMS`.

Agent PASS ≠ CT ACCEPTED — returning to CT.
