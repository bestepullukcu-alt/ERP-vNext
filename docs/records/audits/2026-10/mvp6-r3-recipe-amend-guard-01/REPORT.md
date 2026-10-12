# R-3 (Q386, Q398) — the recipe amended from R-2's cost, and the enforced-key guard for every module

- Lane: R-3, REPLAN Phase 3 merged with the Q398 guard. Roles: documentation-writer (part A), integration-agent
  (part B). Recorded 2026-10-04.
- Preflight: `Sun Oct  4 16:46:04 UTC 2026` · `feature/mvp6-logistics` · HEAD `983dcd4d0` · porcelain 28 · staged 0.
  Q372-R2's mongod held 57373; it was not touched. This lane's suite used 57398.
- Step 0 (sha256/16):
  - `AGENTS.md` `ce8c12ad80f93bb5`
  - `git-safety.md` `4181c697b96dc9f8`
  - `code-style.md` `6032fbb07819dc47`
  - `documentation-writer.md` `d2c09df48e8f9bbb` (first read in this session)
  - `integration-agent.md` `f760471e69efb7dc`
  - `MODULE-RECIPE.md` `49691537338d639e` (before)
  - R-2 `REPORT.md` `7d69c72129471b07`
- **Written:**
  - `docs/roadmap/plans/mvp6-process-pilot-01/MODULE-RECIPE.md` (amended);
  - one new test file,
    `services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/ModuleRegistration/EnforcedPermissionDeclarationGuardTests.cs`.
- **Not touched:** `frontend/**`, Shipments' controller, any provider, Loads/Claims/S&OP/Capacity code, the §32.10
  catch-all question. Nothing staged or committed. Agent verdict ≠ CT ACCEPTED.

## Part B — the guard

### How the enforcing attributes were enumerated (by reading, not guessing)

1. **Every attribute class** declared in `services/Diten.SupplyChainService/src`: `grep -rn --include='*.cs' -E 'class \w+Attribute\b'`.
   Seven permission attributes, one per module plus the shared one:
   - `HasPermissionAttribute` (`Infrastructure/Authorization`)
   - `ReturnPermissionAttribute`
   - `LoadPermissionAttribute`
   - `CarrierPermissionAttribute`
   - `ClaimPermissionAttribute` (params)
   - `SandopPermissionAttribute`
   - `CapacityPermissionAttribute`
2. **Every usage on a controller:** `grep -noE '(HasPermission|[A-Z][a-z]+Permission)\([^)]*\)'` over `*Controller.cs`.
   - A first usage grep that required `[` directly before the name found only `HasPermission`, because the module
     controllers write `[HttpPost(...), ReturnPermission(...)]`.
   - **That miss is the same class of error as CT's first probe.** It was caught here because step 1 listed seven
     classes and the usage grep showed only one.
3. **The test does not depend on either grep.** It reads `CustomAttributeData` on every `[Http*]` action and controller
   class, takes every attribute whose type name ends in `PermissionAttribute`, and reads its constructor arguments
   (a string, or a `params string[]`). Two more tests guard the enumeration itself:
   - `Every_routed_endpoint_carries_a_recognised_permission_attribute`: an endpoint whose attribute does not follow
     the naming would otherwise add no keys, and the guard would pass vacuously.
   - `Enumeration_finds_every_module_attribute`: all seven attribute types are seen.

### CT's measurement, verified (by the guard's own enumeration, `evidence/counts.txt`)

| module (key area) | enforced | declared | CT said | verdict |
|---|---:|---:|---|---|
| shipments | 5 | 5 | 5/5 | TRUE |
| returns | 3 | 3 | 3/3 after R-2 | TRUE |
| carriers | 3 | 3 | 3/3 | TRUE (the provider exists, unregistered by ship rule) |
| loads | 3 | 0 | 3/0 | TRUE |
| claims | 5 | 0 | 5/0 | TRUE |
| **sandop-plans** (S&OP) | **4** | **0** | — | **CT missed it** |
| **capacity-plans** | **4** | **0** | — | **CT missed it** |

**S&OP and Capacity carry the same ship rule** (`MOD-0190-sop-workflow-signoffs.md:552`, `MOD-0192-capacity-planning.md:574`),
so the named exception holds **four** modules, not two. With only Loads and Claims listed, the guard would have failed on
S&OP and Capacity at once. The completeness test was what made those two visible.

### The control (`EnforcedPermissionDeclarationGuardTests.cs`, 4 tests, pure reflection, no Mongo)

