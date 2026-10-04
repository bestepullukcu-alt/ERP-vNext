# R-2 (Q386) — Returns (MOD-0186) built against MODULE-RECIPE.md, end to end

- Lane: R-2, REPLAN Phase 2. Roles: frontend-ui-ux (UI), integration-agent (gateway, provider). Recorded 2026-10-04.
- Preflight: `Sun Oct  4 14:32:05 UTC 2026` · `feature/mvp6-logistics` · HEAD `983dcd4d0` · porcelain 9 · staged 0. Port 57373
  was held by Q372-R2's paused mongod; it was not touched. This lane used 57386 for the stack and 57387 for the suite.
- Step 0 (sha256/16):
  - `AGENTS.md` `ce8c12ad80f93bb5`
  - `git-safety.md` `4181c697b96dc9f8`
  - `code-style.md` `6032fbb07819dc47`
  - `frontend-ui-ux.md` `90247ddc689b1e06`
  - `integration-agent.md` `f760471e69efb7dc`
  - `MODULE-RECIPE.md` `49691537338d639e` (read in full)
  - MOD-0186 `be669789f16d5963`: §9, §11, §12 and §22 as dispatched, plus **§32 (UI) and §33 (self-registration)**. In this
    pack the dispatched numbers are not the UI sections; see R-3 item 12.
- **Two owner decisions taken in this lane** (asked in chat, answered):
  1. Add the Returns provider and its test under `services/`, as new files, beyond the one `AddSingleton`.
  2. Declare `supplychain.returns.transition` in the manifest (F-R2-1).
- **Not touched:** `SupplyChainShipmentsController.cs` and the other Shipments files, `frontend/Diten.Web/Program.cs`, and
  `ocelot.json` (no change was needed; see "Gateway"). No permission was widened: `AdminModules` is untouched, and keys
  reached the Admin through the tenant's entitlement. Nothing staged or committed. No token or secret printed.
  Agent verdict ≠ CT ACCEPTED.

## CT's measurements, verified

| CT said | measured | verdict |
|---|---|---|
| 36 Application feature files and a service controller | 36 Returns `.cs` files across **five** projects (Api 3, Application 14, Domain 10, Infrastructure 3, Persistence 6); `Api/Features/Returns/ReturnsController.cs` | count TRUE; "Application" FALSE (14) |
| NO manifest provider at all | 0 in `services/` (a draft existed only in `docs/records/audits/2026-09/mvp6-returns-ui-draft-0{1,2}`) | TRUE |
| NO gateway route | no Returns-specific route, **but** C-03's `/api/shipment-bundle/{everything}` → 5061 already routes the whole family; Returns answered through it with `ocelot.json` unchanged | **FALSE in effect** (F-R2-3) |
| contract groups `/returns` (:1364), `/returns/{returnId}/transition` (:1927) | both at those lines | TRUE |
| nine keys | `ReturnPermissions.cs`: 3 constants (read, create, transition) + 6 `ForTarget` values (authorize, transit, cancel, receive, disposition, close) | TRUE |

**Key → surface map** (the manifest after this lane):

| key | surface |
|---|---|
| `read` | page `RETURNS` |
| `create` | `CREATE` (toolbar) |
| `transition` | `CHANGE_STATUS` (row action: opening the status panel). Before this lane it was API-only, see F-R2-1 |
| `authorize` | `AUTHORIZE` and `REJECT` |
| `transit` | `MARK_IN_TRANSIT` |
| `cancel` | `CANCEL` |
| `receive` | `RECEIVE` |
| `disposition` | `DISPOSITION` |
| `close` | `CLOSE` |

**API-only keys: none.** MOD-0183 has one (`reconcile`); Returns had one by pack M-03, and that one was the defect.

## What was built, in the recipe's order

Base: the Q160 draft overlay v2 (`docs/records/audits/2026-09/mvp6-returns-ui-draft-02/returns-ui-draft-overlay-v2.tar.gz`,
2026-09-28, "not built, not run"). All 23 files were placed as **new** files (0 pre-existed). **18 of 23 were changed** before
shipping; the full diff is in `evidence/draft-v2-to-final.diff`, and file hashes in `evidence/new-files.sha256-16.txt`.

