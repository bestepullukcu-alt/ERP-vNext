# R-4a — Carriers (MOD-0184) UI, end to end, against MODULE-RECIPE.md

- Lane: R-4a, frontend-ui-ux (UI) and integration-agent (provider registration). Recorded 2026-10-04.
- Preflight: `Sun Oct  4 17:58:50 UTC 2026` · `feature/mvp6-logistics` · HEAD `cea01354e` · porcelain 3 · staged 0. Port 57373
  (Q372-R2) was not touched; this lane used 57404 (stack) and 57405 (suite).
- Step 0 (sha256/16):
  - `AGENTS.md` `ce8c12ad80f93bb5`
  - `git-safety.md` `4181c697b96dc9f8`
  - `code-style.md` `6032fbb07819dc47`
  - `frontend-ui-ux.md` `90247ddc689b1e06`
  - `integration-agent.md` `f760471e69efb7dc`
  - `MODULE-RECIPE.md` `04925ba82d8c1f69`, read in full
  - MOD-0184 `35bead9735062407` before this lane
  - R-2 REPORT, the worked example
- **Owner decision taken in this lane.** MOD-0184 had no approved UI scope: `shell: none`, and §11 requires a separately
  approved revision. Its scope package was PREPARED/HELD. Asked in chat; the answer was **"Approve both scopes, build"**.
  It is recorded as `docs/records/decisions/2026-10/mvp6-carrier-loads-ui-scope-owner-decision-01.md`.
- **Not touched:** Shipments, Returns, Loads, Claims, S&OP, Capacity, `frontend/Diten.Web/Program.cs`, `ocelot.json`, any
  permission (no widening). Nothing staged or committed. No secret printed. Agent verdict ≠ CT ACCEPTED.

## CT's measurement, verified

| CT said | measured | verdict |
|---|---|---|
| service controller present | `Api/Features/Carriers/CarriersController.cs` | TRUE |
| provider already exists, unregistered | `CarrierManagementManifestProvider.cs`, not in `Program.cs` | TRUE |
| 0 views | 0 under `Views/SupplyChain/Carriers` | TRUE |
| **NO UI draft** | **FALSE.** A complete Carrier UI was written, **built and tested** in September in a codex worktree and rescued into `docs/records/audits/2026-10/mvp6-q246r2-worktree-rescue-01/mvp6-carrier-ui-dev.tar.gz`: 21 UI paths; writer COMPLETE (`mvp6-carrier-ui-quality-close-01`, 14/14 focused tests, frontend build 0/0). There is also a full scope package, `docs/roadmap/plans/mvp6-carrier-ui-scope-01/` | the "unfiltered list" looked only under `docs/records/audits/2026-09/*ui-draft*` |
| 3 keys, all declarable by that provider | read, create, status.change; guard 3/3 | TRUE |
| (implied) the UI is authorized | **FALSE until today**: `shell: none`, §11 "not applicable", scope HELD | owner decision above |

## What was built, in the recipe's order

| step | what | where |
|---|---|---|
| pack | approved scope applied as **§32**, frontmatter `shell: tenant`, `golden_reference: slim`, `form_field_count: 4` | MOD-0184 (`evidence/shared-and-pack.diff`). §32.3's "A changed payload is a new intent and key" was **replaced** by the Q403 definition (recorded in §32 and in the decision) |
| gateway | **none needed**: C-03's catch-all routed every Carrier call | `ocelot.json` untouched |
| UI | the rescued 21 paths placed as new files; **15 of 21 changed** before shipping | `evidence/rescued-to-final.diff` |
| nav keys | already 7/7 at HEAD (R-1 measured them ahead of the UI) | no `SharedResource` edit |
| provider | existing file, unchanged; **`AddSingleton` added** in the same change as the UI (ship rule MOD-0184:473); the stale "Carrier is absent on purpose" comment removed | `SupplyChainService.Api/Program.cs:100-102` |
| guard | Carriers had no `KnownWithoutProvider` entry (its provider already existed); guard green | — |

### What was wrong in the rescued code (each measured, fixed, re-measured)