- **`Every_enforced_key_is_declared_by_its_module_provider`.** Groups enforced keys by area (`supplychain.<area>.…`).
  For every area not listed as known, each enforced key must be declared by some provider, as a page's
  `RequiredPermission` or an action's `PermissionKey`. A provider counts whether or not `Program.cs` composes it, since
  the ship rule withholds the `AddSingleton`, not the declaration.
- **`Known_without_provider_entries_are_still_true`.** The self-invalidating exception. It lists `loads`, `claims`,
  `sandop-plans` and `capacity-plans`, each with its `ModuleCode` from the pack and its ship-rule line. It fails if:
  - a provider with that `ModuleCode` exists;
  - or any provider declares a key of that area;
  - or no endpoint enforces that area any more.

  This is the Q366 `KnownUncomposed` pattern.
- **`Every_routed_endpoint_carries_a_recognised_permission_attribute`** and **`Enumeration_finds_every_module_attribute`**:
  guards on the enumeration, as above.

### Sabotage (§24.2), in a scratch copy (`evidence/guard-sabotage.txt`)

| run | change | result |
|---|---|---|
| fixed | — | 4/4 |
| **1** | Returns' `CHANGE_STATUS` action removed | **red**: `supplychain.returns.transition — enforced at ReturnsController.Transition, declared by no provider` |
| **2a** | a dummy `ScratchOnlyLoadsProvider` (`routing-load-planning`, no pages) | **red, the exception test itself**: `'loads': ScratchOnlyLoadsProvider now provides routing-load-planning — remove the entry; its enforced keys must be declared (MOD-0185:602 …)` |
| 2b | the dummy declaring `supplychain.loads.read` | red, same message |
| **2c** | the `loads` entry removed, as the message instructs | **red, the main guard**: `supplychain.loads.create` and `supplychain.loads.transition` declared by no provider. The guard forces the keys in |
| restored | the scratch copy equals the repository (rsync: 0 differences; the dummy file emptied to a comment, no `rm`) | guard + every `*ManifestProvider*` test **18/18** |

Run 2a shows a provider is caught by its `ModuleCode` even when it declares nothing, so a module cannot slip a provider
in ahead of its UI without the entry failing.

## Part A — R-2's 15 recipe items: applied or rejected

