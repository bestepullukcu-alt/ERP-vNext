# R-4c — Claims (MOD-0187) UI, end to end, against MODULE-RECIPE.md

- **Lane:** R-4c (ledger Q386, REPLAN Phase 4, the last module): frontend-ui-ux (UI) and integration-agent (provider). Recorded 2026-10-04.
- **Preflight:** `Sun Oct  4 19:47:39 UTC 2026` · `feature/mvp6-logistics` · HEAD `cea01354e` · staged 0.
- **Ports:** Q372-R2's 57373 was not touched (it is still running and is not mine). This lane used 57409 (stack) and 57410 (suite).
- **Step 0 (sha256/16):**
  - `AGENTS.md` `ce8c12ad80f93bb5`
  - `git-safety.md` `4181c697b96dc9f8`
  - `code-style.md` `6032fbb07819dc47`
  - `frontend-ui-ux.md` `90247ddc689b1e06`
  - `integration-agent.md` `f760471e69efb7dc`
  - `docs-organization.md` `0be4f6823776da6e` (K5, K6)
  - `MODULE-RECIPE.md` `36f35a4de2c895b4`, read in full, including "Sealing: two manifests"
  - MOD-0187 `85eb06185409e415`: §22, §32, §33 and the ship rule
  - the R-2, R-4a and R-4b reports
