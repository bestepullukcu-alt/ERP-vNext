# MOD-0147 spec delta — policy-selected, concurrence-held

Work package: `MVP6-SUPPLIER-SPEC-SPLIT-01`

Module: `MOD-0147 Supplier Performance & Risk`

Status: **SPEC TARGET SELECTED / EXTERNAL CONCURRENCES OPEN / PACK DRAFT / DEV HELD**

## Decision binding

The controlling user message for this work package selects every `SS-01…SS-09` option A and `AC-01…27` in
`mvp6-supplier-exact-policy-01/DECISION-PACK.md` solely as the policy target for the next specification preparation.
That session-level selection supersedes the file's historical “Karar henüz alınmadı” observation for this preparation
step; the source file remains unchanged. It is not MOD-0140, Platform auth/security, Metric Registry, Risk Register or
strict-consumer concurrence. It grants no contract publication, pack promotion, runtime, fixture implementation or DEV GO.

For MOD-0147 the selected policy binds:

| Policy | MOD-0147 specification consequence | State after this split |
|---|---|---|
| SS-01 A | Prospective execution placement is SCE / `Diten.SupplyChainService`; Supplier master remains MOD-0140-owned | Policy target; durable domain/DCP/registry alignment still open |
| SS-02 A | Consume exact FROZEN `SUPPLIER` 1.0.0 as identity/status base; never create a local Supplier master | Target bound; live producer and consumer concurrence open |
| SS-03 A | Single-Supplier commands use `getSupplier`; new evaluation/first publish requires Active, new risk accepts all four known statuses; trusted Tenant+LE affirmative eligibility is mandatory | Business target bound; LE carrier and exact wire coverage open |
| SS-04 A | Frozen bytes remain unchanged; one CT seam owner may later prepare a separately reviewed successor | Shared GAP; this lane creates no amendment |
| SS-05/06 A | Portal actor binding and actor-isolated replay belong to the MOD-0148 split | No MOD-0147 producer or contract design here |
| SS-07 A | Use `supplier-score-policy/1` exact percentage scoring and immutable registry revision snapshot rules | Policy target; Metric owner/pointer/carrier concurrence open |
| SS-08 A | Risk Register is taxonomy/reference authority; MOD-0147 owns SupplierRisk instance and linear lifecycle | Policy target; Risk owner and source-resolver concurrence open |
| SS-09 A | A future `supplier-exact-policy-fixture/1` may simulate the selected cases and must be labelled SIMULATED | Fixture not implemented; never live-producer evidence |

## Frozen surface retained

The exact `SUPPLIER-PERFORMANCE` 1.0.0 bytes remain the only current wire authority. The MOD-0147 bounded surface stays
at these nine operations:

1. `listSupplierEvaluations`
2. `createSupplierEvaluation`
3. `getSupplierEvaluation`
4. `submitSupplierEvaluation`
5. `listSupplierScorecards`
6. `getSupplierScorecard`
7. `listSupplierRisks`
8. `registerSupplierRisk`
9. `changeSupplierRiskStatus`

No `/portal/**` operation, internal review operation, new endpoint or shared schema is added by this specification.

## What current SUPPLIER v1 can support

| Need | Existing usable authority | Bounded use |
|---|---|---|
| Opaque identity lookup | `GET /api/suppliers/{supplierId}` | Exact, ordinal `supplierId` lookup for one command target |
| Base status | `Active`, `OnHold`, `Blocked`, `Inactive` | Input to the selected status table only after the returned identity is exact |
| Unknown identity | Declared 404 `UnknownSupplier` | Candidate source for MOD-0147's unknown-Supplier path, subject to exact consumer error concurrence |
| Tenant-level base | Contract description says tenant-scoped | Confirms that the base is not a global unscoped directory; does not prove LE eligibility |
| Bulk validation | `POST /api/suppliers/validate` exists | Not used by the bounded single-Supplier commands; no bulk operation is introduced |

The contract does not prove a deployed producer. Supplier name, contact, tax and profile data are never copied into
MOD-0147 persistence.

## Open consumed-seam GAPs

| GAP | Why the selected policy is not executable authority by itself | Required owner route |
|---|---|---|
| LegalEntity eligibility | SUPPLIER v1 has no LE field or affirmative eligibility result | Single Supplier seam coordinator with MOD-0140 and security concurrence |
| Base strictness/errors | `validate` completeness/nullability is ambiguous; UUID correlation and all needed 503/error paths are not declared | CT contract writer plus MOD-0140 and strict consumers |
| Metric authority | No repo-resolvable live Metric Registry contract/pointer or immutable revision carrier exists | Metric Registry owner; shared successor need returns to CT seam owner |
| Risk taxonomy authority | No repo-resolvable live taxonomy revision contract/pointer exists | Risk Register owner; shared successor need returns to CT seam owner |
| Source resolution | SCORECARD is locally resolvable; scoped PORTAL_SUBMISSION needs MOD-0148 uptake; MANUAL/EXTERNAL_SIGNAL have no authority | MOD-0148 consumer concurrence and Risk owner; no new Evidence API |
| New dependency errors | `METRIC_REGISTRY_UNAVAILABLE`, `RISK_REGISTER_UNAVAILABLE` and all route coverage are policy targets, not current frozen wire authority | CT single contract writer and affected consumers |
| Snapshot provenance | Current response/request models do not carry metric/taxonomy revision and source hashes | Exact successor carrier decision; no consumer-private contract |

