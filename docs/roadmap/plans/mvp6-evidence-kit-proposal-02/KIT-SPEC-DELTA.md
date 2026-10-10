# KIT-SPEC delta — v1.0 (proposal-01) → v1.1 (proposal-02)

This page lists only what changes against `docs/roadmap/plans/mvp6-evidence-kit-proposal-01/KIT-SPEC.md`. Every section
not named here is unchanged: phase purposes, lane slots and ports, DB-010, the 27017 refusal, gateway sink, K11 ownership
checks and the PNG mechanisms (§5).

## New phase K00 — lane supervisor (D4)

- `k00_supervisor.py` starts after K05. It generates one value per rotated group and one password per `actors.tsv` label
  with `secrets.token_urlsafe`. It holds them only in memory.
- It serves one Unix socket outside W (`EK_SOCK`) and refuses a socket inside W.
- Operations (client `k00_ctl.py`):
  - `launch <svc>`: env file with placeholders replaced in memory, then K06 semantics.
  - `run <label> -- argv`: a helper gets `LANE_SECRET_<GROUP>` and `ACTOR_PW_<LABEL>` in its environment.
  - `reveal <actor>`: terminal only.
  - `pids`, `ping`, `shutdown`: `shutdown` clears the values.
- Logs go to `W/logs` with `-aN` names. After K11 they go next to the socket, outside W and E.
- Each launch and run writes one COMMANDS row (phase `SUP`). Nothing is written after the seal.

## Changed phases

| Phase | v1.0 | v1.1 | Item |
|---|---|---|---|
| K01 | wrapped archives extracted as-is; header-only manifests | one wrapper folder stripped only when it is absent from HEAD; headerless manifests accepted; `wrapper_stripped` column | D3 |
| K04 | `secrets` generated values into `W/secrets`; per-group values; `BackgroundJobs__Enabled=false` | `shared` (alias `secrets`) writes issuer/audience only; `render` writes `@@LANE:<group>@@`; `check` asserts placeholders, D2 pairings (`#pairing`) and no literal secret in env files or overrides; Platform jobs on, standard jobs and dashboard off; service identity on with `KeyId=lane-<suffix>` | D1 D2 D4 |
| K06 | launched services itself with secrets from files; fixed log names | `launch` goes through the supervisor; build, restore, netcheck and health files are `-aN`; restore failure stops the run; new `test` mode with `--results-directory` + TRX | D4 G3 G6 |
| K07 | generated and stored actor passwords; `reveal` read a file | reads `ACTOR_PW_<LABEL>` from its environment (run by the supervisor); arguments passed by env; output `raw/identities-aN.json` | D4 G3 |
| K08 | exact scan read `W/secrets`; ran before cleanup and before the last files | exact values from the environment only (no vacuous PASS); `--seal` is the last write, records the ARTIFACTS.sha256 hash, and fails if evidence changes during the scan | D4 G4 |
| K09 | binding of served assets by path | `record-served` mode + `served-asset-hash.js`; every `route.fulfill` hash must equal a recorded one | G1 |
| K10 | counts for listed collections; overwritable file names | `dbTotals` for every collection; strict diff (unlisted must be unchanged; whole-DB deltas asserted); `k10_snap.sh` noclobber `-aN`; reuse and before==after refused | G3 G5 |
| K11 | removed `W/secrets`; stopped everything | removes `W/shared`; `CLEANUP` and listener files `-aN`; leaves the supervisor running for the seal | D4 G3 G4 |
| driver | `up` / `down` (scan → cleanup → rescan) | `up` / `down` (netcheck → cleanup) / `seal` (ARTIFACTS first, then the final scan, then shutdown); every phase is one `ek_run` row | G2 G4 |

## Hand-off conditions (replaces the v1.0 list in the guide)

- `SECRET-SCAN-FINAL-aN.txt` is `PASS` and carries the ARTIFACTS.sha256 hash (`SECRET-RESCAN-AFTER-CLEANUP.txt` no longer
  exists).
- No `#pairing` row is `FAIL`.
- Every setup mutation and negative case has a K10 row with whole-DB deltas asserted.
- `raw/served-overrides.tsv` has no `FAIL` row.

## Open items closed or carried

- **Closed by D2:** v1.0 §7.1 (internal credential pairings), measured in the A12 VER-02 run (SOP-22 §7.4).
- **Carried unchanged:** PNG decision B, and the validation run (now Q24, on v1.1 bytes, after approval of
  `ADOPTION-DECISION-v1.1.md`).
