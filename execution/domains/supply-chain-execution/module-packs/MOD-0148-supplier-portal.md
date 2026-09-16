---
id: MOD-0148
name: Supplier Portal
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

# MOD-0148 — Supplier Portal

> **Identity gate:** `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0148 --name "Supplier Portal"`
> returned `OK` on 2026-09-15 against Blueprint 8.1 and the module ID registry.
>
> **Phase A only:** this draft records the proposed initial Phase B backend/contract slice. It does not authorize
> runtime code, service scaffold, frontend, gateway, permission-registry or integration changes.
>
> **ASSUMPTION:** WP-MVP6 assigns MOD-0148 execution ownership to `supply-chain-execution`. The current domain-config
> does not expressly list Supplier Portal. Domain-config and central ownership must be reconciled before this pack may
> become `ready-for-dev`.

## 1. Module Summary

MOD-0148 owns the authenticated supplier actor's portal access status and module-owned submission lifecycle. The
proposed initial Phase B slice is backend-only and contract-first against
`docs/analysis/contracts/supplier-performance.openapi.yaml` (`SUPPLIER-PERFORMANCE` v1). Supplier identity is always
resolved server-side from the authenticated actor and cannot be overridden by a request.

## 2. Ownership and Boundaries

**Owns:** portal access projection, submission records/payload snapshots, submit/review lifecycle, own-record queries,
idempotency, audit/outbox evidence and correlation propagation.

**Consumes:** API Gateway/authenticated actor mapping, Event Bus and
**CONSUMES: SUPPLIER-BASE (merkez üretecek)** through a frozen mock contract.

**Does not own:** MOD-0140 Supplier master/onboarding/credentials, tenant or legal-entity selection, supplier KYC,
MOD-0147 evaluations/scorecards/risks, source document SoRs, gateway/shared permission seams or foreign persistence.

## 3. Owned Objects

| Object | Purpose |
|---|---|
| `SupplierPortalAccessProjection` | Server-resolved supplier access state and submission summary |
| `SupplierPortalSubmission` | Supplier-owned draft/submitted/reviewed payload record |
| Commands | Create own submission; submit own draft; internal review is a later bounded command |
| Queries | Get own portal status; list/get own submissions |
| API | `/portal/status` and `/portal/submissions/**` in `SUPPLIER-PERFORMANCE` v1 |
| Events | Portal submission submitted/reviewed |
| Permissions | `supplychain.supplier-portal.read`, `.submit`, `.review` |

## 4. Entity Fields

### SupplierPortalSubmission

| Field | Rule |
|---|---|
| `Id` / `SubmissionId` | Server-minted stable identifier |
| `TenantId`, `LegalEntityId` | Server-resolved; never request body |
| `SupplierId` | Server-resolved from authenticated actor; never request body/query override |
| `SubmissionType` | Frozen enum: corrective action/evidence/certificate/response |
| `Subject` | Required, trimmed, max 250 |
| `ReferenceType`, `ReferenceId` | Optional typed reference; referenced SoR is not copied |
| `Payload` | Submission snapshot object; allowlisted and size-limited by type validator |
| `Status` | `DRAFT`, `SUBMITTED`, `UNDER_REVIEW`, `ACCEPTED`, `REJECTED` |
| `Version` | Optimistic concurrency token |
| `CreatedAt`, `SubmittedAt` | Server/audited UTC timestamps |
| `CorrelationId` | Required UUID propagated through response/audit/event |
| Soft-delete/audit fields | Repo-standard `EntityBase` fields |

Proposed indexes are tenant-first: `(TenantId, LegalEntityId, SupplierId, Status, CreatedAt)` and unique replay
index `(TenantId, SupplierId, Operation, IdempotencyKey)`.

## 5. Repo Scope

After all ready-for-dev gates pass, the proposed Phase B slice may write only:

- `services/Diten.SupplyChainService/src/**/Features/SupplierPortal/**`.
- `services/Diten.SupplyChainService/tests/**/SupplierPortal/**`.
- Module-specific API reference/audit evidence paths explicitly assigned by the implementation WP.
- This module pack's status/evidence fields.

