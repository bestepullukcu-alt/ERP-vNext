# SOP §22 DEV handoff — MVP6-CARRIER-AUTH-TOKEN-REWORK-DEV-01

## Verdict

**DEV PASS — writer complete.** The exact authorized two-file token-validation rework was applied in an isolated checkout. F-01 and F-02 are GREEN in unit and bounded runtime evidence. Independent VER and CT acceptance remain open.

## Execution identity

- Repository: `/Users/natig/Projects/ERP-vNext-recovery`
- Branch: `feature/mvp6-logistics`
- HEAD: `4a8d4d4b339528a88e6220fb8402e5a2c771136c`
- Isolated checkout: `/private/tmp/mvp6-carrier-auth-token-rework-dev-01/repo`
- Authority: the user's exact `MVP6-CARRIER-AUTH-TOKEN-REWORK-DEV-01` dispatch, recorded in `OWNER-AUTHORIZATION.md`
- Patch: `b346415769da7542533b816084d56fe314eb6e6c00d8368a9a419774434572e1`
- Delta manifest: `5f7369c8b09ab28f37a8b21e958e2a14258dba60e1307d474d8110cca5f80153`
- Final 22-path manifest: `6221006b5e4bdccff9eb567b49a3e6c111ecb611b9e96d169ee3ff5fe57c1ad2`

## Exact source delta

Only these files changed in the isolated source:

1. `services/Diten.MdmService/src/Diten.MdmService.Api/Security/PlatformServiceTokenValidator.cs`
2. `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/PlatformServiceTokenValidatorTests.cs`

The validator now requires one exact audience; one exact `sub`, `scope`, tenant, actor, and Legal Entity claim; one nonempty `jti`; and one numeric `iat`, `nbf`, and `exp`. It requires `iat == nbf`, rejects issuance beyond configured skew, preserves configured lifetime skew, and enforces the existing maximum lifetime. No validation was weakened.

The final 22-path source set matched 22/22 and the complete build-source set matched 3333/3333. The shared repository product sources remained at their prior 22/22 baseline; the only repository output from this task is this audit directory.

## Build and test results

Native toolchain: SDK 8.0.417 and runtime 8.0.23; no major roll-forward.

| Check | Result |
|---|---|
| Auth API build | PASS; 0 errors, 0 warnings |
| Platform API rebuild | PASS; 0 errors, 32 pre-existing warnings |
| MDM API rebuild | PASS; 0 errors, 5 pre-existing warnings |
| MDM token validator | 28/28 PASS |
| Platform successor regression | 8/8 PASS |
| Real HTTP validator cases | 10/10 PASS |

The initial sandboxed VSTest attempt could not create its local IPC socket and is retained as environment evidence. The approved rerun outside that sandbox boundary produced the controlling TRX.

## Runtime evidence

The controlling run used the lane replica set `rsCarrierAuthLeDev01` on port 37484 and service ports 5456/5457/5459. Real Auth login and refresh both returned 200 and their redacted JWT payloads contained the same single authoritative `legal_entity_id` for the tenant actor.

The real HTTP matrix accepted an exact token and an expiry inside the configured skew. It rejected a duplicate subject, missing `jti`, extra audience, future `iat` with current `nbf`, unequal `iat`/`nbf`, future `nbf` outside skew, expiry outside skew, and excessive lifetime. No token or secret is stored.

An earlier MDM launch used the wrong configuration section and fell back to localhost:27017. It was stopped, its claim-less result is excluded, and the corrected lane-bound run supersedes it. `ATTEMPT-DISPOSITION.md` records the limitation; this handoff does not claim that port 27017 was untouched during that failed launch.

## Source → binary → process

| Service | DLL SHA-256 | Process/port |
|---|---|---|
| Auth | `19f52247a63b52e48616d777cf280a66b83edf4858a4ca74ccca35eb02584084` | native .NET 8, 5456 |
| Platform | `2cfa49b7acd76a7ce134d9641f384671a75664568618a13a44e36f9864cc106e` | native .NET 8, 5457 |
| MDM | `e1aed1d36d4b4a60d88ab845eaa622bf96eda8cba0a9b902540a3cdca615eaad` | native .NET 8, 5459 |

Raw logs, TRX, redacted HTTP results, binary/process table, and cleanup record are in `evidence.tar.gz` (`ba051ce775814a66b6e59abe9027c03ff67a48037376f64554c03d7c900a42a5`).

## Remaining gates

1. A different verifier must execute `INDEPENDENT-VER-HANDOFF.md` from the immutable source archive.
2. Carrier UI E2E and CT acceptance remain separate gates.
3. Operational rollout is not authorized.
4. The rejected first MDM launch means this handoff cannot assert a pristine port-27017 history; independent VER must use a new isolated database.

## Cleanup and Git safety

All lane-owned Auth, Platform, MDM, and Mongo processes were stopped; ports 5456, 5457, 5459, and 37484 were free at close. No commit, push, stash, branch switch, rollout, gateway, permission, guard, or Carrier UI change occurred. Pre-existing dirty work was preserved.