| # | recipe line | defect | fix |
|---|---|---|---|
| 1 | **none: new** | `ajax: loadRows` (async) made `ajax.reload()` throw after every successful create or status change, so a **committed 201 was reported as "outcome unavailable"** | `ajax: (data, callback) => { void loadRows(data, callback); }` |
| 2 | 2.4 | the adapter actions carried **no `[JsonAdapterEndpoint]`**; unauthenticated adapter calls got 302 to the login page | marker on the 3 `api` actions; pinned by a reflection test |
| 3 | 2.1 | upstream 5xx passed through without an envelope check | `HasContractError` mapping to 503 `PERSISTENCE_UNAVAILABLE` (as Shipments, Q371); 2 tests, 4 cases |
| 4 | 2.3 | 3 log lines without the correlation, nothing on success | every line carries the trace; one Information line per gateway answer |
| 5 | 3.1 / Q403 | key re-minted per payload (`ensureIntent(existing, signature)`), and **after a 409 the intent was nulled, so the next click minted a new key** | key minted when the form or status panel opens; a 409 blocks that form |
| 6 | 3.6 / 3.11 / 4.1 | no visible skeleton; a failed load left the empty table under a write-worded toast | own skeleton id; list surface + denied card + load-failure alert with read wording (`ListUnavailable` ×7) |
| 7 | 5.3 | the bridge was an anonymous object | `Dictionary<string,string>` (54 entries) |
| 8 | 8.3 | the rescued test `KeepsStablePerPayloadIntentAndUsesSharedModalPrimitives` **pinned the defect** | re-pinned to the fix |
| 9 | (F-Q371-4 shape) | the 409 text "The request identity belongs to different content. Start the action again." | the Q403 wording ×7 |

## Golden flow — real user, reload after each step, the database agrees

**Actor.** `r4a-admin`, tenant Admin of STARTER tenant `a84ebe0a…`. The tenant is entitled to `CARRIER-MANAGEMENT`; the token
carries `legal_entity_id` `9ec9e032…`. Holdings: `create`/`read`/`status.change`, all `GrantSource=1 / carrier-management`
(`evidence/carrier-holdings.txt`).

**Registration.** After the `AddSingleton`, SupplyChain self-registered `carrier-management` on attempt 1, and Auth held all
3 `supplychain.carriers.*` keys. Q353 had measured **0**.

| step | UI | after reload |
|---|---|---|
| 1 Create | Add Carrier → `R4A-CARRIER-1`, "R4A Road Freight", mode Road, ext. ref → Save (first on the rescued code: T0) | `R4A-CARRIER-1 … Active` (and `R4A-CARRIER-2`, Air+Parcel, created on the fixed code with "Carrier created successfully.") |
| 2 Suspend | Change Status → offered Suspended/Retired → reason `R4A-AUDIT-HOLD` → confirm | Suspended |
| 3 Reactivate | offered Active/Retired → empty reason (valid per scope) | Active |
| 4 Retire | offered Suspended/Retired → `R4A-END-OF-CONTRACT` | Retired; **no status action** on the row (terminal) |

**Database (`evidence/golden-db.txt`).**
- `R4A-CARRIER-1`: `Retired`, version 4, modes `[Road]`, tenant and LE equal the session.
- 4 audit rows, `null>Active`, `Active>Suspended (R4A-AUDIT-HOLD)`, `Suspended>Active ('')` and `Active>Retired (R4A-END-OF-CONTRACT)`,
  each with its correlation.
- 5 receipts.

**Gateway.** **15** Carrier lines (0 before): 10 GET list, 2 POST create, 3 POST status, all through the catch-all
(`evidence/golden-gateway-lines.txt`).

**Web.** 15 adapter lines, each with its correlation. The create's `b0c8bcde…` is the same id on its audit row, so one action
spans Web, Gateway and the database (`evidence/golden-web-lines.txt`).

## Traps (`evidence/traps-browser.md`)

| trap | produced | fixed behaviour | sabotage, old behaviour back |
|---|---|---|---|
| async ajax option (new) | a successful create | success toast, list reloads | rescued script: "outcome unavailable" after a 201 (twice: T0 and T2) |
| 5xx without an envelope (2.1) | SupplyChain stopped | list: read-worded alert; create: 503 write wording; never validation | rescued controller: 3 unit cases red |
| key per payload (3.1/Q403) | lost response + edited `carrierCode` | 409, **1** carrier, a third click sends nothing | rescued script: new key, **2 carriers from one intent** |
| unchanged retry (3.2/Q393) | lost response + unchanged retry | 201 replay, 1 carrier | n/a: Carrier's receipt has no correlation check (`CarrierRepository.cs:48`) |
| JSON 401 (2.4) | unauthenticated adapter call | 401 JSON with the caller's correlation; page still 302 | before the fix: 302 to login; rescued controller: marker test red |
| skeleton / failed load (4.1, 3.6, 3.11) | SupplyChain frozen | skeleton; then a read-worded alert at 15 s, table hidden, no toast | rescued code under the same freeze: no skeleton, empty table, write-worded toast |
| 7 languages, wire English, Dictionary (5.2/5.3) | culture `ar` | RTL, Arabic labels, `?status=Retired` | — (the lookups go through resx-named keys, so camel-casing could not bite; the Dictionary is defence) |
| UAS-001 | user without keys | only the denied card, 0 adapter calls | — |

## Suites

| suite | result |
|---|---|
| Frontend, final | **282 / 0 / 282** (263 + 19 Carrier tests: the rescued 14 + 2 adapter 5xx tests (4 cases) + marker + list-state pin; one rescued test re-pinned) |
| SupplyChain, Q335 recipe, own mongod 57405 | **432 / 1 / 433** over the module filters (the known Claims restart-mode test); 449 listed |
| Outside the filters: guard, provider and registration tests | **20 / 0 / 20** |

