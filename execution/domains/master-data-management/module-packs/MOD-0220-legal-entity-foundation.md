---
id: MOD-0220
name: Corporate Secretarial / Entity Management
domain: master-data-management
service: Diten.MdmService
shell: none
golden_reference: none
entity_base: EntityBase
status: ready-for-dev
owner: mdm-domain-team
branch: feature/mdm/mod-0220-legal-entity-foundation
started: ""
target: ""
form_field_count: 0
---

# MOD-0220 - Legal Entity Foundation

> **Ready-for-dev note:** MOD-0220 is ready-for-dev for the explicitly authorized minimal backend slice only.
> This does not authorize frontend, gateway, full Corporate Secretarial scope, business-country catalog, or
> MOD-0040 implementation.

> **Audit note — lifecycle rollback:** `ready-for-dev` -> `under-review`.
> Reason: minimal backend schema reconciliation before orchestrator implementation. The prior ready-for-dev
> decision remains historical and must be re-approved after schema reconciliation review.

> **Promotion note — schema reconciliation:** `under-review` -> `ready-for-dev`.
> Reason: minimal backend schema reconciliation reviewed and explicitly approved.

> **Canonical-ID note:** `MOD-0220` is reserved by explicit user decision after authoritative planning Excel mapping
> confirmation (`MOD-0220` -> `Corporate Secretarial / Entity Management`). Authoritative Enterprise Blueprint
> repository migration remains pending.

## 1. Module Summary

MOD-0220 is the MDM-owned Corporate Secretarial / Entity Management module. The first delivery slice is Legal
Entity Foundation: a narrow system-of-record foundation for Legal Entity identity and the read-only
`LegalEntityId` lookup / validation contract consumed by downstream modules such as MOD-0040.

## 2. Ownership and Boundaries

**V1 owned scope:**

- Legal Entity master record
- stable `LegalEntityId`
- legal name
- display name
- tenant-scoped only ownership model
- soft-delete / archival semantics
- audit semantics
- `DRAFT` / `ACTIVE` / `ARCHIVED` lifecycle and referenceable state
- minimal validation
- read-only `LegalEntityId` lookup / validation contract for consumers such as MOD-0040

**V1 out of scope:**

- Entity relationships
- Corporate actions
- Filing obligations
- Filing records
- Statutory document links
- Full workflow / evidence engine
- Full approval workflow
- Full Legal Entity UI
- Gateway route implementation
- Business-country catalog ownership
- RegistrationNumber and TaxIdentifier for the minimal lookup slice
- Jurisdiction and related uniqueness rules before Reference Data governance is settled
- Legal Form hardcoded enum
- Territory
- Permission evaluation
- `IDataScopeResolver` algorithm
- MOD-0040 organization structure implementation
- Global/shared Legal Entity ownership
- Mixed ownership model
- Cross-tenant permitted scope
- `IN_REVIEW`, `APPROVED`, `SUSPENDED`, or `INACTIVE` lifecycle states
- Workflow approval engine
- Evidence gate
- Frontend UI
- Gateway route
- Business-country catalog

## 3. Owned Objects

Conceptual baseline for the minimal backend slice:

- Legal Entity master record
- Legal Entity lifecycle/referenceable state
- Legal Entity lookup / validation contract

Concrete entity, collection, repository, command, query, DTO, endpoint, permission, and test details are authored
during minimal backend implementation. No frontend route is authorized by this pack.

## 4. Entity Fields

Field-level schema reconciliation has been reviewed and explicitly approved. The first backend slice locks only
the minimal lookup-safe schema below.

| Field | Rule |
|---|---|
| `LegalEntityId` | Stable canonical entity identifier. Repo-standard `EntityBase` identifier usage must be reviewed before adding any duplicate second identifier. |
| `TenantId` | Set from server-side tenant context. Not accepted from body, DTO, form, or other client payload. |
| `Code` | Required. Unique business code within the current tenant. |
| `LegalName` | Required. |
| `DisplayName` | Optional. |
| `LifecycleStatus` | Required. Enum: `DRAFT`, `ACTIVE`, `ARCHIVED`. Default: `DRAFT`. |
| `IsDeleted` | Technical soft-delete semantics; inherited or explicit according to repo standard. |
| Audit fields | Repo-standard `EntityBase` audit semantics. |

