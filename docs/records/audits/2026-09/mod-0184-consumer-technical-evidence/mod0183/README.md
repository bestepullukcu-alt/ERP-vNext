# MOD-0183 technical compatibility evidence — SHIPMENT-BUNDLE 1.1.0 proposal

2026-09-16 · Agent technical verdict: **PASS for unchanged bounded MOD-0183 contract projection; future Carrier composition requires regression verification.** This is technical evidence, not consumer-owner approval, publication authority, runtime conformance, GAP closure or CT acceptance.

## Reviewed target and method

Branch `feature/mvp6-logistics`, HEAD `4a8d4d4b339528a88e6220fb8402e5a2c771136c`. Read the approved MOD-0183 pack, publication package/annex and actual service implementation. Existing dirty MOD-0184 pack and preparation records were preserved. Only this new evidence directory was written.

The independent [comparison script](compare.py) checks all 12 publication manifest entries, applies the exact patch to a disposable copy, checks both proposed published hashes, validates resulting OpenAPI, compares every pre-existing component and every non-Carrier path, and recursively compares the Shipment operations' referenced schema/response/parameter closure. It does not call or import the publisher's validator.

Exact patch SHA-256: `2cbcf65b4909419a2d0a79d688b3ed95ee33f0993fc666ef5c5edda246f0920f`.
Canonical baseline SHA-256: `f6415bbfda42a61a9845e7e1fc843be087bf249cac6bcdc9cb678284766450a1`.
Proposed published YAML: `8954f8af0024fe6c31f1cc49dc717a408d7d48e511fbacb4571d7887c4fac6a4`.
Proposed published annex: `87557ef9f5b5a4427862effbece2bb528ebc1b95361c29416373709223021fee`.

## Fresh evidence

| Check | Result |
|---|---|
| Publication package hashes | 12/12 match |
| Temporary published YAML and annex hashes | 2/2 match |
| Resulting published-shape OpenAPI 3.1 | PASS |
| Non-Carrier path objects | 10/10 semantically identical |
| Shipment paths | All four path objects identical |
| Shipment transitive local references | 22/22 identical |
| Every existing component object | Identical; added Carrier responses do not alter shared definitions |
| OpenAPI version, servers, security, tags, webhooks | Identical |
| Existing SupplyChain source files/canonical YAML | Unchanged during comparison |

Full measured values and source hashes: [results.json](results.json). Dictionary comparison checks parsed structural identity; it does not assert whole-file byte identity (info/version and Carrier operations deliberately change).

Reproduce from repo root using the already available temporary tooling environment:

```sh
/private/tmp/mod0184-oas-tools/bin/python docs/records/audits/2026-09/mod-0184-consumer-technical-evidence/mod0183/compare.py
```

Dependencies: PyYAML, openapi-spec-validator. The local LibreSSL urllib3 warning did not affect offline validation. Initial script execution failed because the root-parent index was one too high; corrected before the successful run recorded above. No source, contract or package artifact was modified.

## Actual MOD-0183 dependency and composition findings

Source inspection (`rg -n 'carrier|Carrier|HttpClient|shipment-bundle' services/Diten.SupplyChainService/src --glob '*.cs'`) found:

- `Domain/Features/Shipments/Shipment.cs:16` stores nullable opaque `CarrierId`; `Application/Features/Shipments/ShipmentProjection.cs:13` projects it and `Persistence/Features/Shipments/ShipmentRepository.cs:95` puts it in lifecycle event payloads. These shapes are unchanged in the proposal.
- `Api/Controllers/ShipmentsController.cs:10` routes Shipment operations only. No Carrier controller/client exists in this service. The only typed external HTTP clients found are WarehouseReadClient and InventoryReadClient (`Infrastructure/SourceIntake/ReadOnlySourceClients.cs`). Thus MOD-0183 has no measured runtime dependency on new Carrier error/status/header/replay behavior.
- The proposal is not a source deployment and this service does not generate routes from the modified YAML on startup. Contract publication alone does not activate Carrier behavior.

**Future composition conditions (not fixed here):**

1. `Api/Middleware/ShipmentContextMiddleware.cs:9` intercepts the entire `/api/shipment-bundle` prefix. It validates correlation before auth, rejects nil UUIDs (:11), checks trusted/scope headers, and trims/rejects whitespace-only idempotency keys (:31–34). Carrier annex precedence and valid input semantics differ. A Carrier controller simply mounted behind this middleware would not conform. An approved future composition change must isolate Carrier handling while preserving existing Shipment behavior.
2. `Api/Program.cs:45` configures the global invalid-model response factory using Shipment `RequestContext`. Future Carrier schema errors must use the approved Carrier correlation/error policy without altering Shipment responses. Authentication → Shipment middleware → authorization ordering (:51–53) must be considered explicitly.
3. `Persistence/Features/Shipments/ShipmentRepository.cs:54–58` rejects different-correlation replay and returns 200 for a Shipment create replay. Carrier proposal allows current response correlation while preserving original audit root, with original 201/200 replay status. Do not reuse or globally change Shipment receipt semantics for Carrier; separate persistence/replay behavior is required by the approved design.

These are concrete future integration conditions, not evidence that the unchanged Shipment projection breaks. When Carrier composition is implemented, rerun Shipment HTTP/security/replay regression probes and add cross-route precedence/isolation cases before declaring composed-service compatibility.

## Limits and owner handoff

This run executed specification comparisons and an OpenAPI validator; it did **not** start a service, execute Mongo transactions, replay runtime captures, rebuild the service, or claim new E4 evidence. Existing bounded MOD-0183 acceptance remains historical and was not reissued. No expensive runtime rerun was warranted because no runtime source or Shipment schema changed.

Technical conclusion: no MOD-0183 wire-schema delta or existing Carrier-client dependency was found in this repository at the measured HEAD. This supports owner review of the exact 1.1.0 proposal for the current bounded Shipment consumer. It does not certify external consumers, future generated SDKs, Carrier implementation or integrated middleware composition. Publisher-owned consumer inventory and owner decisions remain necessary. GAP-184-04/05 stay BLOCKED until authorized publication and uptake; no runtime GO is issued.
