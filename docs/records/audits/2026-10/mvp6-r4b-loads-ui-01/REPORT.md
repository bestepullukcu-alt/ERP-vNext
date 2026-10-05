# R-4b — Loads (MOD-0185) UI, end to end, against MODULE-RECIPE.md

- Lane: R-4b, frontend-ui-ux (UI) and integration-agent (provider). Recorded 2026-10-04, in the same session as R-4a.
- Tree: `feature/mvp6-logistics` · HEAD `cea01354e` · staged 0. Port 57373 (Q372-R2) was not touched; this lane used
  57406 (stack), 57407 (first suite attempt, discarded: see Suites) and 57408 (suite).
- Step 0 (sha256/16):
  - `AGENTS.md` `ce8c12ad80f93bb5`
  - `git-safety.md` `4181c697b96dc9f8`
  - `code-style.md` `6032fbb07819dc47`
  - `frontend-ui-ux.md` `90247ddc689b1e06`
  - `integration-agent.md` `f760471e69efb7dc`
  - `MODULE-RECIPE.md` `04925ba82d8c1f69`
  - MOD-0185 `9ec4ef1bc1db21e0` at HEAD, `4473520320b221af` after this lane
- **Owner decision.** MOD-0185 had no approved UI scope (`shell: none`; the scope prep was HELD). The owner's answer
  "Approve both scopes, build" covers Loads and Carriers: `docs/records/decisions/2026-10/mvp6-carrier-loads-ui-scope-owner-decision-01.md`.
- **Not touched:** Carriers beyond R-4a, Claims (R-4c waits), Shipments, Returns, S&OP, Capacity,
  `frontend/Diten.Web/Program.cs`, `ocelot.json`, any service configuration file. Nothing staged or committed. No secret
  printed. Agent verdict ≠ CT ACCEPTED.

## What was built, in the recipe's order