| recipe step | done | where |
|---|---|---|
| Gateway route pair | **not needed**, measured (F-R2-3) | `ocelot.json` diff: 0 lines |
| Web controller | `SupplyChainReturnsController.cs`: 1 page and 4 JSON adapters (list, shipment resolve, create, transition) | + a success log line on every gateway answer, with trace (and root for mutations) — recipe 2.3 |
| Views | `Index.cshtml` + 6 partials (Slim, pack §32) | the time field becomes `datetime-local` + zone hint |
| Scripts | `index.js`, `index.l10n.js` | one key per opened form; local→UTC with rejection of non-existent times; alert cleared on a valid retry |
| Module resx ×7 | `ReturnsIndex.{en,tr,fr,es,zh,ar,ru}.resx`, 106 keys each | + `OccurredAtInvalid`; `OccurredAtHelp` rewritten in 7 languages |
| L10n bridge | `_IndexL10n.cshtml` is a `Dictionary<string,string>` (120 entries) | recipe 5.3 |
| Nav keys in the seven SharedResource files | `Nav.Module.REVERSELOGISTICS`, `Nav.Page.RETURNS` (draft values) | +2 lines × 7 (`evidence/shared-files.diff`) |
| Manifest provider + `AddSingleton` | `ReverseLogisticsManifestProvider.cs` + `ReverseLogisticsManifestProviderTests.cs`; `Program.cs:99-100` | `CHANGE_STATUS` action added (F-R2-1) |

The provider shipped with the UI in the same change (§33 ship rule).

## The golden flow: real user, reload after every step, the database agrees

**Actor.** `r2t-admin`, tenant Admin of lane tenant `29fb1c14…` (registered through Platform with plan STARTER, entitled to
`REVERSE-LOGISTICS`). The token carries `legal_entity_id` `634e06e0…`, 5 Shipment keys and 9 Returns keys, all
module-sourced from `reverse-logistics` (`evidence/returns-holdings-after-declare.txt`).

**Source.** Shipment `e3872318…` (2 lines: 5 EA, 3 BOX), created and taken to Delivered through the gateway
(`evidence/ships.jsonl`), root `66b8e14d…`.

| step | UI | after reload | database (`evidence/golden-db.txt`) |
|---|---|---|---|
| 1 Create | Add Return → shipment UUID → Look Up (Delivered, 2 lines) → line 1 qty 2, reason DAMAGED → Save; toast "Return created." | Requested | `RMA-6CD3B7EF…` v1; tenant/LE from the session; `CorrelationRoot` = the Shipment's root `66b8e14d…`; +1 audit, +1 Pending outbox, +1 receipt, +1 entitlement |
| 2 Authorize | row action → panel (field `19:10` local, hint `Europe/Istanbul (UTC+03:00)`) → confirm dialog "Authorized" | Authorized; actions Mark In Transit / Cancel Return | v2; audit `Requested>Authorized@16:10:00Z` |
| 3 Mark In Transit | the same | In Transit; action Receive only | v3 |
| 4 Receive (manual assertion) | inventory ref `INV-R2-RCV-1`; the dialog says it is not a warehouse receipt | Received | v4; audit row carries `INV-R2-RCV-1` |
| 5 Disposition | code `RESTOCK` | Dispositioned | v5; `DispositionCode RESTOCK` |
| 6 Close | — | Closed; **0** actions left | **v6, 6 audit rows, 6 outbox events (Pending), 6 receipts**; every audit row carries the same root |

Screenshot: `shots/G6-list-closed-after-reload.jpg`.

### Gateway: the requests it logged zero of

Before this lane no client had ever called `/api/shipment-bundle/returns`. During the run the gateway logged **19** Returns
lines, all through the existing catch-all. In the flow window alone:

- 12 `GET /returns`;
- 1 `POST /returns` (201);
- 5 `POST /returns/{id}/transition`;
- plus 6 Shipment lookups the adapter made.

Each line carries a `CorrelationId` (`evidence/golden-gateway-lines.txt`). The Web logged 67 adapter lines in the same
window, each with its trace, and root for mutations (`evidence/golden-web-lines.txt`).

## The known traps — each produced, none happened (`evidence/traps-browser.md`)

