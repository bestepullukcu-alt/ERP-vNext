# MVP6-184-CANDIDATE-R1 — CARRIER-FIXTURE-01 narrow candidate correction

2026-09-17 · SOP §22 · Artifact writer only · **NOT PUBLISHED / NOT OWNER APPROVED**

## Agent verdict / decision

**Candidate technical correction PASS; proposed example policy awaits contract-owner approval.**
CARRIER-FIXTURE-01 reproduced on the old candidate and absent from R1. This is candidate-level repair,
not a declaration that the published contract, owner gate or GAP-184-04/05 is closed. Separate independent
candidate verification and publication/consumer decisions remain required by development plan v2.0.
No runtime, canonical publication, pack promotion or DEV GO is inferred.

## Exact scope / owner proposal

Only semantic candidate delta:
`#/components/responses/CarrierCreateUnprocessable/content/application~1json/examples` is removed.
POST /carriers422 response reference, description, Error schema and correlation header are unchanged.
Existing shared Unprocessable and all original shared components are unchanged. No request, success,
lifecycle, error code, create business rule or status is added or removed.

Removed local inherited examples: shipmentTransition, loadTransition, returnTransition, claimTransition
and inventory. The first four reproduce the reported failure: foreign lifecycle codes plus error.details.
The fifth (INVENTORY_REFERENCE_INVALID) passes the sanitized parser but describes an Inventory reference
failure that this bounded Carrier create does not perform. Retaining it would leave a misleading default
mock scenario after removing the four failing examples. Hence remove the whole local examples map;
all five remain available in the untouched shared response for their original users.

**Owner option A — recommended, encoded in R1:** retain create422 as a declared response/schema without
an inline example until an applicable create business condition is approved. No policy exception or new
business scenario is invented. Mock/test tools must not use inherited foreign examples or claim a
schema-generated422 response establishes an approved business case.
**Owner option B:** retain inherited illustrations but explicitly mark them inapplicable to Carrier mock
selection through a reviewed normative/tooling policy. This leaves tool-selection ambiguity and requires
additional semantics; it is not implemented. Replacing them with a made-up Carrier422 code is not an option
for this WP. Owner must approve option A for these exact hashes or choose a separately recorded policy.
The user's request authorizes preparation of this candidate; no missing owner decision is treated as consent.

ASSUMPTION-R1-01: optional OpenAPI examples may be omitted while retaining the response/schema.
This changes candidate mock guidance, not a runtime outcome. No Carrier create422 scenario is certified.

## Before / after evidence

| Check | Old candidate rerun | R1 |
|---|---|---|
| Consumer inline policy | 4 findings, all create422 lifecycle examples | 0 findings |
| Operation/status inventory | 27 unchanged | 27 unchanged |
| Inline response groups | 26 PASS / create422 finding | 26 PASS / create422 N/A (no approved example) |
| Static exchange fixtures | 44 PASS | 44 PASS; fixture bytes unchanged |
| Deliberately bad fixtures | 7 rejected | 7 rejected |
| OpenAPI3.1 / refs | Existing original package retained | PASS;238 refs |
| Inline schema examples | 93 in old package | 88 after removing5 local examples |
| Original frozen Carrier examples | 5 | 5 preserved |
| Non-Carrier paths | 10 | all unchanged |
| Existing canonical shared components | baseline | all unchanged |

Negative fixtures: missing response correlation; success-body correlation leakage; wrong status-replay
status; unknown202; outer data envelope; error header/body mismatch; absent401 Bearer challenge.
The old candidate is run through the same adapted harness and still records its four failures: this proves
that the new zero-finding result is not produced by ignoring populated create422 examples.
The only harness applicability change allows **empty C422 examples only** and explicitly records N/A;
any other empty response group still fails. All original parser/policy checks and seven mutations remain.
The separate revision assertion deep-compares old and new YAML and fails on any semantic change beyond
that one examples map. This is not a generic relaxation of the Error schema or details policy.

Evidence files: before/results.json, after/results.json, validation-results.json, revision-results.json.
No HTTP/server/SDK, precedence execution, DB counts, audit durability, concurrency or restart was measured.
Headers in inline tests are expected fixture values. RP02 assertions are fixture declarations, not persisted
state. Static fixture PASS is neither actual consumer uptake nor owner acceptance nor E4.

