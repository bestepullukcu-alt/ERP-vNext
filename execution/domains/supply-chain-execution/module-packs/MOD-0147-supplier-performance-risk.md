---
id: MOD-0147
name: Supplier Performance & Risk
domain: supply-chain-execution
service: Diten.SupplyChainService
shell: none
golden_reference: none
entity_base: EntityBase
status: draft
status_note: "Review draft (2026-10-19); no runtime code or service-scaffold authority. Placement CONCURRED (DC-01, 2026-10-18, docs/records/decisions/2026-10/mvp6-supplier-dc01-approved-01.md); domain-config, DCP-009 and the module-id registry record it. SUPPLIER-BASE is frozen (docs/analysis/contracts/supplier.openapi.yaml v1.2.0, 780f8368a). ready-for-dev stays blocked: DC-02/DC-03 are unbuilt build seams, DC-04/DC-05 deferred (no producer service); target 2026-12-31 is a placeholder bound to those gates; see §0, §18 and docs/roadmap/plans/mvp6-mod0147-spec-split-01/CONCURRENCE-OPEN.md."
owner: supply-chain-execution / control-tower
branch: feature/sce/mod-0147-supplier-performance-risk
started: 2026-09-15
target: 2026-12-31
form_field_count: 0
---

# MOD-0147 — Supplier Performance & Risk

> **Identity gate:** `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0147 --name "Supplier Performance & Risk"`
> returned `OK` on 2026-09-15 against Blueprint 8.1 and again on 2026-10-19 against Blueprint 8.8 and the module ID
> registry.
>
> **Phase A only:** this draft records the proposed initial Phase B backend/contract slice. It does not authorize
> runtime code, service scaffold, gateway, permission-registry or integration changes.
>
> **Placement (CONCURRED, DC-01 2026-10-18):** MOD-0147 is a Blueprint Procurement Suite (P2P) module placed in
> `supply-chain-execution` / `Diten.SupplyChainService` for delivery (`docs/records/decisions/2026-10/mvp6-supplier-dc01-approved-01.md`). MOD-0140 remains the Supplier
> master owner (domain `procurement`). Placement grants no runtime, contract, gateway or permission ownership.

## 0. Requirement status and traceability (review 2026-10-19)

Every requirement below carries one of three labels. A label is a claim about authority, not about code (there is none).

| Label | Meaning |
|---|---|
| **ONAYLI** | Fixed today: the frozen wire surface of `SUPPLIER-PERFORMANCE` v1.0.0 FROZEN (`docs/analysis/contracts/supplier-performance.openapi.yaml`, `eb1306590`, sha256 `e311cd102bed0041…`), or a CONCURRED gate. |
| **ÖNERİLEN** | Selected target written in `docs/roadmap/plans/mvp6-mod0147-spec-split-01/SPEC-DELTA.md` (`ce3396656`, sha256 `68732d5ae31740e0…`). **Not merged into this pack** — that delta may be merged only after its named owner artifacts are exact and hash-bound. Reviewable, not binding. |
| **BLOKE** | Depends on an OPEN gate in `docs/roadmap/plans/mvp6-mod0147-spec-split-01/CONCURRENCE-OPEN.md`; the named owner must close it. A fixture or mock never closes it. |

Where this pack's own text (sections 1–21) states a behaviour as a rule and the label below says ÖNERİLEN or BLOKE, **the
label wins**: the text is the target, not an approved rule.

