# MVP6-SANDOP-CAPACITY-CANDIDATE-RECONCILE-01 — SOP §22

**Independent verdict: PARTIAL technical reconciliation; publication NO-GO.** Both historical candidates have exact, applicable patches and preserve the twelve frozen operation IDs, old success/business responses, request schemas, shared model/event schemas, security scheme, server, and DEMAND file. They are **not semantically interchangeable**. Recommend `mvp6-sandop-capacity-amendment-01` (the two-file 2.0.0 candidate) as the **successor working base**, not as approved release bytes: its annex and operation matrix provide the fuller receipt, precedence, unknown-commit and executor specification. The narrower `mvp6-sandop-capacity-amendment-candidate-01` remains intact as historical alternative and supplies evidence of one unresolved 422 evaluation-policy conflict. Do not apply either or combine them silently.

## Authority and preflight

Branch `feature/mvp6-logistics`, HEAD `4a8d4d4b339528a88e6220fb8402e5a2c771136c`; staged state empty and common checkout already dirty. This lane changed only this new audit directory. AGENTS.md, CT SOP §22, `docs/analysis/contracts/README.md:45-50`, frozen SANDOP-CAPACITY and DEMAND, both packages and D190/C192 decision sources were read. The actual 2026-09-22 user message explicitly approves D190-04/05 and C192-02…07 **for amendment-candidate preparation**, with exact parsed key/no trim, no automatic 0190 approval, 0192 fixture oracle and startup/10-second scan/30-second fenced lease/three attempts, Pending outbox and earlier test-only fixture limits. Thus the second package's README/annex claim that this design approval was absent is stale (`mvp6-sandop-capacity-amendment-01/README.md:7-9`, annex lines 1 and 45). It does not authorize the proposed new wire dispositions, final version, consumer consent, publication, pack promotion or DEV GO. Both candidate YAMLs and both candidate reports remain **UNAPPROVED for publication**.

## Exact inputs, patches and reconstruction

| Item | A: amendment-candidate-01 | B: amendment-01 |
|---|---|---|
| Frozen baseline, shared | `c255e92923ba91714cec8daf229262101b5d45648b7f714a03ab402811db683c` | same |
| Frozen DEMAND, shared | `3c77262e411976e311bcf9b65be131e2035fd18a33215d24075f87209f87bb9d` | same |
| Patch | `e0a35b3cc3f2259a133b12117c359288db3c7ffcfe1c6ae6e92853eec62cf4d3` | `20b295e96ca9d934578fd9e5155e32a09675bb1f0815f0e3e697cc138f861648` |
| Candidate YAML | `aa6a1e1e238ad347e03dc793dfef8f6f7dad786ec0bb9d7871603f63f4d8a658` | `88070dc3fa27aff9ba0d4b40ba1a43641906f77d369e4ce1a84be75ae267816d` |
| Annex | none; separate proposed semantics only | `c3fb876cf19d8f6943781a6ea55d0c9f07d6c4137505b6b27b1718739aaa644c` |
| Patch targets | YAML only | YAML + new SANDOP-specific annex |

Both `SHA256SUMS` inventories passed: 6/6 A and 10/10 B. Disposable `git apply --check` and apply returned 0 for both. Reconstructed YAML bytes equalled each candidate; reconstructed B annex bytes also matched. `git apply --numstat` lists only the SANDOP YAML for A and SANDOP YAML plus SANDOP annex for B. No DEMAND, other contract, pack or runtime target. Independent raw results: `validation-results.json`. The baseline and DEMAND still match their recorded hashes. These checks do not certify published authority.

## Twelve-operation comparison

See `operation-diff.tsv` for every operation's method/path, A/B declared status and the lifecycle, fixture, receipt, correlation, error, executor or event consequence. The common distinctions are:

