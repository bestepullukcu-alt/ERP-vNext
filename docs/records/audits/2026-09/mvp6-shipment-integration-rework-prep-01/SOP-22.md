# MVP6-SHIPMENT-INTEGRATION-REWORK-PREP-01 — SOP §22

**Date:** 2026-09-24  
**Role:** orchestrator / integration preparation  
**Repository:** `/Users/natig/Projects/ERP-vNext-recovery`  
**Branch / HEAD:** `feature/mvp6-logistics` / `4a8d4d4b339528a88e6220fb8402e5a2c771136c`  
**Verdict:** **READY FOR EXACT OWNER DECISION; APPLICATION HELD**

## Scope and controlling evidence

This successor starts from the already-applied Shipment UI target at `/private/tmp/mvp6-shipment-pod-ui-exec-01`. Its controlling blocker record is `mvp6-shipment-pod-ui-exec-01/BLOCKER-DISPOSITION.tsv`. The 28 UI-owned files are preserved byte-for-byte; `UI-PRESERVATION.tsv` records 28/28 matches in both the applied target and disposable successor.

The accepted source provenance is the BC successor archive and its 422-entry manifest listed in `INPUTS.tsv`. That source set was previously independently verified and narrowly accepted; this lane does not extend that acceptance or call it integrated runtime acceptance.

## Root cause and disposition

| Finding | Exact cause | Candidate disposition | Status |
|---|---|---|---|
| B-01 / 23 CS0234 | Applied `Program.cs` references six accepted feature namespaces, registrations, middleware and readers, but 258 files from the accepted 422-entry BC source set are absent. | Transfer only those 258 absent files. Preserve 161 identical files and the three later Shipment successor files. | Disposable GREEN; target application requires the exact owner decision. |
| B-02 / Carrier port drift | The CRM migration correctly moved 35 `/api/crm/**` routes to 5065 but also moved two `/api/shipment-bundle/carriers*` routes owned by SupplyChain. | Two route values return to 5061; route-owner tests bind 35 CRM routes to 5065 and all six ShipmentBundle routes to 5061. | Disposable GREEN; two-file patch remains unapplied. |
| Stale gateway port allowlist | The existing generic test did not list already-routed Human Capital 5063, Talent Ecosystem 5064, or CRM 5065. | Add the three documented ports and exact CRM/SupplyChain ownership assertions in the same test file. | Disposable GREEN. No route was changed for 5063/5064. |

`DEPENDENCY-CLOSURE.tsv` maps every CS0234 namespace family to `Program.cs` references and the accepted production source count. The selected closure has 202 production feature files and 56 accepted test/probe files among the 258 absent inputs. It does not overwrite any present file.

## Option decision

`OPTIONS.tsv` compares the alternatives. The single recommendation is the absent-only accepted source transfer. Removing or conditionally suppressing existing Program registrations would discard accepted module behavior merely to make the compiler pass. Reconstructing a new narrow composition would duplicate a source closure already accepted and independently verified.

The proposed transfer is immutable in `BC-SOURCE-CLOSURE.tar.gz`; its 258 members are bound in `SOURCE-TRANSFER-MANIFEST.tsv`. The complete 422-row classification is `SOURCE-CLOSURE-PLAN.tsv`:

| Classification | Count | Effect |
|---|---:|---|
| `TRANSFER` | 258 | Absent accepted files to add |
| `PRESERVE_IDENTICAL` | 161 | Already identical; no write |
| `PRESERVE_SUCCESSOR` | 3 | Later csproj, Program.cs and appsettings bytes retained |

## Gateway successor

The unapplied patch touches exactly two files. `SUCCESSOR-MANIFEST.tsv` is the baseline-to-target authority candidate:

| File | Baseline SHA-256 | Target SHA-256 |
|---|---|---|
| `gateway/Diten.ApiGateway/ocelot.json` | `77363833c9ee973db6af6e798141afe9843515a324b4b00b37d86a80709d6618` | `67060bf9bd38c62623652a521ce79ddf3aed26a73b65c7fb9eb38f513018e9a7` |
| `gateway/Diten.ApiGateway.Tests/OcelotConfigurationTests.cs` | `2e64c273310391268d37f4b1fc1abc4976dab38854057d0f8e16286d47581dfa` | `efcc548efe58845d9f7bae28dd61a8ccdcca8e85e3d80fbbd689d3cef92e538f` |

The patch SHA-256 is `b3d0cd5b00a1c24819df765c3d1b834cd06dc1d306de1c20799616d9bcd191fd`. It was applied from those preimages in a second disposable directory and reproduced both target hashes. `ROUTE-OWNERSHIP.tsv` records all 41 relevant routes individually: 35 CRM routes at 5065 and six ShipmentBundle routes at 5061.

## Disposable verification

| Verification | Result | Evidence |
|---|---|---|
| SupplyChain fresh build after source closure | PASS, 0 warnings / 0 errors | `raw/supplychain-build.log` |
| Gateway fresh build | PASS, 0 warnings / 0 errors | `raw/gateway-build.log` |
| CRM fresh build | PASS, 2 inherited warnings / 0 errors | `raw/crm-build.log` |
| Focused gateway route suite | PASS, 22/22 | `raw/shipment-integration-route-tests-final.trx` |
| UI source preservation | PASS, 28/28 | `UI-PRESERVATION.tsv` |
| Patch reproducibility | PASS | `SUCCESSOR-MANIFEST.tsv` and second disposable apply |

The first gateway test attempt was blocked by sandbox test-host socket restrictions. The next native attempt exposed the stale 5063/5064 allowlist; both records are retained rather than rewritten. The final test used the exact candidate source and passed. No HTTP/JWT/browser, runtime process, database or rollout acceptance is claimed.

## Authority boundary and next step

Existing Shipment execution authority covered the earlier 40 backend inputs, 18 Carrier predecessors, 28 UI paths, CRM migration and then-approved Shipment shared patch. It did not authorize transferring these 258 additional bytes to this target or changing the two gateway files to the new target hashes. Therefore application is **HELD**.

`OWNER-DECISION-PACK.md` is the smallest concrete decision: authorize the exact absent-only archive plus the exact two-file patch, with fail-closed preimage checks. After a real owner decision, one integration writer may apply them; then a different agent runs `INDEPENDENT-VER-HANDOFF.md`.

## No-change statement

No integration checkout, UI/product source, Auth lane, canonical contract, guard, pack, Git state or rollout state was changed. Repository writes are confined to this audit directory. This preparation is not an owner decision, DEV GO, runtime acceptance or full-module acceptance.
