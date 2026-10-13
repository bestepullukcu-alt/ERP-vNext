# MVP6-ROOT-R2-RUNTIME-EVIDENCE-DEV-02 — SOP §22

## Verdict

**PARTIAL / writer-complete with restart authorization evidence still open.** This was evidence-only rework; no product source was changed. The updated three Root uptake probes are the only source changes.

## Evidence

- Disposable DB-010 replica set `mvp6r2dev02` on `127.0.0.1:27119` reached PRIMARY; operational 27017 was not used.
- Fresh disposable API binary was built from the source snapshot. Binary hash is recorded in `binary.sha256`.
- `runtime.json` captures authenticated HTTP detail reads for missing, BSON null, malformed, valid UUID and stored nil UUID, plus soft-delete and missing-read-grant cases. All fixture insert/query before-after measurements remained equal; malformed returned `500 SHIPMENT_ROOT_INVALID`, soft-delete `404`, no-read `403`.
- `verify_evidence.py` returned PASS for the runtime capture.
- Restart probe used the same binary and persisted fixture but received `401 Unauthorized`; no restart PASS is claimed. The exact restart environment and result are retained.

## Open criteria

R03/R04/R05/R06/R07/R08 are covered by the runtime capture at the bounded HTTP level; R09 remains OPEN due to the 401 restart attempt. R12 is partial with source→binary→process/runtime artifacts; process command/PID and a successful restart response require a follow-up evidence run. No historical or runtime decision was rewritten.

## Scope

Only `tests/root_uptake/runtime_probe.py`, `restart_probe.py`, and `verify_evidence.py` changed. No production source, Program.cs, serializer, contract, guard, pack, migration, commit, push or stash changed. Writer-complete; independent VER is required.
