# SOP §22 — MVP6-CARRIER-AUTH-TOKEN-FINDINGS-CLOSE-01

Date: 2026-09-23  
Role: single candidate source writer / Control Tower disposition  
Branch / HEAD: `feature/mvp6-logistics` / `4a8d4d4b339528a88e6220fb8402e5a2c771136c`  
Verdict: **BLOCKED ON EXACT OWNER AUTHORITY — REVIEWABLE SUCCESSOR READY**

## 1. Controlling findings

The controlling independent report is `mvp6-carrier-auth-le-successor-ver-01/SOP-22.md`, SHA-256 `674269a15842488f634acb65751b33ad3d2b550429ff2f74cab931c4bd2c8a09`. It binds F-01/F-02 to `PlatformServiceTokenValidator.cs` target SHA-256 `24d4e680c4007bb22287869af14dc5fd4533ee2def251677164540ff9aad9c15` and its focused tests SHA-256 `e32d3f8c86c95fad706b7c977a6cd3550d55d0d7986cfde931b9f9f9d37eaca3`.

The later DEV handoff used the same exact 22-path source manifest `275f204c29113b2bb80b9aa10c87e08b81bd2ab2c71c59a92a98d18130ccb93d`. Its successful login/refresh/MFA/forced-password runtime evidence does not close F-01/F-02 because those negative token shapes were absent. Therefore DEV PASS and independent VER REWORK are consistent and apply to the same source and acceptance scope.

`mvp6-carrier-real-auth-e2e-ver-02` correctly stopped before Carrier E2E because no independent Auth successor PASS existed. This task does not enter that scope.

## 2. Required versus observed behavior

F-01 requires exact cardinality. The existing source delegates audience matching to `ValidAudience` and uses first-value selection for several claims. Reproduction with signed tokens showed conflicting duplicate `scope`, tenant, actor and LE values and expected-plus-extra audience could be accepted.

F-02 requires an explicit temporal relationship. The existing source bounded `exp - iat` but accepted a future `iat` when `nbf` was current and accepted `iat != nbf`. Existing JWT lifetime validation already provides configured-skew handling for `nbf`/`exp`; the successor makes the missing issuance relationship explicit.

Exact row-level disposition is in `FINDING-DISPOSITION.tsv`.

## 3. Candidate delta

The proposed successor changes only:

1. `services/Diten.MdmService/src/Diten.MdmService.Api/Security/PlatformServiceTokenValidator.cs`
2. `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/PlatformServiceTokenValidatorTests.cs`

Patch SHA-256: `b346415769da7542533b816084d56fe314eb6e6c00d8368a9a419774434572e1`.

New target hashes:

- validator: `435b12e93c2c51c99b099e4e5b399d7072b7f3b0a14a389133436a3be05a434e`;
- focused tests: `05836e94f1805a78c56c0d9de46ba9314b7295574358efaa931f1f4e3a7c9bb0`.

The patch applies cleanly to the exact prior targets. The remaining 20 authorized successor paths remain byte-identical. The full successor identities are recorded in `DELTA-MANIFEST.tsv` and `FINAL-22-SOURCE-MANIFEST.tsv`.

## 4. Technical candidate evidence

Native .NET 8.0.23 test execution against the old validator plus new closure tests produced **RED: 21 passed / 7 failed / 28 total**. Failures reproduced duplicate claim/audience acceptance, future `iat` and `iat/nbf` mismatch.

After the disposable candidate delta, the same test set produced **GREEN: 28/28**. It covers missing claims, conflicting duplicates, extra audience, future `iat`, future `nbf`, `iat != nbf`, expired inside/outside configured skew and excessive lifetime. Patch apply/check and byte targets passed.

This is E2 candidate evidence. It is not an authorized production-source application, independent VER or Phase 2 runtime acceptance.

## 5. Authority disposition

The earlier owner authorization is bound to patch `e6b2a3d3…` and explicitly requires a new decision for any patch or path change. This successor has a different patch and makes the temporal relationship explicit. It was therefore not applied to the real checkout.

`OWNER-DECISION-TEXT.md` is the single required decision. It separately makes the proposed `iat = nbf` and configured-skew behavior reviewable instead of treating verifier guidance as owner policy.

## 6. Evidence and preservation

- Full immutable build-source archive: `build-source.tar.gz`, 3,333 source inputs.
- Source manifest: `BUILD-SOURCE-MANIFEST.tsv`.
- RED/GREEN logs and TRX: `evidence.tar.gz`.
- Historical input hashes: `INPUT-HASHES.tsv`.
- Independent execution prompt: `INDEPENDENT-VER-PROMPT-HELD.md`.

No Auth, Platform, MDM, Carrier, UI, Gateway, contract, pack or guard source in the real checkout was modified. No runtime service was started for this candidate and no commit, push, stash or rollout occurred.

## 7. Closure state

- F-01: **OPEN pending exact owner authorization, application and independent runtime VER**.
- F-02: **OPEN pending exact temporal-policy authorization, application and independent runtime VER**.
- Prior successful issuance evidence: content-bound and retained; not relabeled as independent evidence.
- Carrier E2E: outside scope and remains gated.

