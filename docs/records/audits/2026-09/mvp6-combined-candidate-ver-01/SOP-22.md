# MVP6-COMBINED-CANDIDATE-VER-01 — SOP §22

Date: 2026-09-20. Role: verifier independent of candidate author. Review and disposable contract/model execution only.

## Verdict

**PASS — exact combined candidate, bounded E1/E2. Publication remains HELD.**
Actual baseline→candidate patch applied, both recorded context-rebased orders applied at fuzz0, and outputs match expected YAML SHA256:
`d763b571a1c6b88cbfadc56af2c51da1c0009589e3b82bcbe122329b17213c88`.
Combined patch SHA256: `6ab16d480df3d4d38743e18f019c45c781cab6ec69350c7319b2aebf7318765b`.
Baseline SHA256: `93c696e2fba13dbc8fbfcf2cd1ae0ae0bd93cd9d0935b3ee743229e810163571`.
No candidate corrections were made. This is not HTTP/JWT/Mongo, runtime uptake, publication or owner consent.

## Authority and baseline

Actual user messages authorize Root persisted-root design/candidate preparation and reconciled D186/D187 separate consumer amendment candidates. The current message authorizes independent combined verification. None is converted to consumer release consent or publication authority. Historical A/B proposals and candidate authority files are not human consent. Existing reconciled policies are preserved.
Branch `feature/mvp6-logistics`; HEAD `4a8d4d4b339528a88e6220fb8402e5a2c771136c`; preexisting concurrent dirty work preserved.
Disposable directory `/private/tmp/mvp6-combined-ver01-x6kwmo_1/`.
Input package has SOP-22.md, not README.md; INDEPENDENT-VER.md is present and consumed. All outer/inner candidate manifest hashes checked, plus original3archive and named member pins. Accepted successor model archive hashes checked before execution. Exact inputs in `candidate-manifest.sha256` and archive `original-input-pins.json`.

## Actual application and semantic ownership

- Original Root→Returns→Claims and Root→Claims→Returns were each actually applied. In each order the third original patch has one adjacent-context hunk failure. Fresh rejects/logs retained. These failures are expected textual context observations, not suppressed success.
- Root original patch followed by the recorded consumer context-rebased patches succeeds in both orders, fuzz0. Both final files match byte-for-byte and by AST, and match the handed-off candidate.
- Exact combined.patch also independently applies to the original baseline with the same resulting hash. No dry-run substituted.
- Returns and Claims operation **text blocks and ASTs** equal their respective original candidates. Each original consumer delta is confined to its own paths. Whole-document expected union (Root candidate + exact4consumer paths) equals the combined candidate. Therefore context regeneration introduced no extra business policy.
- Shared components equal Root R2 exactly; only original Root changes are inherited. Root ShipmentDetail/getShipment content matches Root R2. Webhooks unchanged. All7other paths are baseline-identical: shipment list/create, shipment transition/POD, Carrier operations, Loads operations. Top-level nonowned content preserved by whole-document union comparison. Decimal/Error not widened or narrowed by composition.
- Annex hashes are original bytes: Root `fceac091e75a4407210c80b81ca6b6757cc7199d4e09546dda39f3f8271c269b`; Returns `ae5db7f45c4ba5d0d1a6772904037193c52c4cc730f3277096d74579f413a49c`; Claims `c33cefe16c5cee75e4f8102dba13328c974753be5ca4c31acbf37c45226d47fa`. Returns alias identical. Loads/Carrier annexes unchanged. Local annex map retained; final destination/link choices still release work.

## Fresh validation and command outcomes

Exact commands/results/logs are archived. Validator installed only into disposable tooldeps; version/dependency metadata saved.

|Check|Fresh result|Evidence|
|---|---|---|
|Full OpenAPI3.1 validation|0errors, openapi-spec-validator0.7.2|openapi31-results.json, openapi31-rerun.log|
|Official bundled meta-schema|`https://spec.openapis.org/oas/3.1/schema/2022-10-07`; schema snapshot retained|openapi31-metaschema.json; validator-toolchain.json|
|Meta-schema negative controls|3/3 rejected: wrong OpenAPI version, missing info title, non-string response description|metaschema-negative-controls.json|
|Combined ref/example/source-union checks|68/68;298refs;236examples|combined-refs-examples.log; combined-results/validation-results.json|
|Returns accepted successor model/schema harness|293checks PASS;279refs;174examples on original Returns schema with operation-exact combined binding|returns-model-schema.log; returns-results/tests.json|
|Claims accepted stateful model|35checks PASS|claims-stateful.log; claims-results/stateful-results.json|
|Claims negative model mutations|7/7 caught by assertion failures|claims-mutants.log; claims-results/mutation-results.json and mutant raw outputs|
|Additional independent combined/model checks|126/126 PASS|independent-extra-results.json|