| Endpoint (operationId) | Wire | Behaviour | Gate / decision owner | Tests (`ACCEPTANCE-TEST-MAP.r2.tsv`) |
|---|---|---|---|---|
| `POST /evaluations` (createSupplierEvaluation) | ONAYLI | **BLOKE** — Supplier eligibility (DC-02, MOD-0140), metric policy (DC-04, Metric Registry — deferred); score policy ÖNERİLEN (§0.1) | MOD-0140 owner; Metric Registry owner | AC-03, AC-04, AC-18…AC-22 |
| `POST /evaluations/{id}/submit` (submitSupplierEvaluation) | ONAYLI | **BLOKE** — DC-04 snapshot; atomic publication ÖNERİLEN (§0.3) | Metric Registry owner | AC-18…AC-22 |
| `GET /evaluations`, `GET /evaluations/{id}` | ONAYLI | ÖNERİLEN — Tenant+LE isolation, 404 without leakage | MOD-0147 owner | T-0147-ISO-01 |
| `GET /scorecards`, `GET /scorecards/{id}` | ONAYLI | ÖNERİLEN — immutable publication | MOD-0147 owner | AC-22 (revision snapshot), T-0147-ISO-01, T-0147-IMM-01 |
| `POST /risks` (registerSupplierRisk) | ONAYLI | **BLOKE** — risk taxonomy (DC-05, Risk Register — deferred), DC-02; lifecycle ÖNERİLEN | Risk Register owner; MOD-0140 owner | AC-23…AC-26 |
| `POST /risks/{id}/status` (changeSupplierRiskStatus) | ONAYLI | ÖNERİLEN — linear `OPEN→ACKNOWLEDGED→MITIGATED→CLOSED`; **BLOKE** — taxonomy snapshot (DC-05) | Risk Register owner | AC-23, AC-24 |
| `GET /risks` | ONAYLI | ÖNERİLEN — Tenant+LE isolation | MOD-0147 owner | T-0147-ISO-01 |
| Risk source `PORTAL_SUBMISSION` | ONAYLI enum | **BLOKE** — portal source resolution (with MOD-0148) | MOD-0148 owner; Risk owner | AC-25 |
| Replay receipt tuple / error precedence (all four mutations) | not declared in v1 | **BLOKE** — MOD-0147 Phase 1.5 amendment; 409 `IDEMPOTENCY_KEY_REUSED` not in v1 | MOD-0147 owner; CT contract writer | — (Phase 1.5) |
| Permissions, Program.cs composition | — | **BLOKE** — security/permission single writer | security owner, integration owner | — |
| Placement / Supplier pointer / frozen contract hash | — | ONAYLI (DC-01) / pointer re-pinned to SUPPLIER v1.2.0 / hash guard | — | AC-01 (r2: CONCURRED), AC-02, AC-07 (hash only) |
| Fixtures (`SIMULATED-ONLY`) | — | never evidence for a live producer | CT fixture coordinator | AC-06, AC-27 |


**Test map revision r2 (2026-10-19).** The original `ACCEPTANCE-TEST-MAP.tsv` and `SHA256SUMS` stay byte-identical: four records pin their hashes. The controlled revision is additive — `ACCEPTANCE-TEST-MAP.r2.tsv` (AC-01 oracle refreshed for DC-01; the `T-…` rows below added) and `SHA256SUMS.r2`, which records the folder's current bytes. The original `SHA256SUMS` already failed on `CONCURRENCE-OPEN.md` since `12416679d` (DC-01 edit, sums not updated); r2 records that file's current hash. r2 is the map this pack follows.

Test IDs added in r2 (MOD-0147) — the behaviours are proposed (§0.2, §0.3); the IDs are in `ACCEPTANCE-TEST-MAP.r2.tsv`:

| ID | Asserts | Rule |
|---|---|---|
| T-0147-ISO-01 | Every read (evaluations, scorecards, risks) with a foreign Tenant or LE → 404, no existence detail | §8 |
| T-0147-IMM-01 | A published scorecard never changes; a newer metric/policy revision does not rewrite it | §0.1, §8 |
| T-0147-PER-01 | Second create for the same Supplier+period **and the same `CorrectionOf`** (sequential and concurrent) → 409, one active record | §0.2 |
| T-0147-PER-02 | A `DRAFT` and a `PUBLISHED` for one Supplier+period **and the same `CorrectionOf`** cannot coexist; an original and its correction (different `CorrectionOf`) can | §0.2 |
| T-0147-COR-01 | A correction target is checked in this order and the first failure wins: (1) not a `PUBLISHED` scorecard of the same Tenant+LE+Supplier+period → 422; (2) superseded — it already has a published correction → 422; (3) an active `DRAFT` correction of it exists → 409. Only a target passing all three gets a new correction — BLOKE until the contract successor | §0.2 |
| T-0147-TXN-01 | Transaction abort leaves no state, receipt, audit or outbox record | §0.3 |
| T-0147-TXN-02 | A duplicate delivery of one outbox event is ignored by the consumer (EventId) | §0.3 |
| T-0147-TXN-03 | A crash between commit and publish is recovered by the publisher from the Pending record | §0.3 |

