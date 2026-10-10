# MVP6-SHIPMENT-REAL-AUTH-HTTP-RECOVERY-01 — SOP §22

## Verdict

**REWORK (bounded):** the fresh real-Auth HTTP, isolation, persistence, replay, zero-write, and restart scenarios passed. The final Shipment producer does not explicitly persist `LifecycleCorrelationId`, so the Root R2 explicit-emission criterion remains open. Browser and PNG closure were outside this work package.

## Scope and provenance

- Branch: `feature/mvp6-logistics`
- HEAD: `4a8d4d4b339528a88e6220fb8402e5a2c771136c`
- Shipment writer artifact: `3842230d59a650f5842e344abad4a34711b28390a740fd07539e847f2fb1d183`
- Shipment overlay archive: `8aba3d890f160cbed88eecc284cdcba78eda03b6b0955cf3061199a62171112e`
- Shipment 351-row manifest: `a469678926c0b085075e6c14d8977789e26eb2e787558acf86905081030fa51e`
- Auth recovery handoff: `95dafe0fbfdfdf490f9ecdb57f65582d0146da435ad938f1df13c11105586cd2`
- Auth 22-path manifest: `b9713185aba02bf325a9149b37a8cc6ffe3b1dd0e979febdaa773946ae9b1731`
- Auth final archive: `f50350b8e541a4c1cbc640926c581cf510b4c2dbb57b31ef4193bf4c25c9e2cd`

The immutable source was reconstructed from HEAD plus the two hash-bound overlays. Both source manifests matched exactly (351/351 and 22/22).

## Environment and execution

Native .NET SDK 8.0.417/runtime 8.0.23 built Auth, Platform, MDM, and SupplyChain. An isolated DB-010 Mongo replica set ran on `40994`; no connection to operational `27017` was made. Fresh Auth login exercised the Auth → Platform service identity → MDM active-LE resolver chain. Tokens and secrets were not archived.

The first no-restore build correctly failed for absent disposable `project.assets.json`; recorded restores and subsequent builds passed. Platform's aggregate health remained 503 and is not represented as a Platform health PASS.

## Acceptance result

`ACCEPTANCE.tsv` is the controlling row-level matrix. HTTP results include list/create/detail, lifecycle transition, POD, replay and changed-payload conflicts, read-only permission denial, tenant and legal-entity isolation, five-collection zero-write checks, Pending-only outbox, and same-binary/same-DB restart durability.

The one product defect is documented in `FINDINGS.md`. No source, pack, contract, guard, Gateway, or UI file was changed.

## Evidence limits

This is E4 evidence for the isolated bounded HTTP work package. It is not browser/PNG acceptance, CT acceptance, full-module acceptance, rollout, E5, or G5.
