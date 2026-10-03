# MVP6-SHIPMENT-ROOT-R2-CT-DISPOSITION-01 — SOP §22

**Date:** 2026-09-24  
**Role:** Control Tower, read-only evidence reviewer  
**Verdict:** **ACCEPTED — bounded isolated Root R2 create-time emission mutation**

## Decision

Control Tower accepts the owner-approved three-file Shipment Root R2 mutation at final source manifest SHA-256 `7b6d2f6a679275b74ab88989f5ca943b139e7f1a365b5ab99d6f1285ceb1acae`. The exact isolated source, fresh binary and independent process evidence establish create-time persistence of the authoritative `LifecycleCorrelationId`, detail emission, replay/concurrency safety, rollback/retry atomicity, legacy-state preservation, tenant/LE/RBAC isolation and real restart persistence.

This is a bounded successor acceptance. It does not accept Shipment browser behavior, durable PNG, canonical publication, common-checkout uptake, full MOD-0183 completion, E5/G5 or rollout.

## Exact authority and source chain

1. The owner decision text SHA-256 `33dabd9e3f36289d6b00f6634a38bee4b8f7e2efec1e079ac327894be1d5b204` authorized candidate patch `2d44f0d3b61b29c5bf34437fa12564d29319fd64beaded366f443b08b1a0319e` for three isolated paths and independent runtime verification.
2. The candidate decomposes into production patch `c78fefe4cfe0377ec014b58918656ddf05121504fd54fb40bcf0521677852011` and test patch `c154f3352f3b266e2a52c3eee8d97008dc2823fc2a9fa6aaa5dc0d066a5363fe`.
3. Writer `APPLIED-DELTA.tsv` binds the three approved preimages to the exact target hashes. No fourth path was added.
4. Writer and verifier `FINAL-SOURCE-MANIFEST.tsv` files are byte-identical, contain 354 entries and share SHA-256 `7b6d2f6a679275b74ab88989f5ca943b139e7f1a365b5ab99d6f1285ceb1acae`.
5. The independent verifier rebuilt that source with native .NET 8 into binary SHA-256 `754329d79a2aa6b94430acc4b81f9bf60204229e45bfb3b4a0e4e436e6104333`, launched it on isolated port 5783 and used replica set `rsShipmentRootR2Ver01` on port 41132. Operational Mongo 27017 remained untouched.
6. The independent raw evidence archive SHA-256 is `e4f1da354b385334fa284ae0ffad3a56f83717fc515c16674a920a1433885ce6`.

All three input package checksum manifests passed. No runtime test was repeated for this CT review.

## Transition from the prior acceptance

The prior `mvp6-root-r2-producer-ct-accept-01` decision accepted an 11-path, read-only detail projection/materialization slice. It explicitly accepted **no mutation** and did not prove that a newly created Shipment persisted an authoritative lifecycle root.

This successor adds a separately authorized mutation boundary:

- `Shipment.cs` gains the nullable persisted root property;
- `CreateShipmentHandler.cs` writes the first validated create correlation as the authoritative lifecycle root;
- `ShipmentTests.cs` adds mutation/persistence assertions.

The prior read-only decision remains valid for legacy detail materialization. It is not reinterpreted as historical create acceptance. The create/persist behavior is accepted only through this new exact three-file chain and its independent runtime evidence.

## Bounded acceptance

`BOUNDED-ACCEPTANCE.tsv` is controlling. Create, persist, detail, replay/concurrency, rollback/retry, legacy variants, isolation, restart and audit/receipt/Pending-outbox atomicity are PASS. The known process-global BSON serializer race remains a test-process constraint; the writer and verifier ran affected suites in separate processes and preserved discarded setup attempts.

The first writer restart attempt used the wrong lane-owned database and returned 404. The verifier independently reproduced restart on the correct same database, fixture and binary, so the discarded attempt does not weaken the controlling PASS. The verifier's initial missing-test-environment and configured-port attempts were also retained and superseded before product execution.

## Effort disposition

The existing Root R2 planning envelope is **12/24/44 O/M/P gross** and **0/0/0 net new** because it is allocated within existing Shipment reserves.

- Application/writer delivery `0183-R2-02`, 4/8/16: **CLOSED by exact writer evidence**.
- Independent runtime VER `0183-R2-03`, 6/12/20: **CLOSED by independent evidence**.
- Browser regression `0183-R2-04`, 2/4/8: **OPEN**.

Thus the remaining Root R2 gross planning envelope becomes **2/4/8**, entirely browser-bound. The closed 10/20/36 envelope is not measured actual effort and is not added to portfolio totals. `EFFORT-ROW-MAPPING.tsv` prepares a successor effort report to update delivery states without duplicating Backend/Test-VER reserves.

## Open gates

- Shipment real-Auth browser regression and durable PNG remain OPEN.
- The parallel browser lane has no result in this review and no outcome is assumed.
- Canonical Root R2 publication/uptake, common-checkout source application, migration/backfill, full-module acceptance, E5/G5 and rollout remain separate.
- The browser handoff may proceed only against the exact accepted 354-entry source manifest. If source bytes change, this disposition does not transfer automatically.

## No-change statement

This review created only `docs/records/audits/2026-09/mvp6-shipment-root-r2-ct-disposition-01/`. It changed no product, pack, contract, board or Git state.