## Evaluation and scorecard delta

This is the selected specification target after the open seams receive their exact owner artifacts:

- `createSupplierEvaluation` resolves trusted Tenant+LE, calls exact `getSupplier`, requires affirmative LE eligibility
  and Active status, then validates every metric against an effective immutable `supplier-score-policy/1` revision.
- Metric codes are unique; unit is `PERCENT`; direction is `HIGHER_IS_BETTER`; measured values are `0..100`; weights are
  `>0..100`; exact total weight is 100. Input values and weights accept at most four fractional digits. Negative values
  and excess scale return 422 without silent rounding.
- Client weight must exactly equal the selected registry policy weight. The server stores the resolved metric revision,
  policy revision, weights, inputs, Tenant+LE scope and source hash snapshot. A missing, stale or unverifiable authority
  does not produce an evaluation, receipt, audit success record or Pending outbox item.
- Arithmetic uses decimal exactness: `sum(measuredValue * weight) / 100`, with no intermediate rounding. Individual
  display scores project measured values to two decimals using round-half-even. Overall output rounds once to two
  decimals using round-half-even; the aggregate never uses display-rounded individual scores.
- The risk band is selected from the unrounded aggregate: `[90,100] LOW`, `[75,90) MEDIUM`, `[50,75) HIGH`, `[0,50)
  CRITICAL`. This projection never creates or changes a `SupplierRisk` automatically.
- Submit uses the evaluation's immutable snapshot. A newer registry revision cannot rewrite an existing evaluation or
  scorecard. Explicitly withdrawn revision is 422; unavailable authenticity/revocation verification is proposed 503 and
  remains wire-held. Publication produces one immutable scorecard and one Pending publication outbox item atomically.

## SupplierRisk delta

- MOD-0147 owns the SupplierRisk instance, version, local lifecycle, audit and Pending outbox. The external Risk
  Register validates only an immutable taxonomy revision for the six current categories and four current levels.
- Registration resolves exact Supplier identity and affirmative Tenant+LE eligibility. Active, OnHold, Blocked and
  Inactive are all eligible because remediation must remain possible. Unknown or scope-ineligible Supplier is 404 under
  the selected policy; base failure is 503. Exact carrier/error coverage remains shared-wire held.
- A new risk starts `OPEN`. The only transitions are `OPEN→ACKNOWLEDGED→MITIGATED→CLOSED`. Self transitions, reopen and
  skips are 422 `INVALID_STATUS_TRANSITION`. Stale `If-Match` wins with 409 before lifecycle evaluation.
- MITIGATED and CLOSED require a trimmed nonempty `resolutionNote` of at most 2000 characters. MITIGATED sets server UTC
  `resolvedAt`; CLOSED preserves it. Every successful transition increments version once and emits one Pending outbox
  event. Existing remediation uses the stored taxonomy snapshot and is not blocked by a later registry outage.
- `SCORECARD` source must be a same Tenant+LE+Supplier published scorecard. `PORTAL_SUBMISSION` must be same-scope and
  have `SUBMITTED`, `UNDER_REVIEW`, `ACCEPTED` or `REJECTED` status; live cross-module resolution remains open. `MANUAL`
  and `EXTERNAL_SIGNAL` stay in the frozen enum
  but are rejected with generic 422 in the bounded slice. No source existence detail is disclosed.
- There is no delete, reopen, automatic close, automatic risk creation or score-driven risk-level mutation.

## Failure, isolation and atomicity targets

- All reads and writes are scoped by server-resolved TenantId and LegalEntityId; foreign scope returns 404 without
  existence leakage. Payload Tenant/LE values cannot become trusted scope.
- Supplier, Metric and Risk dependencies fail closed. A malformed, mismatched or incomplete authoritative result is a
  dependency failure, never an implicit Active/valid result.
- Mutation success commits business state, idempotency receipt, audit record and Pending outbox atomically. Replays do
  not add state, version or events. The precise MOD-0147 receipt tuple/error precedence remains for its Phase 1.5 pack
  amendment; SS-06's supplier-actor tuple is not copied from MOD-0148.
- `X-Correlation-Id` must be a UUID and propagates to response/audit/outbox. Current frozen examples and route coverage
  do not close the successor wire GAP.

## Spec completion gates

This delta may be merged into a future pack amendment only after the named owner artifacts are exact and hash-bound.
The pack stays `draft`. Phase 1.5, ready-for-dev, source writing and integration dispatch remain HELD until at least:

1. Enterprise/domain ownership reconciliation is recorded without treating this policy choice as that concurrence.
2. MOD-0140 and security approve the exact Tenant+LE Supplier eligibility seam and failure mapping.
3. Metric owner approves the exact live pointer, revision/scope/UoM/withdrawal behavior and scoring/band concurrence.
4. Risk owner approves taxonomy-only ownership, exact revision/source validation and lifecycle concurrence.
5. CT single writer prepares and independently verifies any required contract successor; no parallel module contract is made.
6. MOD-0147 owner reviews a pack amendment with exact current owned paths, implementation manifest and explicit status change.
