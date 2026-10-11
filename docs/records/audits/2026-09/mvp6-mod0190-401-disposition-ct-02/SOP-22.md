# MVP6-MOD0190-401-DISPOSITION-CT-02 — SOP §22

**Control Tower verdict: ACCEPTED — approved isolated MOD-0190 work package only.** The published application-generated 401 rule is conditional on an application-generated 401 existing. The exact accepted implementation exposes authentication failure through ASP.NET Core's parser/challenge path before its module controller and does not expose an application-generated 401 path. That conditional branch is therefore **NOT APPLICABLE to this implementation**, not a missing PASS and not a waiver. The observed parser-level 401 remains classified only as a parser challenge.

This decision supersedes the `PARTIAL` disposition in `mvp6-mod0190-ct-acceptance-review-01` for the bounded work package. It does not declare repository PASS, pack `done`, common-checkout integration, gateway/public-route acceptance, live DEMAND/Workflow/Event Bus delivery, E5/G5, operational rollout or full-module completion.

## Exact authority and inputs

- Published SANDOP-CAPACITY YAML SHA-256: `9543e3f295dcabb9f7c1ab464fadfacfcbc53ada2f9bec9d32deb8f52cb02ff3`.
- Published annex SHA-256: `eb1df1383e637c744179abe4c8faa3b3b7ebe4cccd19738aadf41896a9311bda`.
- Promoted isolated pack SHA-256: `6a57769ced4396d2bc4228749a7e24b0daf36ce279930bb77c5dfdfe19fd0983`.
- Final successor source archive SHA-256: `8fa00d40814a81472ec4221ad909321d058dea18a11f64dd2ac16ea800ecb745`.
- Final 379-entry source manifest SHA-256: `ae7ef59e0158113c22389ccd3eec84207418f3a859c40480e70b42ee888a9bef`; independent before/after verification was 379/379.
- Final `Program.cs` SHA-256: `a2a216be15d07fa656f5fd7da43711aa989c4a8d52cd51c58e8ded48d5d735e5`.
- Initial CT report/matrix SHA-256: `2a0e88898ea42e278f0c0a5b195671fc0394ca5c2cead858d940726ad5189b99` / `524f69bbe8753202b3b8d49cf913a68c2d0961b3024970020992046e4fd8c9f4`.
- F01–F04 independent report/raw archive SHA-256: `f0e39309c6503380aea434645f476b8fb6913b485da2a5214af14eb5755c67b1` / `33270f8bedc63e456320e89195de6eb28dd1e55e82dd9a9eaff4072a07523a77`.
- F01–F04 fresh native .NET 8 API DLL SHA-256: `afb5971962b3ce2a65e728937dd728a50ac76eec91deee877e53463dd77d590f`.

The branch and HEAD at review were `feature/mvp6-logistics` / `4a8d4d4b339528a88e6220fb8402e5a2c771136c`. This review reused checksum-valid evidence and did not rerun tests.

## 401 applicability disposition

The published annex's common transport section states both rules:

1. an **application-generated** 401 carries `WWW-Authenticate`, a matching `X-Correlation-Id` and Error-body correlation; and
2. a parser-level authentication challenge before application context may omit the correlation header and Error envelope and must not be labelled an application response.

The frozen OpenAPI `#/components/responses/Unauthenticated` repeats that distinction. It describes how either reachable response form behaves; it does not require a consumer implementation to add a second authentication-failure route.

The exact final source establishes the only reachable unauthenticated path:

- `SandopPlansController.cs:4` applies `[Authorize]` to the controller.
- `Program.cs:95` calls `UseAuthentication`; `Program.cs:101` calls `UseAuthorization`; controller mapping follows at line 103.
- `SandopContextMiddleware.Resolve` has an application 401 return at line 13, but it is called only inside controller actions at `SandopPlansController.cs:9–20`.
- An unauthenticated request is challenged by authorization before an action invokes `Resolve`. No final-source custom authentication event, endpoint or middleware converts that challenge into the application Error response.

The independent wire evidence matches this topology: unauthenticated GET returned 401 with `WWW-Authenticate: Bearer`, zero body and no `X-Correlation-Id`. It is the annex-permitted parser-level challenge. It is not relabelled as application-generated. Reaching the dormant `Resolve` 401 through HTTP would require bypassing/reordering authorization or adding another surface, all outside the approved implementation and expressly prohibited by this review.

The promoted pack at §16 line 199 says that an “application-generated 401 [must] follow published fallback.” Read with the published annex, this is a conformance condition **if such a response is generated**. The pack does not require a distinct application-generated 401 endpoint or authorization bypass. Its six-operation 401 requirement is satisfied by the actual parser challenge under the published parser exception. Therefore there is no pack/contract conflict and no silent waiver.

## Successor acceptance consolidation

The independent F01–F04 run closes the earlier evidence gaps without changing production source:

- F01: Approved, Rejected and Archived capture each returned 409 `SANDOP_PLAN_STATE_CONFLICT` with six scoped collections unchanged.
- F02: Draft, Approved, Rejected and Archived sign-off each returned 409 `SANDOP_SIGN_OFF_STATE_CONFLICT` with six scoped collections unchanged.
- F03: a real snapshot owned by another same-scope plan returned 422 `INVALID_SNAPSHOT_REFERENCE`, with no scoped state delta.
- F04 correlation: missing, malformed and duplicate `X-Correlation-Id` each returned 400 `INVALID_CORRELATION_ID`; each response used one valid generated UUID matching header and body and produced no scoped write.
- All 24 independent Mongo before/after queries exited 0. The probe recorded exact request bytes and 30 HTTP records. Terminal fixtures used explicit scoped Mongo state updates and were not represented as normal lifecycle transitions.

Together with the content-bound prior 22-record HTTP/DB run, final 19/19 core successor, isolated 1/1 oracle regression and direct-Mongo re-VER, every acceptance row in the approved isolated pack is now satisfied within its stated boundary. The isolated 1/1 remains a subset of 19/19; record and test counts are not summed.

The row-level result is in [SUCCESSOR-ACCEPTANCE-MATRIX.tsv](SUCCESSOR-ACCEPTANCE-MATRIX.tsv). Exact hash bindings are in [EVIDENCE-HASHES.tsv](EVIDENCE-HASHES.tsv).

## Preserved non-PASS and exclusions

The broad same-host result remains **271/276**. Loads `UnknownCommitResultRetriesAndCommitsExactlyOnce` remains **0/1**. Neither is waived, reclassified or presented as a repository PASS. The bounded acceptance does not cover live DEMAND validation, Workflow, publisher/Event Bus delivery, gateway or shared permission registration, common-checkout uptake, operational migration/rollout, E5/G5 or full-module acceptance.

## Repository disposition

Only `docs/records/audits/2026-09/mvp6-mod0190-401-disposition-ct-02/` was written. No source, test, pack, contract, guard, runtime configuration or historical record was changed. No build/test was rerun and no commit, push or stash occurred.

