# MVP-1 FIX-F4R1 — evidence (WP-MVP1-FIX-F4R1, CT-MVP1-FIX-F4R1 v1) — 2026-10-13

> **Status: agent result, pending CT.** Work branch `fix/mvp1-f4r1`, opened from the CT tree `ct/mvp1-int-f4-r1` at `1e15e3d59` in the isolated
> worktree `C:\Users\Ali\erp-vnext-wt-fix`. Nothing written to the MVP-1 branch, `ct/mvp1-int-f4-r1`, `main` or the CT worktree; no push, no
> PR, origin/main not synced (WP rule). No production code changed. The owner's checkout and fleet were not touched.

## 1. Commits

| Commit | Content |
|---|---|
| `823fb8425` | finding 1 two-company FEFO test; finding 2 `Nav.Module.RECALLREADINESS` × 7 + the self-expiring page-less exception |
| `c20c4d86a` | finding 1 split into (a) order and (b) issue, so the sabotage turns each red on its own |
| (this commit) | this evidence + MOD-0176 §19.14 and MOD-0177 §19.9 notes |

## 2. Finding 1 — FEFO candidate company filter (F4)

`FefoCandidateReader.ReadAsync` (`Persistence/Expiry/ExpiryReadModel.cs:36`) — the ONE candidate read of the FEFO order read and the F2
posting hook — filters `LegalEntityId`; no test failed without it (CT: 179/179). Production code unchanged; two tests added to
`ExpiryReadTests` (class already in `LotExpiryCollection`):

| Test | Seed | Assertion |
|---|---|---|
| `Another_companys_earlier_lot_under_the_same_warehouse_id_is_not_in_the_FEFO_order` | company A: lot +120 (10 AVAILABLE) through the API. Company B of the SAME tenant: a released lot +20 whose 10 AVAILABLE sit under A's SAME `WarehouseId` string and A's SAME SKU | (a) `getFefoOrder` for A: `eligible = [A's lot]`; B's lot neither eligible nor excluded |
| `Another_companys_earlier_lot_under_the_same_warehouse_id_is_not_first_eligible_for_an_issue` | same | (b) A's `GOODS_ISSUE_SALES` of 1 from its own lot over the HTTP posting route → **201**, no reason, no case |
| (c) `Another_company_and_another_tenant_see_none_of_it` (F4) | — | measured only, not rewritten (panel / lists) |