### 0.1 Score policy — ÖNERİLEN (SPEC-DELTA "Evaluation delta"), BLOKE on DC-04/DC-05

The selected target, by reference — not merged:

- Metrics: unique codes, unit `PERCENT`, direction `HIGHER_IS_BETTER`, measured `0..100`, weights `>0..100`, **total weight
  exactly 100**, at most four fractional digits; negative values or excess scale → 422, no silent rounding. Client weight
  must equal the registry policy weight (`supplier-score-policy/1`).
- Aggregate `sum(measuredValue × weight) / 100` in exact decimal, no intermediate rounding; overall rounded **once** to two
  decimals, **round-half-even**; display scores never feed the aggregate.
- Band from the **unrounded** aggregate: `[90,100] LOW`, `[75,90) MEDIUM`, `[50,75) HIGH`, `[0,50) CRITICAL`. The band never
  creates or changes a `SupplierRisk`.
- Evaluation and scorecard carry the metric revision, policy revision, weights, inputs, Tenant+LE scope and source hash
  snapshot; a newer revision never rewrites them. Missing, stale or unverifiable authority → no evaluation, receipt, audit
  success record or outbox item.

**Still for the Metric/Risk owners to decide (BLOKE):** the live registry pointer and immutable revision shape; scope, UoM,
direction, validity and withdrawal oracle; `policy1` weights; band consent; a missing-metric rule (the target above has
none: every policy metric is required — owner to confirm).

Derived acceptance examples (from the proposed policy; they become acceptance examples only when it is approved):

| Inputs (value × weight) | Aggregate (exact) | Overall (half-even, 2dp) | Band (from exact) |
|---|---|---|---|
| 92.5 × 60, 80 × 40 | 87.5 | 87.50 | MEDIUM |
| 89.995 × 100 | 89.995 | 90.00 | **MEDIUM** (band uses the unrounded value) |
| 75.125 × 100 | 75.125 | 75.12 (tie → even digit 2) | MEDIUM |
| 49.995 × 100 | 49.995 | 50.00 | **CRITICAL** (unrounded 49.995 < 50) |
| 60 × 60, 40 × 39.9999 | — | — | 422 (total weight 60 + 39.9999 = 99.9999 ≠ 100) |
| 60 × 60, 39.9999 × 40 | 51.99996 | 52.00 | HIGH (weights sum to 100 — a valid input, not a negative case) |
| 50.12345 × 100 | — | — | 422 (five fractional digits) |

### 0.2 Evaluation period, correction and CANCELLED — proposal of this review (not in SPEC-DELTA), MOD-0147 owner decides

- Duplicate period (proposed): at most **one active** evaluation per `(TenantId, LegalEntityId, SupplierId, PeriodStart,
  PeriodEnd, CorrectionOf)`, active = `DRAFT` or `PUBLISHED`. Enforced by a unique index on exactly those fields — **`Status` is
  not in the key**, so a `DRAFT` and a `PUBLISHED` for one period cannot coexist — whose partial filter selects only active
  records (a server-maintained `IsActivePeriod` boolean, true for `DRAFT`/`PUBLISHED`; the exact filter form is verified when
  implemented). A second create — sequential or concurrent — gets 409 and nothing commits. `CorrectionOf` is `null` for an
  original evaluation. Overlapping but non-identical periods are allowed (owner to confirm).
- `CANCELLED` (proposed): v1 has **no cancel operation**, so this slice never produces `CANCELLED`; the enum value stays
  unused until a cancel command is approved in a contract successor.