| Concern | A | B | Disposition |
|---|---|---|---|
| Metadata | 1.1.0-rc.1 / CANDIDATE / wire v1 | 2.0.0 / **FROZEN** inside an unapproved candidate / wire v1, annex pointer | These are proposal metadata, not publication. B's FROZEN marker is a proposed final-publication byte inside an unapproved archive; it cannot be treated as currently frozen. Neither version is selected. |
| Lifecycle | 0190 Draft→InReview capture, InReview-only sign-off, no auto approval; 0192 evaluation-only Accepted→Running→Completed/Failed | Same intended design, fuller annex testable details | Owner's candidate-preparation approval applies; frozen YAML alone does not carry all lifecycle semantics. |
| Receipt/fingerprint | Exact parsed key, scoped tuple, decoded JSON fingerprint, original result/current response header, no duplicate writes in separate proposal | Same, with explicit precedence, actor exclusion, no TTL, unknown commit receipt lookup in annex | B better release base; neither is runtime evidence. |
| Correlation/error precedence | 400 `INVALID_CORRELATION_ID` all twelve; no explicit 401/403 or GET 503; 503 `PERSISTENCE_UNAVAILABLE` on six POSTs | 400 `INVALID_CORRELATION_ID` or `INVALID_REQUEST`, 401/403/503 on all twelve; 503 `DEPENDENCY_UNAVAILABLE` or `COMMIT_RESULT_UNRESOLVED` | Material wire difference. B 401 response lacks `X-Correlation-Id` despite annex's all-error header rule. B's shared 503 component advertises mutation-only unknown-commit on GET, though annex forbids it. Exact operation-specific responses need owner selection and repair. |
| Checksum | 422 `INVALID_DEMAND_REFERENCE` for 0190 snapshot and 0192 create against scoped fixture | Same, with explicit checksum example in YAML | Neither verifies live DEMAND producer version/checksum. Wire disposition UNAPPROVED. |
| Constraint | 422 `INVALID_CONSTRAINT_REFERENCE` for scenario create **and evaluate** | 422 only for scenario create; evaluate has no 422 | Exact evaluation-time policy conflict requires owner choice; cannot silently port A's 422 into B. |
| Unknown commit | 503 `PERSISTENCE_UNAVAILABLE`, same-key retry; code merges unresolved with persistence failure | 503 `COMMIT_RESULT_UNRESOLVED` for unresolved mutation, `DEPENDENCY_UNAVAILABLE` for known failure | B's distinction is stronger and closer to requested separate disposition, but exact code is UNAPPROVED. |
| Executor/event | 10s/30s/3 and terminal Pending event in proposal prose | Same in annex, with startup, CAS, lease/recovery, fixture-only terminal oracle | B annex gives durable publication artifact; no publisher, optimizer or live delivery claim. |

A's six modified 409 components include concrete new error examples. B uses `x-error-codes` arrays but retains only original 409 examples, so the `IDEMPOTENCY_KEY_REUSED` and sign-off-state examples should be added in a successor **if the wire codes are approved**. B also exposes a 401 response without correlation header (`sandop-capacity.openapi.candidate.yaml:1391-1403`) while its annex requires a current/rejection correlation for application errors. The shared B `OperationUnavailable` component (`:1414-1431`) is referenced from GET and POST, despite annex line 15 assigning `COMMIT_RESULT_UNRESOLVED` to mutations only. Evaluate's A/B 422 difference appears at A YAML `:498-543` and B YAML `:548-595`.

## Independent structural checks and limits

`validation-results.json` records 12 operations each, zero changed old responses/requests or shared schemas, all local refs resolved (168 A/200 B encounters), and valid response examples in the independent operation traversal (62 A/93 B checks; repeated shared examples count once per operation reference, so these counts are **not** distinct fixtures). Candidate-authored checks also returned 0 (A: 23 distinct response examples; B: 216 reference encounters/93 examples and four simple negative mutants). No full OpenAPI 3.1 **document meta-schema** validator was installed. An attempt to retrieve the official meta-schema from `spec.openapis.org` failed with DNS exit 6. Therefore **full OpenAPI 3.1 document validation is NOT VERIFIED**, even though parsing, local refs, response examples, patch application, old-success/event preservation and non-owned immutability passed. A trusted offline/full validator remains a technical release gate. This is E1/E2 static evidence, not HTTP/JWT/Mongo, consumer uptake or executor runtime evidence.

## Compatibility and minimum successor delta

`docs/analysis/contracts/README.md:45-50` permits additive minor changes after freeze and forbids breaking changes. Both candidates add observable rejection states and new codes; B also adds auth and GET 503 responses. Existing exact-code/exhaustive-error consumers may break, and B's stricter lifecycle/fixture checks can reject previously schema-valid calls. A `1.1.0` minor classification is **not demonstrated**. B's proposed `2.0.0` is more defensible as a breaking-release proposal, but a major label with unchanged wire `v1` and routes does not supply negotiation/migration or satisfy the freeze rule by itself. The contract owner must classify compatibility and obtain exact S&OP/Capacity consumer release consent before version/publication selection. No consent or runtime uptake is inferred.

**Use B as working base only after these narrow decisions/repairs; do not produce a third artifact in this WP:**

1. Bind the actual user design approval to candidate documentation and remove B's stale “design unapproved/executor unapproved” assertions. Keep newly proposed wire responses, 2.0.0 and release consent marked UNAPPROVED.
2. Contract owner selects the exact operation-specific error matrix, including A's evaluate-constraint 422 versus B's omission, GET 503 code set, mutation unknown-commit code, 400/401/403 behavior and correlation header for 401. Do not infer missing decisions from either candidate.
3. Bring B YAML and annex into one oracle: specify 401 header/body correlation, restrict GET 503 to known dependency failure, add operation-specific 409 examples if approved, and preserve six POST exact-key/replay behavior and B annex executor/event provisions. The successor must get its own patch/YAML/annex hashes and new independent verification.
4. Contract owner selects a version/compatibility path and obtains exact-hash consumer consent. Run full OAS 3.1 validator once available, then separate publication/pack/Phase1.5/runtime gates.

**Release-owner handoff:** technical comparison supports B as the foundation, with the above material deltas unresolved. Neither existing hash is publishable from this review; no old owner approval transfers to changed bytes. No canonical or candidate edit, commit, push or stash occurred.
