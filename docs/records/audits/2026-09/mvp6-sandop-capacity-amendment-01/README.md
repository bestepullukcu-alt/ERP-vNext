# MVP6-SANDOP-CAPACITY-AMENDMENT-01 — SOP §22 candidate handoff

**Technical verdict: CANDIDATE GENERATED / PARTIAL AUTHORITY. Publication, release consent and DEV GO: HELD.** The one proposed versioned release is `SANDOP-CAPACITY` **2.0.0 candidate**, still wire `contractVersion: v1`. This is a version recommendation, not an approved version. Current branch/HEAD: `feature/mvp6-logistics` / `4a8d4d4b339528a88e6220fb8402e5a2c771136c`; initial common-checkout dirty inventory 170 rows. One writer produced only this owned archive and disposable verification copies under `/private/tmp`; no canonical, DEMAND, pack, guard, Program.cs, runtime or Git mutation.

## Authority boundary

The real 2026-09-22 user scope approval is recorded at `docs/records/audits/2026-09/mvp6-mod0190-0192-scope-disposition-01/README.md`: test-only scoped DEMAND exact fixture; local trusted actor/no Workflow; atomic Pending outbox/no publisher; Capacity fixture references/literal oracle/no live optimizer. It expressly says the D190/C192 policy proposals were **not** approved. The inspected `docs/roadmap/plans/mod-0190-exact-policy-01/OWNER-DECISION.md` and `mod-0192-exact-policy-01/DECISION-PACK.md` still label their exact lifecycle/replay/executor choices proposed/unapproved. No later exact human approval record was found in the inspected decision/audit paths. The current user request authorizes preparing a candidate, but its word “approved” is not enough to identify which exact policy version was accepted. This package therefore **does not present those proposals as approved**. An asynchronous request for the actual owner record was sent; bind its answer before any authority promotion.

## Exact artifacts and method

| Artifact | SHA256 / status |
|---|---|
| Frozen baseline `docs/analysis/contracts/sandop-capacity.openapi.yaml` | `c255e92923ba91714cec8daf229262101b5d45648b7f714a03ab402811db683c` (unchanged) |
| Candidate YAML `sandop-capacity.openapi.candidate.yaml` | `88070dc3fa27aff9ba0d4b40ba1a43641906f77d369e4ce1a84be75ae267816d` |
| Candidate annex `sandop-capacity-semantics-v2.0.0.md` | `c3fb876cf19d8f6943781a6ea55d0c9f07d6c4137505b6b27b1718739aaa644c` |
| Two-file `publication.patch` | `20b295e96ca9d934578fd9e5155e32a09675bb1f0815f0e3e697cc138f861648` |
| Frozen DEMAND `docs/analysis/contracts/demand.openapi.yaml` | `3c77262e411976e311bcf9b65be131e2035fd18a33215d24075f87209f87bb9d` (unchanged) |

The patch has **exactly two publication targets**: existing SANDOP-CAPACITY YAML and new proposed `docs/analysis/contracts/sandop-capacity-semantics-v2.0.0.md`. `git apply --check` and `git apply` succeeded in `/private/tmp/mvp6-sandop-capacity-amendment-verification-02/`; reconstructed bytes match both candidate hashes. The candidate preserves all twelve operation IDs/routes, all original success/business response statuses, request schemas, pre-existing schema/parameter/header/security/event objects and `contractVersion: v1`. It adds proposed 400/401/403/503 responses, constraint 422, new response components/code variants, metadata version and annex pointer. Six S&OP and six Capacity operations appear in `operation-matrix.tsv`. No Demand operation or other repo file is in the patch.

`verify_candidate.py` parsed OpenAPI 3.1.0, resolved 216 local references and validated 93 response examples with JSON Schema Draft 2020-12: zero failures. `semantic_checks.py` confirmed 12 operations/six mutations, no key or name max/trim, required new statuses/code tags; four deliberately broken mutants were rejected. `verification.log` contains the exact local output. **A full OpenAPI 3.1 document meta-schema validator was not installed locally**, so this check is syntax/root/ref/example/schema validation, not a claimed full OAS meta-schema PASS. Independent VER must repeat and may add that check. No HTTP/JWT/Mongo/consumer run occurred.

## Candidate policy and compatibility limits

The annex and `operation-matrix.tsv` give every operation's status/error/header/correlation/replay oracle. Proposed 422 `INVALID_DEMAND_REFERENCE` covers mismatch of a **test-only** ID/version-matched fixture checksum on 0190 capture or 0192 create; it is not live DEMAND checksum verification. New 422 `INVALID_CONSTRAINT_REFERENCE` applies only to 0192 fixture tuple/reference mismatch; no live constraint producer exists. Proposed unresolved unknown commit returns 503 `COMMIT_RESULT_UNRESOLVED` only after durable receipt lookup fails; first 503 cannot prove zero writes and same-key recovery is required. Exact parsed Idempotency-Key strings remain exact; `minLength:1` is unchanged, without invented trim/max length. Header/body uses current valid correlation; original replay body and persisted audit/event retain original business result/trace.

The proposed major metadata change reflects potential compatibility impact from new rejection/state rules, not route negotiation or migration. Existing consumers may need to handle 400/401/403/503, new 409/422 codes and the current-header/original-body replay split. No prior SANDOP consumer consent was found or inferred. Candidate preparation does not authorize publication, shared composition, pack promotion or isolated runtime.

## Only remaining release decisions

1. Bind the actual human approval for D190-04/05 and C192-02…07, or explicitly mark the candidate policy portions for owner rework. The earlier scope approval covers only four limits. In particular, 0192 executor 10s/30s/3 bounds and 0190 lifecycle/sign-off behavior cannot be silently promoted.
2. Contract owner approves the exact 2.0.0 YAML+annex hashes and compatibility disposition; consumers consent to **those bytes**, not to earlier v1 or a draft policy.
3. Independent candidate VER executes `INDEPENDENT-VER.md`; separately check full OpenAPI meta-schema if available. Only then can a distinct single-writer publication/guard step be considered. Runtime/Phase 1.5/pack promotion remain separate.

No historical inputs were rewritten. Parallel writers make whole-repository status equality unsafe to claim; the frozen baseline/DEMAND and the specific published packs were hash-checked, and this lane changed only its owned archive. No commit, push or stash.
