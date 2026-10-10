# MVP6-CAPACITY-DUPLICATE-NAME-CANDIDATE-01 — SOP §22

**Writer-complete; independent static VER PASS; publication/runtime HELD.**
Actual C authorizes only a narrow versioned successor candidate. This package proposes
**3.0.0-rc.1 / CANDIDATE / wire v1** and implements no production source changes.

## Authority and baseline

The actual user message at 2026-09-22T19:47:22.171Z explicitly selects preparation of
`409 CAPACITY_SCENARIO_NAME_CONFLICT` while retaining exact-name uniqueness and
requiring deterministic/unique-index convergence. AUTHORITY.md records its user role,
message ID, exact transcript location and hashes; owner-message.txt preserves the
message. This is not the unapproved wording of CONTRACT-DELTA.md being treated as consent.
A/B authorities are separate and not exercised here. C reserves final version, exact
artifact consent, canonical publication and production implementation for later gates.

| Artifact | SHA256 |
|---|---|
| Published YAML baseline, unchanged | `9543e3f295dcabb9f7c1ab464fadfacfcbc53ada2f9bec9d32deb8f52cb02ff3` |
| Published v2 annex baseline, unchanged | `eb1df1383e637c744179abe4c8faa3b3b7ebe4cccd19738aadf41896a9311bda` |
| Candidate YAML | `81f9a34b178c50e7a59b4512f77444be3a5dd0699b786a663ad5f72461245f64` |
| Candidate v3.0.0-rc.1 annex | `d040723c83990255e719b63b80e5fc189cd149c8ea19d5e06e1e2d69b0d3e3af` |
| Exact successor-candidate.patch | `231c83f239f1b55c42c4446e7f63ae55928e41bf2cc0bb2426a8e1c75d5ef303` |

Baseline hash checks use the corrected published annex `eb1df1…`, not the older
pre-status-correction `e2599f…`. INPUT-HASHES.tsv also binds CONTRACT-DELTA, DEMAND and
the two root packs; PRESERVATION.tsv proves these exact inputs remained unchanged.
Root MOD-0192 remains draft and has historical trim/max200 prose; neither that stale
prose nor Lane B's evolving source overrides C and the published exact parsed-string
contract. No new pack promotion is inferred or performed.

## Exact scope

YAML has six changed structural locations: `info.version`, `info.x-status`,
`info.x-semantics-annex`, and the existing `CapacityPlanStateConflict` response's
`description`, `x-error-codes`, `content.application/json.examples.scenarioNameConflict`.
Only createCapacityScenario uses that response. Twelve operation objects, paths,
headers, security, shared schemas, all prior codes/examples and event payloads are
unchanged. The old annex stays in place; patch adds a separately versioned annex.
Its preface identifies candidate authority; only one matrix row and one new bounded
clause change behavior. The remaining annex sections compare byte-identical.

The new example's exact message is `Capacity scenario name already exists in this plan`.
It uses the frozen Error shape, wire v1 and current request correlation in body/header.
Active exact parsed names collide only in tenant/LE/plan under simple collation.
No trim, case folding, Unicode normalization or new maximum is introduced.
Receipt replay and valid-payload idempotency conflict precede business conflicts;
Draft lifecycle and existing fixture validation precede name collision. A proven
unique-name loser has the same 409 and no extra scenario/receipt/audit/Pending outbox.
Unknown commit, arbitrary duplicate-key exceptions and exhausted transient retries
cannot be relabeled as speculative name 409. Existing 503/recovery policy is preserved.

## Verification

| Check | Result / evidence |
|---|---|
| Exact patch | patch(1) dry-run + apply in disposable baseline, output byte-equal; old annex preserved; no Git command used for application |
| Full OAS 3.1 | openapi-spec-validator 0.7.2 complete meta-schema + semantic validator PASS |
| Writer static | 28/28 checks, 204 local refs, 53 examples: 47 media/header and 6 embedded event-schema examples |
| Negative validators | Bad OAS version, missing title, broken ref, missing code, wrong wire version, malformed correlation all rejected |
| Strict-code consumer fixture | Baseline allowlist rejects new code; candidate allows it; schema alone accepts arbitrary string code, proving the compatibility blind spot |
| Behavioral model | 15/15 in-memory specification cases; deterministic/racing duplicate, scope/string distinctions, replay/state/fixture precedence, uncertain commit |
| Mutation controls | Trim, duplicate-before-receipt, race-as-503 and unknown-as-name-409 mutants all rejected |
| Independent static VER | Separate read-only subagent reran checks/model, independently verified six event-schema examples, C provenance and exact artifact hashes; final result PASS |

Raw writer results: validation-results.json, validation-stderr.txt, model-results.json;
commands are reproducible from VER-HANDOFF-v1.0.md. Validator was installed only in a
disposable tool directory after the prior temporary tool location was absent. Repository
dependencies/lockfiles were not changed. TOOLCHAIN.txt records versions and schema hash.
LibreSSL/RefResolver warnings are retained; both verification commands exited 0.
Independent raw evidence is archived separately under independent-ver/.

## Compatibility, limits and next gate

COMPATIBILITY-AND-CUTOVER.md evaluates patch/minor/major options. Proposed major v3
conservatively exposes strict-code risk. It does not negotiate routes or convert
wire v1 into a new transport. A 2.1.0 classification is not assumed compatible; real
consumer evidence and explicit owner classification would be required. MOD-0190
semantics are unchanged, but its old shared-artifact consent cannot move to new hashes.

This is E1/E2 artifact/model evidence, not real HTTP/JWT/Mongo race/rollback/restart,
consumer uptake, Lane B X01/X07 acceptance, source application or production readiness.
No full runtime/architecture suite is claimed. No guard or canonical change, migration,
rollout, pack promotion, permission/gateway/Program.cs edit, commit/push/stash occurred.
Only this new audit package was written; other lanes' files were not edited. Because
lanes run concurrently, no repository-wide no-change assertion is made.

VER-HANDOFF-v1.0.md is the hash-bound independent candidate verification handoff.
Further formal VER may reproduce it; final version/artifact consent, consumer cutover,
canonical publication and duplicate-name production implementation remain explicitly
HELD. A later status/version edit changes hashes and must not inherit this review's
exact-byte approval or any old consent.
