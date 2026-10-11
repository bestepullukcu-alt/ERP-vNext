# MVP6-CARRIER-AUTH-TOKEN-REWORK-VER-01 — SOP §22

Date: 2026-09-23  
Role: independent verifier; candidate writer: no  
Branch / HEAD: `feature/mvp6-logistics` / `4a8d4d4b339528a88e6220fb8402e5a2c771136c`  
Verdict: **BLOCKED — PHASE 1 COMPLETE; PHASE 2 NOT RUN**

## 1. Gate result

The controlling package is internally consistent, but it is not authorized or
writer-complete:

- package SOP verdict: `BLOCKED ON EXACT OWNER AUTHORITY — REVIEWABLE SUCCESSOR READY`;
- `OWNER-DECISION-TEXT.md` status: `UNAPPROVED`;
- no applied successor writer-complete or immutable final writer handoff exists;
- `INDEPENDENT-VER-PROMPT-HELD.md` explicitly prohibits execution before those gates.

Accordingly, Phase 2 native build/runtime execution was not started. Candidate E2
RED→GREEN evidence was inspected but was not relabeled as independent evidence.

## 2. Exact artifact verification

| Artifact | SHA-256 | Result |
|---|---|---|
| `token-findings-successor.patch` | `b346415769da7542533b816084d56fe314eb6e6c00d8368a9a419774434572e1` | PASS |
| `DELTA-MANIFEST.tsv` | `5f7369c8b09ab28f37a8b21e958e2a14258dba60e1307d474d8110cca5f80153` | PASS |
| `FINAL-22-SOURCE-MANIFEST.tsv` | `6221006b5e4bdccff9eb567b49a3e6c111ecb611b9e96d169ee3ff5fe57c1ad2` | PASS |
| `build-source.tar.gz` | `47c39cdbeaa61a59b9b4891439bc758589911d535f7b84736e8ce3a4a88688e6` | PASS |
| `BUILD-SOURCE-MANIFEST.tsv` | `1b3eaf5101cd6a9ee2919c14e71fd51b50c6c433a08ef4dcdbf4492cc09d914e` | PASS |
| Package artifact manifest | 12/12 | PASS |
| Build-source entries | 3333/3333 | PASS |
| Final successor targets | 22/22 | PASS |

The earlier authorized 22-path source was extracted into a disposable baseline.
The patch applied cleanly and produced all 22 final target hashes. Only the validator
and its focused test file changed. Their exact transitions are:

- validator `24d4e680…` → `435b12e9…`;
- focused tests `e32d3f8…` → `05836e94…`.

The other 20 targets remained byte-identical. No repository source was modified.

## 3. Authority and acceptance binding

F-01 and F-02 originate from the independent successor verifier, whose controlling
SOP hash is `674269a15842488f634acb65751b33ad3d2b550429ff2f74cab931c4bd2c8a09`.
The earlier owner authorization expressly required a new decision for any patch or
path change. This successor has a different patch and introduces the proposed
`iat = nbf` rule. The candidate correctly keeps both findings OPEN pending the exact
new owner decision, application and independent runtime verification.

The candidate source implements:

- exactly one audience and exact-one checks for caller, scope, tenant, actor, LE,
  JTI, IAT, NBF and EXP;
- numeric time parsing;
- `iat == nbf`;
- future issuance bounded by configured skew;
- `exp > iat` and bounded `exp - iat`.

This source inspection supports the proposed design. It does not close either
finding without the missing authority and Phase 2 evidence.

One precision point remains unproven in the candidate itself. The helper uses
`long.TryParse(claim.Value)` but does not inspect the underlying JWT payload type.
The owner text requires NumericDate values. A correctly signed token carrying a
digit-only JSON string such as `"iat":"123"` may therefore be treated like a JSON
number. The 28-test helper always emits IAT as `Integer64` and constructs NBF/EXP
through `DateTime`, so it cannot falsify this case. Phase 2 must probe raw signed
payloads; if string-valued dates are accepted, F-02 requires rework rather than PASS.