**How B's stock is seeded.** In the test host a warehouse is a node of ONE company's real location tree (`InventoryScenario` seeds it under
A's legal entity), so posting B's stock into A's warehouse id is not a path the API offers. B's lot is created through TRACE and its `LegalEntityId` set to B in `trace_lots`; B's balance row is a clone of A's
balance row with a new `_id`, `LegalEntityId = B`, `LotId = B's lot` — the exact document shape the reader filters. Asserted in the test:
the clone carries A's `WarehouseId` and `SkuId`.

## 3. Finding 2 — the menu guard (R1)

| Item | Where |
|---|---|
| (a) `Nav.Module.RECALLREADINESS` in 7 languages | `frontend/Diten.Web/Resources/SharedResource.{en,tr,fr,es,zh,ar,ru}.resx`, next to `Nav.Module.EXPIRYFEFOMANAGEMENT`: Recall Readiness · Geri Çağırma Hazırlığı · Préparation au rappel · Preparación para retiradas · 召回准备 · الاستعداد للاستدعاء · Готовность к отзыву |
| Key name | The WP wrote `Nav.Module.RECALL-READINESS`; the guard and the runtime both derive the key with the shipping `NavNameLocalizer.Normalize`, which drops the dash → **`Nav.Module.RECALLREADINESS`** (as `EXPIRY-FEFO-MANAGEMENT` → `EXPIRYFEFOMANAGEMENT` in F4). A dashed key would be read by nobody |
| (b) the exception | `NavManifestL10nGuardTests.PagelessUntil` — ONE entry `["RECALL-READINESS"] = "RECALL-READINESS — API-only until WP-0177-R6 (CT FIX-F4R1)"`. `Every_manifest_provider_…_yields_a_module_code_and_pages` accepts 0 pages only for a listed module code; `RawPageOccurrences == ParsedPageCount` stays for every manifest |
| Self-expiry | new `A_pageless_exception_expires_the_moment_its_manifest_declares_a_page` — for each entry the manifest must exist AND declare 0 pages (raw and parsed); otherwise red with "delete the entry from PagelessUntil" |
| Nothing else relaxed | the module / page / domain key tests, the floors, the parser guards are unchanged; an unlisted page-less manifest is red (s4) |

The guard reads the provider SOURCE; the exception lives in the test, so no production manifest changed (error protocol not triggered).

## 4. Sabotage (runner: scratch `fix/sabotage.sh`, the F4 runner shape)

Each: one edit → build → guarding tests RED → `git checkout` → `git diff` empty → GREEN.

| # | Sabotage | File | Red | Restored |
|---|---|---|---|---|
| F1 | drop `& b.Eq(x => x.LegalEntityId, _tenant.LegalEntityId)` from `FefoCandidateReader` | `Persistence/Expiry/ExpiryReadModel.cs` | **(a)** `…_is_not_in_the_FEFO_order` (collections differ at 0 — B's lot first) and **(b)** `…_is_not_first_eligible_for_an_issue` (expected 201, got **422 `FEFO_BYPASS_REASON_REQUIRED`**, B's lot named first eligible); (c) `Another_company_and_another_tenant_see_none_of_it` stays green — the panel reads balances through its own scope | 3 / 3 green, diff empty |
| s1 | delete `Nav.Module.RECALLREADINESS` from `ar` | `SharedResource.ar.resx` | `Every_manifest_module_has_a_Nav_Module_key_in_all_seven_languages` | 8 / 8, diff empty |
| s2 | delete the `RECALL-READINESS` entry | `NavManifestL10nGuardTests.cs` | `Every_manifest_provider_in_the_repo_is_parsed_and_yields_a_module_code_and_pages` | 8 / 8, diff empty |
| s3 | give the recall manifest a (nav-invisible) page, entry kept | `Api/Recall/RecallReadinessManifestProvider.cs` | `A_pageless_exception_expires_the_moment_its_manifest_declares_a_page` | 8 / 8, diff empty |
| s4 | empty the pages of `CarrierManagementManifestProvider` (not listed) | `SupplyChainService/…/CarrierManagementManifestProvider.cs` | `Every_manifest_provider_…_yields_a_module_code_and_pages` | 8 / 8, diff empty |

Final `git status`: 0 changed paths.

## 5. Tests (before = `1e15e3d59`, measured by the agent)

| Suite | Before | After |
|---|---|---|
| Build Inventory | 0 errors / 0 warnings | **0 / 0** |
| Build Web (`--no-incremental`) | 0 errors, 24 distinct warning lines | **0 errors, the same 24 lines** (sorted diff empty) — 0 new |
| Inventory | 1060 passed + 2 skipped | **1062 passed + 2 skipped** (+2 finding-1 tests) |
| Web | 1438 / 1440 (red: `…Nav_Module_key_in_all_seven_languages`, `…yields_a_module_code_and_pages`) | **1441 / 1441** (both reds fixed; +1 self-expiry test) |
| Architecture | 69 / 77 | **69 / 77**, the same 8 reds (sorted names diff empty) |
| Gateway | 134 / 135 | **134 / 135** (`EveryRoute_DownstreamPortIsInKnownServiceSet`, as before) |

Not run (WP): `AccountKindAcceptanceHost.Tests`; no test bound to 27017 (`INVENTORY_TEST_MONGO` unset — the suite starts its own replica set).

## 6. Diff (`git diff --stat 1e15e3d59..HEAD`)

12 files: `ExpiryReadTests.cs` (+57), `NavManifestL10nGuardTests.cs` (+23 / −1), `SharedResource.{ar,en,es,fr,ru,tr,zh}.resx` (+1 each), this evidence, MOD-0176 pack §19.14 (+9), MOD-0177 pack §19.9 (+11). No production code, no contract, no manifest, no other resx key.
