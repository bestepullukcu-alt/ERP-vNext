# MVP6-ROOT-R2-PRODUCER-DEV-01 — SOP §22 DEV handoff

## Scope and authority

Writer-complete for the approved isolated Root R2 detail-read slice. The exact candidate archive was verified from `root-r2-candidate.tar.gz`; extracted YAML SHA-256 is `dab5a2f9974283e2404a29a7fb8ed07a77ae8ed843929b1c7b4546c5615dd7cb`. The unrecovered `91d505…` target was not used. No canonical publication, guard activation, migration, rollout, commit, push or stash occurred.

## Implemented exact scope

Only the approved 11 paths were changed:

1. Shipment repository interface detail-read method.
2. Read-only `ShipmentDetailReadResult` and `RawRootState`.
3. Scoped raw BSON detail read in `ShipmentRepository`.
4. Detached `ShipmentDetailMaterializer` preserving missing/null/malformed/UUID/nil states.
5. Detail query handler projection/error mapping.
6. Detail-only `lifecycleCorrelationId` projection.
7. Presence-aware storage tests.
8. Bounded HTTP-scope test guard.
9. Runtime capture probe.
10. Restart probe.
11. Fail-closed evidence verifier.

No Program.cs, serializer registration, entity, command, SourceIntake, gateway, contract, guard or other module file changed.

## Behavior

- Tenant, legal-entity and `IsDeleted=false` predicates are applied to the raw detail read.
- Missing and explicit BSON null are represented separately and serialize as explicit JSON null.
- Valid UUID and stored nil UUID remain distinct from missing/null; malformed stored values fail closed with `SHIPMENT_ROOT_INVALID` and are never rewritten.
- The raw document is detached before typed materialization; GET has no write path, derivation or backfill.
- `lifecycleCorrelationId` is emitted only on detail; summary and mutation/POD shapes are unchanged.

## Verification

- Root archive manifest: PASS.
- Debug build: PASS, exit 0.
- Root storage tests: PASS, 6/6.
- Root bounded test filter: PASS, 7/7.
- Existing Shipment regression: BLOCKED, 0/12 because `MOD0183_TEST_MONGO` isolated replica-set configuration was unavailable. No operational Mongo was contacted.
- HTTP/JWT, real BSON persistence, no-write query counters and two-process restart: NOT RUN; probes require isolated API/Mongo environment and fail closed when variables are absent.
- Source manifest: `source-manifest.sha256`.
- Evidence archive: `evidence.tar.gz`, SHA-256 `caae7d707b27250da40fb3e934b36c418d24840c96eaba992f5abf3853aeca28`.

## Handoff state

Source writer is complete. Independent VER may begin only against this handoff and `changed-files.json`; it must not treat the blocked runtime checks as PASS and must independently bind source→binary→process evidence.