Referenceable rule:

- Only `ACTIVE` records are referenceable.

Registration and jurisdiction boundary:

- `RegistrationNumber` and `TaxIdentifier` are not required for the first minimal lookup slice.
- Jurisdiction and uniqueness rules are not locked until Reference Data governance is settled.

Tenant model:

- V1 is tenant-scoped only.
- `TenantId` is set server-side from tenant context.
- `TenantId` is not accepted from body, DTO, form, or other client payload.
- Legal Entity records may be read and changed only inside the current tenant scope.
- Cross-tenant access must fail closed.
- Global/shared and mixed ownership models are out of scope for v1.

Deferred field groups:

**MDM Reference Data Governance Foundation follow-up**

- `LegalFormId`
- `JurisdictionCountryId`
- `CurrencyId`
- `StatutoryStatusId`
- `EntityKindId`
- `AccountingStandardId`
- `TaxRegimeId`
- `ControlTypeId`

Legal Form is not a hardcoded enum. It comes from a Reference Data Management source. PSS-011 countries lookup is
Platform provisioning/support only and is not the Legal Entity business-country source.

**Entity Relationship follow-up**

- `ParentEntityId`
- `OwnershipPercent`
- `ControlTypeId`
- Subsidiary / Holding / Joint Venture relationship semantics

These relationship semantics must not be embedded directly into the Legal Entity core aggregate.

**Core Profile follow-up**

- `RegistrationNumber`
- `TaxIdentifier`
- VAT/GST Number
- PlaceOfIncorporation
- IncorporationDate
- DissolutionDate
- RegisteredAddress
- CorrespondenceAddress
- OfficialEmail
- OfficialPhone
- Website
- `BaseCurrencyId`
- FiscalYearVariant

**Workflow / Evidence follow-up**

- ApprovalStatus
- ReviewDue
- SourceSystem
- LegacyCode
- EvidenceStatus
- CompletenessScore
- Review & Submit workflow

**Broader Corporate Secretarial follow-up**

- Corporate Actions
- Filing Calendar / Inbox
- Statutory Documents

Organization Role modeling note:

- Prototype Organization Role dropdown mixes different concepts and must not be implemented as one hardcoded enum.
- EntityKind follow-up: Legal Entity, Branch, Representative Office.
- EntityRelationship follow-up: Subsidiary, Holding, Joint Venture, ownership / control semantics.
- Headquarters follow-up: evaluate as address / facility / org-unit concept.

## 5. Repo Scope

This promotion milestone may touch only governance documents:

- `execution/domains/master-data-management/**`
- `execution/registries/module-id-registry.md`
- `execution/portfolio/master-development-plan.md`
- `execution/portfolio/blueprint-master-plan-reconciliation.md`
- minimal boundary synchronization in DCP-001 and MOD-0040 references

Allowed implementation paths for the first backend slice:

- `services/Diten.MdmService/**`
- repo-standard `Diten.MdmService` test paths

The first backend slice is limited to:

- minimal Legal Entity aggregate
- MongoDB persistence
- tenant isolation
- `TenantId` server-side only
- cross-tenant fail-closed behavior
- `IsDeleted` technical soft-delete
- lifecycle: `DRAFT` / `ACTIVE` / `ARCHIVED`
- referenceable: `ACTIVE` only
- read-only `LegalEntityId` lookup / validation contract
- backend tests

This promotion task does not create or edit production implementation files.

Not authorized in the first backend slice:

- frontend UI implementation
- gateway route implementation
- full Corporate Secretarial scope
- business-country catalog
- MOD-0040 implementation

Conditional paths:

- `gateway/**` only through integration-agent and separate approved scope.
- `frontend/**` only through separate approved UI scope.
- MOD-0040 implementation only through its own ready-for-dev gate.

## 6. Protected Paths

- `frontend/**` - protected for the first backend slice.
- `gateway/**` - protected for the first backend slice; route changes require integration-agent and separate
  approved implementation scope.
- `services/Diten.Platform/**` - protected; MOD-0040 implementation has its own gate.
- `services/Diten.AuthService/**` - protected unless later explicit contract integration scope is approved.
- other domain services - not owned by MOD-0220.
- `.antigravity/**` - protected global engineering system.
- archive / frozen paths - reference-only unless explicitly approved.
- `execution/domains/platform-shared-services/**` - protected except explicitly approved minimal DCP/MOD-0040
  reconciliation references.