The frozen `supplier-performance.openapi.yaml` is read-only implementation authority in Phase B. This draft grants
no current write permission to runtime paths.

## 6. Protected Paths

- `.antigravity/**`, all frozen contracts and MOD-0140 Supplier contract/schema.
- `gateway/Diten.ApiGateway/**/ocelot.json`, authentication mapping, shared permission registries/registrations.
- All frontend paths in this backend-only slice, Archive paths and the frozen shared layout.
- Other domain services and non-MOD-0148 features, including MOD-0147 runtime paths.
- Supplier master/profile/onboarding/identity persistence and direct foreign database access.

## 7. Dependencies

| Dependency | State | Rule |
|---|---|---|
| `SUPPLIER-PERFORMANCE` v1 | FROZEN | Portal paths are implementation authority |
| `SUPPLIER-BASE` | GAP — central frozen mock absent | **CONSUMES: SUPPLIER-BASE (merkez üretecek)**; no local substitute |
| Authenticated actor mapping | Central/Gateway seam | `supplierId` resolved server-side; client override forbidden |
| Event Bus | Platform dependency | Correlation-preserving outbox publication |
| Domain ownership | OPEN | **GAP: merkez CT üretmeli** ownership reconciliation before ready-for-dev |

## 8. Runtime Constraints

- Proposed service is `Diten.SupplyChainService` on port 5061; this draft does not authorize runtime work.
- Tenant, legal entity and supplier identity are resolved from trusted server context.
- Portal mutation schemas reject `supplierId`, `tenantId`, `legalEntityId` and unknown fields.
- Own-record queries never expose another supplier; inaccessible records return 404.
- Mutations require `Idempotency-Key`, UUID `X-Correlation-Id` and version checks where specified.
- Submission write and outbox event are atomic or explicitly compensated; no silent partial success.
- Portal does not store Supplier credentials, KYC or base-profile replicas.
- SUPPLIER-BASE/auth mapping unavailability fails closed.

## 9. Layout & Shell Contract

`shell: none`. The proposed first slice has no Razor UI, route, menu, DataTable or localization surface.

## 10. Backend File Convention

Proposed feature root is `Features/SupplierPortal/` with separate `Commands`, `Queries`,
`Handlers/CommandHandlers`, `Handlers/QueryHandlers`, `Validators` and one models file. Each public command, query,
handler and validator stays in its own file. Five-layer CQRS, pipeline and response-envelope rules apply after
authorization; this section is not scaffold permission.

## 11. Frontend File Contract

Not applicable. A usable supplier-facing Razor surface requires a separate approved pack revision defining tenant
shell, actor journey, exact fields, DataTable choice, golden reference, denied state and seven-language localization.

## 12. Validation Rules

| Field/Header | Required | Rule | Pre-check |
|---|---|---|---|
| `Idempotency-Key` | Mutations | Trimmed, 1..200 | Supplier+operation replay record |
| `X-Correlation-Id` | All operations | UUID | Propagate to response/audit/event |
| Supplier identity | Every operation | Authenticated actor mapping only | SUPPLIER-BASE central mock; fail closed |
| `SubmissionType` | Create | Frozen enum | Type-specific payload validator exists |
| `Subject` | Create | Trimmed, 1..250 | — |
| `Payload` | Create | Allowlisted shape and bounded size | No secret/token fields |
| `If-Match` | Submit/review | Current version | Conditional update |
| Status transition | Submit/review | Frozen lifecycle | Terminal record cannot be silently reopened |

## 13. Failure Path to Verify

- Missing/unmapped authenticated supplier returns 403 `PORTAL_SUPPLIER_IDENTITY_UNRESOLVED`; no write.
- Body/query attempts to override supplier/tenant/legal entity return 400; trusted context remains unchanged.
- Cross-supplier or cross-tenant lookup returns 404 without existence leakage.
- Duplicate idempotency key replays the original result without duplicate submission/event.
- Stale `If-Match` returns 409 and never overwrites a concurrent update.
- Invalid payload or lifecycle transition returns the frozen 400/422 error with UUID correlation.
- SUPPLIER-BASE/auth mapping outage fails closed and does not grant portal access.

