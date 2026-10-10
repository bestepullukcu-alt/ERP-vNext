# SOP §22 — MVP6-SHIPMENT-UI-B01-B02-INDEPENDENT-VER-01

## Verdict

**PARTIAL / INDEPENDENT RUNTIME VER OPEN.**

The writer artifact, exact source identities, patch applicability, fresh native .NET 8 builds, and focused B01/B02 tests independently pass. The lane also reached a clean isolated runtime smoke state. A fresh real Auth-issued browser session and the required browser transition/POD/replay/conflict/restart scenarios were not completed in this verifier run, so B01-RUNTIME and all B02 runtime rows remain OPEN. Writer runtime evidence is preserved as inherited evidence and is not relabeled as independent PASS.

## Scope and authority

- Repository HEAD: `4a8d4d4b339528a88e6220fb8402e5a2c771136c`.
- Writer package: `docs/records/audits/2026-09/mvp6-shipment-ui-b01-b02-rework-01/`.
- Product source and writer package were read-only. This verifier wrote only this report directory and `/private/tmp/mvp6-shipment-ui-b01-b02-independent-ver-01/`.
- PNG remains OPEN; no unsupported capture method was attempted.

## Exact source and artifact verification

| Check | Result | Evidence |
|---|---|---|
| Writer `ARTIFACTS.sha256` | PASS, 13/13 | `evidence.tar.gz::artifact-check.txt` |
| Writer source archive | PASS | `source.tar.gz` SHA-256 `b922ab8f0d4d4c00c72a0e3fa8f6e53728cdcece21479b36d13a14d992358c14` |
| Final Shipment/UI source | PASS, 354/354 | manifest SHA-256 `e6551f4552bd05682dcd9f2753044aa0b803dd4df90875952cca89608ab98ab3` |
| Final Auth successor source | PASS, 22/22 | manifest SHA-256 `b9713185aba02bf325a9149b37a8cc6ffe3b1dd0e979febdaa773946ae9b1731` |
| Build source | PASS, 3333/3333 | manifest SHA-256 `516ba828bed602e4355a5cc6871501cbb3b3c72d60473fa7ec73d135bbadf911` |
| B01/B02 patch | PASS | reverse apply, forward `git apply --check`, apply, and final 354/354 recheck; patch SHA-256 `12fa97aef9e2ea360882a3a0f2c80fc32f5658fe7aa1f05d0af03b86c62034af` |

The first checksum invocation from inside the writer package failed because `ARTIFACTS.sha256` stores repository-root-relative paths. Re-running the unchanged list from repository root passed 13/13; this is a command working-directory issue, not an artifact mismatch.

## Build and focused tests

Native `/Users/natig/.dotnet/dotnet` SDK `8.0.417`, runtime `8.0.23` was used. All final builds completed with zero errors:

- Auth: PASS, 1 warning.
- Platform: PASS, 32 warnings.
- MDM: PASS, 5 warnings.
- SupplyChain: PASS, 0 warnings.
- Gateway: PASS, 0 warnings.
- Web: PASS, 15 warnings.

The first parallel attempt caused a shared BuildingBlocks output lock. The next restore attempt waited on inaccessible NuGet metadata. Neither is a source/product failure. Existing local `obj/project.assets.json` build metadata was copied into the disposable checkout; final builds then ran serially with `--no-restore -m:1 /nr:false` against the independently verified 3333-file source set.

Focused tests: **9/9 PASS**, 0 failed, 0 skipped. TRX: `evidence.tar.gz::tests/shipment-ui-focused.trx`.

## Isolated runtime smoke

- Mongo: `rsShipmentUiB01B02Ver`, `127.0.0.1:42124`, PRIMARY confirmed.
- Operational Mongo `27017`: not used.
- Ports: Gateway 5920, Web 5921, Auth 5956, Platform 5957, MDM 5959, SupplyChain 5961.
- MDM, SupplyChain, Gateway health: 200; Web root: 302 to login.
- Auth and Platform aggregate health: 503 because unavailable aggregate dependencies such as RabbitMQ; this is recorded separately and is not represented as healthy.
- All verifier listeners were stopped and all seven ports were FREE at cleanup.

## Acceptance disposition

See `ACCEPTANCE.tsv`. Static B01/B02 controls and builds are independently PASS. Browser-render, actual outbound mutation root, replay/conflict zero-write, and same-binary/database restart remain OPEN because the fresh real-Auth browser session was not executed to completion.

## Preserved boundaries

- No product source, contract, Gateway source, Auth source, pack, guard, or Git mutation.
- No bearer token, password, signing key, or connection secret archived.
- No CT/full-module/G5/rollout acceptance claim.

