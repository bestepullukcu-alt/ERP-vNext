# MVP6-ROOT-R2-PRODUCER-CT-ACCEPT-01 — SOP §22

## CT decision

**ACCEPTED — bounded isolated Root R2 Producer work package.**

This acceptance covers the approved 11-path Phase 1.5 implementation and its independently reproduced isolated runtime evidence. It does not publish the contract, activate the guard, promote the pack, authorize migration/backfill, operational rollout, consumer uptake or E5/G5.

## Authority and artifact

- MOD-0183 pack status is `ready-for-dev` and the slice is backend-only.
- The exact approved Root R2 candidate is bound to SHA-256 `dab5a2f9974283e2404a29a7fb8ed07a77ae8ed843929b1c7b4546c5615dd7cb`.
- The old unrecovered `91d505…` target was not used.
- Existing canonical Shipment-Bundle remains unchanged at SHA-256 `93c696e2fba13dbc8fbfcf2cd1ae0ae0bd93cd9d0935b3ee743229e810163571`.

## DEV → VER chain

1. DEV-01 implemented only the approved 11 paths and recorded the initial bounded tests.
2. VER-01 independently reproduced build/storage/regression evidence and left runtime R03–R09/R12 open.
3. DEV-02 changed only the three approved `root_uptake` probes and supplied authenticated runtime fixture evidence; the old 401 restart attempt is preserved.
4. DEV-03 changed only `restart_probe.py`, identified stale authorization/correlation handoff as the 401 cause, and produced a successful same-fixture restart record.
5. VER-02 independently built a different binary, ran a separate replica set/API, reproduced the runtime cases and restart, and supplied the final bounded evidence archive.

VER-02 binary SHA-256: `1433ff88004c2fdebc8516f1162d8bf36a69b54c5deb7722afdfe2a7e49986a3`.

## Acceptance-evidence matrix

| Criterion | Evidence | CT result |
|---|---|---|
| R03 | VER-02 `runtime.json`, valid-root HTTP body and separate request correlation; restart response preserves identity/root | ACCEPTED bounded |
| R04 | Missing/null/valid/nil detail bodies include `lifecycleCorrelationId`; malformed fails closed; existing list/mutation shapes covered by 12 Shipment regressions | ACCEPTED bounded |
| R05 | Authenticated detail read and missing permission `403`; 12/12 existing Shipment security/regression tests | ACCEPTED bounded |
| R06 | Tenant/LE-scoped fixture, soft-delete `404`, same DB before/after measurements; existing scope tests | ACCEPTED bounded |
| R07 | Scoped raw BSON read and unchanged persistence counts in `runtime.json` | ACCEPTED bounded |
| R08 | Every required fixture has successful before/after query results; verifier fails closed on missing/query errors | ACCEPTED bounded |
| R09 | Same persisted fixture and same binary after process restart; authenticated `200` and exact root equality | ACCEPTED bounded |
| R10 | Existing Shipment regression 12/12; Root tests 7/7 (overlaps DEV storage 6/6, not additive) | ACCEPTED bounded |
| R11 | No root derivation/backfill; malformed and nil/value states remain distinct; no provenance claim inferred from UUID parse | ACCEPTED bounded |
| R12 | VER-02 source snapshot → fresh build → binary hash → API port/process → HTTP bodies/headers → DB evidence archive | ACCEPTED bounded |

## Exact scope accepted

The accepted implementation is limited to the 11 paths in DEV-01 `changed-files.json`, with the evidence-only probe revisions in DEV-02/03. It performs a single tenant/LE/soft-delete-scoped raw detail read, detached materialization, explicit missing/null/invalid/present state handling, detail-only nullable UUID projection and fail-closed evidence capture. No mutation, SourceIntake, serializer registration, Program.cs or shared composition change is accepted.

## Remaining gates

Canonical contract publication and version disposition, DocsPath guard activation, consumer consent/uptake, operational rollout, migration/backfill, pack promotion, independent downstream runtime work, E5/G5 and full-module acceptance remain open. This CT decision does not authorize any of them.

## No-change evidence

CT modified only this owned acceptance directory. Existing source, contract, pack, guard and historical evidence were preserved. No build or test was rerun because VER-02 provided fresh independent evidence and no drift was reported.