## 14. Authorization Convention

Actors: authenticated supplier actor for own read/create/submit; tenant reviewer only for a separately approved review
command. Proposed server-side permissions:

- `supplychain.supplier-portal.read`
- `supplychain.supplier-portal.submit`
- `supplychain.supplier-portal.review`

Permission definition/seed and supplier actor mapping are shared seams and are not authorized by this draft.

## 15. Gateway / API Routing Decision

The frozen contract base is `/api/supplier-performance`, with portal paths under `/portal/**`. A future
`integration-agent` WP owns Ocelot and authenticated supplier context propagation. It must verify Authorization,
tenant/legal-entity context, correlation and idempotency headers. This draft grants no gateway write.

## 16. Acceptance Criteria

- [ ] Implementation payloads and failures match `SUPPLIER-PERFORMANCE` v1 portal contract tests.
- [ ] Portal status resolves supplier exclusively from authenticated server context.
- [ ] Create body cannot bind supplier/tenant/legal-entity identifiers or unknown fields.
- [ ] List/get returns only the authenticated supplier's records; cross-scope reads return 404.
- [ ] Submit transitions one draft exactly once and emits one correlation-preserving lifecycle event.
- [ ] Idempotent replay and stale version paths create no duplicate or overwritten side effects.
- [ ] Unresolved identity and SUPPLIER-BASE outage fail closed without access or writes.
- [ ] No Supplier master/KYC/credential replica exists locally.
- [ ] G5 evidence links accepted portal feedback to MOD-0147 without overriding Supplier/procurement SoRs.

## 17. Test Expectations

- DCP-002 identity verifier PASS and OpenAPI syntax/reference/mock validation.
- Domain tests for submission lifecycle and terminal-state behavior.
- Handler/API tests for actor-derived identity, forbidden override fields, idempotency and version conflict.
- Tenant/legal-entity/cross-supplier isolation and permission denial tests.
- Contract tests for each portal success/error response and event payload.
- Mongo integration tests follow DB-010 isolated database naming.
- E4 module runtime evidence when authorized; G5 remains open until cross-module E5 integration passes.

## 18. Ready-for-dev Checklist

- [x] Canonical ID/name passed DCP-002 on 2026-09-15.
- [x] Owned OpenAPI portal surface is frozen as `SUPPLIER-PERFORMANCE` v1.
- [x] Backend-only proposed first slice, identity boundary, paths, failures and acceptance are explicit.
- [ ] Central CT publishes/freezes SUPPLIER-BASE and mock.
- [ ] Central owner freezes authenticated supplier actor mapping behavior.
- [ ] **GAP: merkez CT üretmeli** domain-config/central ownership reconciliation for MOD-0148.
- [ ] Module owner reviews and explicitly changes `status` to `ready-for-dev`.
- [ ] Runtime implementation WP supplies base HEAD, agent lane and bounded allowed paths.

## 19. Implementation Notes

- **ASSUMPTION:** the initial slice is backend-only and uses `SupplierPortal` as feature root.
- **ASSUMPTION:** structured payload validation is selected by `submissionType`; arbitrary executable content and
  secrets are rejected.
- **ASSUMPTION:** supply-chain-execution ownership comes from WP-MVP6; domain-config/central ownership reconciliation
  is mandatory before ready-for-dev.
- **CONSUMES: SUPPLIER-BASE (merkez üretecek)**; supplier identity is server-derived and opaque.

## 20. Follow-up Items

- **GAP: merkez CT üretmeli** and freeze SUPPLIER-BASE plus authenticated supplier actor mapping mock behavior.
- Reconcile MOD-0148 ownership in domain-config and central records.
- Define and approve the internal review command if required beyond the frozen portal self-service surface.
- Separate permission/gateway integration WP after endpoints exist.
- Future supplier-facing UI pack revision and G5 integration with MOD-0147.
