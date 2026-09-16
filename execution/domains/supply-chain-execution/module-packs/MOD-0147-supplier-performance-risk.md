---
id: MOD-0147
name: Supplier Performance & Risk
domain: supply-chain-execution
service: Diten.SupplyChainService
shell: none
golden_reference: none
entity_base: EntityBase
status: draft
status_note: "Phase A specification draft only; no runtime code or service-scaffold authority. Domain ownership and central SUPPLIER-BASE contract gates remain open."
owner: supply-chain-execution / control-tower
branch: feature/mvp6-logistics
started: 2026-09-15
target: 2026-09-30
form_field_count: 0
---

# MOD-0147 — Supplier Performance & Risk

> **Identity gate:** `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0147 --name "Supplier Performance & Risk"`
> returned `OK` on 2026-09-15 against Blueprint 8.1 and the module ID registry.
>
> **Phase A only:** this draft records the proposed initial Phase B backend/contract slice. It does not authorize
> runtime code, service scaffold, gateway, permission-registry or integration changes.
>
> **ASSUMPTION:** WP-MVP6 assigns MOD-0147 execution ownership to `supply-chain-execution`. The current domain-config
> does not expressly list Supplier Performance & Risk. Domain-config and central ownership must be reconciled before
> this pack may become `ready-for-dev`.

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
| `SUPPLIER-BASE` | GAP — central frozen mock absent | **CONSUMES: SUPPLIER-BASE (merkez üretecek)**; no local substitute |
| Metric Registry / Risk Register | Building-block dependency | Contract adapters only; no local registry clone |
| Event Bus | Platform dependency | Correlation-preserving outbox publication |
| Domain ownership | OPEN | **GAP: merkez CT üretmeli** ownership reconciliation before ready-for-dev |

## 8. Runtime Constraints

- Proposed service is `Diten.SupplyChainService` on port 5061; this draft does not authorize runtime work.
- MongoDB records are tenant/legal-entity scoped; cross-scope access returns 404 without existence leakage.
- `TenantId` and `LegalEntityId` are server-resolved and absent from mutation payloads.
- Commands require `Idempotency-Key` and UUID `X-Correlation-Id`; replay creates no duplicate state/event.
- Scorecards are immutable publications; corrections require a new evaluation/publication trail.
- State write and outbox event are atomic or explicitly compensated; silent partial success is forbidden.
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
| `Idempotency-Key` | Mutations | Trimmed, 1..200 | Tenant+operation replay record |
| `X-Correlation-Id` | All operations | UUID | Propagate to response/audit/event |
| `SupplierId` | Create evaluation/risk | Non-empty opaque ID | SUPPLIER-BASE frozen mock; fail closed |
| Period | Evaluation | Valid ISO dates; start <= end | No duplicate active period |
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
- [ ] G5 evidence links scorecard/risk feedback to MVP-6 integration without overriding Supplier or procurement SoRs.

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
- [ ] Central CT publishes/freezes SUPPLIER-BASE and mock.
- [ ] **GAP: merkez CT üretmeli** domain-config/central ownership reconciliation for MOD-0147.
- [ ] Module owner reviews and explicitly changes `status` to `ready-for-dev`.
- [ ] Runtime implementation WP supplies base HEAD, agent lane and bounded allowed paths.

## 19. Implementation Notes

- **ASSUMPTION:** scores and weights are decimal strings in range 0..100; overall score is server-derived.
- **ASSUMPTION:** the initial slice is backend-only and uses `SupplierPerformance` as feature root.
- **ASSUMPTION:** supply-chain-execution ownership comes from WP-MVP6; domain-config/central ownership reconciliation
  is mandatory before ready-for-dev.
- **CONSUMES: SUPPLIER-BASE (merkez üretecek)**; only opaque `supplierId` crosses this boundary.

## 20. Follow-up Items

- **GAP: merkez CT üretmeli** and freeze the SUPPLIER-BASE contract/mock owned by MOD-0140.
- Reconcile MOD-0147 ownership in domain-config and central records.
- Separate permission/gateway integration WP after endpoints exist.
- Future UI pack revision if an operator surface is approved.
- G5 cross-module verification with MOD-0148 and the MVP-6 logistics/planning feedback flow.
