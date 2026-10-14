# WP-0173-UI2 evidence — MOD-0173-FU01 Inventory Ledger UI, slice UI-2, 2026-10-02

Base: `c634557c5`. Raw outputs. No token, password or signing secret is written here: test tokens were minted in memory
(persona-helper.js, post-automatic.js) from the local dev signing settings; the real login's token stayed in the
browser's HttpOnly cookie.

| File | What it shows |
|---|---|
| `d1-build-web.log`, `d1-build-inventory.log` | Release `--no-incremental`: Web 0 errors / 17 warnings (the same pre-existing 17, 0 in UI-2 files); InventoryService 0 / 0 |
| `d2-web-tests.trx`, `d2-web-tests-summary.log` | `Diten.Web.Tests` 496/496, 0 skipped (LOC2: 440; +56: movement rules, form post / correction / lookups, page render, resx payload) |
| `d2-inventory-tests.trx`, `d2-inventory-tests-summary.log` | InventoryService 353/353, 0 skipped (same count: the 4 completeness tests now cover the UI-2 pages / actions) |
| `d3-architecture-tests.log`, `d3-architecture-tests-run1-docs-path-guard.log` | 18/18; the FIRST run was 17/18 — `DocsPathGuardTests` caught `run-ui2-env.ps1` naming a path into `docs/analysis`; the script now takes `-ProductMasterSpec` (reported, not hidden) |
| `d4-sabotage.sh`, `d4-sabotage.log` | (a) Correct-row condition forced true → 1 red → green; (b) inbound target location no longer required → 2 red → green; (c) a new key per post instead of the form's → 1 red → green; SHA-256 identical each time, `git status` clean after |
| `d5-compact-manual-checklist.md`, `d5-greps.log` | the Compact / form / details template checked by hand, rule by rule (Python verifier BLOCKED-ENV — not reported as PASS); greps: layout 4/4, no Gateway / port / `/api` literal in browser code, no Model B call, no raw `Swal.fire`, no balance write, no status transfer, no float arithmetic, no inline style |
| `e1-js-guards-base.txt`, `e1-js-guards-ui2.txt`, `e1-js-guards-diff.txt`, `mini-vitest.js` | the 10 repo-wide JS guard files run unchanged with the minimal runner on base and on UI-2: identical (137 pass, 21 environmental failures on both — no jsdom, Windows paths) |
| `run-ui2-env.ps1`, `ui2-fixtures.js`, `persona-helper.js`, `post-automatic.js` | the probe chain (Web :5091 → gateway copy :5090 → InventoryService :5067, Prism :4011, Model A mock :4012), the fixtures, the persona cookie helper, the one automatic movement |
| `d6-gateway-copy-override.json`, `d6-gateway-copy-changes.json` | the gateway copy = committed `ocelot.json` (SHA-256) + exactly the 11 listed route changes |
| `d6-prism-spec-lot.openapi.yaml`, `d6-prism-spec-serial.openapi.yaml` | the probe copies of the frozen PRODUCT-MASTER spec (examples only changed) |
| `d6-runtime-smoke.json` | every runtime check with its measured value (U2-1 … U2-11, upstream down, fixes made during the smoke) |
| `d6-automatic-movement.json` | the GOODS_RECEIPT_PO (MOD-0142) posted for the "no Correct on automatic rows" check (test JWT masked) |
| `d6-network-paths.log` | every path the gateway copy received from the Web, by prefix; `/api/mdm/products` = 0; the Model A mock's own request log |
| `d6-mongo-{transactions,audit,outbox,balances}-final.json` | `diten_inventory_ui2_smoke` at the end: 9 / 9 / 9 / 6 |
| `d6-platform-catalog.json` | Platform catalog after 5 pushes: 8 pages, 19 actions, 0 duplicates |
| `screenshots/01…25` | form + stock effect → receipts → S-1 row → insufficient stock → transfers → S-1 "Movement" pre-fill → UoM → lot → S-2 Correct rule → Correct confirm → partial correction → completed correction → forced archived location → serial → stock entry → 7 types → product master down → page alert + retry → ar RTL (before / after the bidi fix) → viewer S-1 / S-2 → UAS-001 |
| `d7-fleet-before.json`, `d7-processes-after.json`, `d7-fleet-after.json`, `d7-git.log` | the user's fleet before / after the fast-forward, every probe process stopped, git range + allowed-path check |

Notes
1. **Worktree.** Written, built, tested and run in a detached scratch worktree (`C:\Users\Ali\erp-vnext-wt-ui2`,
   removed afterwards) and landed on the branch by ONE fast-forward (WP instruction). `C:\Users\Ali\erp-vnext-wt-inventory-ui`
   was not touched. E1's base side ran in a second temporary worktree at `c634557c5`, removed right after.
2. **Probe stand-ins** — see `d6-runtime-smoke.json` "why_probes": no Gateway `/api/product-master` route (FU-PM-01) and
   no usable Model A data for the seed user. The shipped code calls the real routes; only the probe gateway copy
   points them at the stand-ins.
3. **Screenshots** 20 (before) and 22 (after) the ar bidi fix; 05 was re-shot after the number-format fix. The browser
   pane paints only part of an emulated viewport, so several images show the upper half; the text checks are DOM reads.
4. The chain runs used the script before the `-ProductMasterSpec` change (same spec file, path written inline).
