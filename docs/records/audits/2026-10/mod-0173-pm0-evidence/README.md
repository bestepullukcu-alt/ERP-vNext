# WP-0173-PM0 evidence — development-only product-master bridge + FU-UI2-05, 2026-10-02

Base: `64629d27b`. Raw outputs. No token, password or signing secret is written here: the seed user's login token stayed
in the browser's HttpOnly cookie; persona tokens were minted in memory (persona-helper.js); the gateway copy logs
tokens as `[REDACTED]`.

| File | What it shows |
|---|---|
| `d1-build-web.log` | Diten.Web Release `--no-incremental`: 0 errors / 17 warnings (the same pre-existing 17) |
| `d2-diten-web-tests.trx` | `Diten.Web.Tests` 511/511, 0 skipped (UI2: 496; +15 = ProductMasterBridgeTests 10, reversal replay 1, reversal instant range 4) |
| `d3-architecture-tests.trx` | `tests/architecture` 18/18 (incl. DocsPathGuardTests over this folder) |
| `d4-sabotage.sh`, `d4-sabotage.log` | (a) the "only in Development" condition replaced by `if (false)` → 2 red (Production, Staging) → green; (b) reversal dates taken at send time instead of the dialog's instant → 1 red → green; SHA-256 identical, `git diff HEAD` empty after |
| `run-pm0-env.ps1`, `pm0-fixtures.js`, `persona-helper.js` | the probe chain: Web :5091 (Development, `Inventory__ProductMasterBaseUrl=http://127.0.0.1:4011/` from the environment) → gateway copy :5090 (inventory routes only — NO product-master route) → InventoryService :5067 (DB `diten_inventory_pm0_smoke`), Prism :4011, Model A mock :4012, persona helper :5097 |
| `d6-gateway-copy-override.json`, `d6-gateway-copy-changes.json` | the gateway copy = committed `ocelot.json` + the listed inventory / Model A probe changes; no `/api/product-master` route |
| `d6-prism-spec-lot.openapi.yaml` | the probe copy of the frozen PRODUCT-MASTER spec (lot-required example) |
| `d5-runtime-smoke.json` | D5 measured: gateway 404 on `/api/product-master` (control); S-3 units + lot flag read through the bridge (Web → 4011 with the prefix stripped, Prism 18 requests all 200); opening balance 201; S-2 Reversal → 201, the same request re-sent → 200 `idempotentReplay: true`, same number INV-2026-0000002 |
| `screenshots/01…03` | S-3 product + SKU via the bridge; unit hint + stock effect; S-2 after the reversal and its replay (one reversal row) |
| `d6-script-checks.log` | `bash -n` on the three .sh = OK; PowerShell 5.1 ParseFile `watch-diten.ps1` = 0 errors; numstat deletions 0; BOM `239 187 191` kept; the added lines |
| `d7-fleet-before.json`, `d7-processes-after.json`, `d7-fleet-after.json`, `d7-git.log` | the user's fleet before / after; every probe process stopped (fleet PIDs unchanged by the run); git range + allowed-path check after the fast-forward |

Notes
1. **Worktree.** Written, built, tested and run in a detached scratch worktree (`C:\Users\Ali\erp-vnext-wt-pm0`,
   removed afterwards) and landed by ONE fast-forward. `C:\Users\Ali\erp-vnext-wt-inventory-ui` was not touched.
2. **Port.** The run scripts point the bridge at the fleet's Prism (4010); the probe chain pointed it at its own Prism
   (4011) so the user's fleet was not used.
3. **Model B.** `/api/mdm/products` was not called; no hardcoded lookup or fallback list exists in the bridge.
