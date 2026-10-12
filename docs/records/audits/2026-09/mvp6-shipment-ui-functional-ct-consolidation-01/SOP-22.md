# SOP §22 — MVP6-SHIPMENT-UI-FUNCTIONAL-CT-CONSOLIDATION-01

**Date:** 2026-09-24  
**Role:** Control Tower evidence consolidation  
**Verdict:** **PARTIAL — Root R2 and exact SHIP-UI-B01/B02 functional closure ACCEPTED; complete UI183-A01–A16 acceptance remains OPEN/PARTIAL.**

## Decision

Control Tower accepts, on final 354-source manifest SHA-256 `e6551f4552bd05682dcd9f2753044aa0b803dd4df90875952cca89608ab98ab3`:

1. the previously accepted Root R2 create-time emission/persistence slice, because every backend path remains byte-identical;
2. SHIP-UI-B01 explicit nested-partial resolution; and
3. SHIP-UI-B02 authoritative persisted lifecycle-root propagation for transition/POD, including replay, changed-payload conflict, scoped zero-write and same-binary/database restart.

This is an exact functional closure, not acceptance of the complete bounded Shipment UI, full MOD-0183, G5 or rollout.

## Authority and source lineage

The real owner approval binding for bounded 28-path UI DEV/VER is `mvp6-shipment-pod-ui-exec-01/OWNER-APPROVAL-BINDING.md`, SHA-256 `dbed2b881b8606a94fd80a697d17e9066f1890adc1463230cc5b7de2153ba3f9`. Root R2 authority and three-file acceptance remain recorded in `mvp6-shipment-root-r2-ct-disposition-01`.

The prior real-Auth browser source manifest `7b6d2f6a...` is byte-identical to the rework baseline. The successor changes exactly four approved UI/test paths listed in `SOURCE-IMPACT.tsv`; every backend/Auth/Gateway and other UI path is unchanged. The final writer and verifier manifests are byte-identical at `e6551f4552...`. Rework artifacts verify 13/13; independent artifacts verify 15/15.

## Evidence provenance

The old browser run remains the controlling RED for B01 and B02 and the source of unaffected create/detail/Auth/UAS/persistence observations. The rework keeps both RED failures and subsequent GREEN results. The final verifier independently checked manifests, patch roundtrip, native .NET 8 builds, focused tests, HTTP/DB state, correlation, zero-write, Pending outbox and restart.

The final browser actions were performed by the root thread against the verifier-owned isolated live stack because the subagent could not bind IAB/Chrome. This is **root-assisted browser operation**. It is not claimed as a second fully independent browser execution. The verifier independently reconciled the resulting HTTP and persisted state.

## Acceptance boundary

`ACCEPTANCE.tsv` is controlling. Root R2 and B01/B02 exact behavior are accepted. The complete UI matrix remains partial because these items are not fully evidenced on the successor baseline:

- all missing-permission target combinations and direct adapter variants;
- stale/race transition 422 handling;
- POD_ALREADY_CAPTURED 409 and POD 422 browser handling;
- unknown-response/500/503 stable retry for every mutation class;
- cross-tenant/LE/deleted/unknown browser presentation;
- seven-language browser execution, RTL, 390/768 layouts, keyboard focus and accessible error summary;
- full responsive DataTable behavior; and
- persistent PNG export.

PNG remains **OPEN** without waiver. Platform aggregate health remains observed **503**; successful Shipment consumer behavior does not convert it to Platform health PASS.

## Effort disposition

`EFFORT-DISPOSITION.tsv` marks delivered evidence row by row. Prior Root writer and runtime VER rows remain closed and are not recounted. The root browser functional behavior is now evidenced, but its existing 2/4/8 row stays partial and reserved because PNG and broader UI browser obligations remain and no approved effort split exists. B01/B02 DEV and VER are closed as evidence rows without inventing new O/M/P values. All remaining acceptance reserves stay open.

## Verification and no-change

No build or runtime test was repeated because the exact manifests, artifact checksum sets and source-impact chain were consistent. This task writes only `docs/records/audits/2026-09/mvp6-shipment-ui-functional-ct-consolidation-01/`. It changes no product, pack, contract, guard, board or Git state and grants no commit, push, rollout, full-module, E5 or G5 authority.