## 7. Dependencies

- DCP-001 Access Governance, for MOD-0040 consumer boundary.
- MOD-0040 Tenant Organization Foundation, as a future read-only `LegalEntityId` consumer.
- Module ID Registry, where `MOD-0220` is reserved.
- Blueprint / Master Plan Reconciliation, where authoritative Enterprise Blueprint repository migration remains pending.

## 8. Runtime Constraints

- MOD-0220 is ready-for-dev for the explicitly approved minimal backend-only slice after schema reconciliation
  review.
- Runtime persistence is authorized only under `services/Diten.MdmService/**`.
- Frontend, gateway, full Corporate Secretarial scope, business-country catalog, and MOD-0040 implementation are
  not authorized by this pack.
- Future implementation must follow `.antigravity/rules/` standards and the approved MDM domain-config.
- Legal Entity Foundation v1 uses tenant-scoped only ownership.
- `TenantId` must be set server-side from tenant context and must not be accepted from body, DTO, form, or other
  client payload.
- Same-tenant validation only is authorized for v1.
- Future explicitly permitted cross-tenant scope remains a separate governance follow-up.

Lifecycle / referenceable state:

| State | Referenceable |
|---|---|
| `DRAFT` | no |
| `ACTIVE` | yes |
| `ARCHIVED` | no |

`IsDeleted` is technical soft-delete. `ARCHIVED` is a business lifecycle status.

## 9. Layout & Shell Contract

`shell: none`. Legal Entity Foundation is backend/contract-only for the authorized first slice.

- No Razor layout applies.
- No frontend route applies.
- No DataTable verifier applies in this governance step.

## 10. Backend File Convention

No backend files are authored by this governance promotion.

Future implementation must follow the approved module pack, the real MDM service scaffold decision, and the
standard 5-layer CQRS architecture referenced by `.antigravity/rules/erp-architecture.md`.

## 11. Frontend File Contract

No frontend files are authorized by this pack.

Future planning note:

- Future Legal Entity UI is expected to exceed 8 user-editable fields.
- If UI is later approved, the default candidate is `GoldenReferenceCompact`.
- This is a planning note, not an implementation authorization.

## 12. Validation Rules

Concrete validation rules are deferred. V1 validation intent:

- Legal Entity identifiers must be stable and non-duplicated within the approved tenant/business boundary.
- Legal/display names must satisfy approved requiredness and length rules.
- Registration and tax identifiers must satisfy approved minimal format rules.
- Reference validation must fail closed for missing, cross-tenant, inaccessible, or non-referenceable Legal Entities.
- MOD-0040 validation is same-tenant only: `LegalEntityId` exists, `LegalEntity.TenantId` equals the current
  tenant ID, and `LegalEntity.Status == ACTIVE`.

V1 read-only `LegalEntityId` lookup / validation contract:

- Validate `LegalEntityId` exists.
- Validate `LegalEntity.TenantId == current TenantId`.
- Validate `LegalEntity.Status == ACTIVE`.
- Return:
  - `LegalEntityId`
  - legal name
  - display name
  - lifecycle state
  - `referenceable = true`

Cross-tenant permitted scope is out of scope for v1.

## 13. Failure Path to Verify

Future implementation must verify at least:

- Unknown `LegalEntityId` rejected.
- Cross-tenant or unauthorized `LegalEntityId` rejected.
- Archived or non-referenceable Legal Entity rejected for new MOD-0040 references.
- Duplicate legal/registration identity handled according to approved v1 uniqueness rules.

## 14. Authorization Convention

No permission keys are fixed in this promotion.

Permission evaluation remains owned by MOD-0018. MOD-0220 may define CRUD/admin permissions during
`ready-for-dev`, but it does not own authorization evaluation or data-scope algorithms.

## 15. Gateway / API Routing Decision

No gateway route in this governance step.

Future gateway changes require:

- approved / ready-for-dev module pack scope
- production API endpoint decision
- integration-agent ownership for Ocelot route changes

## 16. Acceptance Criteria

This pack is ready-for-dev when:

1. `MOD-0220` is reserved in the registry as MDM-owned and ready-for-dev after schema reconciliation approval.
2. MDM domain scaffold exists with README, domain-config, and this module pack.
3. Legal Entity is recorded as the MDM system of record.
4. MOD-0040 remains only a read-only `LegalEntityId` contract consumer.
5. PSS-011 countries lookup remains Platform provisioning/support only.
6. No production, frontend, gateway, test, CI, or `.antigravity/**` implementation files are changed.
7. First implementation scope is limited to `services/Diten.MdmService/**` and repo-standard
   `Diten.MdmService` test paths.

## 17. Test Expectations

No tests are authored in this governance reconciliation.

First backend-slice tests should cover lookup validation, tenant boundary behavior, server-side `TenantId`,
cross-tenant fail-closed behavior, soft delete, lifecycle/referenceable state, and fail-closed handling.

## 18. Ready-for-dev Checklist

- [ ] Enterprise Blueprint repository migration completed
    Non-blocking governance follow-up for the minimal backend slice.
- [x] reserved MOD-0220 registry entry reviewed
- [x] MDM domain scaffold reviewed
- [x] OD-MDM-le-contract approved
- [x] MOD-0040 OD-MOD-le-contract reconciled
- [x] service scaffold milestone approved
- [x] tenant ownership model approved: tenant-scoped only
- [x] lifecycle / referenceable states approved: DRAFT / ACTIVE / ARCHIVED; only ACTIVE referenceable
- [x] MDM production-service scaffold strategy approved
- [x] implementation repo scope explicitly authorized before orchestrator development
- [x] OD-MDM-le-contract final reconciliation review completed
- [x] MOD-0040 OD-MOD-le-contract reconciliation completed
- [x] test expectations final review completed
- [x] v1 entity fields schema reconciliation reviewed and explicitly approved
- [x] test expectations approved
- [x] implementation branch strategy approved
- [x] explicit human approval for ready-for-dev promotion granted
- [x] MOD-0220 under-review -> ready-for-dev promotion re-approved after schema reconciliation

## 19. Implementation Notes

**OD-MDM-le-contract:** Resolved. The minimal read-only `LegalEntityId` lookup / validation contract consumed by
MOD-0040 is locked for v1.

Minimal contract candidate:

- Validate `LegalEntityId` exists.
- Validate `LegalEntity.TenantId == current TenantId`.
- Validate `LegalEntity.Status == ACTIVE`.
- Return minimal lookup metadata:
  - `LegalEntityId`
  - legal name
  - display name
  - lifecycle state
  - `referenceable = true`

Cross-tenant permitted scope is out of scope for v1 and remains a future governance follow-up.

Lifecycle:

- `DRAFT` -> not referenceable
- `ACTIVE` -> referenceable
- `ARCHIVED` -> not referenceable

`IsDeleted` is technical soft-delete. `ARCHIVED` is a business lifecycle status.

Country boundary:

- PSS-011 countries lookup is Platform provisioning/support only.
- It is not the MDM business-country source of record.
- MDM business-country reference ownership is a separate governance follow-up.
- Legal Entity Foundation must not silently default business-country ownership to PSS-011.

Service / UI / gateway:

- MOD-0220 is ready-for-dev for the explicitly approved minimal backend-only slice after schema reconciliation
  review.
- Allowed implementation paths are `services/Diten.MdmService/**` and repo-standard `Diten.MdmService` test paths.
- First backend slice is limited to minimal aggregate, MongoDB persistence, tenant isolation, server-side
  `TenantId`, cross-tenant fail-closed behavior, `IsDeleted` technical soft-delete, `DRAFT` / `ACTIVE` /
  `ARCHIVED` lifecycle, `ACTIVE`-only referenceability, read-only `LegalEntityId` lookup / validation contract,
  and backend tests.
- Frontend and gateway remain outside this first implementation slice.
- Future gateway changes require separately approved scope and integration-agent.
- MOD-0040 implementation requires its own ready-for-dev gate.

## 20. Follow-up Items

- Authoritative Enterprise Blueprint repository migration for `MOD-0220`.
- MDM business-country reference ownership module/follow-up.
- `Diten.MdmService` minimal scaffold implementation will be executed by @orchestrator after this
  ready-for-dev promotion and an explicit implementation handoff.
- Legal Entity UI planning, likely `GoldenReferenceCompact` if approved.
- Entity relationships, corporate actions, filing obligations, filing records, statutory document links, and
  workflow/evidence gates.
