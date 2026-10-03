# MVP6-SANDOP-CAPACITY-AMENDMENT-CANDIDATE-01 — SOP §22

**Agent verdict: CANDIDATE READY FOR CONTRACT-OWNER REVIEW; PUBLICATION NO-GO.** The 2026-09-22 user message approved preparation of one amendment candidate and the bounded D190-04/05, C192-02…07 design choices with exact-key/no-trim correction. It did **not** approve the three newly proposed wire dispositions, canonical release, consumer consent, pack promotion, Phase 1.5, Program.cs or runtime DEV. The previous test-only DEMAND/constraint and Pending-outbox decisions remain scoped as stated in `semantics-proposal.md`.

## Exact inputs and outputs

Branch `feature/mvp6-logistics`, HEAD `4a8d4d4b339528a88e6220fb8402e5a2c771136c`. Checkout was dirty before this work; staged state empty. Frozen baseline `docs/analysis/contracts/sandop-capacity.openapi.yaml` SHA-256 `c255e92923ba91714cec8daf229262101b5d45648b7f714a03ab402811db683c`; DEMAND v1 SHA-256 `3c77262e411976e311bcf9b65be131e2035fd18a33215d24075f87209f87bb9d`. Design inputs: `mod-0190-exact-policy-01/OWNER-DECISION.md` SHA `3fcefe080f84fc0fc756e7d1ed10e97e2e2a60eb8956dbc9a59f149dec04f085`; `mod-0192-exact-policy-01/DECISION-PACK.md` SHA `acb1b8f0ec1756aa72782c02b0a8af8eb067289a6770a9ca6bef5ccde46c7f6b`; fixture oracle SHA `d4af417d50ff038fa0db7cc3a8bbaa80143ddf4ce550edd2781fba96c00fed9a`.

Candidate YAML SHA-256 `aa6a1e1e238ad347e03dc793dfef8f6f7dad786ec0bb9d7871603f63f4d8a658`; applicable patch SHA-256 `e0a35b3cc3f2259a133b12117c359288db3c7ffcfe1c6ae6e92853eec62cf4d3`. Proposed metadata is `1.1.0-rc.1` / `CANDIDATE` / wire `v1`, not a published final-version selection. The patch changes metadata, exact-key/header descriptions, operation responses, the shared DEMAND error description/examples, and six operation-specific 409 examples. It does **not** change successful response bodies, event schemas, shared model schemas, security scheme, routes or DEMAND contract. Full operation matrix and candidate-only semantics are in `semantics-proposal.md`.

## Validation and evidence level

`python3 .../verify_candidate.py` exit 0; output `validation-results.txt`: OpenAPI 3.1.0 parsed, 12 operations, six mutations, all local `$ref` pointers resolved, 23 response examples validated against the Error shape, previous response refs and all 200/201/202 response objects unchanged, shared schemas/events unchanged, disposable `git apply --check` and apply produced a **byte-identical** candidate YAML. The validator is a targeted E1/E2 check, not a full OpenAPI met schema linter, mock runtime, consumer test or E4/E5 evidence. The historical `FIXTURE-ORACLE.json` remains untouched despite its old UNAPPROVED label; the current user message is design authority for candidate preparation only.

## Release and consumer impact

`docs/analysis/contracts/README.md:45-50` freezes contracts and permits additive minor changes while prohibiting breaking changes. This candidate adds 400 on all twelve operations, 503 on six mutations, 422 invalid constraint on two operations, new 409 variants on six mutations, and a checksum-mismatch interpretation of existing 422. Existing exact-code clients may need changes even though success shapes remain stable. Consequently **1.1.0 minor compatibility is unproven**. Contract owner must review the wire matrix and decide whether minor is valid or a major replacement is required; both module consumers must consent to the exact final artifact/hash. No prior consent transfers automatically. Canonical publication, pack/Phase 1.5 and runtime uptake remain separate gates.

## Output / preservation

Only this new audit directory was written: candidate YAML, `publication-candidate.patch`, `semantics-proposal.md`, `verify_candidate.py`, `validation-results.txt`, this report and `SHA256SUMS`. Canonical contract, DEMAND, packs, Program.cs, runtime, guard, historical records and Git state were not modified. No commit/push/stash or runtime tests. No publisher, migration, rollout, E5/G5 or DEV GO.