The126 include actual model calls for **64Returns +49Claims canonical source×target pairs**; forbidden pairs preserve modeled business state. Returns InTransit→Cancelled remains forbidden. Additional checks cover nullable/missing/nil/uppercase/malformed root values, Claims policy binding, Returns UUID replay and a deliberately broken lexical comparator, plus combined root-required/conflict-code/shared-Decimal mutants. Counts overlap in subject matter and are not added into a fictitious unique behavior total.

OpenAPI schema snapshot canonical-JSON SHA256: `0714800c90c6e36240e9ed7854797449553bce259e0c5d58a311c0cdf2e15056` (file formatting hash separately in manifest).
The first schema-report attempt failed to serialize the validator's lazy schema proxy; the verifier loaded the same bundled schema as a plain object and reran. Both logs retained; this was verifier instrumentation, not a candidate change or OpenAPI failure. Offline validation emits a LibreSSL/urllib3 warning; no schema/reference fetch is required, and checks exit0.

## Policy and evidence boundaries

Returns: inherited authoritative root, Delivered/Closed, exact quantity/UoM, entitlement/release model, manual Received, opaque reference, no Inventory/Warehouse HTTP model clients, numeric/instant fingerprint preserved. Root nil is syntactically valid; parse success is not historical authority proof. Schema optionality does not enforce the normative upgraded-producer always-emit obligation; that remains runtime work.
Claims: Withdrawn=investigate; Closed=decide;422INVALID_CLAIM_TRANSITION;409IDEMPOTENCY_KEY_REUSED;409CLAIM_CORRELATION_MISMATCH; lexical amount/time, zeroapproval and granted selfapproval policies preserved. Four distinct modeled stages/rollback, root-before-payload receipts, unknown-commit recovery and Pending-only outbox exercised only within accepted model scope. No worker/publisher/finance posting added.
Module-specific permissions, error distinctions and fingerprint rules remain distinct. Declaration-only and unverified R01–R30 items are not promoted to executed behavior. In particular model PASS does not establish full monetary/reference/parser/storage enforcement or unlimited transport/DB capacity.

No actual HTTP/JWT auth, Mongo transactions/concurrency, persistence/restart, producer emission, consumer uptake, gateway/UI, operational DB or deployment was performed. Root access still needs published AND implemented AND verified producer seam for real consumer create; fixtures do not supply it. Loads multi-Shipment root disposition remains outside this result.

## Only remaining release decisions

1. Accountable release owner selects final version and annex destinations/link/status finalization. Current artifact is **info.version2.0.0 / CANDIDATE / wirev1**.3.0.0 is only a recommendation. Any finalization changes produce new hashes, with targeted delta verification.
2. Bind actual consumer release dispositions/consents and publication inventory to those final YAML/annex/patch hashes. Prior grants do not transfer automatically. Draft Returns/Claims design compatibility remains distinct from running Shipment/Loads consumer readiness.
3. Prepare separately authorized guard target binding/payload and actual decision-file hash for final bytes, preserving17historical seals. Run production-mode ReadAuthority/full guard only in the authorized coordinated publication process. This task does not activate guard or waive its open disposition.

No new business decision, product rework or repeat PREP loop is required by this technical result. Runtime uptake, operational migration/data provenance, rollout, pack promotion, Phase1.5/DEV GO are separate excluded authorities, not implied by release consent.

## SOP §22 completion / preservation

Golden flow: actual three-way contract composition. Subflows/failure paths: expected original context rejects, schema negatives and model mutants. Persistence/security: modeled only, no runtime claim. Audit/evidence: fresh raw outputs and exact manifests archived. Observability: command exits/logs retained. Migration/rollback: no application; no live rollback needed. Known gaps: release decisions and runtime boundaries above. Candidate/3original archive hash pins rechecked at end; inspected bytes unchanged. No global repository no-change assertion with concurrent lanes. Only this owned directory and lane disposable output written; no canonical/runtime/guard/pack/git mutation, commit/push/stash or synthetic approval.