## 4. Assessment of the 28 focused tests

The archived TRX genuinely contains 28 executed cases. The controlling source gives
21 PASS / 7 FAIL; the candidate gives 28/28 PASS. The seven RED failures reproduce
the intended original defects. This is useful mutation evidence, but the count is
not complete security coverage.

Covered candidate cases include conflicting duplicates for seven claims, missing
seven claims, expected-plus-extra audience, missing audience, future IAT, future NBF,
IAT/NBF mismatch, expiry inside/outside skew and excessive lifetime.

The suite does not independently cover several exact policy boundaries. They are
listed in `INDEPENDENT-SCENARIOS.tsv`, including identical duplicates, NBF/EXP
cardinality and malformed values, reversed duplicate order, expected-audience
duplication, isolated future-IAT evaluation, skew/lifetime exact boundaries,
`exp <= iat`, JSON NumericDate type enforcement, and mediator/repository zero-read
for every rejected token.

Two existing cases have mixed rejection causes:

- `Future_iat_is_rejected_even_when_nbf_is_current` also sets `iat != nbf`, so it
  does not isolate the future-IAT predicate;
- `Future_nbf_outside_skew_is_rejected` may be rejected by standard JWT lifetime
  validation before the new custom predicate runs.

They remain valid rejection tests, but cannot alone prove the intended predicate or
ordering.

## 5. Finding disposition

| Finding | Static candidate | Independent runtime | Verdict |
|---|---|---|---|
| F-01 exact claim/audience cardinality | Proposed code is structurally consistent; candidate RED→GREEN exists | NOT RUN; exact missing scenarios and zero-read still required | **OPEN** |
| F-02 temporal boundary | Proposed code expresses most of the pending policy; JSON NumericDate type enforcement is unproven | NOT RUN; raw-payload and isolated clock-boundary scenarios required | **OPEN** |
| F-03 trust-boundary ordering | Unchanged; inherited source ordering observed | NOT RUN; rejected-token zero-read must be reproduced | **OPEN for this VER** |
| Login/refresh/re-resolution | Auth source unchanged by two-file delta | NOT RUN on final authorized validator | **OPEN** |
| MFA/forced-password/fail-closed | Source impact is indirect through the MDM validation seam | NOT RUN; targeted successor checks required | **OPEN** |
| Broad Auth suite | Prior attempt produced no usable TRX/output | NOT RUN | **OPEN / NOT ESTABLISHED** |

## 6. Source→binary→process boundary

`SOURCE-BINARY-PROCESS.tsv` is controlling. The immutable proposed source is
hash-bound, but no independent binary or process was produced because the execution
gate is closed. No login, refresh, MFA, forced-password, refusal/timeout or real
Auth→Platform→MDM runtime result is claimed.

The reusable native environment remains `/Users/natig/.dotnet/dotnet`, SHA-256
`6d06c4a53676021a7572c989528067a3fe1195f569c27fbcac25e5302dc0d72c`,
SDK 8.0.417 and runtime/ASP.NET Core 8.0.23. It must be used with roll-forward
variables removed only after the gate opens.

## 7. Exact resume conditions

1. The owner must approve the exact patch, manifests and temporal policy.
2. A single writer must apply those bytes and publish immutable writer-complete
   source plus a final manifest.
3. A different verifier must execute all mandatory rows in
   `INDEPENDENT-SCENARIOS.tsv`, fresh-build the three services, and run the bounded
   real Auth→Platform→MDM chain.
4. Secrets and bearer tokens must be redacted; all lane processes must be stopped.

A later bounded PASS will close only the rows it actually executes. It will not be
Carrier E2E, CT acceptance, rollout or full-module/E5/G5 acceptance.

## 8. Preservation

This verifier added only this audit directory. No product source, Auth policy,
configuration, contract, pack or Git state changed. No runtime process was started,
and no secret or bearer token was created or archived.
