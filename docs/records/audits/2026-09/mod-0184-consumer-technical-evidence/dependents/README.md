# Dependent consumer technical evidence — MOD-0185/0186/0187

2026-09-16. **Static compatibility PASS for unchanged owned surfaces; runtime consumer conformance NOT APPLICABLE to absent local implementations.** No owner consent, publication, uptake, runtime acceptance or development authorization is asserted.

## Exact subject and reproducibility

`results.json` records SHA-256 of canonical 1.0.0, candidate YAML, exact publication patch, annex, proposed publication hash file and three draft packs. Patch SHA-256 is `2cbcf65b4909419a2d0a79d688b3ed95ee33f0993fc666ef5c5edda246f0920f`. Proposed published YAML remains `8954f8af0024fe6c31f1cc49dc717a408d7d48e511fbacb4571d7887c4fac6a4`; canonical source remains `f6415bbfda42a61a9845e7e1fc843be087bf249cac6bcdc9cb678284766450a1`. These checks compare candidate semantics; they do not publish the proposed output.

Run from the repository root:

```sh
/private/tmp/mod0184-oas-tools/bin/python docs/records/audits/2026-09/mod-0184-consumer-technical-evidence/dependents/verify.py
```

The script needs PyYAML and `rg`; outputs only its own directory. It inventories 12,541 rg-visible files across services, frontend, gateway, scripts, tests, events and observability. It runs case-insensitive text searches for bundle names, carrierId, /carriers and Carrier operation names. A second active-repository search includes docs and execution to identify planned dependencies. Commands, exclusions, result counts and matched paths are saved in `results.json`; line-numbered matches in `*-matches.txt`; the enumerated runtime file set in `runtime-file-inventory.txt`.

Discovery intentionally honors rg ignore rules and excludes generated bin/obj, node_modules, bundled libraries, minified JS/maps, package-lock, historical audit records/archive and logs. This is a local source inventory, not proof about ignored/binary artifacts, unpublished repositories, deployed systems, external generated SDKs or remote clients. Full repository filenames matching Carrier/LoadPlan/Reverse/Claim were additionally inspected: CRM ContentComposition Claims and Platform task claiming are unrelated capabilities, not Shipment Claims. No generated Carrier SDK, executable Carrier mock, Carrier HTTP client or MOD-0185/0186/0187 implementation was found in the searched source scope. No absent consumer was invented to manufacture executable signoff.

## Module dispositions

| Module | Measured current state | Candidate effect | Technical disposition / remaining future check |
|---|---|---|---|
| MOD-0185 Routing/Load | Pack `draft` line 9; line 35 consumes Carrier; lines 72/98 require an existing Active Carrier. No local runtime implementation located. | `/loads` surfaces and all existing schema/component objects unchanged. Required Carrier reference still future dependency. | Static owned-surface compatibility PASS. At implementation, use published Carrier response/error/header matrix; same-scope Active lookup and failure handling must be tested. No current consumer runtime test to execute. |
| MOD-0186 Reverse Logistics | Pack `draft` line 9; lines 34/68–74 consume Shipment, Inventory and warehouse seam, not Carrier. | `/returns` surfaces/schema/events unchanged. | Static owned-surface compatibility PASS; no direct Carrier uptake identified. Future Shipment/Inventory regression tests required by its pack, not fictitious Carrier replay tests. |
| MOD-0187 Claims | Pack `draft` line 9; lines 34/72 optional Carrier reference; line 97 requires reference match/relation to Shipment. No local runtime implementation located. | `/claims` surfaces/schema/events unchanged; Carrier identifiers unchanged. | Static owned-surface compatibility PASS. Future optional reference validation must obey tenant/LE isolation and Carrier error handling; no Carrier mutation/replay dependency should be assumed. |
| MOD-0183 | Implemented Shipment controller, domain reference, projection, lifecycle serialization and runtime tests found. | Non-Carrier path and shared component invariance PASS. | Separate MOD-0183 technical lane owns its runtime/conformance verdict. Broad bundle middleware remains a future composition boundary; this lane does not certify it. |
| MOD-0140 / other local runtime roots | No Carrier/bundle consumption matches outside SupplyChain in searched runtime roots. | No demonstrated contract delta dependency. | No new local runtime consumer located; no external/no-additional-consumers owner attestation implied. |

Pack references are relative to `execution/domains/supply-chain-execution/module-packs/`:
- `MOD-0185-routing-load-planning.md`
- `MOD-0186-reverse-logistics.md`
- `MOD-0187-claims-management.md`

The 17 runtime matches all belong to MOD-0183. Source evidence includes `Shipment.cs:16` (nullable opaque CarrierId), `ShipmentProjection.cs:13`, `ShipmentRepository.cs:95`, `ShipmentsController.cs:10`, `ShipmentContextMiddleware.cs:9` and existing Shipment test clients. The middleware uses a bundle-wide prefix; future Carrier composition must isolate its semantics as already required by the owner package. An opaque CarrierId property is not an implemented Carrier client.

## Independent structural comparison

Fresh parsing of canonical and candidate YAML found only `/carriers` and `/carriers/{carrierId}/status` path objects changed (three operations). All ten non-Carrier path objects are structurally identical; every previously existing component object is identical; no path is removed or newly added. Load/Return/Claim success schemas, request schemas, lifecycle events and existing shared responses therefore remain unchanged. Assertions are implemented in `verify.py`, not inferred merely from the earlier publication report.

This supports the bounded technical claim that the proposed Carrier amendment does not change these modules' owned wire surfaces. It does not establish compatibility of future clients' assumed error/replay behavior. Any generated all-bundle client must be generated from the approved version, compiled and checked for added Carrier response statuses and headers when such a client exists. References to an Active carrier in MOD-0185 and optional carrier association in MOD-0187 are planning requirements, not deployed consumers or implicit owner approvals.

## Handoff and limitations

Evidence delivered: this report, executable discovery/comparison script, two match logs, full runtime filename inventory and machine-readable results. Script first run failed before reads/writes due an incorrect repository-parent calculation; corrected locally, final execution exited 0. No service build, network HTTP, Mongo, failure injection or draft-module execution occurred. Existing canonical contract, packs and publication artifacts were read only. Only this newly owned directory was written; no git mutation occurred.

Publisher must still identify external consumers and receive accountable owner decisions. These technical findings reduce the concrete question for draft modules to planned dependency review; they do not close GAP-184-04/05 or authorize freeze, promotion or runtime dispatch.