- Correction (BLOKE): scorecards are immutable; a correction is a new evaluation and publication with `CorrectionOf` =
  the scorecard it corrects. Because `CorrectionOf` is part of the uniqueness key, a correction does not collide with the
  original's active record, and only one active correction per corrected scorecard exists.
  - **Target rule (proposed):** `CorrectionOf` must name a `PUBLISHED` scorecard of the **same Tenant, LE, Supplier and
    period** as the correction; anything else is 422 without disclosing the foreign record.
  - **Chain rule (proposed):** corrections form one linear chain per original — a correction may target only the **latest**
    published scorecard of that chain (the original, or the newest published correction); there are no forks. Every
    scorecard stays immutable; "current" means the latest in the chain.
  - **Error precedence (proposed):** a correction target is checked in this order and the first failure wins: (1) not a `PUBLISHED` scorecard of the same Tenant+LE+Supplier+period → 422; (2) superseded — it already has a published correction → 422; (3) an active `DRAFT` correction of it exists → 409. Only a target passing all three gets a new correction.
    A foreign-scope target is reported as 422 without disclosing that it exists.
  - The field is not in v1 — contract successor (CT contract writer); until then no correction path exists.

### 0.3 Transaction, outbox and delivery — ÖNERİLEN (SPEC-DELTA "Failure, isolation and atomicity targets")

- One MongoDB multi-document transaction commits business state, idempotency receipt, audit record and Pending outbox
  item; no compensation path. Replay adds no state, version or event.
- "Exactly once" applies to the **state and the outbox record**. **Delivery** (outbox publisher, outside this slice) is
  at-least-once; consumers deduplicate by EventId.
- Acceptance tests to add when authorised: transaction abort leaves zero of the four records; publisher retry delivers a
  duplicate that the consumer ignores; a crash between commit and publish is recovered by the publisher from the Pending
  record.

## 1. Module Summary

MOD-0147 owns tenant/legal-entity scoped supplier evaluations, published scorecards and supplier risk lifecycle.
Its initial proposed Phase B slice is backend-only and contract-first against
`docs/analysis/contracts/supplier-performance.openapi.yaml` (`SUPPLIER-PERFORMANCE` v1). Supplier identity is an
opaque external reference; this module never becomes the base Supplier system of record.

## 2. Ownership and Boundaries

**Owns:** evaluation periods and metric results, scorecard publication, overall score/risk classification,
supplier-risk records, lifecycle transitions, idempotent commands, audit/outbox evidence and correlation propagation.

**Consumes:** Metric Registry and Risk Register building blocks, Event Bus and
**CONSUMES: SUPPLIER-BASE (merkez üretecek)** through a frozen mock contract.

**Does not own:** MOD-0140 Supplier master/onboarding, supplier names/status/profile/KYC, MOD-0148 portal submissions,
source procurement facts, gateway/shared permission seams or other services' persistence.

## 3. Owned Objects

| Object | Purpose |
|---|---|
| `SupplierEvaluation` | Period-bound metric evaluation for an opaque `supplierId` |
| `EvaluationMetricResult` | Metric Registry code, score and weight snapshot |
| `SupplierScorecard` | Immutable publication derived from a submitted evaluation |
| `SupplierRisk` | Module-owned risk record and lifecycle |
| Commands | Create/submit evaluation; register/change status of supplier risk |
| Queries | List/get evaluations; list/get scorecards; list risks |
| API | Evaluation, scorecard and risk paths in `SUPPLIER-PERFORMANCE` v1 |
| Events | Evaluation published; risk raised/status changed |
| Permissions | `supplychain.supplier-performance.read`, `.evaluate`, `.publish`, `.risk-manage` |

## 4. Entity Fields

### SupplierEvaluation / SupplierScorecard

| Field | Rule |
|---|---|
| `Id`, `EvaluationId`, `ScorecardId` | Server-minted stable identifiers |
| `TenantId`, `LegalEntityId` | Required, server-resolved; never mutation payload |
| `SupplierId` | Required opaque MOD-0140 reference; no Supplier fields copied |
| `PeriodStart`, `PeriodEnd` | ISO dates; start must not exceed end |
| `Metrics` | At least one registry code; unique per evaluation; decimal-string score/weight |
| `OverallScore` | Derived 0..100 decimal string; client cannot override |
| `RiskLevel` | `LOW`, `MEDIUM`, `HIGH`, `CRITICAL` |
| `Status` | `DRAFT`, `PUBLISHED`, `CANCELLED` |
| `Version` | Optimistic concurrency token |
| Soft-delete/audit fields | Repo-standard `EntityBase` fields |

### SupplierRisk

