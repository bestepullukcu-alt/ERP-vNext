# MVP6-ROOT-R2-RESTART-EVIDENCE-DEV-03 — SOP §22

## Verdict

**PASS for the DEV-02 restart evidence gap; writer-complete.** The prior 401 was a harness/configuration defect: the restart probe consumed a stale `ROOT_AUTHORIZATION`/correlation environment rather than constructing the same issuer/audience/secret/scope token used by the runtime capture. No JWT validation or clock-skew setting was changed.

## Evidence

- Same persisted DB-010 fixture directory from DEV-02 was reused; no fixture recreation or backfill occurred.
- Disposable replica set `mvp6r2dev02` on `127.0.0.1:27119` reached PRIMARY; operational 27017 was not used.
- Same API binary hash: `7a92f3d38d8342a170124c92ab67087053d11b23e549863a81da57c6bf08f7f5`.
- Restart process used the same Mongo URI, database, issuer, audience and secret configuration. Secret and bearer token are not written to evidence.
- Corrected probe generated a fresh short-lived test JWT for the same tenant/LE and `shipments.read` permission.
- Post-restart authenticated detail GET returned `200` with the persisted valid UUID root `495ccdc8-3827-4c47-811a-89d62607f36b`, unchanged shipment identity and no write operation.

## Acceptance closure

R09 is now PASS for the persisted valid-root restart case. DEV-02 runtime capture remains the evidence for missing/null/malformed/valid/nil, soft-delete, permission and scoped before/after measurements. R03–R08 are therefore covered by the combined DEV-02 runtime and DEV-03 restart evidence. R12 source→binary→process→HTTP linkage is complete for the recorded disposable process; independent VER must independently inspect these files and may not infer canonical publication or CT acceptance.

## Exact source change

Only `services/Diten.SupplyChainService/tests/root_uptake/restart_probe.py` changed in this rework. The old failed DEV-02 evidence remains preserved. No production source, Program.cs, serializer, contract, guard, pack, migration, commit, push or stash changed.
