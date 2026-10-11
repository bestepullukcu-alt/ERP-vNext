# MVP6 evidence kit — proposal 02 (v1.1, queue Q57)

**Status: PROPOSAL, nothing active. NOT APPROVED.** Prepared by AL-MVP6-KIT-V11-01 (chat lane) for Control Tower on
2026-09-26. Repository `/Users/natig/Projects/ERP-vNext-recovery`, branch `feature/mvp6-logistics`, HEAD
`4a8d4d4b339528a88e6220fb8402e5a2c771136c`. Basis: owner decision
`docs/records/decisions/2026-09/mvp6-evidence-kit-v1-1-revision-owner-decision-01.md` (revise to v1.1; own approval; then
validation run Q24).

**Commit pending — to be committed by the next Mac Terminal session (chat lanes cannot commit).**

## What changed and why

Kit v1.0 (proposal-01, approved for install under A1) was first used for real in the A12 VER-02 Mac run. That run proved
four defects. Its independent review (Q55) found five evidence gaps, and the Loads run added one lesson. v1.1 fixes each
with the smallest change. One row per item is in `CHANGES.tsv`; the phase-level delta is in `KIT-SPEC-DELTA.md`.

| Id | Problem in v1.0 | v1.1 change |
|---|---|---|
| D1 | Platform crashed at DI validation with forced `BackgroundJobs__Enabled=false` | forced `Enabled=true`, `RegisterStandardJobs=false`, `DashboardEnabled=false`; Hangfire DB stays `DitenHangfire_<suffix>` on the lane Mongo |
| D2 | internal keys generated per group, so pairs could differ | one `auth-platform` value for 5 keys, one `platform-mdm-active` value for 2, one `svcid` for 2; K04 `check` asserts every pairing (`#pairing` rows) |
| D3 | K01 extracted a wrapped archive one folder too deep; headerless manifests rejected | strip exactly one top folder only when it is not a real HEAD path; headerless manifests accepted; `wrapper_stripped` recorded |
| D4 | K04/K07 wrote secrets to `W/secrets/` | new `k00_supervisor.py` holds all lane values in memory; env files hold `@@LANE:<group>@@` only; K04 rejects literals |
| G1 | negative control served a file without recording its hash | `k09 record-served` + `templates/served-asset-hash.js` hash the served bytes; K09 fails on a mismatch |
| G2 | thin/multi-line COMMANDS.tsv; `$?` read after `$(date)` | `ek_run`: one sanitised row per command, `rc=$?` right after it; the supervisor adds one row per launch/run |
| G3 | late-async attempt 1 files overwritten | `ek_unique` `-aN` names everywhere; `k10_snap.sh` noclobber; K10 refuses reused snapshot files |
| G4 | exact-value scan ran before the last 18 files | `run-kit.sh seal`: the exact + pattern scan runs after ARTIFACTS.sha256, is the last write, and records that hash |
| G5 | setup mutation did not assert the outbox (+2) | K10 strict: unlisted collections must be unchanged; whole-DB `dbTotals` deltas must be asserted (`--totals`) |
| G6 | `dotnet test` default TestResults would overwrite evidence | `k06_build_launch.sh test` always uses `--results-directory` inside the evidence folder, with TRX |

Checks (details in `STATIC-CHECKS.txt`): bash -n, Python syntax (in memory, no `__pycache__`), node --check, shellcheck
(warning level 0 findings), secret-scanner self-test **3/3**, and synthetic tests of K01, K04, K10, the supervisor and G1/G2/G3.
No service, Mongo or browser was run. That is Q24, after approval.

## Files

`proposed/` is the full v1.1 file set (26 files; `PLACEMENT.tsv` gives install paths, unchanged from v1.0 plus 4 new files).
There is also `CHANGES.tsv`, `KIT-SPEC-DELTA.md`, `ADOPTION-DECISION-v1.1.md` (NOT APPROVED text), `STATIC-CHECKS.txt`,
and `SHA256SUMS` (paths relative to this folder; verify with `shasum -a 256 -c SHA256SUMS` from here).

## ASSUMPTIONS (no-question policy)

1. **Guide file name.** The guide keeps the name `mvp6-evidence-kit-v1.0.md` (content v1.1), so the §5 line approved in A1
   stays valid unchanged.
2. **K04 `secrets` mode.** It is kept as an alias of the new `shared` mode (issuer/audience only, no secret), so a v1.0-style
   call still works but writes no secret.
3. **Rotated groups.** The default is all five (`jwt mfa auth-platform platform-mdm-active svcid`). `platform-mdm-previous`
   stays inherited, as in v1.0.
4. **Service-identity KeyId.** The value is `lane-<suffix>`, which is not secret. The A12 run used `lane-a12-rtver02`.
5. **D2 key names.** They are taken from A12 SOP-22 §7.4 and `raw/overrides.tsv`, plus the v1.0 `auth-platform` member
   Auth `PlatformService__InternalApiKey`.
6. **Seal record.** The seal's own report `SECRET-SCAN-FINAL-aN.txt` is its command record. Neither the seal nor the
   supervisor shutdown adds a COMMANDS.tsv row, so COMMANDS.tsv stays covered by ARTIFACTS.sha256 (G2 vs G4).
7. **Unchanged v1.0 files.** k02, k03, k04b, k05 and the three templates are unchanged bytes. The other Q55/A12
   observations (culture cookie, MDM health path, time zone, fixture design) are outside the listed D/G items and out of
   scope.
8. **Python check.** `ast.parse` plus in-memory `compile()` replace `py_compile`, so no `__pycache__` is written. Scratch
   files were used only in `/tmp`.
9. **Commit.** Not done; this chat lane cannot commit (see the line at the top).