| Field | Rule |
|---|---|
| `RiskId`, `SupplierId` | Server ID + opaque supplier reference |
| `Category` | Contract enum: delivery/quality/compliance/financial/continuity/other |
| `Level`, `Status` | Frozen contract enums; transition matrix enforced server-side |
| `SourceType`, `SourceId` | Traceable source reference; source record is not copied |
| `Summary` | Required, trimmed, max 1000 |
| `DetectedAt`, `ResolvedAt` | UTC; resolution only for terminal/mitigated states |
| `CorrelationId` | Required UUID propagated to audit/outbox event |

Proposed indexes are tenant-first: evaluation `(TenantId, LegalEntityId, SupplierId, PeriodStart, PeriodEnd)`,
scorecard `(TenantId, LegalEntityId, SupplierId, PeriodEnd)`, risk `(TenantId, LegalEntityId, SupplierId, Status, Level)`.

## 5. Repo Scope

After all ready-for-dev gates pass, the proposed Phase B slice may write only:

- `services/Diten.SupplyChainService/src/**/Features/SupplierPerformance/**`.
- `services/Diten.SupplyChainService/tests/**/SupplierPerformance/**`.
- Module-specific API reference/audit evidence paths explicitly assigned by the implementation WP.
- This module pack's status/evidence fields.

The frozen `supplier-performance.openapi.yaml` is read-only implementation authority in Phase B. This draft grants
no current write permission to runtime paths.

## 6. Protected Paths

- `.antigravity/**`, all existing frozen contract files and MOD-0140 Supplier contract/schema.
- `gateway/Diten.ApiGateway/**/ocelot.json`, shared permission registries and shared registrations.
- Archive controllers/views, frozen layout and all frontend paths in this backend-only slice.
- Other domain services and non-MOD-0147 features, including MOD-0148 portal runtime paths.
- Supplier master/profile/onboarding persistence and any direct foreign database access.

## 7. Dependencies

| Dependency | State | Rule |
|---|---|---|
| `SUPPLIER-PERFORMANCE` v1 | FROZEN | Owned contract; implementation must match exactly |
| `SUPPLIER-BASE` | FROZEN in `docs/analysis/contracts/supplier.openapi.yaml` (x-owner MOD-0140, `780f8368a`, v1.2.0, sha256 `373ebb21bae15f152d1b9ac24e03f7bfd02e595d343cf68cf918da553f5f42f7`; first pinned at `c8badff96` v1.0) — identity surface only; consumption authority open (DC-02) | **CONSUMES: SUPPLIER-BASE (merkez üretecek)**; no local substitute |
| Metric Registry / Risk Register | Building-block dependency | Contract adapters only; no local registry clone |
| Event Bus | Platform dependency | Correlation-preserving outbox publication |
| Domain ownership | **CONCURRED** (DC-01, 2026-10-18) | Placement recorded in domain-config, DCP-009 `placed_non_canonical_modules`, module-id registry |

## 8. Runtime Constraints

