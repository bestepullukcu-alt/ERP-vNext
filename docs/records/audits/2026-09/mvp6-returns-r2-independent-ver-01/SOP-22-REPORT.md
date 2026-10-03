# MVP6-RETURNS-R2-INDEPENDENT-VER-01 — SOP §22

Verification Verdict: **REWORK**. Separate verifier /root/independent_returns_ver independently inspected and executed candidate evidence; candidate author/parent performed packaging and integrity only.

## F01 — model root UUID equality (model/evidence defect)
returns_model.py:44 and:51 compare raw strings, contrary to semantics-candidate.md:16 and:63 (hex case allowed, UUID by value).
Witness: fresh create with lower-case root→201; same UUID upper-case replay→409 CORRELATION_ROOT_MISMATCH;
same UUID upper-case Authorized transition→409. Expected replay201/REPLAY with zero new writes; transition200
when required grants/lifecycle valid. Model root_status already compares parsed UUIDs, so model paths disagree.
No production implementation exists or was tested here; this is not a reported runtime defect.

Closure: separate rework to model equality and case-equivalence fixtures only; candidate YAML/annex policy need not change.
Test lower/upper/mixed forms in create receipt replay, transition receipt replay and fresh transition. Preserve true different
UUID409, root-before-payload precedence and same UUID+changed payload IDEMPOTENCY_KEY_REUSED; replay write count unchanged.
Re-run all293+6 checks,64 actual lifecycle calls and mutation controls; issue new hash-bound evidence and independent VER.
This VER changed neither source nor candidate.

## Fresh executed evidence
- Archive + embedded manifest27 checks PASS; full SHA256SUMS validated.
- Supplied293 schema/model checks PASS;174examples and279refs, exact patch application verified by suite.
-6root schema cases independently PASS; dependency schema/semantics snapshots match live RootR2 hashes.
- Canonical lifecycle8states/64pairs/7arrows. Independent Store.mutate calls all64 pairs PASS; InTransit→Cancelled422.
-8disposable mutants all caught: forbiddenedge,nil lost,lexical quantity,no release,no UoM,no payload conflict,
  external-call counter and Closed release. These negative controls do not cover F01, which was found separately.
- Only2Returns paths/3operations modified; components/webhooks unchanged; producer root patch not embedded.
- Candidate baseline equals current canonical93c696…3571. Exact input identities in exact-inputs.tsv.

## Limits / required boundaries
Entitlement cap and release, exact quantity/UoM, numeric/instant fingerprint, manual Received and opaque references are
normative/model checks. Source drift/required-field classification, fullJWT/header middleware, opaque retention/audit details,
event causation/number collisions and all write-position faults are not exhaustively modeled. External-call counter is
model evidence, not actual network tracing. Serialized race/rollback/unknown-commit models are not Mongo transaction or
concurrent process tests. No actual HTTP/JWT/persistence/restart/consumer uptake or operational DB access.
Real D186 candidate preparation approval preserved; no repeat consent. Old CT technical PASS not inherited.

## SOP22 fields
Branch/HEAD: feature/mvp6-logistics /4a8d4d4b339528a88e6220fb8402e5a2c771136c.
Worktree: pre-existing dirty; fresh baseline and no-change in archive. Existing-file drift0; concurrent additions not attributed.
Changed files: only this new permanent audit directory plus unique disposable evidence directories.
Golden/Contract flow: unpublished Returns candidate only; RootR2 dependency not publication/uptake.
Sub-flows/failure paths: independent report and witness files; F01 is open.
Tests: commands/exits and independent mutation outputs archived; first parent extraction API incompatibility recorded separately.
Persistence/Security/Audit/Observability: model/static only; no operational enforcement claim.
Migration/Rollback: none; no source/canonical/candidate repair.
Decisions/Blockers: REWORK F01 before candidate technical acceptance; separate release/composition gates remain.
Out-of-scope: no canonical/runtime/pack/guard/git modifications or DEV GO.

[Independent report](INDEPENDENT-REPORT.md), [exact inputs](exact-inputs.tsv), [evidence archive](evidence.tar.gz), [hashes](SHA256SUMS).
Evidence archive contains JSON/scripts as immutable test evidence; no new loose JSON inventory into legacy docs paths.
