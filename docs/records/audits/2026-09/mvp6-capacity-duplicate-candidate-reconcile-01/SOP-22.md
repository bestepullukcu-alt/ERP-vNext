# MVP6-CAPACITY-DUPLICATE-CANDIDATE-RECONCILE-01 — SOP §22

**Verdict: A candidate recommended as the single successor release working base; release HELD.** Use the existing `mvp6-capacity-duplicate-name-candidate-01` package (`3.0.0-rc.1`, YAML SHA-256 `81f9a34b178c50e7a59b4512f77444be3a5dd0699b786a663ad5f72461245f64`) for subsequent *review*. Preserve the existing B (`2.1.0-rc.1`) package as historical evidence without editing or merging it. This is a working-base recommendation, not final version selection, final-byte approval, consumer consent, publication, or runtime GO.

## Authority, baseline and exact artifacts

- Actual owner C decision is preserved in A's `AUTHORITY.md` and `owner-message.txt` (SHA-256 `ebae377670587256108e3dc8107f178486f2097dfa1d72d2e880ed8cdfe78579`; user message `msg_01a0caa8-99bb-7003-83ca-9aa6ce3bcccc`, 2026-09-22 19:47:22.171Z). C authorizes preparation of a narrow versioned `createCapacityScenario` 409 `CAPACITY_SCENARIO_NAME_CONFLICT` candidate, retaining exact-name uniqueness and deterministic/unique-index race convergence. It explicitly leaves final version, exact artifact consent, canonical publication, and production application separate. A/B owner grants for other lanes do not enlarge C.
- Published canonical 2.0.0/FROZEN/wire-v1 YAML and annex hash to `9543e3f295dcabb9f7c1ab464fadfacfcbc53ada2f9bec9d32deb8f52cb02ff3` and `eb1df1383e637c744179abe4c8faa3b3b7ebe4cccd19738aadf41896a9311bda`. Current branch is `feature/mvp6-logistics`, HEAD `4a8d4d4b339528a88e6220fb8402e5a2c771136c`; checkout was already dirty and left intact.
- Both package `SHA256SUMS` files passed every entry. Patch/YAML/annex triples and all nonidentical declarations are in `EXACT-DIFFERENCES.md`; this review's checked input hashes are in `exact-inputs.tsv`. No third candidate was created.

## Validation and decision

Existing A evidence binds full OAS 3.1 meta/semantic, 204 refs, 47 schema-bound examples and 6 embedded schema examples to YAML `81f9a34b…`; its independent static VER and behavioral model checks are A-specific. Existing B evidence was 19/19 structural checks and explicitly lacked full OAS. This review ran `validate_b_oas.py` with the already available `openapi-spec-validator 0.7.2` toolset against YAML `206a5976…`: full OAS 3.1 meta and semantic errors **0**, 204 local refs resolved, 47 schema-bound and 6 embedded examples valid. Exact result and stderr are in `b-full-oas-results.json` and `b-full-oas-stderr.txt`. B patch dry-run/apply in a disposable baseline copy exited 0 and recreated its exact YAML/annex bytes. These checks close B's *static validation gap*, not its release gates. No historical A PASS, independent verdict, model result or consent is transferred to B.

A is preferred because it preserves the previous 409 code ordering, keeps all three response reasons explicit, gives its new example a distinct correlation UUID, and spells out exact-name edge cases, proof of a scenario-name index collision, receipt-before-name precedence, and unknown-commit/read-failure behavior. The two candidates seek the same bounded business outcome and no contrary core rule was found; the extra A detail makes release review and future runtime acceptance less ambiguous. The proposed major line also signals the risk to strict-code consumers more conservatively. It does **not** solve that risk: the route, status, Error envelope and `contractVersion: v1` remain the same, with no version negotiation. An exhaustive consumer can reject the new code even when OpenAPI/JSON Schema accepts it. B's minor label does not establish compatibility; A's major label does not establish rollout safety. Both candidate labels remain unapproved.

## Remaining release decisions — one table

| Gate | Exact disposition needed | Current state |
|---|---|---|
| Final version and final bytes | Release owner selects version classification and final metadata/status/annex bytes from the A working base, records exact final YAML, annex and publication patch hashes; rerun full validation on those final bytes. `rc.1`→final or CANDIDATE→FROZEN changes hashes. | **HELD**; neither 3.0.0 nor 2.1.0 approved. |
| Strict-code consumer inventory | Identify every actual producer/consumer/pin and affected owner, including MOD-0190 and MOD-0192; check exhaustive code switches, generated adapters, UI/error dispatch, monitoring and retry logic against the exact new 409 code. Record owners or explicit unknowns. | **HELD**; a fixture counterexample is not actual consumer acceptance. |
| Exact compatibility consent | Obtain each affected owner's consent to the final hashes and same-route/wire-v1 behavior. Existing 2.0.0 consent is not portable. | **HELD**. |
| Cutover/rollback | Owner decides mixed-binary deployment order and rollback constraints on the shared route; consumer readiness must precede a producer that emits the new code. Metadata does not negotiate versions. | **HELD**. |
| Independent exact release VER and publication | Verify final artifact, patch/baseline, preservation, refs/examples and manifests; obtain separate single-writer canonical publication authority. | **HELD**. No canonical/guard mutation. |
| Production uptake | Separately authorize and verify deterministic collision, proven index-race loser, receipt/replay, write rollback, unknown-commit and HTTP/consumer behavior. | **HELD**; static OAS/model checks are not runtime proof. |

## Scope and no-change record

This review wrote only `docs/records/audits/2026-09/mvp6-capacity-duplicate-candidate-reconcile-01/`. It did not edit either source package or canonical YAML/annex, guard, pack, runtime or another lane, and did not stage, commit, push or stash. A candidate remains the one recommended successor working base; B remains intact historical evidence. **SOP §22 disposition: candidate reconciliation complete, release decision BLOCKED on the table above.**
