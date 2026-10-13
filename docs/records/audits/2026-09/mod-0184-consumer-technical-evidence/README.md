# MOD-0184 consumer technical evidence — 2026-09-16

WP: MVP6-CONSUMER-TECH-01. Three bounded agent lanes, independently rerun by CT.
Target: exact unpublished SHIPMENT-BUNDLE 1.1.0 proposal.
Branch: feature/mvp6-logistics; HEAD: 4a8d4d4b339528a88e6220fb8402e5a2c771136c.
Technical verdict: PARTIAL — unchanged consumers pass static compatibility;
Carrier fixture consistency has one actionable finding. Publication remains NO-GO.

## Lane results

| Lane | Evidence | Result |
|---|---|---|
| [MOD-0183](mod0183/README.md) | Independent temporary patch application; 12 package hashes, 2 proposed hashes; 10 non-Carrier paths, existing components, 22 Shipment references | PASS for contract projection; no runtime rerun |
| [Carrier test/mock](carrier/README.md) | Executed synthetic JSON consumer parser: 27 response groups, 44 exchange fixtures, 7 negative mutations | 44 fixtures PASS; 7 mutations rejected; CARRIER-FIXTURE-01 remains |
| [0185/0186/0187 and local inventory](dependents/README.md) | 12,541 runtime files searched; 17 matches, all existing Shipment files | No implemented downstream Carrier client/mock/SDK located; draft dependencies classified |

The root reran all three scripts successfully. Carrier script exit 0 means its evidence collection
completed, not that the recorded finding passed. No real Carrier service, generated SDK or existing
consumer implementation was exercised. These are executable specification/fixture checks and local
source discovery, not E4/E5, consumer uptake, consent or owner approval.

## CARRIER-FIXTURE-01 — actionable owner finding

Candidate POST /carriers 422 retains shared Unprocessable examples named shipmentTransition,
loadTransition, returnTransition and claimTransition. All four include error.details and foreign
module transition codes/messages. They remain JSON-schema-valid but do not satisfy the candidate
annex's bounded Carrier no-details policy; mocks choosing them could emit inconsistent Carrier examples.
The other 26 response groups pass the harness's inline checks. This inherited-example finding
was not repaired by altering the exact reviewed patch.

Publisher/contract owner must disposition the operation-specific examples: either approve a revised
Carrier-specific example selection without altering shared Shipment definitions, or explicitly explain
and constrain their use as inherited non-Carrier examples. Any artifact change requires new exact hashes
and rerunning affected checks. No new create business rule or arbitrary 422 code is proposed here.

## Future composition conditions

Existing ShipmentContextMiddleware covers the whole bundle prefix and validates correlation before
authorization, rejects nil UUIDs and trims keys. Its replay repository rejects different roots and
uses Shipment-specific replay statuses. Program.cs also shares Shipment model-error correlation handling.
Carrier must have its approved isolated behavior with Shipment regression coverage before composed
runtime compatibility can be claimed. Contract publication alone does not activate Carrier code.

MOD-0185 is a planned Active Carrier consumer; MOD-0187 has an optional Carrier association;
MOD-0186 has no demonstrated direct Carrier consumption. All three packs are draft and no actual
runtime consumer was found. They need design/dependency disposition, not fabricated runtime sign-off.
External consumers cannot be enumerated by repository search; publisher inventory remains incomplete.

## Authority and preservation

Technical evidence only. No approval issued for any owner. GAP-184-04/05 remain BLOCKED pending
publication and actual uptake; pack remains draft and existing prompts HELD. Central contracts,
publication candidate/patch, production code, existing docs, git index and stashes were preserved.
Only this new evidence directory was written; no commit or push.

Exact patch SHA-256: 2cbcf65b4909419a2d0a79d688b3ed95ee33f0993fc666ef5c5edda246f0920f.
Proposed YAML SHA-256: 8954f8af0024fe6c31f1cc49dc717a408d7d48e511fbacb4571d7887c4fac6a4.
Proposed annex SHA-256: 87557ef9f5b5a4427862effbece2bb528ebc1b95361c29416373709223021fee.
Reproduction commands are in each lane report. Preservation evidence and the complete new artifact
inventory are in preservation.json and manifest.json (self excluded).
