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

## 21. FG-A1-OWNER-AMENDMENT-01 v1 — bounded Legal Entity writer hardening (planning only)

**Owner-decision record:** This amendment records the current code truth and freezes a two-step A1 plan. The user
later explicitly approved and completed the bounded A1a 25-field slice as recorded in §21.3; that approval does not
change this pack's frontmatter/status or promote another slice to `ready-for-dev`. A1b remains `BLOCKED` until every
unresolved decision in §21.5 is resolved. Completed MOD-0290/FU03 A0 evidence is unchanged.

Cross-pack authority links:

- MOD-0290 records the Finished Good writer-fence integration boundary and the same planning-only verdict in
  [§21.15.7](MOD-0290-product-item-sku-master.md#21157-fg-a1-owner-amendment-01-v1--legal-entity-owner-decision-link).
- MOD-0290-FU03 records the A0 reuse/non-expansion boundary in
  [§22](MOD-0290-FU03-product-legal-entity-scope-assignment.md#22-fg-a1-owner-amendment-01-v1--a0-reuse-boundary).

### 21.1 Current code truth and ordered scope

**Done / existing:** the current shared `LegalEntityWriteRequest` exposes 30 business fields, and Update maps the
request into an aggregate before the generic `RepositoryBase.ReplaceOneAsync` path. That stale whole-document
replacement can write back `OperationalStatus`/`IsDeleted` and the five owner-governance fields. Existing Suspend,
Archive, Delete, Activate and Create behavior remains the regression baseline; no claim is made that those writers
are already physically fenced.

The exact current writer/side-effect chain is frozen below. Line references describe the current binary source, not
future authorization:

| Writer / side effect | Current code-truth chain | Required state after this amendment |
|---|---|---|
| Create | `services/Diten.MdmService/src/Diten.MdmService.Api/Controllers/LegalEntitiesController.cs:56-61` → `services/Diten.MdmService/src/Diten.MdmService.Application/Features/LegalEntity/Handlers/CommandHandlers/CreateLegalEntityHandler.cs:16-40` → `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/RepositoryBase.cs:29-37` | A1-outside expansion writer; regression-only and unchanged. |
| Update | `services/Diten.MdmService/src/Diten.MdmService.Api/Controllers/LegalEntitiesController.cs:64-69` → `services/Diten.MdmService/src/Diten.MdmService.Application/Features/LegalEntity/Handlers/CommandHandlers/UpdateLegalEntityHandler.cs:16-52` → `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/RepositoryBase.cs:40-51` | A1a must replace this terminal generic whole-document route with the dedicated editable-field CAS and close the generic Update bypass. |
| Activate | `services/Diten.MdmService/src/Diten.MdmService.Api/Controllers/LegalEntitiesController.cs:72-77` → `services/Diten.MdmService/src/Diten.MdmService.Application/Features/LegalEntity/Handlers/CommandHandlers/ActivateLegalEntityHandler.cs:17-38` → `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/RepositoryBase.cs:40-51` | A1-outside expansion/restoration writer; current generic writer is explicitly retained as regression-only, not claimed fenced. |
| Suspend | `services/Diten.MdmService/src/Diten.MdmService.Api/Controllers/LegalEntitiesController.cs:80-85` → `services/Diten.MdmService/src/Diten.MdmService.Application/Features/LegalEntity/Handlers/CommandHandlers/SuspendLegalEntityHandler.cs:17-34` → `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/RepositoryBase.cs:40-51` | A1b target; terminal physical write must move behind the approved guarded transaction. |
| Archive | `services/Diten.MdmService/src/Diten.MdmService.Api/Controllers/LegalEntitiesController.cs:88-93` → `services/Diten.MdmService/src/Diten.MdmService.Application/Features/LegalEntity/Handlers/CommandHandlers/ArchiveLegalEntityHandler.cs:17-34` → `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/RepositoryBase.cs:40-51` | A1b target; terminal physical write must move behind the approved guarded transaction. |
| Delete | `services/Diten.MdmService/src/Diten.MdmService.Api/Controllers/LegalEntitiesController.cs:96-101` → `services/Diten.MdmService/src/Diten.MdmService.Application/Features/LegalEntity/Handlers/CommandHandlers/DeleteLegalEntityHandler.cs:16-21` → `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/RepositoryBase.cs:54-66` | A1b target; terminal physical soft-delete must move behind the approved guarded transaction. |
| Startup migration | `services/Diten.MdmService/src/Diten.MdmService.Persistence/DependencyInjection.cs:38-43` → `services/Diten.MdmService/src/Diten.MdmService.Persistence/Configurations/LegalEntityOperationalStatusMigration.cs:19-38` | A1b blocker; owner disposition is retire/default-disable with completion precondition or separately approved enrollment. |
| Audit forwarding | `services/Diten.MdmService/src/Diten.MdmService.Application/Behaviors/AuditForwardingBehavior.cs:30-57` | Post-handler best-effort forwarding with swallowed failure; it is not the same-transaction immutable outcome required by A1b. |

After A1a, only Update's generic replacement bypass is closed; Create and Activate remain explicit current writers
outside the A1b contraction set. After A1b, Suspend/Archive/Delete must have no reachable generic physical-write
route. The startup migration disposition and durable-outcome decisions still prevent an all-writer-fence claim.

**A1a — first bounded slice, approved and completed:** the explicit edit CAS and field-scoped Mongo update are
evidenced in §21.3. This does not enroll Update in the A1b foreground contraction fence.

**A1b — later bounded slice, blocked:** enroll only `Suspend`, `Archive` and `Delete` in a separate Legal Entity typed
foreground contraction authority. `Activate`, `Create` and A1a Update are non-enrolled and regression-only. A
handler call by itself is not a physical writer fence.

### 21.2 A1a approved owner decision — exact editable CAS

The proposed request/version contract is:

- add nullable `ExpectedVersion` to `LegalEntityWriteRequest` and carry it through browser, API and Update command;
- Create rejects a non-null `ExpectedVersion`;
- Update requires `ExpectedVersion >= 0`;
- missing, foreign-tenant or already-deleted targets return the same non-disclosing `404`;
- a matching visible target with a stale version returns `409` and performs no write;
- `RepositoryBase` remains unchanged;
- add `ILegalEntityRepository.UpdateEditableFieldsAsync(proposed, expectedVersion, ct)` and implement it only in
  `LegalEntityRepository` with filter `TenantId + Id + IsDeleted:false + Version`;
- the physical update uses `$set` for only the approved editable fields plus server-derived
  `CompletenessScore`/`UpdatedAt`, and `$inc` for `Version`.

The exact approved **25-field UI-backed editable set** is:

1. `Code`
2. `LegalName`
3. `DisplayName`
4. `LegalFormCode`
5. `OrganizationRoleCode`
6. `RegistrationNumber`
7. `TaxId`
8. `VatNumber`
9. `PlaceOfIncorporation`
10. `IncorporationDate`
11. `DissolutionDate`
12. `CountryCode`
13. `StatutoryStatus`
14. `ParentLegalEntityId`
15. `OwnershipPercent`
16. `ControlTypeCode`
17. `FiscalYearVariant`
18. `AccountingStandardCode`
19. `TaxRegimeCode`
20. `BaseCurrencyCode`
21. `RegisteredAddressJson`
22. `CorrespondenceAddressJson`
23. `OfficialEmail`
24. `OfficialPhone`
25. `Website`

This 25-only set is the explicitly approved owner decision: it matches the current wizard-backed edit surface while
preventing ordinary edits from owning lifecycle, deletion, identity, provenance or evidence state.
It preserves the five owner-governance fields `ApprovalStatus`, `ReviewDueUtc`, `SourceSystem`, `LegacyCode` and
`EvidenceStatus`; it also preserves `OperationalStatus`, `IsDeleted`, `DeletedAt`, `TenantId`, `Id`, `CreatedAt` and
unknown BSON fields. `CompletenessScore` and `UpdatedAt` are server-derived, not client-owned. The user approved this
exact set; §21.3 records its bounded implementation and evidence.

#### A1a exact runtime allow-list (proposed; closed)

1. `services/Diten.MdmService/src/Diten.MdmService.Application/Features/LegalEntity/LegalEntityModels.cs`
2. `services/Diten.MdmService/src/Diten.MdmService.Application/Features/LegalEntity/Commands/UpdateLegalEntityCommand.cs`
3. `services/Diten.MdmService/src/Diten.MdmService.Application/Features/LegalEntity/Validators/LegalEntityCommandValidators.cs`
4. `services/Diten.MdmService/src/Diten.MdmService.Application/Features/LegalEntity/Handlers/CommandHandlers/UpdateLegalEntityHandler.cs`
5. `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/ILegalEntityRepository.cs`
6. `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/LegalEntityRepository.cs`
7. `services/Diten.MdmService/src/Diten.MdmService.Api/Controllers/LegalEntitiesController.cs`
8. `frontend/Diten.Web/wwwroot/assets/js/MasterData/LegalEntities/wizard.js`

#### A1a exact test allow-list (proposed; closed)

1. `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/LegalEntityCommandTests.cs`
2. `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/InMemoryLegalEntityRepository.cs`
3. `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/LegalEntityUpdateContractTests.cs` (**new, proposed**)
4. `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/LegalEntityEditableCasMongoTests.cs` (**new, proposed**)
5. `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/Authorization/LegalEntitiesControllerPermissionTests.cs`
6. `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/LegalEntityWriteRequestValidatorTests.cs`
7. `frontend/Diten.Web/tests/legal-entities.test.js` (**new, proposed**)

No file outside these two lists is authorized by A1a. Existing regression suites may be executed read-only but are
not writable scope.

The existing MVC proxy at `frontend/Diten.Web/Controllers/LegalEntitiesController.cs:70-75` already forwards the
Update request body without remodeling it and therefore needs no A1a runtime edit. That file is read-only evidence,
not A1a runtime scope. The proposed frontend contract test must inspect that raw-body preservation together with the
wizard contract; if implementation proves the controller must change, work stops for an explicit allow-list
amendment.

### 21.3 A1a measurable plan — planned assertions, not PASS evidence

The planned focused unit/contract/security count is **13 assertions**:

| # | Planned assertion |
|---|---|
| 1 | Shared request contract exposes nullable `ExpectedVersion`; existing business-field JSON names remain compatible. |
| 2 | `LegalEntityWriteRequestValidatorTests` proves Create rejects non-null `ExpectedVersion` without persistence. |
| 3 | `LegalEntityWriteRequestValidatorTests` proves Update rejects missing `ExpectedVersion`. |
| 4 | `LegalEntityWriteRequestValidatorTests` proves Update rejects negative `ExpectedVersion`. |
| 5 | The frontend test proves detail/edit hydration captures the returned version. |
| 6 | The frontend test proves the same captured version is present in the Update PUT JSON payload. |
| 7 | The frontend test inspects the MVC proxy contract and proves raw request-body preservation to the Gateway route. |
| 8 | API Update permission and route binding remain unchanged. |
| 9 | Update maps missing/foreign/deleted to the same non-disclosing `404`. |
| 10 | A visible stale-version miss maps to `409`, while a matching version succeeds. |
| 11 | Repository contract statically limits `$set` to the exact 25 fields plus server-derived values and `$inc Version`; `RepositoryBase` is untouched. |
| 12 | Owner/lifecycle/identity/deletion/unknown-BSON fields remain preservation contracts. |
| 13 | Existing Activate behavior remains an explicit regression contract. |

The planned real-Mongo count is **8 assertions**:

| # | Planned assertion |
|---|---|
| 1 | Matching-version normal edit updates the exact 25-set, server-derived completeness/time and increments `Version` once. |
| 2 | All five owner-governance fields remain byte-for-byte/equivalent-value preserved. |
| 3 | Operational/deletion/tenant/id/created fields remain preserved. |
| 4 | An injected unknown BSON field survives the edit. |
| 5 | Stale CAS produces zero physical mutation and the application returns `409`. |
| 6 | Two concurrent edits from one version yield exactly one success and one stale conflict. |
| 7 | Missing, foreign and deleted probes are indistinguishable `404` with zero mutation. |
| 8 | Edits captured before Suspend, Archive and Delete cannot revert those outcomes; normal edit and existing Activate behavior still regress GREEN. |

The real-Mongo evidence fixture is fixed to `AuditIntentTemporalMongoFixture` plus
`ProductLegalEntityScopeMongoCollection`, database `diten_mdm_product_scope_itest`, a fresh tenant per test and
tenant-owned cleanup. A localhost fallback is prohibited. `LegalEntityMongoRoundTripTests` may remain a general
regression signal, but its fallback-capable harness is **not A1 evidence** and contributes zero to the counts above.
Existing Create/Update validation, controller permission, lifecycle and Legal Entity tests are regressions reported
separately; overlapping runs are never summed.

#### A1a completed implementation and evidence — 2026-09-13

The user explicitly approved the bounded A1a slice and its exact 25-field decision after the planning record above.
That approval supersedes the earlier A1a-only `proposed`/`no code-start` gate; it does not change frontmatter/status,
approve A1b, or alter any A0/history. The implementation consumed only this exact runtime change set:

1. `services/Diten.MdmService/src/Diten.MdmService.Application/Features/LegalEntity/LegalEntityModels.cs`
2. `services/Diten.MdmService/src/Diten.MdmService.Application/Features/LegalEntity/Validators/LegalEntityCommandValidators.cs`
3. `services/Diten.MdmService/src/Diten.MdmService.Application/Features/LegalEntity/Handlers/CommandHandlers/UpdateLegalEntityHandler.cs`
4. `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/ILegalEntityRepository.cs`
5. `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/LegalEntityRepository.cs`
6. `frontend/Diten.Web/wwwroot/assets/js/MasterData/LegalEntities/wizard.js`

The exact implemented test change set is:

1. `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/LegalEntityCommandTests.cs`
2. `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/InMemoryLegalEntityRepository.cs`
3. `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/LegalEntityWriteRequestValidatorTests.cs`
4. `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/LegalEntityUpdateContractTests.cs` (**new**)
5. `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/LegalEntityEditableCasMongoTests.cs` (**new**)
6. `frontend/Diten.Web/tests/legal-entities.test.js` (**new**)

The already-compatible `UpdateLegalEntityCommand.cs`, MDM API controller and MVC raw-body proxy required no runtime
change. `LegalEntitiesControllerPermissionTests.cs` remained a read-only regression/evidence input. No other planned
path was consumed.

The RED records under `.testoutput/fg-a1a-legal-entity-editable-cas-01/red/` were separate runs and are not summed:

- .NET focused RED: `34/10/0` (passed/failed/skipped). The exact ten failures were:
  1. `Diten.MdmService.Application.Tests.LegalEntityEditableCasMongoTests.UpdateEditableFieldsAsync_TwoConcurrentEditsFromOneVersionYieldOneSuccessAndOneConflict` — expected one success, observed two;
  2. `Diten.MdmService.Application.Tests.LegalEntityWriteRequestValidatorTests.Update_WithNegativeExpectedVersion_Fails` — `ExpectedVersion` property was absent;
  3. `Diten.MdmService.Application.Tests.LegalEntityUpdateContractTests.LegalEntityRepository_ExposesDedicatedEditableVersionCas` — dedicated repository method was absent;
  4. `Diten.MdmService.Application.Tests.LegalEntityCommandTests.Update_WhenLifecycleChangesAfterRead_ReturnsConflictAndDoesNotResurrect` — stale update incorrectly succeeded;
  5. `Diten.MdmService.Application.Tests.LegalEntityEditableCasMongoTests.UpdateEditableFieldsAsync_StaleCapturedEditCannotResurrectLifecycleInvalidator(invalidatedStatus: Suspended)` — stale update incorrectly succeeded;
  6. `Diten.MdmService.Application.Tests.LegalEntityWriteRequestValidatorTests.Update_WithoutExpectedVersion_Fails` — `ExpectedVersion` property was absent;
  7. `Diten.MdmService.Application.Tests.LegalEntityEditableCasMongoTests.UpdateEditableFieldsAsync_StaleCapturedEditCannotResurrectLifecycleInvalidator(invalidatedStatus: Archived)` — stale update incorrectly succeeded;
  8. `Diten.MdmService.Application.Tests.LegalEntityWriteRequestValidatorTests.Create_WithExpectedVersion_Fails` — `ExpectedVersion` property was absent;
  9. `Diten.MdmService.Application.Tests.LegalEntityUpdateContractTests.LegalEntityWriteRequest_ExposesNullableExpectedVersion` — nullable property contract was absent; and
  10. `Diten.MdmService.Application.Tests.LegalEntityUpdateContractTests.UpdateHandler_UsesDedicatedCasAndDoesNotUseGenericWholeDocumentUpdate` — handler source did not call `UpdateEditableFieldsAsync`.
- Frontend RED: `1/2/0`. The exact failures were
  `MOD-0220 Legal Entity editable-version contract > captures the detail version while hydrating an edit` and
  `MOD-0220 Legal Entity editable-version contract > sends the captured expectedVersion in the update JSON payload`;
  the MVC raw-body pass-through contract already passed.

The bounded correction implemented nullable `ExpectedVersion`, Create/Update validation, browser detail-version
hydration and PUT payload propagation, handler stale-version conflict behavior, and the dedicated tenant/id/
`IsDeleted:false`/version field-scoped CAS. The repository `$set`s only the approved 25 fields plus server-derived
`CompletenessScore`/`UpdatedAt`, `$inc`s `Version`, and preserves governance, lifecycle, deletion, identity,
creation-time and unknown BSON state; generic `RepositoryBase` was not changed.

Corrected focused .NET evidence was `42/0/0`. The first A1a real-Mongo correction run was `7/1/0`: only
`Update_NormalEditUpdatesExactEditableSetAndPreservesAllProtectedStateAndUnknownBson` failed with a test-harness
`FormatException` because the test asked the typed `LegalEntity` serializer to read the deliberately injected raw
`A1aUnknown` BSON field. This was not a business-write failure. The test harness was narrowed to a tenant-scoped raw
BSON filter (`TenantId + _id`), a typed projection excluding the deliberate unknown field, and tenant+id cleanup;
the corrected exact A1a real-Mongo rerun was `8/0/0`.

The final frontend contract run was `3/0/0`, and JavaScript syntax validation for
`frontend/Diten.Web/wwwroot/assets/js/MasterData/LegalEntities/wizard.js` passed. The initial MDM Release build had
`3` existing warnings and `0` errors; the final recorded MDM Release build and final MDM test-project Release build
were each `0` warnings / `0` errors. The frontend Release build had `14` existing warnings and `0` errors.

A0 and adjacent regressions were intentionally executed and reported separately, with overlap never aggregated:
focused A0 `25/0/0`; write-admission Mongo `23/0/0`; activation Mongo `3/0/0`; rollout Mongo `20/0/0`; Finished Good
human-context `17/0/0`; DI `4/0/0`; and non-Mongo manifest/audit `11/0/0`. Mongo evidence used the existing
test-owned dynamic loopback replica set and fixed `diten_mdm_product_scope_itest` database; no
`localhost:27017` fallback was used. No general suite or live acceptance was run.

The post-correction security-agent verdict was `PASS` for this bounded A1a implementation/evidence. Two limitations
remain explicit: parent Legal Entity referenceability is still checked before, rather than atomically inside, the
editable CAS and can race a parent lifecycle change; and the A1b-target Suspend/Archive/Delete paths remain current
generic writers until a separately approved A1b implementation. A1a therefore closes only stale editable Update;
it grants no A1b, all-writer-fence, Production or live-acceptance claim.

**A1a module-pack-author verdict:** `PASS — BOUNDED A1a ONLY`. This verdict records the approved implementation and
evidence above; it does not change the existing A1b planning/blocker contract.

### 21.4 A1b frozen proposal — typed foreground contraction authority

The exact operations and permissions proposed for A1b are:

| Operation | Required outer permission | Required input concepts after owner decision |
|---|---|---|
| Suspend | `mdm.legal-entities.update` | `LegalEntityId` + stable `CommandId` + nonnegative `ExpectedVersion`; final wire/record shape unresolved. |
| Archive | `mdm.legal-entities.update` | `LegalEntityId` + stable `CommandId` + nonnegative `ExpectedVersion`; final wire/record shape unresolved. |
| Delete | `mdm.legal-entities.delete` | `LegalEntityId` + stable `CommandId` + nonnegative `ExpectedVersion`; final wire/record shape unresolved. |

Current producer/transport is bodyless end to end: MDM API endpoints at
`services/Diten.MdmService/src/Diten.MdmService.Api/Controllers/LegalEntitiesController.cs:72-100` construct the
three commands whose records currently contain only `LegalEntityId` at
`services/Diten.MdmService/src/Diten.MdmService.Application/Features/LegalEntity/Commands/SuspendLegalEntityCommand.cs:7`,
`ArchiveLegalEntityCommand.cs:7` and `DeleteLegalEntityCommand.cs:7`; MVC proxies at
`frontend/Diten.Web/Controllers/LegalEntitiesController.cs:78-99` forward bodyless PATCH/DELETE; list actions at
`frontend/Diten.Web/wwwroot/assets/js/MasterData/LegalEntities/index.js:139-156` send bodyless requests, and the
details action in `frontend/Diten.Web/wwwroot/assets/js/MasterData/LegalEntities/details.js:112-121` is bodyless too.
Consequently the exact HTTP shape, version source, idempotency owner and compatibility behavior are not frozen.
The conceptual command shape above is not a final C# record signature and cannot be implemented until §21.5.

The separate Legal Entity authority binds tenant, canonical human subject, Legal Entity id, stable `CommandId`,
nonnegative `ExpectedVersion`, exact operation (`Suspend|Archive|Delete`), required permission, mutation kind and
fingerprint, and proof fingerprint. The provider independently validates exact outer permission plus tenant/actor;
controller authorization is necessary but not sufficient. JWT revocation is a point-in-time invocation boundary:
this proposal does not claim continuous revocation after the authority is issued or invent a new credential flow.

Persistence owns the Mongo session/transaction. In the same transaction it revalidates the active unexpired
token/generation using Mongo server time, performs the qualified rollout-state write, applies the Legal Entity
semantic/version CAS or soft-delete, and persists an immutable exact outcome/readback. Exact committed replay is a
read-only success; payload, actor, operation or fingerprint drift is denied. Rollback or ambiguous outcome must not
release authority until exact persisted resolution. There is no automatic state reset, takeover, extension, blind
unlock, recovery/background grant, new credential/audience, schema/index/collection authority.

MOD-0290-FU03 A0 `ProductLegalEntityScope*` authority types/methods remain named and limited to foreground
`Replace`; A1b does not rename, generalize or expand them. Only the proven low-level server-time expiry predicate,
lease-retention/no-takeover discipline and technical guarded-write patterns may be reused in separately named Legal
Entity types.

The existing FU03 singleton tenant rollout-state writer lease is frozen as an **ordering/serialization dependency
only**. It is never Legal Entity authority and never confers permission. The separately named Legal Entity provider
must issue proof-bound typed authority, and the guarded transaction must independently validate the existing lease
token/generation. No second Legal Entity lease/state/document/collection/index/schema may be created. These existing
paths are read-only/reused dependencies unless a later code-start amendment explicitly authorizes an exact change:

1. `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/IProductLegalEntityScopeRolloutStateRepository.cs`
2. `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/ProductLegalEntityScopeWriterLease.cs`
3. `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/ProductLegalEntityScopeRolloutState.cs`
4. `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/ProductLegalEntityScopeRolloutStateRepository.cs`

If A1b requires any interface/model/repository change in this dependency set, the exact file/member change is a
new owner/code-start amendment decision; it is not silently authorized here.

#### A1b verified existing minimum paths (proposed scope; no code-start)

1. `services/Diten.MdmService/src/Diten.MdmService.Application/Features/LegalEntity/Validators/LegalEntityCommandValidators.cs`
2. `services/Diten.MdmService/src/Diten.MdmService.Application/Features/LegalEntity/Handlers/CommandHandlers/SuspendLegalEntityHandler.cs`
3. `services/Diten.MdmService/src/Diten.MdmService.Application/Features/LegalEntity/Handlers/CommandHandlers/ArchiveLegalEntityHandler.cs`
4. `services/Diten.MdmService/src/Diten.MdmService.Application/Features/LegalEntity/Handlers/CommandHandlers/DeleteLegalEntityHandler.cs`
5. `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/ILegalEntityRepository.cs`
6. `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/LegalEntityRepository.cs`
7. `services/Diten.MdmService/src/Diten.MdmService.Application/DependencyInjection.cs`
8. `services/Diten.MdmService/src/Diten.MdmService.Persistence/DependencyInjection.cs`
9. `services/Diten.MdmService/src/Diten.MdmService.Infrastructure/DependencyInjection.cs`

#### A1b candidate producer/transport paths (unresolved; no code-start)

These paths are not a closed runtime allow-list. They are the verified current producer/transport surface that an
owner-approved request-shape decision may amend:

1. `services/Diten.MdmService/src/Diten.MdmService.Application/Features/LegalEntity/LegalEntityModels.cs` (candidate home for an exact request model; a separate model/file requires a further amendment)
2. `services/Diten.MdmService/src/Diten.MdmService.Api/Controllers/LegalEntitiesController.cs`
3. `services/Diten.MdmService/src/Diten.MdmService.Application/Features/LegalEntity/Commands/SuspendLegalEntityCommand.cs`
4. `services/Diten.MdmService/src/Diten.MdmService.Application/Features/LegalEntity/Commands/ArchiveLegalEntityCommand.cs`
5. `services/Diten.MdmService/src/Diten.MdmService.Application/Features/LegalEntity/Commands/DeleteLegalEntityCommand.cs`
6. `frontend/Diten.Web/Controllers/LegalEntitiesController.cs`
7. `frontend/Diten.Web/wwwroot/assets/js/MasterData/LegalEntities/index.js`
8. `frontend/Diten.Web/wwwroot/assets/js/MasterData/LegalEntities/details.js`
9. `frontend/Diten.Web/tests/legal-entities.test.js` (**new, proposed transport/compatibility evidence**)

The following filenames are **PROPOSED candidates requiring explicit owner approval**. Listing them neither creates
the files nor authorizes their implementation, storage collection, index or schema:

1. `services/Diten.MdmService/src/Diten.MdmService.Domain/ValueObjects/LegalEntityVerifiedWriterAuthority.cs`
2. `services/Diten.MdmService/src/Diten.MdmService.Application/Contracts/Authorization/ILegalEntityWriterAuthorityProvider.cs`
3. `services/Diten.MdmService/src/Diten.MdmService.Infrastructure/Security/LegalEntityWriterAuthorityProvider.cs`
4. `services/Diten.MdmService/src/Diten.MdmService.Application/Features/LegalEntity/LegalEntityMutationIdentity.cs`
5. `services/Diten.MdmService/src/Diten.MdmService.Application/Features/LegalEntity/LegalEntityWriteFenceCoordinator.cs`
6. `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/ILegalEntityGuardedWriteSession.cs`
7. `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/LegalEntityGuardedWriteSession.cs`
8. `services/Diten.MdmService/src/Diten.MdmService.Domain/ValueObjects/LegalEntityContractionWriteOutcome.cs`
9. `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/LegalEntityContractionOperation.cs`

The final two candidates describe only the typed immutable result and recommended durable journal concept. Their
physical collection/index/retention model is deliberately unresolved and unauthorized.

#### A1b exact test allow-list (proposed; no code-start)

1. `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/LegalEntityCommandTests.cs`
2. `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/InMemoryLegalEntityRepository.cs`
3. `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/LegalEntityWriterAuthorityTests.cs` (**new, proposed**)
4. `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/LegalEntityScopeWriteFenceMongoTests.cs` (**new, proposed**)
5. `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ProductLegalEntityScopeWriteAdmissionContractTests.cs` (exact inventory/direct-bypass checks only)
6. `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/Authorization/LegalEntitiesControllerPermissionTests.cs`
7. `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/DependencyInjectionSmokeTests.cs`
8. `frontend/Diten.Web/tests/legal-entities.test.js` (**new, proposed producer/transport compatibility evidence**)

### 21.5 Real blockers and owner decisions

1. **Command producer/transport is undecided.** The owner must freeze an exact PATCH/DELETE HTTP request shape; the
   authoritative `ExpectedVersion` source and refresh behavior; the component that creates one stable `CommandId`
   per human intent and reuses it for transport retries; and compatibility/error behavior for current bodyless
   browser/MVC/API callers. Body JSON versus headers (for example, but not authorizing, `If-Match` and an idempotency
   key), model location and final command record signatures remain choices. No producer/transport file may change
   until one exact end-to-end decision is approved.
2. **Durable exact outcome/audit is missing.** Current `AuditForwardingBehavior` forwards only after the handler and
   treats forwarding failures as best-effort; it cannot prove same-transaction immutable exact outcome or safely
   resolve an ambiguous commit. The recommended decision is a tenant-owned immutable Legal Entity contraction
   operation/receipt journal, but this amendment does not authorize its collection, index, retention, schema or
   audit ordinals. If the owner instead requires central audit, the Legal Entity operation names must be
   collision-checked and [Platform MOD-0021-FU01](../../platform-shared-services/module-packs/MOD-0021-FU01-trusted-durable-source-audit-intent-ingestion.md)
   must receive a separate mapping amendment before code-start.
3. **An automatic writer remains outside the fence.** `LegalEntityOperationalStatusMigration.cs`, invoked from
   `Persistence/DependencyInjection.cs`, is a startup, untenant-qualified Legal Entity writer. The owner must either
   retire/default-disable it with an explicit completion precondition or enroll it through a separately approved
   design. A lazy-compatible substitute or new startup migration must not be silently invented. Until this is
   decided and proven, no all-writer-fence claim is valid.
4. **A1a prerequisite — satisfied, not an A1b approval.** The exact 25-field editable set was explicitly approved and
   the bounded implementation/evidence is recorded in §21.3. A1b still requires its own owner/code-start decision.

Therefore A1b implementation is `BLOCKED`; this document is not a DoR/code-start approval.

### 21.6 A1b measurable plan — planned assertions, not PASS evidence

The planned focused unit/contract/security count remains **9 assertion groups**; each group is counted once even
when one contract test inspects more than one layer:

1. the approved browser/MVC/API wire shape carries the authoritative version and one stable per-intent `CommandId`,
   preserves retry identity and defines bodyless-caller compatibility;
2. the three final command shapes bind `LegalEntityId`, stable `CommandId` and nonnegative `ExpectedVersion`;
3. operation-to-permission mapping is exact and independently enforced by the provider;
4. tenant, canonical human actor, Legal Entity identity, operation and mutation/proof fingerprints are proof-bound,
   and any drift is denied;
5. A0 `ProductLegalEntityScope*` inventory remains Replace-only, the shared lease is ordering-only and direct-bypass
   contracts stay closed;
6. A1a Update, Activate and Create are not enrolled and remain regression-only;
7. stale token/generation and expired authority are denied with lease retention;
8. exact committed replay/readback succeeds without mutation while rollback/ambiguous paths retain authority; and
9. DI resolves only the approved foreground graph, honors the migration disposition and exposes no
   background/recovery authority.

The planned real-Mongo count is **13 assertions**:

1. qualified Suspend commits rollout qualification, version CAS, status mutation and exact outcome atomically;
2. qualified Archive does the same;
3. qualified Delete performs the soft-delete semantic/version CAS and exact outcome atomically;
4. stale token is denied with zero business mutation;
5. wrong generation is denied with zero business mutation;
6. a lease expired by Mongo server time is denied and retained;
7. a forced transaction failure rolls back rollout, Legal Entity and outcome together;
8. an ambiguous commit is resolved only by exact persisted readback and retains authority until resolution;
9. exact committed replay returns the stored result without a duplicate mutation;
10. actor/operation/payload/proof drift after the same `CommandId` is denied;
11. direct repository/handler bypass cannot produce a physical mutation;
12. concurrent contraction contenders produce exactly one committed semantic/version transition; and
13. edits captured before Suspend, Archive or Delete cannot overwrite the committed contraction state.

The A1b real-Mongo harness has the same fixed fixture/database/tenant-cleanup requirements as A1a. Counts are plans,
not observed PASS totals. General suites, live acceptance and overlapping regression runs remain separately reported.

### 21.7 Explicitly out of scope and module-pack-author verdict

Out of scope: any new module ID; frontmatter/status change; RepositoryBase rewrite; A1a fields beyond the proposed
25; A1b enrollment of Update/Create/Activate; A0 type or method renaming; A2/Finished Good/background/recovery
authority; automatic reset/takeover/extension/unlock; new credential or audience; schema/index/collection/migration;
any second Legal Entity lease/state/document; central-audit operation ordinals/mappings without the separate Platform
amendment; gateway changes; live acceptance; and any runtime/test/build/Mongo/Git mutation in this authoring turn.

**module-pack-author verdict:** `A1a PASS; A1b CONDITIONAL / BLOCKED — PLANNING ONLY`. The explicitly approved bounded
A1a implementation/evidence is complete, subject to the stated parent-referenceability limitation. A1b has no
code-start until its producer/transport, durable outcome/audit and startup-migration writer decisions are explicitly
approved. No `ready-for-dev`, all-writer-fence, Production or live-acceptance claim is granted.
