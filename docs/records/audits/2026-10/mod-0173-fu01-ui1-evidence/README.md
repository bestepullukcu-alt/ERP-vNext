# WP-0173-UI1 evidence — MOD-0173-FU01 UI-1: S-1 Stock list + S-2 Movement history (+ Reversal), 2026-10-01

Base `3341f76c`. Raw outputs. No token, password or signing secret is written here: test tokens were minted per run
into a scratch folder and handed to the browser through a localhost-only helper; secrets went from config files to
child-process environments only.

| File | What it shows |
|---|---|
| `d1-build-web.log` | `Diten.Web` Release `--no-incremental`: 0 errors; 17 warnings, all pre-existing (same 17 at base), 0 in files of this WP |
| `d1-build-inventory.log` | `Diten.InventoryService.sln` (manifest): 0 errors, 0 warnings |
| `d2-web-tests.trx`, `d2-web-tests-summary.log` | `Diten.Web.Tests` 377/377, 0 skipped (base 333; +44: adapter, proxy controllers, resx parity, UAS render) — includes `NavManifestL10nGuardTests` |
| `d2-inventory-tests.trx`, `d2-inventory-tests-summary.log` | InventoryService 282/282, 0 skipped (278 + 4 manifest completeness) |
| `d3-architecture-tests.log` | `tests/architecture` 18/18 |
| `d4-sabotage.log` | (a) Reversal visibility condition removed → 2 red → restored → green, SHA-256 identical, diff empty; (b) UAS-001 gate removed from S-1 Index → 1 red → restored → green, identical |
| `d5-datatable-manual-checklist.md` | the DataTable template checked by hand, `file:line` per rule — the Python verifier is BLOCKED-ENV (no Python) and is not reported as PASS |
| `d5-greps.log` | U2 layout 2/2 · U7 "Permission denied/Forbidden" 0 · U11 `/api/inventory`/5066/`window.API` in browser code 0 · U8 `/api/mdm/products` 0 · no serverSide/ajax.data/ext.search/matches:/pageSize=/import/bulk |
| `run-ui1-env.ps1` | Phase 4.5 chain: Prism ×2 + InventoryService :5066 (Mock) + this branch's Diten.Web :5091; reuses the user's Gateway/Auth/Platform/MDM; Start/Stop by recorded PIDs |
| `d6-runtime-smoke.json` | every runtime check with its measured value (U3–U12), the seed, the persona setup and the environment notes |
| `d6-web-gateway-calls.log` | the Web's outgoing Gateway calls in order (page 2 → `balance?page=2&pageSize=10`; pickers only Model A routes) |
| `d6-service-log-excerpt.log` | manifest push at each start; the scope refusal of the REAL AuthService token (no `legal_entity_id` claim) |
| `d6-u4-kpi-vs-availability.json` | KPI strip vs `/availability` measured at the same moment — identical |
| `d6-mongo-reversal-transactions.json`, `-audit.json`, `-outbox.json` | persisted REVERSAL rows (one per original), their audit (`InventoryMovementReversed`) and outbox events |
| `d6-platform-catalog.json` | Platform catalog after two pushes: 1 module, 2 pages, 2 actions, no duplicates |
| `screenshots/01…11` | S-1 with KPI · History → S-2 filtered · Automatic badge · Reversal confirm · posted toast · ALREADY_REVERSED in the dialog · after reversals · view-only (no Reversal) · UAS on both routes · Arabic RTL |
| `d7-processes-after.json` | every process this WP started is stopped; no listener left on 4010/4011/5066/5091/5093; the user's services listed (they were restarted at 13:21 by someone else, before the runtime phase) |

Screenshot 05 (success toast) was taken with the first Reversal dialog (raw SweetAlert); the dialog was then moved onto
the shared `window.showConfirm` (product rule `tests/dialog-one-implementation.test.js`) and re-verified live —
screenshots 04 and 06 and the run-3 entries of `d6-runtime-smoke.json` are from the final implementation; the success
path (`list.reload` toast) is the same code in both.
