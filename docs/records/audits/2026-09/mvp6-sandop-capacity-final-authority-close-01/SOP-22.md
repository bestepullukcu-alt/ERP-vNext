# MVP6-SANDOP-CAPACITY-FINAL-AUTHORITY-CLOSE-01 — SOP §22

**Control Tower verdict: PARTIAL — authority provenance reconciled; publication NO-GO.** The original owner selected preparation of `2.0.0 / wire v1` and clarified that there are no repository-external SANDOP application/SDK/integration consumers. Those two facts are now bound to original user messages in [AUTHORITY-MATRIX.md](AUTHORITY-MATRIX.md). The exact MOD-0190 and MOD-0192 final-hash release consents and separate canonical publication authority remain open. [One copyable decision text](OWNER-DECISION-REQUIRED.md) contains only those missing clauses. No decision draft is treated as granted.

## Scope, baseline and changed files

- Agent verdict: technical final-package evidence remains PASS at E1/E2; this CT authority review is PARTIAL.
- Branch/HEAD: `feature/mvp6-logistics` / `4a8d4d4b339528a88e6220fb8402e5a2c771136c`. Staged index empty. Checkout was dirty before this task; unrelated work was preserved. No branch switch, commit, push or stash.
- Owned changes: only `SOP-22.md`, `AUTHORITY-MATRIX.md`, `OWNER-DECISION-REQUIRED.md`, `INPUT-HASHES.tsv` and `SHA256SUMS` in this new directory. No canonical, pack, guard, runtime or predecessor-audit edits.
- Frozen canonical input: `docs/analysis/contracts/sandop-capacity.openapi.yaml`, SHA-256 `c255e92923ba91714cec8daf229262101b5d45648b7f714a03ab402811db683c`.
- Single exact decision target: [final-pack-01](../mvp6-sandop-capacity-final-pack-01/) YAML `9543e3f295dcabb9f7c1ab464fadfacfcbc53ada2f9bec9d32deb8f52cb02ff3`; annex `e2599fd9b8cc7cf7e4a39b4d5d1bccb3fa985c156fec6c6082f5227334770442`; two-file patch `0ebc6a6fa6a867170b1d32545295e88676d74fb47794879a671c9f6c0375bd04`. The alternate final-release-pack-01 is retained as history and is **not** silently substituted.

## Verification and evidence level

- [Independent final VER](../mvp6-sandop-capacity-final-ver-01/SOP-22.md) SHA-256 `2c1fdbe120a41215c72e0d81c573b578557c9d0a1c2ba0c11c6597826a8974fb`; raw archive SHA-256 `eb125140e92cb56829ac17523496b31efe0607378e27ae145370524d3e3e91bf`. Its SHA256SUMS verified **15/15** entries. The final package SHA256SUMS verified **8/8** entries. The canonical baseline and target trio were also hashed directly and matched.
- The independent VER established disposable patch apply/byte equality, full OpenAPI 3.1 meta-schema and validator, refs/examples, 12-operation YAML↔annex parity, and non-owned invariance. It recommended this final-pack-01 artifact set. Its conclusion was **PARTIAL** on authority because it did not have the original owner message; this successor binds that original message without rewriting the historical report.
- Original owner evidence: source rollout and message IDs, UTC times, line numbers and extracted-text hashes are in [AUTHORITY-MATRIX.md](AUTHORITY-MATRIX.md). The 08:41 message expressly authorizes final-package *preparation* while withholding canonical publication and exact-hash consumer consent. Its bracketed outside-consumer choice was resolved by the 08:42 user answer “Yoktur.” The latter is an owner assertion, not a repository scan result.
- No unchanged validator/model test was rerun. E1/E2 is inherited only while all listed input hashes remain fixed; no E3/E4/E5, runtime uptake or published contract is claimed. Search of accessible user messages and decision records found no final-hash MOD-0190/MOD-0192 consent or publication authority. Missing evidence is represented as OPEN, not as rejection of a decision that may exist elsewhere.

## Decision, compatibility and blockers

The final-version/inventory preparation provenance is **CLOSED**. Publication remains **NO-GO** for three separate missing authorities: MOD-0190 consent, MOD-0192 consent, and conditional canonical publication authorization. The canonical contract remains frozen `1.0.0`. `x-status: FROZEN` in the proposed YAML does not publish it. The same-route `wire v1` change is compatibility-sensitive because lifecycle, fixture rejection and status/error behavior differ even where success/event schemas remain stable. Both consumers must acknowledge that cutover at the exact final bytes. The owner's “no external consumers” clarification does not waive MOD-0190/MOD-0192 review or cover any later-discovered consumer. SANDOP is not a current DocsPathGuard target; this work does not create a binding or expand the guard.

## Single-writer publication and preservation plan — conditional, not executed

1. Record the two consumer consents and the separate publication clause from the actual authorities. Check for any newly discovered consumer and disposition it. Recheck branch, HEAD, staged state, dirty inventory, contract writer exclusivity, canonical baseline hash and exact package YAML/annex/patch hashes. A mismatch stops the publication.
2. In a hash-bound disposable copy of the current dirty-source baseline, run `git apply --check` and apply only the exact patch. Require byte equality to the two target hashes and only the canonical YAML plus new annex paths changed. Repeat full OpenAPI 3.1, refs/examples, annex links and relevant docs/architecture guard checks in production mode; preserve logs and exit codes.
3. Recheck the real checkout baseline immediately before application. A single contract writer then applies that same patch to only `docs/analysis/contracts/sandop-capacity.openapi.yaml` and `docs/analysis/contracts/sandop-capacity-semantics-v2.0.0.md`. Verify both resulting hashes, protected-file hashes and no unrelated file changes. Record a new publication SOP §22 and consumer uptake as separate evidence; static release checks are not runtime uptake.
4. If a check fails before real application, stop without checkout mutation. If real application partially fails, restore only the exact preimage of the two authorized files after confirming their current hashes; preserve all other dirty work. Do not use `reset --hard`, stash, blanket clean or historical-report edits. Escalate any baseline conflict instead of guessing a merge.

## SOP §22 fields

| Field | Result |
|---|---|
| Golden/contract flow | Proposed two-file final package technically verified; original final-preparation and external-inventory authority now bound. Canonical application held. |
| Sub-flows / failure paths | Consumer and publication gates separated; changed hash, new consumer or baseline drift fail closed. |
| Tests | Inherited/content-bound independent E1/E2 checks; no fresh test run this task. Input checksum checks 8/8 and 15/15 PASS. |
| Persistence / security / observability | No runtime action; not assessed by this authority review. |
| Migration / rollback | No migration. Conditional two-file preimage preservation and scoped rollback plan above. |
| Decisions | Final preparation and no outside consumers bound; three release decisions open. |
| Blockers / known gaps | Exact-hash MOD-0190 consent; exact-hash MOD-0192 consent; separate two-file canonical publication authority. Runtime uptake, pack Phase 1.5 and rollout remain later gates. |
| Out-of-scope changes | None. |
