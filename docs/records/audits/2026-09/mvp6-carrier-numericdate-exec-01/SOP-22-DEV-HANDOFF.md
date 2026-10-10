# SOP §22 — MVP6-CARRIER-NUMERICDATE-EXEC-01

## Verdict

**DEV WRITER COMPLETE / INDEPENDENT VER REQUIRED.** The exact approved two-file
NumericDate patch was applied to the frozen predecessor source in an isolated
target. F-01 remains CLOSED in writer evidence. F-02 is GREEN in fresh unit and
bounded HTTP/Mongo evidence; it is not independently accepted yet.

## Execution identity and authority

- Repository branch/HEAD: `feature/mvp6-logistics` / `4a8d4d4b339528a88e6220fb8402e5a2c771136c`
- Isolated target: `/private/tmp/mvp6-carrier-numericdate-exec-01.vesol7`
- Exact authority: `OWNER-AUTHORIZATION.md`
- Patch: `180143bccc08ad3316eb89f79a3e9386da2ce881b9a4b02f00a725b0be27ef4b`
- Delta manifest: `aed4ddd904ee78120ca47c23b0a65b328c7ae88771bda62183f4f72777b6f172`
- Final 22-path manifest: `b9713185aba02bf325a9149b37a8cc6ffe3b1dd0e979febdaa773946ae9b1731`

The predecessor preimages matched exactly and `git apply --check` passed. Only
`PlatformServiceTokenValidator.cs` and
`PlatformServiceTokenValidatorTests.cs` changed. Their target hashes are
`1c1f112d18712380f7b03166abbbd7a087dc8d20af5645d903802f60a7b914ea`
and `b245e3d2bb328f8718404672a7cdcbf4f7883647d8d833c23152193b93f66022`.
The complete build-source set matched 3,333/3,333.

## Validation

Native SDK 8.0.417/runtime 8.0.23 was used without major roll-forward. Fresh
MDM build passed with zero errors and five inherited warnings. The targeted
validator suite passed 33/33.

The controlling runtime used API 18759, replica set
`rsCarrierNumericDateExec` on 37884, and database
`DitenMdm_CarrierNumericDateExec`. The binary SHA-256 was
`456e776e411153c3679d896a5574cdc27d4dd5cb791e37120c540a931c75eea2`.
The full 49-case HTTP matrix passed. Digit-only string `iat`, `nbf`, and `exp`
each returned 401 with zero scoped repository reads. Valid numeric, duplicate
cardinality, audience, skew, lifetime, missing and malformed cases retained the
controlling behavior.

The retained first run was 48/49 because its evidence harness signed the
inside-skew token before a slow profiler reset. `ATTEMPT-DISPOSITION.md`
records the failed run and the evidence-only correction. It is not counted as
PASS.

## Evidence boundary

The source writer did not complete a fresh real Auth login/refresh chain. The
predecessor login/refresh evidence remains inherited and content-bound, not a
fresh result for this binary. `INDEPENDENT-VER-HANDOFF.md` therefore requires
the different-agent verifier to reproduce login, refresh and LE re-resolution.

## Artifacts and cleanup

- Final source archive: `final-source.tar.gz`, SHA-256
  `f50350b8e541a4c1cbc640926c581cf510b4c2dbb57b31ef4193bf4c25c9e2cd`
- DEV evidence archive: `evidence.tar.gz`, SHA-256
  `5f8c30998f063a5eee535dc66d32393a767474e2a6e031fa40da13e5f1b2033f`
- Result matrix: `RUNTIME-ACCEPTANCE.tsv`

API 18759 and Mongo 37884 were stopped. Operational Mongo 27017 was not used.
Ephemeral secrets were deleted before archiving; no bearer token was stored.
The common product checkout was not modified. No commit, push, stash, rollout,
Carrier UI, gateway, permission, contract, or guard change occurred.

**Writer-complete:** yes. Independent VER and Carrier real-Auth E2E handoff
remain open.