## For R-4c

Claims' carrier path needs `supplychain.carriers.read`. That key now reaches Auth (after registration, 3/3 in the catalogue)
and an entitled tenant's Admin holds it. **R-4c is unblocked** once these changes land in the tree it runs against.

## What the recipe got wrong for Carriers (R-3 input)

| # | recipe line | kind | measured |
|---|---|---|---|
| 1 | 0.7 (drafts) | **MISSED A SOURCE** | prior UI also lives in rescue tarballs (`mvp6-q246r2-worktree-rescue-01/*.tar.gz`), not only in `2026-09/*ui-draft*`. The Carrier one was **built and tested**, more than a draft. 0.7 should list where to look: drafts, rescues, and scope packages under `docs/roadmap/plans/mvp6-<module>-ui-scope-01/` |
| 2 | (none) | **MISSED: authorization** | the recipe assumes the module may have a UI. MOD-0184 (and MOD-0185) said `shell: none` with §11 requiring an approved revision; the scope package was HELD. Step 0 should check "does the pack authorize a UI?" before anything is built |
| 3 | (none) | **MISSED: new defect** | an async `ajax` option breaks `ajax.reload()` and turns a committed write into "outcome unavailable". Paid for here; known to the Returns draft as UI-PM-03 but never a recipe line |
| 4 | 2.4 | **INCOMPLETE** | the line says the marker needs a consumer (Q394 added it). It never says **each module's adapter must carry the marker**; Carrier's did not, and its 401 was a 302 |
| 5 | 3.2 | **BACKEND-SPECIFIC** | the Q393 trap exists only where the receipt compares correlation (Shipments `ShipmentRepository.cs:70`). Carrier's (`CarrierRepository.cs:48`) does not; per-request traces replay fine. The line should say "check the module's receipt rule" |
| 6 | 3.6 / 4.1 | **CONFIRMED, list-level** | both hit again on a list page (the recipe's examples are Details-level); the shared `#skeleton-loader` id is managed by `DtDefaults`, so a page that wants a skeleton must own one |
| 7 | 1.1 | **CONFIRMED** | catch-all routed everything; `ocelot.json` untouched |
| 8 | 0.4 | **CONFIRMED** | the STARTER-plan tenant + entitlement + replay worked first time; the plan id differs per seed |
| 9 | 6.4 / guard | **CONFIRMED** | 3/3 declared; nothing to add |
| 10 | 5.5 | **CONFIRMED (inverse)** | Carrier's nav keys were already 7/7, ahead of its UI (R-1's F-R1-3), so no `SharedResource` edit was needed: the shared-surface table's "7 files per module" does not hold for Carrier |
| 11 | 8.3 | **CONFIRMED again** | a rescued test pinned the per-payload key |

## Findings

- **F-R4a-1 — no approved UI scope existed.** Neither for Carriers nor for Loads. Resolved by the owner decision above.
  The Carrier scope text carried the per-payload intent rule; it was not applied as written.
- **F-R4a-2 — CT's "no UI draft" was wrong for Carriers** (a built rescue exists). The draft search looked in one folder.
- **F-R4a-3 — the rescued UI carried 9 recipe-class defects**, one of them new: the async-ajax false failure after a
  committed write. All fixed and measured; the sabotage reproduced the old behaviour.
- **F-R4a-4 — the 409 message.** The rescued text was cryptic; it now carries the Q403 wording in seven languages. The
  translations are this lane's and want an l10n review, as do the `ListUnavailable` strings.
- **F-R4a-5 — the adapter forwards a missing `X-Correlation-Id` unvalidated** and lets the backend answer 400 (Shipments
  rejects at the adapter, recipe 2.2). Same outcome for the user; recorded, not changed.

## How it was run

- Scratch `~/mvp6-env/r4a-20261004-2103/`: the rescue extracted; a repository copy built there (copy vs repository: 0
  differences at the end).
- **Services:** six services from this copy. Databases: lane mongod 57404 (`rsr4a`), suite mongod 57405 (`rsr4as`); both
  shut down.
- **Tenant:** a STARTER-plan tenant registered through Platform's admin API with a platform-admin token minted into a
  mode-600 file. The fixture created users through Auth's invite endpoint plus the Q185 org fixture. Entitlement and the
  `tenant.activated` replay followed (R-2's method).
- **Browser:** signed in with passwords served by a local helper, so none passed through the transcript; signed out at the
  end. The `fetch` wrappers were test instruments.
- **Sabotage:** the rescued script was served from the scratch copy and then restored (equal to the repository). The
  rescued controller was swapped in only in a separate scratch test build.
- Ports 5000–5061, 5199, 57404 and 57405 are free; Q372-R2's 57373 was never touched.

Nothing committed, nothing pushed, nothing staged.