| # | R-2 item | decision | where in the recipe / why |
|---|---|---|---|
| 1 | 1.1 gateway wrong for the family | **APPLIED** (CT A1) | 1.1 rewritten (family already routed by C-03's catch-all; measure through the gateway, not by a name grep). The shared table now strikes `ocelot.json`. **§32.10's explicit-routes/no-catch-all flagged, not resolved** |
| 2 | 6.4 wrong lesson | **APPLIED** (CT A2) | 6.4 rewritten: every enforced key must be declarable; the per-module attribute names; status GUARDED by Part B |
| 3 | 0.4 understated | **APPLIED** (CT A4) | 0.4: the STARTER-plan tenant, entitlement call, `tenant.activated` replay, fixture in that tenant; `QUOTA_CONFIGURATION_MISSING` cited |
| 4 | drafts never pointed at | **APPLIED** (CT A3), extended | new 0.7: which modules have drafts and where (Returns v2, Claims v4, Capacity v3, S&OP v3; none for Carriers and Loads), all UNBUILT. **R-3 measured statically that the Claims, Capacity and S&OP drafts carry the per-payload key code** |
| 5 | 3.1 vs the pack | **APPLIED**, extended | 3.1 cites the **four** pack lines that say the opposite (MOD-0186:712, MOD-0187:664, MOD-0190:394, MOD-0192:411); CT must amend them |
| 6 | 3.2 / Q393 phrasing | **APPLIED** | 3.2 reworded to "one stable correlation per intent"; status OPEN for Shipments (Q393), CLOSED by design for Returns, with R-2's replay measurement |
| 7 | 3.4/3.5 shared helper cost | **APPLIED** | 3.4 records that the functions were copied; 3.5 now **OPEN for Returns** (no Node zone test inside an IIFE) |
| 8 | 5.3 "partly redundant" | **REJECTED** | R-2 proposed adding "the page must look up exactly the names the resx defines". No defect was paid for it in Returns: the draft's normalizer already defended, and the Dictionary was applied anyway. Under the recipe's own rule a line with no paid-for defect does not go in. 5.3 stays as written |
| 9 | read failures use write wording | **APPLIED** | new 3.11, attributed to F-Q371-5 and F-R2-5, OPEN in both modules |
| 10 | 2.3 ill-defined for root-carrying adapters | **APPLIED** | 2.3's record column: one create is two ids at the gateway; the Web line binds them |
| 11 | 5.5 confirmed | **APPLIED** (confirmation) | 5.5's record column |
| 12 | pack section mapping | **APPLIED** | new 0.8: read the pack by topic; MOD-0186's section lines cited |
| 13 | 8.1 confirmed | **APPLIED**, extended | 8.1 now also requires the packs and contracts for SupplyChain tests: **this lane paid for it** (3 false failures in `ShipmentTrackingPodManifestProviderTests`) |
| 14 | 2.4/2.5 worked | **APPLIED** (status) | both now WORKTREE (Q394, `W/Program.cs` uncommitted), measured for Returns too; the shared table says Q394 must land before R-4 relies on it |
| 15 | 0.1/0.2 worked | **APPLIED** (confirmation) | 0.1's record column |

Also applied from R-2's findings, outside the 15 items:

- **7.1** now carries F-R2-9: Admin's `shipments.read` comes only from Q357's entry, which expires 2026-11-03.
- **The shared-surface table was rewritten from R-2's actual diff:**
  - the SupplyChain `Program.cs` (+2 lines) stays;
  - the seven `SharedResource` files (+2 lines × 7) stay;
  - **new:** the guard's `KnownWithoutProvider` dictionary, since each R-4 module deletes its own entry;
  - `ocelot.json` and the Web `Program.cs` are struck;
  - `DefaultRolePermissionTemplate.cs` is noted as not needed on the entitlement path.

**Lines removed for lack of attribution:** none. Every amended line cites R-2's record, this record, or both.

## Suites

| suite | result | note |
|---|---|---|
| SupplyChain, Q335 recipe, own mongod 57398 | **432 / 1 / 433** over the seven module filters (Shipments 90, Carriers 36, Loads 33, Returns 78, Claims 128/1/129, S&OP 19, Capacity 48) | identical to Q335/R-2; the one failure is the known Claims restart-mode test `ClaimReplayTests.Receipt_TwoIndependentTestProcesses_DurableRecovery`. Read as a range (Q361). 449 listed = R-2's 445 + 4 guard tests (`evidence/sc-suite.txt`) |
| Outside the module filters: guard (4) + every `*ManifestProvider*` test (Returns 10, Shipments and Carriers 4) | **18 / 0 / 18** | `evidence/guard-sabotage.txt` |
| Frontend | **not re-run** | this lane changed no frontend file; the last measurements stand (Q394 164/0/164, R-2 263/0/263) |

## Findings

- **F-R3-1 — CT's guard scope missed two modules.** S&OP (4 keys) and Capacity (4 keys) also enforce keys with no
  provider. The exception lists four modules.
- **F-R3-2 — every remaining draft carries the per-payload key.** Static reading:
  - Claims `index.js:541,679`;
  - Capacity `index.js:217`, `details.js:616`;
  - S&OP `index.js:198`, `details.js:510`.

  All are inside the v4/v3 tarballs. Their packs require it (four lines). R-4 must not ship any draft script unchanged.
- **F-R3-3 — the draft providers reference every enforced key constant** (Claims 5, Capacity 4, S&OP 4), by static
  reading. The guard will prove it when R-4 builds each one.
- **F-R3-4 — my first usage grep had CT's blind spot** (Part B step 2). It was recorded rather than hidden, because it is
  the reason the test reads attributes generically.
- **F-R3-5 — the copy trap, in a third form.** A SupplyChain test copy without the packs gives 3 false failures (recipe 8.1
  amended).

## How it was run

- Scratch `~/mvp6-env/r3-20261004-1951/`: a source copy of the SupplyChain service and its shared projects (plus the
  packs and contracts after F-R3-5), built there.
- Sabotage edits were made only in the copy. It was checked equal to the repository afterwards (0 differences).
- The three latest draft tarballs (Claims v4, Capacity v3, S&OP v3) were extracted there for the static scan.
- Suite mongod 57398 (`rsr3s`), shut down after the run. Q372-R2's mongod on 57373 was never touched.
- A scratch-only diagnostic test wrote `evidence/counts.txt`. It was never in the repository, and the copy's test file was
  restored to the repository version.

Nothing committed, nothing pushed, nothing staged.