- **Authority:** the UI scope is already approved in the pack (§32, owner decisions of 2026-09-26). No new owner decision was needed.
- **Not touched:** Shipments, Returns, Carriers, Loads, S&OP and Capacity modules, `frontend/Diten.Web/Program.cs`, `ocelot.json` (the draft's route fragment was **not** applied), any permission or AdminModules entry, any service config.
- Nothing staged or committed. No secret printed. Agent verdict ≠ CT ACCEPTED.

## CT's measurement, verified

| CT said | measured | verdict |
|---|---|---|
| service controller present | `Api/Features/Claims/ClaimsController.cs` | TRUE |
| no provider | none in the tree; **the draft carries one** (`ClaimsManagementManifestProvider.cs` and its M-01…M-08 tests) | TRUE for the tree |
| 0 views | 0 under `Views/SupplyChain/Claims` | TRUE |
| 5 keys enforced, 0 declarable | all 5 in `ClaimPermissions`; no provider, so 0 declarable; Auth held 0 | TRUE |
| **only create and read are attached to a controller endpoint** | **FALSE.** `ClaimsController.cs:21` puts `ClaimPermission(Create, Investigate, Decide, Settle)` on `Transition`. `ClaimContextMiddleware.cs:61` reads it as any-of. Then `TransitionClaimHandler.cs:13` requires the exact target key through `ClaimWire.Permission` (`ClaimModels.cs:45`): Investigating/Withdrawn → investigate, Settled → settle, Approved/Rejected/Closed → decide | decide, investigate and settle are enforced on the transition endpoint, in **two** layers |
| **the draft is unbuilt** | **FALSE.** Draft v4 was built, tested and runtime-verified on the Mac by Q129 (`mvp6-q129-ver-claims-v4-01`: Web 246/246, CU-25 en/ar, regression), **CT ACCEPTED 2026-09-27** (`CT-QUEUE.tsv` Q129). The recipe's 0.7 says the same wrong thing | "unbuilt" is the recipe's line, not the record |

### R-4a's five defects, checked in the draft

| R-4a defect | in Claims draft v4 | action |
|---|---|---|
| per-payload key | **PRESENT**: `index.js:541` (create), `:679` (transition); also `:598` dropped the intent after some failures, so the next click minted a key | fixed |
| `ajax.reload` abort → committed 201 shown as "outcome unavailable" | **absent**: the draft already had `ajax: (data, callback) => { void loadClaims(…) }` | kept; pinned |
| missing `[JsonAdapterEndpoint]` → 302 instead of 401 | **absent**: all four adapters carry it | kept; measured 401 |
| skeleton kept hidden by the shared CSS | **absent**: the draft stops DtDefaults' fade and owns `display` (Q64d-D1) | kept; sabotage shows the defect |
| cryptic 409 | **PRESENT**: "This request was already used with different values. Start a new action." | Q403 wording ×7 |

## What was built, in the recipe's order

| step | what | where |
|---|---|---|
| gateway | **none**: C-03's catch-all routed every Claims call and every Shipment lookup; 52 + 48 gateway lines | `ocelot.json` untouched |
| draft | the 23 module paths of draft v4 (`claims-ui-draft-overlay-v4.tar.gz`, `2b34741a…` ✓) placed as new files; **18 of 23 shipped changed** (3 comment-only, 7 resx one string each) | `draft-to-final.diff`, `draft-files-status.txt` |
| Web controller | every failure re-enveloped with the trace: the 18 annex codes pass through; anything else maps by status, and an **un-enveloped 5xx → 503 `CLAIM_STORAGE_UNAVAILABLE`** (2.1). One log line per gateway answer, per shipment lookup and per forwarded mutation, all with the trace and, for mutations, the root (2.3) | `SupplyChainClaimsController.cs` |
| views | the list is wrapped in `claimsListSurface`, with a `claimsListDenied` card for a list 403 (3.6); the bridge is a `Dictionary` (5.3) | `Index.cshtml`, `_IndexL10n.cshtml` |
| scripts | **one key per opened create form or transition panel**, minted on open and kept until close; a 409 blocks the surface (3.1/Q403). A failed list load always uses read wording (3.11); a list 403 draws the denied card. The l10n reader copies keys as they are | `index.js`, `index.l10n.js` |
| module resx | `ErrIdempotencyKeyReused` carries the Q403 wording ×7 (R-4a's Carrier translations, reused) | `ClaimsIndex.*.resx` |
| nav keys | `Nav.Module.CLAIMSMANAGEMENT`, `Nav.Page.CLAIMS` ×7, from the draft's fragments | `SharedResource.*.resx` |
| provider | the draft's `ClaimsManagementManifestProvider` (unchanged apart from comments) **and its `AddSingleton`**, in the same change | `ModuleRegistration/`, `Program.cs` |
| guard | the `["claims"]` entry removed from `KnownWithoutProvider` | `EnforcedPermissionDeclarationGuardTests.cs` |
| tests | 2 draft tests that **pinned defects** re-pinned (`Intent_keeps_key_and_body…` → `One_intent_per_opened_form_or_panel…`; the bridge test now requires a Dictionary); new: list states, 5xx theory (3 cases), published-code pass-through | the three Claims test files |

**Enforced keys declared as actions, as R-2 and R-4b did.** `decide`, `investigate` and `settle` are declared through the
draft's six row actions (INVESTIGATE, WITHDRAW → investigate; APPROVE, REJECT, CLOSE → decide; SETTLE → settle), which is
exactly the pack's §33 action table. Nothing extra was needed, unlike Returns and Loads. After registration Auth held
**5/5** `supplychain.claims.*` keys, against Q353's 0. Sabotage: removing SETTLE fails the guard, which names
`supplychain.claims.settle — enforced at ClaimsController.Transition`, and fails M-03 and M-05. Putting the `claims` entry back
fails `Known_without_provider_entries_are_still_true`.

## Golden flow — real user, reload after each step, the database agrees

**Actor.** `r4c-admin` is the tenant Admin of STARTER tenant `a83097e6…` (plan `3abfbc0f…`, generated per seed). The tenant is
entitled to `SHIPMENT-TRACKING-POD`, `CARRIER-MANAGEMENT` and `CLAIMS-MANAGEMENT`. The token carries `legal_entity_id`
`07d0f6c5…`. Holdings: claims ×5 and carriers ×3 at `GrantSource=1`; shipments ×5 at `GrantSource=0`, through Q357's
temporary entry (`claims-holdings.txt`).

**Carrier path.** The claim names a carrier, so the backend reads `/carriers` with the caller's token, which needs
`supplychain.carriers.read` (R-4a). **No code path writes `Shipment.CarrierId`**: no shipment command does, and Loads'
assignment does not either. So a shipment never has a carrier and the UI never enables "link the shipment's carrier". The lane
therefore set `CarrierId` on two lane shipments directly in the lane database (`carrier-fixture.txt`), as a documented
fixture.

| step | UI | after reload |
|---|---|---|
| 1 Create | Add Claim → shipment `21c7563a…` → Resolve (`SHP-…`, Dispatched, carrier link enabled) → link carrier, reason `DAMAGE-IN-TRANSIT`, `250.00`, `EUR`, evidence `EV-PHOTO-1` → Save | `CLM-3b569dd1…` Open, 250.00, EUR |
| 2 Investigate | row action → panel prefilled `2026-10-04T23:06:37+03:00` → resolution `R4C-INV`, note → shared confirm | Investigating |
| 3 Approve | approved amount `200.00` → confirm; "Approved amount: 200.00" | Approved |
| 4 Settle | the only target, labelled "Settled (operational status, no payment posted)", with the no-payment note in panel and confirm | Settled |
| 5 Close | resolution `R4C-CLOSED` | Closed; **no row action** (terminal) |
| branch | a claim without a carrier (`7da68be6…`, link disabled, `75.5 USD`) → Withdraw (danger-styled) | Withdrawn |

**Database (`golden-db.txt`).**
- `CLM-3b569dd1…`: `Closed`, version 5, `ApprovedAmount 200.00`, `CarrierId 3533de1e…`, evidence `[EV-PHOTO-1]`; tenant and LE equal the session.
- `CorrelationRoot` = the Shipment's `LifecycleCorrelationId` `b43dd46b…`.
- `ReferenceSnapshot`: shipment Dispatched, carrier `Active`, which proves the carrier read.
- 5 audit rows (null→Open→Investigating→Approved→Settled→Closed), each with the root; 5 receipts; outbox Pending.
- `OccurredAt` keeps the typed offset text, e.g. `23:06:37+03:00` against `ReceivedAt 20:06:40Z`.

**Gateway.** **52** requests on `/api/shipment-bundle/claims`, against **0** before the Claims page was used:
- GET: 28×200, 4×502, 2×499;
- POST: 7×201, 8×200, 2×409, 1×422.

Plus **48** Shipment lookups, all through the catch-all (`gateway-claims-lines.txt`).

**Web.** 49 "Claims gateway answered", 18 "Claims mutation forwarded" and 23 shipment-lookup lines; **0 lines without a trace**
(`web-claims-lines.txt`). The create's line binds trace `a8567d0e…` to root `b43dd46b…`, and the same root is on every audit
row.

## Traps (`evidence/traps-browser.md`)

| trap | produced | fixed behaviour | sabotage, old behaviour back |
|---|---|---|---|
| key per opened form, create (3.1/Q403) | lost response + edited amount | 409, **1** claim, a third click sends nothing | draft script: new key, **2 claims from one intent**, "Claim created." |
| key per opened panel, transition | lost response + edited note | 409, one transition | draft script: new key, 422 "the claim has changed in the meantime", about the user's own change |
| unchanged retry (3.2) | lost response + unchanged Save | 201 replay, 1 claim, "already created by the same request" | n/a: the adapter sends the Shipment root, so traces never reach the receipt |
| 5xx without an envelope (2.1) | SupplyChain stopped (gateway 502, empty body) | adapter 503 `CLAIM_STORAGE_UNAVAILABLE` | draft controller: 4 unit cases red |
| read wording (3.11) | same | "Claims could not be loaded." + reference | draft script: "The outcome is not confirmed; try the same request again" on a read |
| skeleton (4.1) | SupplyChain frozen | skeleton at 1.7 s; read-worded error at 15 s; no toast | display override removed: **nothing visible** for 15 s |
| list 403 (3.6) | test-instrument 403 | denied card; surface hidden and inert | draft: error state under the title and filter |
| JSON 401 (2.4) | unauthenticated | 401 JSON on all adapters; page 302 | — (present in the draft; pinned) |
| local clock → instant (3.4) | transitions at UTC+3 | stored instant = real time (−3 s) | UTC wall clock with local offset: **3 h early** |
| 7 languages, wire English, Dictionary (5.2/5.3) | culture `ar` | RTL, Arabic labels, `?status=Investigating`, 109 exact keys | — (draft view turns the bridge test red) |
| UAS-001 | `r4c-nokey` | only the denied card, 0 adapter calls, no redirect | — |
| guard (6.4) | provider added | 33/33 | entry back: 1 red; SETTLE removed: 3 red, settle named |

## Suites

| suite | result |
|---|---|
| Frontend, final | **390 / 0 / 390** = 297 + 93 Claims tests (the draft's 88 cases, with 2 re-pinned, + 5 new cases) |
| SupplyChain, Q335 recipe, own mongod 57410 (`enableTestCommands=1`) | **432 / 1 / 433**, inside the range: Shipments 90/0, Carriers 36/0, Loads 33/0, Returns 78/0, **Claims 128/1** (the known `ClaimReplayTests.Receipt_TwoIndependentTestProcesses_DurableRecovery`, "restart mode required"; **this lane's change did not alter it**: no Claims backend file was edited), S&OP 19/0, Capacity 48/0; 466 listed (`sc-suite.txt`) |
| ModuleRegistration (guard + 5 providers) | **33 / 0 / 33** (28 + 5 Claims provider tests) |
| CompositionRootGuardTests (own mongod) | 2 / 0 / 2 |

## Another lane edited the tree during this one

While R-4c ran, three files outside this lane changed in the working tree: `ClaimReferenceReader.cs` (23:01, comment "Q420"),
`ReturnReferenceReader.cs` (23:00) and `Shipments/create.js` (23:14). The scratch copy was taken before them, and
**its copies of all three equal HEAD**. So every live run and suite above measured the tree **without** those edits.

Q420 changes Claims behaviour: a 403 from the Shipment or Carrier read becomes **403 `FORBIDDEN`** instead of 503
`CLAIM_REFERENCE_UNAVAILABLE`. The page handles a create 403 by closing the form, disabling Add and showing "You are not
authorized for this action.", which fits a missing `shipments.read`/`carriers.read`. But it was **not measured against Q420**.
Re-measure after Q420 lands.

## What the recipe got wrong for Claims (R-3 input)

| # | recipe line | kind | measured |
|---|---|---|---|
| 1 | 0.7 | **WRONG** | "Every draft says not built, not tested, not run" is false for Claims: v4 was built, tested and runtime-verified (Q129, CT ACCEPTED). The draft still shipped 18 of 23 files changed and carried 2 of R-4a's 5 defects plus 4 more. 0.7 should say "verified is not correct; hold it against the recipe anyway". Also, "each latest version is a `.tar.gz` overlay on its v1 folder" is wrong for Claims: v4 is complete (39 files) |
| 2 | 6.4 | **INCOMPLETE** | it says to find enforcement "by reading", which is right: CT's probe returned "only create and read attached". But Claims enforces in **two** layers: the endpoint attribute (any-of) and the handler (exact key per target, `ClaimWire.Permission`). The guard reads only the attribute. It catches Claims' keys only because the attribute happens to list all four mutation keys. If it listed only `Create`, investigate, decide and settle would be invisible to the guard |
| 3 | 7.2 | **MISSED A PRECONDITION** | `carriers.read` is now holdable (R-4a), but **nothing ever writes `Shipment.CarrierId`**, so the carrier path cannot happen on real data and the UI's carrier link is never enabled. The lane needed a DB fixture |
| 4 | 3.1 | **CONFIRMED, worst case** | unlike Loads (assignment uniqueness turned the duplicate into a wrong message), Claims has no constraint: the per-payload key created **2 claims** on one shipment. On a transition it produced a false "changed in the meantime" |
| 5 | 3.2 | **CONFIRMED (by design)** | as Returns: the adapter resolves the root server-side; an unchanged retry replays |
| 6 | 3.4 | **DOES NOT FIT** | Claims' `occurredAt` is text with an explicit offset (pack G-DATETIME), not `datetime-local`. "Non-existent local time" cannot occur. The trap that does is the **prefill** (local digits vs UTC digits next to the local offset: 3 h early, measured). The line should name the prefill, whatever the field type |
| 7 | 3.6 / 3.11 / 4.1 | **CONFIRMED, fourth module** | the draft chose the list error text by code (write wording on a read) and drew a 403 as the error state |
| 8 | 5.3 | **CONFIRMED (defence)** | anonymous bridge; the draft's reader re-capitalized names, so it did not bite on these keys |
| 9 | 8.3 | **CONFIRMED, twice** | two draft tests pinned defects (the per-payload key and the anonymous bridge) |
| 10 | 2.4 | **INCOMPLETE** | the shared JSON challenge answers 401 with `INVALID_REQUEST`; the Claims annex code is `UNAUTHENTICATED`. The page maps by status, so the user is unaffected, but API clients get the wrong code |
| 11 | (none) | **MISSED: shared empty text** | `DtDefaults.create` overwrites a module's `language.emptyTable` with the shared `DtEmptyTable`, so the module's `EmptyState` (pack §32.5) never shows, in any language (Claims, Loads and Returns alike) |
| 12 | 1.1 | **CONFIRMED, with the same flag** | catch-all routes Claims; MOD-0187 §32.10 asks for explicit routes and `/claimsXYZ` unmatched, like MOD-0186. The draft's `gateway-claims-routes.ocelot-fragment.json` was not applied (dispatch) |
| 13 | 0.4 | **CONFIRMED** | STARTER tenant + entitlement + replay worked first time. Entitling `SHIPMENT-TRACKING-POD` did not move Admin's shipment keys off Q357's `GrantSource=0` |
| 14 | Sealing | **APPLIED, and it shows the cost** | R-4c's shared edits (`Program.cs`, guard, `SharedResource` ×7) now fail the seals of R-2 (8), R-4a (1) and **R-4b (9, this session's own record, sealed before K6)**. R-3's 2 failing entries include `MODULE-RECIPE.md`, edited by someone else. This record hashes no shared file in `ARTIFACTS.sha256` |

## Findings

- **F-R4c-1 — the draft was verified, not unbuilt** (Q129, CT ACCEPTED), and still carried 6 recipe defects. Verification
  against its own scenarios did not catch the per-payload key, because its test pinned it.
- **F-R4c-2 — CT's enforcement reading was wrong**: decide, investigate and settle are enforced on the transition endpoint and
  in the handler (table above).
- **F-R4c-3 — `Shipment.CarrierId` has no writer.** The claim carrier path, and Claims' carrier mismatch checks, cannot occur
  on real data. Owner of Shipments/Loads to decide where a shipment gets its carrier.
- **F-R4c-4 — the shared empty-table text overrides every module's empty state** (`dt-defaults.js:518-523`). Shared file, not
  touched.
- **F-R4c-5 — Q420 and two other edits arrived mid-lane**; the measured tree predates them (section above).
- **F-R4c-6 — translations:** the Claims Q403 strings reuse R-4a's Carrier translations; the draft's other 94 keys ×6 were
  not re-reviewed. Both want an l10n review.
- **F-R4c-7 — process slip:** `start.sh` truncates the service log on restart, so the first SupplyChain run's log (golden
  flow) was overwritten when SupplyChain was restarted for the skeleton trap. Gateway, Web and database evidence are intact.
  No `rm` and no `mv` in this lane.

## How it was run

- **Scratch:** `~/mvp6-env/r4c-20261004-2248/`. Draft v4 extracted after its hash was verified. A repository copy (without
  `.git` and `docs/records/`) was built there. Copy vs repository at the end: 0 differences over every path this lane changed;
  the 3 other-lane files are listed above.
- **Services:** six services from the copy on lane mongod 57409 (`rsr4c`, fresh JWT secret, mode 600). All stopped by pid;
  mongods 57409 and 57410 shut down. 57373 untouched.
- **Tenant:** STARTER tenant through Platform's admin API (platform-admin token minted into a mode-600 file); users invited
  through Auth; org fixture (Q185 method); entitlements and the `tenant.activated` replay.
- **Reference data:** through the Gateway as the Admin: one carrier, seven shipments, six of them Draft → Planned →
  Dispatched with their lifecycle root (`refs.json`). Then the documented `CarrierId` fixture on two shipments.
- **Browser:** passwords served by a local helper, never printed; signed in as `r4c-admin` and then `r4c-nokey`; signed out
  at the end.
- **Sabotage:** four scripts were served from the scratch copy and then restored; the controller, bridge and guard
  sabotages ran in a separate scratch copy.

## Sealing (MODULE-RECIPE "Sealing: two manifests", docs-organization K5/K6)

- `ARTIFACTS.sha256` covers only this folder's own files, from the repository root, and must pass `shasum -c` for good.
- `SOURCE-AS-MEASURED.sha256` covers the 32 repository paths this lane changed, hashed at measurement time. **It is not for
  verification.** The shared ones in it will change with the next lane.

Nothing committed, nothing pushed, nothing staged.