| trap | condition produced | outcome | sabotage |
|---|---|---|---|
| 5xx without an envelope shown as validation (recipe 2.1) | SupplyChain stopped → gateway 502 empty | transition and create lookup: "The shipment reference data is temporarily unavailable…" (503 `DEPENDENCY_UNAVAILABLE`); list: error state, table hidden | unit: 5xx→400 mapping turns `List_failure_keeps_the_published_code…` red; restored green |
| key per payload (recipe 3.1) | lost response (commit, then a dropped response), then an edited retry | same key → **409**, "already used with different values"; **1** Return | **live:** the draft's per-payload script → new key → 201 → **2 Returns from one intent**; restored |
| Q393, unchanged retry gets 400 | lost response, then an unchanged retry | **201 replay**, "already created by the same request…"; **1** Return | — (the design differs from Shipments; see the decision below) |
| local clock in, UTC out, non-existent time (recipe 3.4) | prefill; typed `18:00`; `2016-03-27T03:30` in Istanbul; empty | field 19:10 → stored 16:10Z; 18:00 → wire `15:00:00.000Z`; non-existent and empty rejected with **0 requests** | — (the draft sent offset text as typed; this lane replaced it) |
| skeleton, not empty cards (recipe 4.1) | SupplyChain frozen (`SIGSTOP`) | t=1.7 s skeleton only, no table, no "Loading" text; t=15.3 s error state (Q394 15 s deadline) | — (the draft's own v2 fix; condition produced only) |
| 7 languages, wire value English, Dictionary (recipe 5.2/5.3) | culture `ar` | RTL, all labels Arabic, 0 Latin status words, filter "مغلق" sends `status=Closed`, bridge keys exact | — |
| UAS-001 | user without the keys | only the denied card in the shell, 0 adapter calls | — |

### What Returns sends as `X-Correlation-Id`, and why (the Q393 question)

**On mutations, the Shipment's lifecycle root, resolved server-side by the adapter (pack §32.4). On reads, the browser's
fresh trace.**

- The backend rejects a create whose correlation is not that root with 409 `CORRELATION_ROOT_MISMATCH` (pack §32.4,
  `ReturnRepository.cs:56`).
- The root is stable for the life of the Shipment, so every retry of an intent carries the same correlation whatever trace
  the browser mints. Q393's 400 therefore cannot occur. Measured: the unchanged retry replayed with a different browser trace.
- The browser never sees the root. The adapter replaces it with the trace in responses, and the Web log binds the two
  ("Trace … Root …").

The Shipments half that was **not** copied is `create.js:76`, which mints a new correlation per Save.

## Permissions: how the Admin came to hold the keys, without widening

1. **SupplyChain registered both manifests** on attempt 1. Auth's catalogue then held **8 of 9** Returns keys: `transition`
   was absent.
2. **On the default tenant, Admin held 0.** Entitling that tenant was refused:
   `400 QUOTA_CONFIGURATION_MISSING` (F-R2-2).
3. **A lane tenant was registered** through `POST /api/admin/tenants` with plan STARTER. The fixture was re-run on it
   (users through Auth's invite endpoint, the Q185 org fixture).
4. **The tenant was entitled** to `REVERSE-LOGISTICS` (201), and `tenant.activated` replayed (Q358 C method). Admin then held
   **8**, all `GrantSource=1 / reverse-logistics`; Viewer held `read` (System).
5. **After declaring `CHANGE_STATUS`** and restarting SupplyChain, Auth held **9 of 9**. After one replay, Admin held **9**.

## Suites

| suite | result | note |
|---|---|---|
| `Diten.Web.Tests`, final | **263 / 0 / 263** | Q394 baseline 164 / 0 / 164 + 99 Returns tests. **Zero failures.** Path in `evidence/suite-results.txt`: 261/2 (nav keys missing — recipe 5.5 predicted it), then 261/2 (two draft tests pinned my changes; one pinned the per-payload defect itself) |
| Returns provider tests (M-01…M-08 + declared-key test) | **10 / 0 / 10** | sabotage (`CHANGE_STATUS` removed): 7/3/10, then restored. These sit outside Q335's seven module filters, so the module runs below do not include them |
| SupplyChain, Q335 recipe, own mongod 57387 | **432 / 1 / 433** over the seven module filters (Shipments 90, Carriers 36, Loads 33, Returns 78, Claims 128/1/129, S&OP 19, Capacity 48) | identical to Q335/Q371/Q374; the one failure is the known Claims restart-mode test `ClaimReplayTests.Receipt_TwoIndependentTestProcesses_DurableRecovery`. Read as a range (Q361); `ShipmentTelemetryTests.cs:71` passed this run. 445 listed = 435 + the 10 provider tests (`evidence/sc-suite.txt`) |

## R-3 input — what the recipe got wrong, missed, or made me do unnecessarily

This section is the second deliverable. Each item names what was measured.

| # | recipe line | kind | what R-2 measured | suggested amendment |
|---|---|---|---|---|
| 1 | 1.1 and the shared-files table ("one route pair per module; `ocelot.json` sequenced") | **WRONG for this family** | C-03's `/api/shipment-bundle/{everything}` routes Returns already; 19 gateway lines, `ocelot.json` diff 0 | "The shipment-bundle family (Shipments, Carriers, Loads, Returns, Claims) is routed by C-03's catch-all: **no gateway edit, no R-4 gateway collision.**" Pack §32.10 wants explicit routes with no catch-all (`/returnsXYZ` unmatched); that conflicts with C-03 and needs a ruling, not a module lane |
| 2 | 6.4 ("API-only key never reaches Auth — expected") | **WRONG LESSON** | an API-only key the backend **requires** (`returns.transition`, `ReturnsController.cs:20`) can never be granted: 8/9 in Auth; every transition 403 for every user | new line: "every key the backend enforces is declared in the manifest", with a test like `Every_enforced_key_is_declared_so_the_permission_sync_can_carry_it`. Check Carriers, Loads and Claims for the same before building them |
| 3 | 0.4 ("pick entitlement or `AdminModules`") | **UNDERSTATED** | entitlement needs a tenant with a commercial plan: default tenant → `QUOTA_CONFIGURATION_MISSING`; without a broker, a `tenant.activated` replay is needed | add the plan precondition and the replay step; the fixture must support a non-default tenant (R-2's does now) |
| 4 | (none) | **MISSED** | complete UI drafts existed for Returns (and Claims, Capacity, S&OP); 23 files built clean on the first try and saved most of the cost; the draft had already predicted F-R1-1 in September (its F3) | new step 0: "look for `docs/records/audits/2026-09/mvp6-<module>-ui-draft-*` and start from the latest" |
| 5 | 3.1 (one key per page) vs the pack | **CONFLICT** | pack §32.8 defines an intent as the payload ("an edited payload is a new intent"); the draft followed the pack and **pinned the defect in a test** (recipe 8.3 again); live sabotage gave 2 Returns from one intent | the recipe line holds; MOD-0186 §32.8 (and MOD-0187's twin) need amending, or the next draft-based lane re-imports the defect |
| 6 | 3.2 / Q393 | **DOES NOT APPLY as written** | Returns sends the Shipment root on every mutation, so retries are safe by design | phrase it as "an intent's retries carry one stable correlation (Shipments: the lifecycle root, or the create's first id; Returns: the Shipment root)". That is also the fix shape for Q393 |
| 7 | 3.4 / 3.5 | **COST NOT PREDICTED** | there is no shared date helper; the functions were copied module-locally from `details.js`, and Returns' parse is inside an IIFE with no Node test at a non-UTC zone (3.5 is Shipments-only) | a shared, exported date helper with one Node zone test, used by every module |
| 8 | 5.3 (Dictionary) | **PARTLY REDUNDANT here** | the draft already defended against camel-casing with a PascalCase normalizer; the Dictionary was applied anyway | keep the line; add "whatever the bridge, the page must look up exactly the names the resx defines" |
| 9 | (F-Q371-5 never became a line) | **MISSED** | a failed list **load** says "The outcome is not confirmed; try the same request again", which is write wording; it recurred in Returns | add "read failures use read wording", attributed to F-Q371-5 and F-R2-5 |
| 10 | 2.3 (one id across three logs) | **ILL-DEFINED for root-carrying modules** | one create is two ids at the gateway (trace for the Shipment lookup, root for the POST); only the Web log binds them | define O-1 for adapters that resolve a root: "the Web log line names both" |
| 11 | 5.5 (nav keys forced by the guard) | **CONFIRMED** | the guard failed (2 tests) until the keys were added | — |
| 12 | the dispatch's pack sections | **WRONG MAPPING** | "§9, §11, §12, §22" are MOD-0183's numbers; MOD-0186's UI is §32 and registration §33 (§9/§11 say "shell: none"/"not applicable"; §22 is field parity) | cite pack sections by topic, not number |
| 13 | 8.1 (copy all of `services/`) | **CONFIRMED** | the Q382 copy trap did not recur because the R-2 copy included every provider-bearing service | — |
| 14 | 2.4 / 2.5 (Q394) | **WORKED: no repeat cost** | `Program.cs` untouched; Returns' 401 is JSON and its list deadline fired at 15 s | — |
| 15 | 0.1 / 0.2 (scope chain, composition) | **WORKED** | the Admin token carried `legal_entity_id` from the first login | — |

## Findings

- **F-R2-1 — the transition key could not reach any user (fixed by owner decision).**
  - `supplychain.returns.transition` was API-only by pack §33 M-03.
  - The Platform→Auth sync carries only declared keys, so Auth held 8 of 9.
  - The backend requires it on every transition (`ReturnsController.cs:20`, `ReturnContextMiddleware.cs:65`), so no Return
    could be authorized, received or closed by anyone.
  - Now declared as `CHANGE_STATUS`. The M-03 allow-list is empty, and a new test pins "every enforced key is declared"
    (sabotage: 3 red). **Pack §33's action table needs the matching amendment** (CT).
- **F-R2-2 — the default tenant cannot be entitled.** `POST …/commercial/module-entitlements` on tenant `…0001` → 400
  `QUOTA_CONFIGURATION_MISSING`. The entitlement path works only on tenants with a plan.
- **F-R2-3 — Returns was already routed.** See R-3 item 1. Also, pack §32.10's "no catch-all, `/returnsXYZ` not
  matched" is **not met** by C-03's catch-all; recorded, not changed.
- **F-R2-4 — the draft carried the per-payload key, and its test pinned it** (`ReturnIndexBehaviorTests.cs:119-120` in the
  draft). Both were fixed here, and the test now pins the fix. The root cause is pack §32.8's wording (R-3 item 5).
- **F-R2-5 — read failures use write wording.** "The outcome is not confirmed; try the same request again." appears on a
  failed list load. This is the F-Q371-5 pattern again, and it is open.
- **F-R2-6 — a stale rejection stayed on screen. Mine, fixed.** After an invalid time, the alert stayed visible while
  the confirm dialog opened for a corrected one. It is now cleared on a valid time, re-measured.
- **F-R2-7 — `Return.InventoryTransactionReferenceId` holds only the last transition's value.** The Received reference
  (`INV-R2-RCV-1`) is null on the Return after Disposition; the audit row keeps it. This is a MOD-0186 observation, not
  changed.
- **F-R2-8 — one create is two ids at the gateway** (R-3 item 10).
- **F-R2-9 — the Returns create depends on a temporary grant (CODE + date).** Creates and transitions need
  `supplychain.shipments.read` (G-SHIPREAD). In this lane the Admin held it **only through Q357's temporary `AdminModules`
  entry, which expires 2026-11-03**.
  - After that date, a tenant entitled to Returns but not to Shipments gets 403 on create.
  - Not measured; it follows from the measured holdings.
  - Owner item: entitlement bundling (Q353 E row 4).
- **F-R2-10 — the dispatch's own statements.** The "36 Application feature files" are 36 across five projects. The pack
  section numbers were MOD-0183's.

## How it was run

- Scratch `~/mvp6-env/r2-20261004-1732/`: the draft v2 extracted; a repository copy (sources); the frontend and SupplyChain
  rebuilt there after every change (copy vs repository: 0 differences at the end).
- **Services:** Auth, Platform, MDM and Gateway ran from Q372-R2's built copy, equal to the repository source (0
  differences). SupplyChain and Web came from this lane's copy.
- **Databases:** lane mongod 57386 (`rsr2`); suite mongod 57387 (`rsr2s`). One throwaway JWT secret, mode 600.
- **Platform-admin token:** minted by Q358's `mint.py` into a mode-600 file. The internal key was read from the Development
  file and never printed.
- **Browser:** signed in by a password served from a local helper, so no password passed through the transcript. Signed
  out at the end. The `fetch` wrappers were test instruments.
- **Stopped by recorded pid:** web, gateway, supplychain, mdm, platform, auth, helper, then the lane mongod. Ports 5000,
  5001, 5056, 5057, 5059, 5061, 5199 and 57386 are free. The suite mongod on 57387 was shut down after the run.
- Q372-R2's mongod on 57373 was never touched.

Nothing committed, nothing pushed, nothing staged.