- Proposed service is `Diten.SupplyChainService` on port 5066 (AGENTS.md §3; 5061 was CRM's port, corrected 2026-10-19); this draft does not authorize runtime work.
- MongoDB records are tenant/legal-entity scoped; cross-scope access returns 404 without existence leakage.
- `TenantId` and `LegalEntityId` are server-resolved and absent from mutation payloads.
- Commands require `Idempotency-Key` and UUID `X-Correlation-Id`; replay creates no duplicate state/event.
- Scorecards are immutable publications; corrections require a new evaluation/publication trail.
- State, receipt, audit and outbox commit in one transaction (ÖNERİLEN, §0.3); silent partial success is forbidden.
- SUPPLIER-BASE unavailability fails closed; supplier validation is never silently skipped.

## 9. Layout & Shell Contract

`shell: none`. The proposed first slice has no Razor UI, route, menu, DataTable or localization surface.

## 10. Backend File Convention

Proposed feature root is `Features/SupplierPerformance/` with separate `Commands`, `Queries`,
`Handlers/CommandHandlers`, `Handlers/QueryHandlers`, `Validators` and one models file. Each public command, query,
handler and validator remains in its own file. The service's five-layer CQRS, pipeline and response-envelope standards
apply after authorization; this section is not scaffold permission.

## 11. Frontend File Contract

Not applicable. Any UI requires an approved pack revision with tenant shell, exact fields, DataTable decision,
golden reference and seven-language localization scope.

## 12. Validation Rules

| Field/Header | Required | Rule | Pre-check |
|---|---|---|---|
| `Idempotency-Key` | Mutations | 1..200 — normalisation (trim or reject) and receipt tuple **BLOKE** with the Phase 1.5 amendment (§0) | Receipt tuple per the Phase 1.5 amendment |
| `X-Correlation-Id` | All operations | UUID | Propagate to response/audit/event |
| `SupplierId` | Create evaluation/risk | Non-empty opaque ID | SUPPLIER-BASE frozen mock; fail closed |
| Period | Evaluation | Valid ISO dates; start <= end | Duplicate = same Supplier+period in DRAFT/PUBLISHED → 409 (proposed, §0.2) |
| Metrics | Evaluation | 1+, unique codes, score/weight 0..100 | Metric Registry contract |
| `ExpectedVersion`/`If-Match` | Lifecycle mutation | Current positive version | Conditional update |
| Risk transition | Status command | Frozen state matrix | Terminal state cannot reopen silently |

## 13. Failure Path to Verify

- Missing/unknown supplier returns the contract error; unavailable SUPPLIER-BASE fails closed with no write.
- Duplicate idempotency key returns the original result without a second evaluation/risk/event.
- Invalid metric, period or transition returns declared 400/422 error with UUID correlation.
- Stale `If-Match` returns 409 and never overwrites concurrent state.
- Cross-tenant/cross-legal-entity lookup returns 404; unauthorized actor returns 403.
- Scorecard publication failure leaves no partial publication/outbox state.

## 14. Authorization Convention

Actor type: tenant user/service actor. Proposed server-side permissions:

- `supplychain.supplier-performance.read`
- `supplychain.supplier-performance.evaluate`
- `supplychain.supplier-performance.publish`
- `supplychain.supplier-performance.risk-manage`

Permission definition/seed is a single-writer shared seam and is not authorized by this draft.

## 15. Gateway / API Routing Decision

The frozen contract base is `/api/supplier-performance`. A future `integration-agent` WP must own Ocelot changes and
verify auth, tenant/legal-entity context, correlation and idempotency propagation. This draft grants no gateway write.

## 16. Acceptance Criteria

- [ ] Implementation payloads and declared failures match `SUPPLIER-PERFORMANCE` v1 contract tests.
- [ ] Evaluation creation derives a score from valid Metric Registry codes and persists one audit/outbox chain.
- [ ] Submit publishes exactly one immutable scorecard and `SupplierEvaluationPublished` event.
- [ ] Risk create/status commands follow the frozen lifecycle and preserve correlation.
- [ ] Idempotent replay and stale version paths have no duplicate or overwritten side effects.
- [ ] Tenant/legal-entity isolation returns 404 and server-side permission denial returns 403.
- [ ] SUPPLIER-BASE is consumed only through its central frozen mock; no Supplier master/schema is local.
- [ ] G5 evidence links scorecard/risk feedback to MVP-6 integration without overriding Supplier or procurement SoRs. **BLOKE** — needs live DC-02/DC-04/DC-05 producers; not part of a first slice.

## 17. Test Expectations

- DCP-002 identity verifier PASS and OpenAPI syntax/reference/mock validation.
- Domain tests for score calculation, publication immutability and risk transitions.
- Handler/API tests for idempotency, optimistic concurrency, permission and tenant/legal-entity isolation.
- Contract tests for every success/error response and lifecycle event payload.
- Mongo integration tests follow DB-010 isolated database naming.
- E4 module runtime evidence when authorized; G5 remains open until cross-module E5 integration passes.

## 18. Ready-for-dev Checklist

- [x] Canonical ID/name passed DCP-002 on 2026-09-15.
- [x] Owned OpenAPI is frozen as `SUPPLIER-PERFORMANCE` v1.
- [x] Backend-only proposed first slice, paths, objects, failures and acceptance are explicit.
- [x] Central CT publishes/freezes SUPPLIER-BASE and mock — done 2026-09-15 in `docs/analysis/contracts/supplier.openapi.yaml` (x-status FROZEN, x-owner MOD-0140, `c8badff96`), the day before this pack was committed.
- [x] Placement: DC-01 CONCURRED 2026-10-18; domain-config / DCP-009 / registry aligned 2026-10-19.
- [ ] Supplier base consumption + Tenant/LE eligibility (DC-02) — MOD-0140 owner; build seam, not built.
- [ ] Metric policy (DC-04) — no Metric Registry service exists; **deferred**. Blocks deterministic evaluation/score.
- [ ] Risk taxonomy (DC-05) — no Risk Register service exists; **deferred**. Blocks risk registration.
- [ ] Shared contract successor, replay/concurrency receipt, permission definition/composition, fixture, portal-source
      resolution — OPEN in `docs/roadmap/plans/mvp6-mod0147-spec-split-01/CONCURRENCE-OPEN.md`.
- [ ] SupplyChain service audit trail accepted under AUD-001 (§21) — the service has no audit-ledger file (BL-615).
- [ ] Module owner reviews and explicitly changes `status` to `ready-for-dev`.
- [ ] Runtime implementation WP supplies base HEAD, agent lane and bounded allowed paths.

## 19. Implementation Notes

- **ASSUMPTION:** scores and weights are decimal strings in range 0..100; overall score is server-derived.
- **ASSUMPTION:** the initial slice is backend-only and uses `SupplierPerformance` as feature root.
- **Resolved 2026-10-19:** the ownership ASSUMPTION is replaced by DC-01 placement (above); the governance records are
  aligned in the same change.
- **CONSUMES: SUPPLIER-BASE (merkez üretecek)**; only opaque `supplierId` crosses this boundary.

## 20. Follow-up Items

- SUPPLIER-BASE contract/mock owned by MOD-0140 is published and frozen: `docs/analysis/contracts/supplier.openapi.yaml` (x-status FROZEN, `c8badff96`, 2026-09-15), offering `listSuppliers`, `getSupplier` and `validateSuppliers`. What remains open is the authority to consume it — DC-02, AWAITING-CT per the 2026-10-05 CT ruling — not the contract's existence.
- ~~Reconcile MOD-0147 ownership in domain-config and central records~~ — done 2026-10-19 (DC-01 governance alignment).
- Separate permission/gateway integration WP after endpoints exist.
- Future UI pack revision if an operator surface is approved.
- G5 cross-module verification with MOD-0148 and the MVP-6 logistics/planning feedback flow.

## 21. Audited Events

AUD-001 (`audit-trail-standard.md`). `Diten.SupplyChainService` has **no accepted audit trail** today: there is no
`tests/architecture/audit-ledger/Diten.SupplyChainService.md` (part of BL-615), and its module-local `*_audit`
collections are not an accepted §5.c equivalent trail. Per §10.1 a service without accepted infrastructure gets no
`a`/`b` path; the trail is a **dependency** and blocks `ready-for-dev`. No command here is K2 class.

| Komut | Olay adi | Nesne (tur) | Yol | Onceki/sonraki ya da degisen alanlar | Istisna sinifi + gerekce |
|---|---|---|---|---|---|
| CreateSupplierEvaluationCommand | supplier-evaluation.created | SupplierEvaluation | — (bagimlilik: SupplyChain izi) | AfterState: SupplierId, PeriodStart, PeriodEnd, Metrics, OverallScore, RiskLevel | — |
| SubmitSupplierEvaluationCommand | supplier-evaluation.published | SupplierEvaluation → SupplierScorecard | — (bagimlilik: SupplyChain izi) | Before/After: Status; AfterState: ScorecardId, OverallScore | — |
| RegisterSupplierRiskCommand | supplier-risk.registered | SupplierRisk | — (bagimlilik: SupplyChain izi) | AfterState: SupplierId, Category, Level, Status, SourceType, SourceId | — |
| ChangeSupplierRiskStatusCommand | supplier-risk.status-changed | SupplierRisk | — (bagimlilik: SupplyChain izi) | Before/After: Status, Level, ResolvedAt | — |

Not recorded: no credential or token is part of these commands. `Summary` (max 1000) and `resolutionNote` (max 2000) are
free text and may carry personal or sensitive data: the audit record keeps their **length and SHA-256, not the text**
(proposal of this review; the business record keeps the text under `supplychain.supplier-performance.read`). Owner to confirm.