## Reproduction

From repository root, using the already installed temporary validation environment:

```sh
/private/tmp/mod0184-oas-tools/bin/python docs/records/audits/2026-09/mod-0184-contract-publication-v1.1.0-r1/validate.py
/private/tmp/mod0184-oas-tools/bin/python docs/records/audits/2026-09/mod-0184-contract-publication-v1.1.0-r1/check_consumer.py docs/records/audits/2026-09/mod-0184-contract-publication-v1.1.0 docs/records/audits/2026-09/mod-0184-contract-publication-v1.1.0-r1/before
/private/tmp/mod0184-oas-tools/bin/python docs/records/audits/2026-09/mod-0184-contract-publication-v1.1.0-r1/check_consumer.py docs/records/audits/2026-09/mod-0184-contract-publication-v1.1.0-r1 docs/records/audits/2026-09/mod-0184-contract-publication-v1.1.0-r1/after
/private/tmp/mod0184-oas-tools/bin/python docs/records/audits/2026-09/mod-0184-contract-publication-v1.1.0-r1/check_revision.py
git diff --check
```

All commands completed exit0; before harness exit0 means execution completed, **not finding-free PASS**.
Validation uses openapi-spec-validator0.7.2, jsonschema4.25.1, PyYAML6.0.3. The final publication patch is
applied only to a disposable copy for output-hash/OAS/annex resolution checks, never canonical files.
Inherited nonfatal patch EOF blank-line warning remains; no unrelated cleanup added to this narrow revision.

## Exact hashes / artifacts

| Artifact | SHA-256 |
|---|---|
| R1 candidate YAML | 05a7ad0c8e26f46983a712e475f39fa5288164f0385f351e3d3a04860d9d8034 |
| R1 proposed publication patch | 9f96389b08758ee4fb5d12fe9d6381a7cadaff058deeb8d46b36f89466e4cd88 |
| Proposed final canonical YAML, not published | ba9d85f086dd2bfc150c1818843fa22c5b00b0dba1948a57a3672e2529d9880f |
| Annex, unchanged | 87557ef9f5b5a4427862effbece2bb528ebc1b95361c29416373709223021fee |

r1-only.diff is the exact old-candidate→R1 delta. shipment-bundle-1.0.0-to-1.1.0.candidate.diff and
publication-after-approval.patch are complete regenerated candidates for review, not grants to publish.
Contract info.version remains the proposed1.1.0; R1 is a review revision, not a newly published version.
manifest.json inventories every other file with SHA-256; self intentionally excluded. Existing publication
package is immutable and remains historical. Previous conformance claims are not automatically rebound
to these hashes; the new before/after evidence explicitly identifies its inputs.

## SOP §22 baseline, scope and handoff

Branch feature/mvp6-logistics; HEAD4a8d4d4b339528a88e6220fb8402e5a2c771136c; staged empty.
Fresh baseline.json records dirty paths/time; pre-write existing-file hashes are held in
/private/tmp/carrier-r1-baseline.json and checked in preservation-results.json. The pre-existing dirty pack,
consumer evidence, owner/CT records, plans and HELD prompts are preserved. Only this new R1 directory is
owned; changed-file inventory is manifest.json plus itself. No existing file edited.

Golden/contract flow: owner decision → Carrier-local candidate example selection → static parser checks.
Subflows/failures: old failing examples;44 retained fixtures;7 negative fixtures; scope-diff assertions.
Persistence evidence: N/A, none executed. Security/RBAC/tenant: fixture-only; no enforcement claim.
Audit/evidence: hashes, exact diffs, before/after results; no external approval fabricated.
Observability: validation outputs only, no service/logging change. Migration/rollback: N/A, no publication.
Blockers: owner example-policy disposition, independent candidate recheck, remaining consumer/publication
and uptake gates. GAP-184-04/05 remain BLOCKED; Phase1.5/pack and HELD prompts unchanged.
Out-of-scope changes: none. No commit, push, stash, branch switch or runtime implementation.