| step | what | where |
|---|---|---|
| pack | approved scope applied as **§30** (surface, 7 field types, intent/correlation/replay, l10n/states/access, open items LIVE-185 and ROOT-UI-01); frontmatter `shell: tenant`, `golden_reference: slim`, `form_field_count: 7`; §29 gains the `CHANGE_STATUS` row; M-03 amended; open gap 2 struck | MOD-0185 (`evidence/shared-and-pack.diff`) |
| gateway | **none needed**: C-03's catch-all `/api/shipment-bundle/{everything}` routed every call | `ocelot.json` untouched |
| Web controller | `SupplyChainLoadsController`: `""`, `api` GET (`status`, `carrierId` only), `api` POST (the browser's body forwarded as exact text). `[JsonAdapterEndpoint]` on both `api` actions; missing/non-UUID correlation or missing key → 400 before the gateway; un-enveloped 5xx → 503; every log line carries the correlation. **No transition route** (ROOT-UI-01) | `frontend/Diten.Web/Controllers/` |
| views | `Index`, `_Filter`, `_DataTable` (5 columns), `_CreateEditOffcanvas` (carrier, mode, local date-time + zone label, repeatable shipments and stops from `<template>`s), `_IndexL10n` (Dictionary, 74 keys) | `Views/SupplyChain/Loads/` |
| scripts | `index.js`: one intent `{key, correlation}` per opened form, 409 blocks the form; wall clock → UTC with non-existent times rejected; list states; envelope validation; filters with UUID check; saved views. `index.l10n.js`: 74 required keys | `wwwroot/assets/js/SupplyChain/Loads/` |
| module resx | `LoadsIndex.{en,tr,fr,es,zh,ar,ru}.resx`, 62 keys each | `Resources/Views/SupplyChain/Loads/` |
| nav keys | `Nav.Module.ROUTINGLOADPLANNING`, `Nav.Page.LOADS` ×7 | `SharedResource.*.resx` |
| provider | **new** `RoutingLoadPlanningManifestProvider` (sort 410, page `LOADS`, actions `CREATE` Toolbar and `CHANGE_STATUS` RowAction) **and its `AddSingleton`** in the same change | `SupplyChainService.Api/ModuleRegistration/`, `Program.cs` |
| guard | the `["loads"]` entry removed from `KnownWithoutProvider` | `EnforcedPermissionDeclarationGuardTests.cs` |

**Enforced key declared as an action, as R-2 did.** `supplychain.loads.transition` is enforced at
`LoadsController.Transition` but had no declaration anywhere. It is declared as the `CHANGE_STATUS` RowAction in the
provider and in §29 of the pack, so a role can hold it. The UI does **not** use it (ROOT-UI-01 keeps the transition out of
this slice). Sabotage: removing `CHANGE_STATUS` turns the guard and M-03/M-05 red, and the guard names the key
(`evidence/suite-results.txt`).

## Golden flow — real user, reload after each step, the database agrees

**Actor.** `r4b-admin`, tenant Admin of STARTER tenant `cedaf26d…`, entitled to `CARRIER-MANAGEMENT` and
`ROUTING-LOAD-PLANNING` (`evidence/entitle.txt`, `register-tenant.txt`). The token carries `legal_entity_id`. Holdings: loads
`create`/`read`/`transition` and carriers ×3, all `GrantSource=1`; `shipments.read` is `GrantSource=0`, through Q357's
temporary Admin entry (`evidence/loads-holdings.txt`).

**Registration.** All four providers self-registered (Shipments, Returns, Carrier, routing-load-planning); Auth held the 3
`supplychain.loads.*` keys.

| step | UI | after reload |
|---|---|---|
| 1 Create | Add Load → carrier `1195039d…` (Road, Rail), mode Road, departure **21:59 local**, 1 shipment, 2 stops (Pickup `LOC-A`, Delivery `LOC-B`) → Save. First answer 503 (T0, config gap); the same open form retried → 201 | `LOAD-659A9B09…` Draft |
| 2 Second load | 2 shipments, 3 stops (one `Return` with an empty location) | `LOAD-E3272E86…` Draft; ids unique |
| 3 Filter | `Draft` + carrier → `?status=Draft&carrierId=…` → 2 rows; `Completed` → 0; an invalid carrier UUID is marked and not sent | same after reload |

**Database (`evidence/golden-db.txt`, `db-final.txt`).**
- `LOAD-659A9B09…`: Draft, version 1, root **`f44306eb…` = the form's correlation**, `PlannedDepartAt 2026-10-04T18:59:00Z`
  (21:59 at UTC+3), the carrier, the shipment, stops `1:Pickup:LOC-A`, `2:Delivery:LOC-B`, tenant and LE equal the session.
- One assignment, receipt, outbox and audit row per load. End state: 6 loads, 6 receipts, 7 assignments, 6 audit, 6 outbox.

**Gateway.** **31** requests on `/api/shipment-bundle/loads` (0 before the Loads page; 42 log lines with the upstream
warnings), all through the catch-all: 14×200, 7×201, 3×409, 3×502, 1×503, 3×499 (`evidence/gateway-loads-lines.txt`).

**Web.** 29 adapter lines, each with its correlation (`evidence/web-loads-lines.txt`). The create's `f44306eb…` appears on
the Web line, the gateway's upstream warning and the stored root.

## Traps (`evidence/traps-browser.md`)

| trap | produced | fixed behaviour | sabotage, old behaviour back |
|---|---|---|---|
| key per payload (3.1/Q403) | lost response + an edited stop | 409 `IDEMPOTENCY_KEY_REUSED`, 1 load, a third click sends nothing | S2: new key, 409 `SHIPMENT_ALREADY_ASSIGNED`, a misleading message (the assignment uniqueness stopped a second load) |
| correlation per intent (3.2) | lost response + unchanged retry | 201 replay, 1 load | S1: fresh correlation → 409 `CORRELATION_ROOT_MISMATCH` after a commit |
| 5xx without an envelope (2.1) | SupplyChain stopped | list: read-worded alert; create: 503 write wording with the intent's correlation; DB unchanged | controller mapping disabled: 3 unit cases red |
| JSON 401 (2.4) | unauthenticated adapter call | 401 JSON with the caller's correlation; page 302 | — (marker pinned by a reflection test) |
| skeleton / failed load (4.1, 3.6, 3.11) | SupplyChain frozen | skeleton; read-worded alert after 15 s; no toast | no skeleton, "Loading…" table, write wording on a read |
| wall clock → UTC (3.4) | Berlin spring gap | rejected; 21:59 local stored as 18:59Z | naive parse: a shifted time the user never chose |
| async ajax (R-4a's new class) | every create | list reloads after a 201 | — (built with the fix from the start; pinned by `ReloadIsSafeAndListStatesAreDistinct`) |
| 7 languages, wire English, Dictionary (5.2/5.3) | culture `ar` | RTL, Arabic labels, `?status=Draft`, 74 exact keys | — |
| UAS-001 | `r4b-nokey` | only the denied card, 0 adapter calls, no redirect | — |

## Suites

| suite | result |
|---|---|
| Frontend, final | **297 / 0 / 297** = 282 (after R-4a) + 15 Loads tests (4 JS, 3 forms, 8 controller cases) |
| SupplyChain, Q335 recipe, own mongod 57408 (`enableTestCommands=1`) | **432 / 1 / 433** over the module filters, inside the expected range: Shipments 90/0, Carriers 36/0, **Loads 33/0**, Returns 78/0, Claims 128/1 (the known `ClaimReplayTests.Receipt_TwoIndependentTestProcesses_DurableRecovery`, "restart mode required", same test as R-4a), S&OP 19/0, Capacity 48/0; 457 listed (449 + M-01…M-08) (`evidence/sc-suite.txt`) |
| Outside the filters: guard, provider and registration tests | **28 / 0 / 28** (20 + M-01…M-08) |

A first SupplyChain attempt on 57407 is **discarded and kept** (`sc-suite-run1-no-testcommands.txt` in the scratch folder):
my mongod lacked `--setParameter enableTestCommands=1`, so the fail-point tests failed (Shipments 2, Loads 2, Returns 1)
with `no such command: 'configureFailPoint'`. A lane setup error, not a product signal; it was stopped and rerun.

## What the recipe got wrong for Loads (R-3 input)

| # | recipe line | kind | measured |
|---|---|---|---|
| 1 | (none) | **MISSED: service configuration** | no tracked config sets `Loads:ReferenceBaseUrl`; the first create answered 503 `DEPENDENCY_UNAVAILABLE`. Returns and Claims carry theirs. The recipe has no "the module's outbound references are configured" check. Worked around with an env override; **no config file edited** |
| 2 | 0.4 / (none) | **MISSED: cross-module dependency** | a Load references carriers and shipments, so the Admin needs `carriers.read` (holdable only after R-4a's registration) and `shipments.read` (today only through Q357's temporary Admin entry, expiring 2026-11-03). Loads depends on R-4a; the recipe orders modules independently |
| 3 | 3.2 | **APPLIES HERE (contrast R-4a #5)** | Loads' receipt checks the root (`LoadRepository.cs:16`): a per-request correlation turns a committed create into 409 `CORRELATION_ROOT_MISMATCH`. The line should say "check the module's receipt rule", and when it compares, mint key and correlation together |
| 4 | 6.4 / guard | **INCOMPLETE** | the pack's §29 lacked a row for an enforced key (`supplychain.loads.transition`); a provider built from the pack alone fails the guard. Declared as an action, as R-2 did |
| 5 | 3.1 / Q403 | **CONFIRMED, with a twist** | the per-payload key returns; on Loads the backend's assignment uniqueness turns the duplicate into a misleading 409, which is easy to misread as "safe" |
| 6 | 3.4 | **INCOMPLETE** | it asks to reject non-existent times; it says nothing about the ambiguous autumn hour, which `new Date(y,m,d,h,mi)` silently resolves to the first occurrence |
| 7 | 3.6 / 4.1 | **CONFIRMED (list-level)** | the list surface holds the Add button, so when the list cannot load there is no create path at all; the form-level 5xx had to be produced with the form open before the stop |
| 8 | 1.1 | **CONFIRMED** | catch-all routed everything |
| 9 | 0.4 | **CONFIRMED, with a step the recipe omits** | the web login needs `?tenantId=` for a non-default tenant (Development falls back to the default tenant), otherwise "Invalid email or password" |
| 10 | 0.7 (drafts) | **CONFIRMED absent** | no Loads draft or rescue; written from the pack and R-4a's pattern |
| 11 | 5.5 | **CONFIRMED** | nav keys absent at HEAD; added ×7 |

## Findings

- **F-R4b-1 — `Loads:ReferenceBaseUrl` is configured nowhere in the tree.** Any environment built from the repository
  cannot create a load. Owner of service config to decide where it belongs; not changed here.
- **F-R4b-2 — Loads' Admin path depends on `shipments.read` via Q357's temporary entry**, expiring 2026-11-03. After that,
  an Admin cannot pick shipments unless Shipments' grant is made permanent.
- **F-R4b-3 — `supplychain.loads.transition` was enforced but undeclared.** Now declared (CHANGE_STATUS); no UI uses it.
- **F-R4b-4 — the ambiguous autumn hour is accepted silently** (first occurrence). Not wrong by the recipe; worth a line.
- **F-R4b-5 — translations** (62 module keys and 2 nav keys ×6 languages) are this lane's and want an l10n review.
- **F-R4b-6 — process slip:** one `mv` of my own scratch log (`supplychain.log` → `supplychain-run2.log`, outside the
  repository) against the no-`mv` rule. Nothing in the repository was moved or deleted.

## R-4a's seal and the shared `Program.cs`

`mvp6-r4a-carriers-ui-01/ARTIFACTS.sha256` now fails on one line: SupplyChain `Program.cs`. R-4b added two lines (the
`// R-4b` comment and `AddSingleton<…, RoutingLoadPlanningManifestProvider>`) after R-4a was sealed. With those two lines
removed, the file hashes to R-4a's sealed value (`d24ef42b2edcfbd5…`). R-4a's record was not reopened; this record seals the
file as it stands now.

## How it was run

- Scratch `~/mvp6-env/r4b-20261004-2153/`: a repository copy built there (copy vs repository at the end: 0 differences over
  every modified or untracked path under `services/` and `frontend/`).
- **Services:** six services from that copy on lane mongod 57406 (`rsr4b`); SupplyChain with the
  `Loads__ReferenceBaseUrl` env override. All stopped by pid (only processes this lane started); mongods 57406 and 57407
  shut down, 57408 after the suite.
- **Tenant:** STARTER-plan tenant registered through Platform's admin API with a platform-admin token minted into a mode-600
  file; users invited through Auth; entitlement and the `tenant.activated` replay (R-2's method).
- **Browser:** passwords served by a local helper (never printed); signed in as `r4b-admin`, then as `r4b-nokey`.
- **Sabotage:** three scripts served from the scratch copy and restored (served file equal to the repository); the controller
  sabotage and the provider sabotage only in scratch test builds.

Nothing committed, nothing pushed, nothing staged.
