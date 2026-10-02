# WP-0173-LOC2 evidence — MOD-0173-FU02 Location / Storage Master, slice LOC2 (screens), 2026-10-02

Measured base: `bc27dda23` (the WP named `eee49fe80`; the CT committed `bc27dda23` — FU01 pack note +
`InventoryService/appsettings.Development.json` PlatformRegistration, break-glass — while LOC2 was being written; none of
those files is touched here). Raw outputs. No token, password or signing secret is written here: test tokens were
minted in memory (persona-helper.js, post-stock.js) from the local dev signing settings; the real login's token stayed
in the browser's HttpOnly cookie.

| File | What it shows |
|---|---|
| `d1-build-web.log` | `Diten.Web` Release `--no-incremental`: 0 errors, 17 warnings — the same 17 pre-existing ones (UI1/LE1), 0 in LOC2 files |
| `d1-build-inventory.log` | `Diten.InventoryService.sln` Release `--no-incremental`: 0 errors, 0 warnings |
| `d2-web-tests.trx`, `d2-web-tests-summary.log` | `Diten.Web.Tests` 440/440, 0 skipped (base 391; +49: location proxy controllers, UAS render, resx parity / payload / nav key) — includes `NavManifestL10nGuardTests` |
| `d2-inventory-tests.trx`, `d2-inventory-tests-summary.log` | InventoryService 353/353, 0 skipped (same count: the 4 manifest completeness tests now cover the location pages) |
| `d2-inventory-tests-run1-prism-timeout.log` | a FIRST full run had 4 Prism contract-smoke fixture timeouts ("Prism did not listen within 90 s", machine under load); the class alone passed 4/4 in 4 s and the full suite re-run is clean — reported, not hidden |
| `d3-architecture-tests.log` | `tests/architecture` 18/18 |
| `d4-sabotage.log` | (a) read-only manage condition forced true → 1 red → restored → green; (b) UAS-001 gate on /Locations removed → 1 red → green; (c) BULKBINS removed from the manifest → 1 red → green; SHA-256 identical each time |
| `d5-datatable-manual-checklist.md` | the DataTable / form template checked by hand, `file:line` per rule — the Python verifier is BLOCKED-ENV and is not reported as PASS |
| `d5-greps.log` | layout 2/2 · data mode triple · "Permission denied/Forbidden" 0 · browser code naming 5066 / Gateway / `/api/locations` 0 · server-mode forbidden constructs 0 · raw `Swal.fire` 0 · delete / quantity 0 |
| `e1-js-guards-loc2.txt`, `e1-js-guards-base.txt`, `e1-js-guards-diff.txt` | 10 repo-wide JS guard test files run unchanged with `mini-vitest.js` on base and on LOC2: identical output (137 pass; the 21 failures are environmental on both — no jsdom, Windows `\` vs `/`) |
| `mini-vitest.js` | the minimal vitest-compatible runner used for E1 (no node_modules) |
| `run-loc2-env.ps1`, `persona-helper.js` | the runtime chain (Web :5091 → gateway copy :5090 → InventoryService :5067) and the localhost-only persona cookie helper |
| `d6-gateway-copy-override.json` | the gateway copy's ocelot.json = committed file (SHA-256), then exactly 4 lines `"Port": 5066` → 5067 |
| `d6-runtime-smoke.json` | every runtime check with its measured value (AC-10, 12, 14, 18, 19, 20, UI error sentences, list, history) |
| `d6-stock-posting.json` | the E4 OPENING_BALANCE into the bulk-created bin (request with the test JWT masked, answer 201) |
| `d6-mongo-*-after-preview.json`, `-after-bulk.json`, `-final.json` | locations / history / balances of `diten_inventory_loc2_smoke` at those moments |
| `d6-platform-catalog.json` | Platform catalog after two pushes: 1 module, 4 pages, 15 actions, no duplicates |
| `d6-ac01-proxy-sentence.json` | LEVEL_INVALID measured through the proxy (unreachable from the form) |
| `screenshots/01…27` | real login → company → empty company → tree built → errors per field / in dialogs → bulk limit / preview / result / skipped → stock presence → view in stock list (S-1 filtered) → list / filters / archive → history → read-only persona → UAS on both routes → Arabic RTL |
| `user-manual-draft.html` | end-user guide draft (its place is `docs/guides/inventory-locations/`, outside this WP's paths — FU-LOC2-02) |
| `e2-fleet-rebuild-1015-origin.json`, `d7-fleet-before.json`, `d7-fleet-after.json`, `d7-processes-after.json` | the user's fleet before / after (PIDs), the 10:15 rebuild's origin (another session), every process this WP started stopped |
| `d7-git.log` | git log / diff --stat of the WP range, allowed-path check, status |

Notes
1. **Worktree.** The user's fleet runs `dotnet watch` from `C:\Users\Ali\erp-vnext`; every intermediate edit there would
   rebuild / restart their Web and InventoryService (the OPS1 incident pattern). LOC2 was therefore written, built,
   tested and run in a detached scratch worktree (`C:\Users\Ali\erp-vnext-wt-loc2`, removed afterwards) and landed on
   the branch by ONE fast-forward. `C:\Users\Ali\erp-vnext-wt-inventory-ui` was not touched.
2. **Screenshot 16** was taken before the select2 field-error fix (ASSUMPTION-LOC2-09): its icon overlaps the parent
   code; the sentence is the evidence.
3. **Browser pane painting.** The pane does not paint while hidden, so CSS transitions (offcanvas slide-in) finish
   only when a screenshot forces a frame. Some steps carry an extra screenshot for that reason; a real browser is not
   affected.
