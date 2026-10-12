# Independent VER handoff v1.0 — candidate verification READY; release/runtime HELD

Task: MVP6-CAPACITY-DUPLICATE-NAME-CANDIDATE-VER-01.
Use a separate verifier, not the candidate writer. Do not write production or Lane B
Capacity files. No canonical publication, consumer consent, pack promotion or Git mutations.

## Exact inputs

Read AUTHORITY.md and owner-message.txt: actual C permits candidate preparation only.
Read CONTRACT-DELTA.md from the production-rework-pack-01 record and INPUT-HASHES.tsv.
Baseline YAML: `9543e3f295dcabb9f7c1ab464fadfacfcbc53ada2f9bec9d32deb8f52cb02ff3`.
Baseline annex: `eb1df1383e637c744179abe4c8faa3b3b7ebe4cccd19738aadf41896a9311bda`.

| Exact candidate | SHA256 |
|---|---|
| sandop-capacity.openapi.candidate.yaml | `81f9a34b178c50e7a59b4512f77444be3a5dd0699b786a663ad5f72461245f64` |
| sandop-capacity-semantics-v3.0.0-rc.1.md | `d040723c83990255e719b63b80e5fc189cd149c8ea19d5e06e1e2d69b0d3e3af` |
| successor-candidate.patch | `231c83f239f1b55c42c4446e7f63ae55928e41bf2cc0bb2426a8e1c75d5ef303` |

## Required independent checks

1. Rehash published baselines, all candidate artifacts and manifest. Treat root draft
   pack's historical trim/max200 prose as stale to this already published exact-name
   scope; do not change the pack. Check actual C authority and its exclusions.
2. Apply patch only to a disposable baseline using patch(1), compare YAML/new annex
   bytes and prove old v2 annex stays untouched. Reject drift or out-of-scope patch paths.
3. Run complete OAS 3.1 document validator, local refs and schema-bound examples.
   Recheck negative invalid OAS/title/ref, missing code, bad wire/correlation. Verify
   every pre-existing example remains exact and only one 409 example is added.
4. Independently compare all 12 operations and shared components. Only the existing
   createCapacityScenario 409 component's description, code list and example may change;
   three metadata fields are allowed. Annex preface, one scenario row and new bounded
   clause are allowed; all other annex bytes remain exact.
5. Confirm no trim/case-fold/Unicode normalization/new maximum; active simple-collation
   uniqueness is scoped tenant/LE/plan. Distinguish same decoded name/alternate JSON
   escaping from genuinely distinct Unicode sequences; deleted names do not occupy key.
6. Exercise deterministic duplicate and modeled unique-index loser convergence; exact
   message/body/current-correlation header; no loser scenario/receipt/audit/outbox.
   Check same-key replay before state/fixture/name, changed valid payload conflict,
   invalid payload and target precedence, lifecycle then fixture before duplicate.
   Do not let arbitrary duplicate-key, retry exhaustion or unknown commit imply name 409.
7. Run model mutation controls (trim, duplicate before receipt, race 503, unknown-as-409).
   These are in-memory specification-model tests. They do **not** prove a Mongo unique
   index race, transaction rollback, HTTP wire or Lane B source correctness. Those
   remain later authorized runtime VER requirements.
8. Evaluate 3.0.0-rc.1 recommendation and COMPATIBILITY-AND-CUTOVER.md. Prove schema
   string-code blind spot and old strict allowlist rejection. Do not infer minor
   compatibility, consent or routing negotiation from a version number.
9. Produce an independent SOP §22 verdict with exact hashes, raw commands/results,
   findings and boundaries. Any artifact correction needs new hashes/reverification.

## Reproduction

`python3 verify_candidate.py` and `python3 contract_model_checks.py` from this folder.
The former uses `openapi-spec-validator==0.7.2` installed only under
`/private/tmp/mvp6-capacity-duplicate-name-candidate-01/tooldeps`; set `OAS_TOOLDEPS`
to an independently provisioned disposable directory if absent. TOOLCHAIN.txt records
versions and packaged OAS schema hash. Do not replace full OAS validation with only
ref checks if dependencies are missing. Dependencies/lockfiles in the repo are unchanged.

Writer results: 28 static checks; 204 local refs; 53 examples (47 media/header + 6 event-schema); six negative
validator controls; 15 model cases and four killed mutants. Independent results must
be reported separately. No full service/architecture runtime suite was run or claimed
for this contract-only candidate task.
