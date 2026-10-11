# MVP6-CAPACITY-DUPLICATE-NAME-CONTRACT-CANDIDATE-01 — SOP §22

**Verdict: CANDIDATE PREPARED / RELEASE HELD.** User authorization in the current conversation covers only a narrow, versioned successor candidate for `createCapacityScenario` 409 `CAPACITY_SCENARIO_NAME_CONFLICT`. This record grants no final version selection, exact-artifact consent, canonical publication, runtime application, pack promotion or CT acceptance.

## Baseline and ownership

- Branch `feature/mvp6-logistics`; HEAD `4a8d4d4b339528a88e6220fb8402e5a2c771136c`; initial dirty inventory 1700 entries. Existing dirty files were preserved. No staging, commit, push or stash.
- Frozen canonical YAML `docs/analysis/contracts/sandop-capacity.openapi.yaml` SHA-256 `9543e3f295dcabb9f7c1ab464fadfacfcbc53ada2f9bec9d32deb8f52cb02ff3`; annex `docs/analysis/contracts/sandop-capacity-semantics-v2.0.0.md` SHA-256 `eb1df1383e637c744179abe4c8faa3b3b7ebe4cccd19738aadf41896a9311bda`.
- Published `createCapacityScenario` 409 references `CapacityPlanStateConflict` at canonical YAML lines 495–496, whose error list at lines 1617–1619 does not include name conflict. Published annex row 31 does not list it. MOD-0192 pack §4 declares a unique scenario name per plan; repository's isolated Capacity schema has a scoped, active, simple-collation unique index. Historical independent core VER reproduced the unpublished code on sequential/racing paths; this is not new runtime verification.
- Exact output ownership: only this new audit directory. No canonical, guard, runtime, pack or other lane file changed by this work.

## Exact candidate and compatibility

| Artifact | SHA-256 |
|---|---|
| `successor.patch` | `d87ccb1f07db31f2126ff0dc49bca8a749c4c46a3da184bc60cd29ded332d8f3` |
| `sandop-capacity.openapi.candidate.yaml` | `206a5976303719b930ad850d83653fbc7d7161e6dabed3c022a0baaf15a57ff6` |
| `sandop-capacity-semantics-v2.1.0-rc.1.md` | `0a413dd641d04dadbf454968b7af99c9ea58f9cf563ed49de98f273d69019aaa` |

The proposed `2.1.0-rc.1 / CANDIDATE` metadata is a review label, **not** approved final version. Wire `contractVersion: v1`, routes, twelve operation IDs, security, requests, success/event/shared schemas and existing errors are preserved. The patch adds one 409 code/example to the response component used only by `createCapacityScenario`, plus an exact behavior row in the new candidate annex; the published v2.0.0 annex remains untouched. The candidate does not introduce a trim, maximum-length, case-folding, endpoint or body-field rule.

**Proposed behavior:** after auth/scope/schema/target, receipt replay/fingerprint, plan-state and fixture validation, an active exact parsed name collision in the same tenant/LE/plan under simple collation gives 409 `CAPACITY_SCENARIO_NAME_CONFLICT`. Deterministic lookup and unique-index race loser converge to the same result. Current request correlation appears in the header and Error body; there is no additional scenario/receipt/audit/outbox write. A valid same-key same-payload request still replays the original 201 before duplicate validation; changed valid payload remains 409 `IDEMPOTENCY_KEY_REUSED`. A truly uncertain commit remains subject to durable receipt reconciliation and is not relabeled as a definite name collision. Different case, plan, tenant or LE stays distinct.

**Compatibility:** This adds a new error code within an existing 409 response, so clients that branch on the exhaustive code set can break even though the status, Error schema, route and wire-v1 marker stay the same. The `2.1.0-rc.1` suggestion is not a compatibility verdict. Release owner must choose the final version/cutover after strict-code consumer review. Existing MOD-0190/MOD-0192 consent to the exact 2.0.0 hashes does not carry over to these candidate hashes. No producer/runtime uptake is established.

## Validation and limits

- Disposable apply: `git apply --check` and `git apply` exited 0 against exact canonical baseline copies in `/private/tmp/mvp6-duplicate-contract-apply-NqxxzY`. Applied YAML SHA-256 `206a5976…` and new annex SHA-256 `0a413dd6…` are byte-identical to the candidate; the old annex remained `eb1df138…`.
- `validation-results.md`: 19 PASS / 0 FAIL for YAML parsing, twelve-operation/path/request/status preservation, component and non-owned parity, exact 409 code/example, Error-schema validation, 204 local refs, annex policy linkage and four negative mutants. These are E1/E2 static checks only.
- Full OpenAPI 3.1 meta-schema plus `openapi-spec-validator` was **not run**: the prior offline tooldeps path recorded by `mvp6-sandop-oas31-validator-01` is absent in this environment. Independent full OAS 3.1/ref/example validation of these exact bytes remains a release-package prerequisite. The 19 checks are not presented as that complete validator.

## Reviewable acceptance scenarios and release gates

1. Same scoped Draft plan, different keys, exact same active name: first request 201; sequential or index-race loser 409 name-conflict; no extra writes.
2. Same key and valid identical body: stored 201 replay with current correlation header; no new dependency read or effects. Same key with changed valid name: 409 idempotency conflict before business duplicate.
3. Different case/name, plan, tenant or LE: no cross-scope duplicate inference; foreign plan remains 404. Non-Draft plan retains 409 plan-state before name check.
4. On definite precommit index collision, classify only the name unique-index failure as name-conflict. Unknown commit follows receipt resolution; unrelated storage faults retain published 503 behavior.
5. Before publication: independent full OAS 3.1 validation, final version choice, exact-hash MOD-0190/MOD-0192 and any other affected consumer consent, compatibility/cutover decision and single-writer canonical authorization. Only afterward can runtime code be aligned and independently verified. Static candidate preparation does not close the frozen-wire GAP.

**Writer complete.** Final input hashes were rechecked; this lane made no changes outside this directory.
