---
id: MOD-0290-FU02
name: Market Supply Assignment Foundation
domain: master-data-management
service: Diten.MdmService
shell: tenant
golden_reference: slim
entity_base: EntityBase
status: draft
owner: product-data-owner / regulatory-information-owner / mdm-domain-team
branch: feature/mdm/mod-0290-fu02-market-supply-assignment
started: "2026-08-09"
target: "2026-08-09"
form_field_count: 7
parent_module: MOD-0290
parent_dcp: execution/portfolio/delivery-capability-packs/DCP-005-material-product-master-data-coding-alignment.md
canonical_blueprint: docs/System Capability & Implementation Blueprint - master 8.1.xlsx
---

# MOD-0290-FU02 - Market Supply Assignment Foundation

> **Draft/no-code-start guard (2026-08-09):** This pack is planning and ownership reconciliation only. It does not
> authorize runtime, API, Gateway, frontend, configuration, data, navigation, entitlement, WorkCenter, branch, stage,
> commit or push changes. Every A-H step below requires its own exact code-start gate after the Ready-for-dev blockers
> are closed.
>
> **Parent/identity guard:** Master 8.1 contains `MOD-0290 - Product / Item / SKU Master` but no standalone Market
> Supply Assignment, Registered Presentation or Marketing Authorization module row. DCP-002 verification passed for
> the exact child tuple `MOD-0290-FU02 / Market Supply Assignment Foundation / parent MOD-0290`. The child does not
> create a new standalone Blueprint capability.
>
> **Regulatory dependency guard:** DCP-005 step 5, the Registered Presentation/Marketing Authorization owner and
> reference contract, must reach its approved exit before this pack can become `approved` or `ready-for-dev`.
> Registered Presentation is a required relationship member, not an optional placeholder. No nullable surrogate,
> free-text presentation or MA field may bypass that gate.

## 1. Module Summary

Market Supply Assignment is a tenant-owned relationship aggregate that states when one tenant Legal Entity may supply
one Registered Presentation of one LSKU as one Finished Good in one verified market. It is the only planned bridge
between LSKU/Registered Presentation and Finished Good. It does not add a direct LSKU-to-Finished-Good property or
foreign key to either existing aggregate.

The foundation is deliberately limited to Draft authoring and read surfaces. It records effective dates and the
future lifecycle vocabulary, but it does not submit, approve, activate, inactivate or retire an assignment. Those
transitions require the separately gated WorkCenter integration and the approved regulatory contract.

Master 8.1 evidence:

- `Blueprint_Data!A291:AG291` identifies `MOD-0290 - Product / Item / SKU Master`, with the canonical Product/SKU
  registry owned by MOD-0290.
- `Module Pages!A3226:M3230` lists the current Product/SKU master pages and contains no Market Supply Assignment page.
- Exact workbook searches for `Market Supply`, `Supply Assignment`, `Registered Presentation` and
  `Marketing Authorization` returned no standalone module match.
- Therefore the correct planning identity is a MOD-0290 follow-up, not a new invented MOD identifier.

## 2. Ownership and Boundaries

### Owned by this follow-up

- The `MarketSupplyAssignment` relationship aggregate and its Draft/read contract.
- Relationship identity, idempotency, effective-date validation, tenant isolation and optimistic concurrency.
- Verification that all referenced records belong to the current trusted tenant and are referenceable.
- Snapshot of the provider-issued market resolution evidence used by the assignment.
- Local audit intent creation and durable delivery participation using the existing MOD-0290 audit contract.

### Referenced, not owned

- `Lsku` and `FinishedGood`: MOD-0290 parent aggregates; neither receives a Market Supply Assignment FK.
- `Gsku`: not persisted on the assignment. The handler derives it and proves
  `Lsku.GskuId == FinishedGood.GskuId`.
- `RegisteredPresentation`: required opaque reference owned by the future DCP-005 regulatory step. This pack does not
  define its entity, lifecycle, MA semantics or persistence.
- Marketing Authorization: owned by the regulatory contract and reached only through the validated Registered
  Presentation. No separate `MarketingAuthorizationId`, number or free-text MA field exists here.
- `LegalEntity`: MOD-0220 remains its source of record. This aggregate stores only `SupplyingLegalEntityId`.
- Market: MOD-0048-FU01 verified universal `market` catalog remains the sole code authority. This pack stores no
  hardcoded market list and creates no market tenant assignment.

### Exact relationship semantics

- Tenant scope is inherited from `EntityBase` and resolved from trusted server context; it is never client input.
- `SupplyingLegalEntityId` identifies the tenant Legal Entity responsible for the supply assignment. It is not
  overloaded as manufacturer, site or MA holder.
- `MarketSelection` identifies the exact verified market code/version resolved by MOD-0048.
- `LskuId` identifies the local SKU whose existing market must equal `MarketSelection.ValueCode`.
- `RegisteredPresentationId` identifies the approved regulatory presentation for the same tenant, market and LSKU.
- `FinishedGoodId` identifies the physical Finished Good for the same underlying GSKU.
- `GskuId` is derived proof only and is never duplicated in persistence or accepted from a client.

Cardinality is many relationship aggregates across the tenant, but exactly one durable aggregate is allowed for one
normalized tuple `(TenantId, SupplyingLegalEntityId, MarketCode, LskuId, RegisteredPresentationId, FinishedGoodId)`.
The tuple is represented by a server-generated `RelationshipKey`. A tombstoned/Retired tuple cannot be silently
re-created under a new identity. Multi-period history or reactivation of the same tuple is a later explicit extension;
the foundation chooses one effective interval per relationship so concurrent interval overlap cannot be created.

## 3. Owned Objects

- Aggregate: `MarketSupplyAssignment : EntityBase, IAuditIntentAggregate`.
- Enum: `MarketSupplyAssignmentLifecycleStatus` with `Draft`, `Active`, `Inactive`, `Retired`.
- Repository: `IMarketSupplyAssignmentRepository` and Mongo implementation.
- Commands:
  - `CreateMarketSupplyAssignmentDraftCommand`.
  - `UpdateMarketSupplyAssignmentDraftCommand`.
  - `DeleteMarketSupplyAssignmentDraftCommand`.
- Queries:
  - `GetMarketSupplyAssignmentListQuery`.
  - `GetMarketSupplyAssignmentByIdQuery`.
  - `GetMarketSupplyAssignmentCreateOptionsQuery`.
- API routes:
  - `GET /api/market-supply-assignments`.
  - `GET /api/market-supply-assignments/{id}`.
  - `GET /api/market-supply-assignments/create-options`.
  - `POST /api/market-supply-assignments/drafts`.
  - `PUT /api/market-supply-assignments/drafts/{id}`.
  - `DELETE /api/market-supply-assignments/drafts/{id}?expectedVersion={n}`.
- Tenant-shell route: `/MasterDataManagement/MarketSupplyAssignments`.
- Manifest page: `MARKET_SUPPLY_ASSIGNMENTS`, navigation hidden until a separate navigation decision.

## 4. Entity Fields

| Field | Type | Persistence/API rule |
|---|---|---|
| `Id` | `Guid` | Server generated. |
| `TenantId` | `Guid` | Inherited; trusted server context only; never accepted from body/header as authority. |
| `SupplyingLegalEntityId` | `Guid` | Required; same-tenant, active, non-deleted MOD-0220 Legal Entity. |
| `MarketCode` | `string` | Required, exact uppercase `^[A-Z]{2}$`; server copied from verified resolution. |
| `MarketSelection` | `ReferenceCatalogSelection` | Required exact `SetCode=market`, code, immutable catalog version and resolution evidence. |
| `LskuId` | `Guid` | Required, same-tenant, non-deleted LSKU; its market equals `MarketCode`. |
| `RegisteredPresentationId` | `Guid` | Required opaque ID; same-tenant/referenceable validation through the approved regulatory contract. |
| `FinishedGoodId` | `Guid` | Required, same-tenant, non-deleted Finished Good; shares the LSKU GSKU. |
| `RelationshipKey` | `string` | Server-only deterministic SHA-256 over length-prefixed normalized tuple values; never client supplied. |
| `LifecycleStatus` | enum | Defaults to `Draft`; foundation endpoints may persist only `Draft`. |
| `ValidFromUtc` | `DateTimeOffset` | Required UTC effective start. |
| `ValidToUtc` | `DateTimeOffset?` | Optional exclusive end; when present must be greater than start. |
| `CreationCommandId` | `string` | Required server-normalized idempotency identity derived from `Idempotency-Key`; max 128. |
| `AuditIntents` | collection | Existing embedded local audit contract; created atomically with business mutation. |
| `AuditIntentReceipts` | collection | Existing durable acknowledgement/compaction contract. |
| `Version` | `int` | Inherited optimistic concurrency/fencing token; incremented only by business mutation. |
| `IsDeleted`, `DeletedAt` | base fields | Draft delete is soft delete; no physical cleanup. |
| `CreatedAt`, `UpdatedAt` | base fields | Server timestamps. |

User-editable form fields are exactly seven: Legal Entity, Market, LSKU, Registered Presentation, Finished Good,
Valid From and Valid To. Therefore Golden Reference Slim applies.

## 5. Repo Scope

The lists below are planning allow-lists, not current write authority.

### A - Domain and ownership contract

New runtime files:

- `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/MarketSupplyAssignment.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Enums/MarketSupplyAssignmentLifecycleStatus.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/IMarketSupplyAssignmentRepository.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Contracts/RegisteredPresentations/IRegisteredPresentationReferenceResolver.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/MarketSupplyAssignmentDomainTests.cs`

Existing exact file allowed only for the new aggregate discriminator:

- `services/Diten.MdmService/src/Diten.MdmService.Domain/Enums/AuditAggregateType.cs`

### B - Persistence, indexes and audit fencing

New runtime/test files:

- `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/MarketSupplyAssignmentRepository.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/MarketSupplyAssignmentMongoTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/MarketSupplyAssignmentAuditIntentMongoTests.cs`

Existing exact files:

- `services/Diten.MdmService/src/Diten.MdmService.Persistence/DependencyInjection.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/AuditIntentDeliveryRepository.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/AuditIntentDeliveryMongoTests.cs`

### C - Application CQRS and provider/reference validation

New exact files under
`services/Diten.MdmService/src/Diten.MdmService.Application/Features/MarketSupplyAssignments/`:

- `Commands/CreateMarketSupplyAssignmentDraftCommand.cs`
- `Commands/UpdateMarketSupplyAssignmentDraftCommand.cs`
- `Commands/DeleteMarketSupplyAssignmentDraftCommand.cs`
- `Queries/GetMarketSupplyAssignmentListQuery.cs`
- `Queries/GetMarketSupplyAssignmentByIdQuery.cs`
- `Queries/GetMarketSupplyAssignmentCreateOptionsQuery.cs`
- `Handlers/CommandHandlers/CreateMarketSupplyAssignmentDraftHandler.cs`
- `Handlers/CommandHandlers/UpdateMarketSupplyAssignmentDraftHandler.cs`
- `Handlers/CommandHandlers/DeleteMarketSupplyAssignmentDraftHandler.cs`
- `Handlers/QueryHandlers/GetMarketSupplyAssignmentListHandler.cs`
- `Handlers/QueryHandlers/GetMarketSupplyAssignmentByIdHandler.cs`
- `Handlers/QueryHandlers/GetMarketSupplyAssignmentCreateOptionsHandler.cs`
- `Validators/CreateMarketSupplyAssignmentDraftValidator.cs`
- `Validators/UpdateMarketSupplyAssignmentDraftValidator.cs`
- `MarketSupplyAssignmentModels.cs`

New tests:

- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/MarketSupplyAssignmentApplicationTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/MarketSupplyAssignmentReferenceValidationTests.cs`

The existing `IVerifiedMarketReferenceResolver`, LSKU, Finished Good, GSKU and Legal Entity repository contracts are
read-only dependencies and are not in the write allow-list.

### D - API and nav-hidden manifest declaration

New files:

- `services/Diten.MdmService/src/Diten.MdmService.Api/Contracts/MarketSupplyAssignmentRequests.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Api/Controllers/MarketSupplyAssignmentsController.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/MarketSupplyAssignmentApiContractTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/MarketSupplyAssignmentAuthorizationTests.cs`

Existing files:

- `services/Diten.MdmService/src/Diten.MdmService.Api/ModuleRegistration/ProductItemSkuMasterManifestProvider.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ModuleRegistration/ProductItemSkuMasterManifestProviderTests.cs`

### E - Permission catalog/entitlement onboarding gate

FU02 runtime allow-list: **none**. The MDM manifest declarations use canonical ModuleCode
`product-item-sku-master`, but catalog/entitlement/default-role changes are Auth/Platform-owned. If the generic
manifest chain does not provide the approved Admin/Viewer matrix, prepare and approve a separate MOD-0018 follow-up;
do not expand this MDM pack into Auth or Platform code.

Read-only evidence paths:

- `services/Diten.AuthService/src/Diten.AuthService.Domain/Authorization/DefaultRolePermissionTemplate.cs`
- `services/Diten.AuthService/src/Diten.AuthService.Application/Common/Services/EntitlementPermissionSyncService.cs`
- `services/Diten.Platform/src/Diten.Platform.Application/Features/ModuleRegistration/**`

### F - Gateway integration

Separately authorized integration-agent allow-list:

- `gateway/Diten.ApiGateway/ocelot.json`

Only explicit base/catch-all routes for `/api/market-supply-assignments` may change. Existing routes and final service
catch-all ordering are immutable. Gateway build and route contract evidence are required.

### G - Golden Slim tenant frontend

New exact files:

- `frontend/Diten.Web/Controllers/MarketSupplyAssignmentsController.cs`
- `frontend/Diten.Web/Models/MarketSupplyAssignments/MarketSupplyAssignmentViewModels.cs`
- `frontend/Diten.Web/Views/MasterDataManagement/MarketSupplyAssignments/Index.cshtml`
- `frontend/Diten.Web/Views/MasterDataManagement/MarketSupplyAssignments/_Filter.cshtml`
- `frontend/Diten.Web/Views/MasterDataManagement/MarketSupplyAssignments/_DataTable.cshtml`
- `frontend/Diten.Web/Views/MasterDataManagement/MarketSupplyAssignments/_IndexL10n.cshtml`
- `frontend/Diten.Web/Views/MasterDataManagement/MarketSupplyAssignments/_CreateEditOffcanvas.cshtml`
- `frontend/Diten.Web/Views/MasterDataManagement/MarketSupplyAssignments/_DetailsQuickView.cshtml`
- `frontend/Diten.Web/Views/MasterDataManagement/MarketSupplyAssignments/MarketSupplyAssignmentsIndex.cs`
- `frontend/Diten.Web/wwwroot/assets/js/MasterDataManagement/MarketSupplyAssignments/index.js`
- `frontend/Diten.Web/wwwroot/assets/js/MasterDataManagement/MarketSupplyAssignments/index.l10n.js`
- `frontend/Diten.Web/Resources/Views/MasterDataManagement/MarketSupplyAssignments/MarketSupplyAssignmentsIndex.en.resx`
- `frontend/Diten.Web/Resources/Views/MasterDataManagement/MarketSupplyAssignments/MarketSupplyAssignmentsIndex.fr.resx`
- `frontend/Diten.Web/Resources/Views/MasterDataManagement/MarketSupplyAssignments/MarketSupplyAssignmentsIndex.es.resx`
- `frontend/Diten.Web/Resources/Views/MasterDataManagement/MarketSupplyAssignments/MarketSupplyAssignmentsIndex.zh.resx`
- `frontend/Diten.Web/Resources/Views/MasterDataManagement/MarketSupplyAssignments/MarketSupplyAssignmentsIndex.ar.resx`
- `frontend/Diten.Web/Resources/Views/MasterDataManagement/MarketSupplyAssignments/MarketSupplyAssignmentsIndex.ru.resx`
- `frontend/Diten.Web/Resources/Views/MasterDataManagement/MarketSupplyAssignments/MarketSupplyAssignmentsIndex.tr.resx`
- `frontend/Diten.Web/tests/market-supply-assignment-foundation.test.js`

### H - Local Development acceptance and evidence

Runtime allow-list: **none**. This step is an explicit operational authorization after A-G tests pass. It may create at
most one Draft relationship through supported UI/API paths and perform read-only API/Mongo/browser verification. It
does not activate, approve, direct-write Mongo, create reference/master data or mutate navigation.

Planning/governance files allowed by this preparation task:

- `execution/domains/master-data-management/module-packs/MOD-0290-FU02-market-supply-assignment-foundation.md`
- `execution/registries/module-id-registry.md`

## 6. Protected Paths

- Existing `Gsku.cs`, `Lsku.cs`, `FinishedGood.cs` and their repository/CQRS contracts: no relationship FK or field.
- Existing Legal Entity runtime and data: referenced read-only; no lifecycle or schema change.
- MOD-0048 provider code, verified market artifact, publication, pointer and tenant-assignment data.
- Registered Presentation/MA implementation until its own approved DCP-005 pack exists.
- `services/Diten.AuthService/**` and `services/Diten.Platform/**` under this FU02.
- `gateway/Diten.ApiGateway/ocelot.json` outside separately approved step F.
- Shared layouts, navigation/left menu, Archive paths and `.antigravity/**`.
- WorkCenter, workflow definitions/tasks/decisions/callbacks and Enterprise Strategy.
- Configuration, secrets, credentials, committed appsettings and environment mutation.
- Direct Mongo writes, cleanup, seed shortcuts, hardcoded markets and test seams/fake providers.
- Production/Staging enablement, branch changes, stage, commit and push.

## 7. Dependencies

| Dependency | Required state | Boundary |
|---|---|---|
| Master 8.1 MOD-0290 | Parent/SoR proven | Product/SKU identity owner. |
| DCP-004 | MOD-0290 coding and audit/concurrency contract | No direct LSKU-FG link; expected-version and audit intent pattern. |
| DCP-005 step 5 | **Approved exit before code-start** | Registered Presentation/MA owner and opaque reference-validation contract. |
| MOD-0048-FU01 verified market | Published/readable | Only `market` resolver/enumeration evidence is accepted. |
| MOD-0220 Legal Entity | Active reference validation | Same-tenant, active, non-deleted or non-leaking 404. |
| Existing GSKU/LSKU/Finished Good | Available | Same tenant; LSKU and FG must derive the same GSKU. |
| MOD-0018 permission chain | Separate owner gate | Catalog/entitlement/default-role onboarding. |
| MOD-0023 WorkCenter | Not needed for Draft/read; required later | Submit/approve/activate/inactivate/retire orchestration. |

## 8. Runtime Constraints

- Trusted tenant comes from validated auth context; `X-Tenant-Id` is transport context, never standalone authority.
- Every repository filter includes current `TenantId`; a foreign-tenant ID returns the same 404 as an absent ID.
- Legal Entity and Registered Presentation are revalidated at create/update and later at submit/activation.
- If a referenced Legal Entity later becomes non-referenceable, historical assignment reads retain the ID; there is no
  cascade, nulling or hidden reassignment.
- Market is freshly resolved by `IVerifiedMarketReferenceResolver`; exact set/code/version evidence is persisted.
- No client-supplied provider evidence, `GskuId`, `RelationshipKey`, tenant or lifecycle is trusted.
- Relationship key hashing uses length-prefixed tuple components, not separator concatenation.
- `Idempotency-Key` is required for create. Same tenant/key/fingerprint replays the same result; same key/different
  fingerprint is 409. Same relationship/different key is also 409.
- Updates/deletes require exact expected `Version`; stale writers receive 409.
- Local audit intent is written in the same aggregate mutation. Claim token/generation/lease fencing controls delivery;
  delivery state changes never increment business `Version`.
- Cancellation is propagated. 503/504 provider failures never mutate the aggregate.
- No startup/hosted provisioning, direct repository bypass, hardcoded market, assignment auto-generation or fallback.

## 9. Layout & Shell Contract

- `shell: tenant`.
- Every Razor page explicitly uses `Layout = "_LayoutTenantShell";`.
- Route: `/MasterDataManagement/MarketSupplyAssignments`.
- Browser calls only same-origin MVC routes; MVC forwards authenticated requests through Gateway port 5000.
- Browser code never calls 5057/5059, emits bearer tokens, or manufactures a tenant identity.
- Page remains navigation-hidden. Direct authorized route access is sufficient for foundation acceptance.
- Golden Slim is selected because the create/edit surface has seven user fields.

## 10. Backend File Convention

Application files follow the exact Golden Reference layout:

```text
Features/MarketSupplyAssignments/
|-- Commands/
|-- Queries/
|-- Handlers/CommandHandlers/
|-- Handlers/QueryHandlers/
|-- Validators/
`-- MarketSupplyAssignmentModels.cs
```

Commands and queries are sealed records. Handlers end in `Handler` without `CommandHandler`/`QueryHandler` suffixes.
Validators end in `Validator`. Controllers remain thin MediatR adapters using `Response<T>` and
`CustomBaseController`. Application handlers own orchestration; repositories own atomic persistence and indexes.

## 11. Frontend File Contract

The Slim surface contains `Index`, `_Filter`, `_DataTable`, `_IndexL10n`, `_CreateEditOffcanvas`,
`_DetailsQuickView`, marker class, `index.js`, `index.l10n.js` and all seven locale resources. `_DataTable` carries
`data-dt-standard="v2"` and the standard skeleton loader. There are no separate Create/Edit/Details pages.

Create options are server-derived:

- active Legal Entities from the existing same-tenant lookup contract;
- verified markets from the MOD-0048 MDM resolver path;
- LSKUs filtered by selected market;
- Registered Presentations filtered by selected LSKU/market;
- Finished Goods filtered by the derived shared GSKU.

No option list has a hardcoded fallback. Save View uses the existing same-origin personalization client and does not
change permission behavior.

## 12. Validation Rules

| Input | Rule | Failure |
|---|---|---|
| `SupplyingLegalEntityId` | Non-empty; current-tenant active/non-deleted reference | non-leaking 404 |
| `MarketCode` | Exact uppercase alpha-2 and latest verified `market` resolution | 400 or provider 404/503/504 |
| `LskuId` | Same tenant, non-deleted, market exact match | non-leaking 404 / 409 mismatch |
| `RegisteredPresentationId` | Required approved-contract resolution; same tenant/LSKU/market | non-leaking 404 / 409 mismatch |
| `FinishedGoodId` | Same tenant, non-deleted; GSKU equals LSKU GSKU | non-leaking 404 / 409 mismatch |
| `ValidFromUtc` | Required UTC | 400 |
| `ValidToUtc` | Null or strictly greater than start; interval is `[from,to)` | 400 |
| `Idempotency-Key` | Required, trimmed, bounded; never blank | 400 |
| Relationship tuple | One durable tuple, including soft-deleted/Retired history | 409 |
| `expectedVersion` | Exact current positive version for update/delete | 409 |

Draft create may reference non-deleted Draft or IdentityApproved LSKU/Finished Good records. The later activation step
must require both identities approved, an active Registered Presentation/MA proof, referenceable Legal Entity and an
effective verified market. Foundation code cannot transition lifecycle out of Draft.

## 13. Failure Path to Verify

- Missing/foreign tenant context fails before repository mutation.
- Foreign-tenant Legal Entity, LSKU, Registered Presentation, Finished Good or assignment returns non-leaking 404.
- Inactive/deleted Legal Entity or non-referenceable Registered Presentation is rejected before mutation.
- Unknown/lowercase/hardcoded/stale market evidence is rejected; provider 503/504 maps exactly and leaves no record.
- LSKU market mismatch or LSKU/Finished Good GSKU mismatch returns 409.
- Empty fields, invalid interval and client-supplied forbidden fields return 400.
- Same command/fingerprint replays; key/fingerprint drift and relationship duplicate return 409.
- Stale expected version returns 409 and writes neither aggregate nor audit intent.
- Audit claim expiry/recovery honors claim generation/token fencing; stale worker cannot acknowledge.
- Soft-deleted records are not physically removed or silently reused.
- Generic PSS lookup, direct Mongo, client tenant header and fake provider paths cannot produce a valid assignment.

## 14. Authorization Convention

Proposed exact permission declarations under canonical ModuleCode `product-item-sku-master`:

- `mdm.market-supply-assignments.read`
- `mdm.market-supply-assignments.create`
- `mdm.market-supply-assignments.update`
- `mdm.market-supply-assignments.delete`

No `approve`, `activate`, `inactivate`, `retire`, `workflow`, `allocate` or navigation permission is declared by the
foundation. Recommended default-role behavior is Tenant Viewer: read only; Tenant Admin: read/create/update/delete for
Drafts only. This recommendation must be confirmed by the MOD-0018 owner before step E. Entitlement
missing/disabled/expired means no module-sourced grant. Manual/system/other-module grants remain source-scoped.

Authorization does not replace domain validation. Permission cannot bypass same-tenant references, lifecycle,
effective dates, idempotency, expected version or future maker-checker rules.

## 15. Gateway / API Routing Decision

Gateway exposure is required for the tenant frontend, but remains separately gated and integration-agent owned.
Step F adds exact explicit base and catch-all routes for `/api/market-supply-assignments`, including GET/POST/PUT/
DELETE/PATCH/OPTIONS as applicable, before the existing MDM catch-all. Downstream is MDM port 5059 in the current repo
runtime. Existing routes are preserved. The browser uses the same-origin MVC proxy; no service port is browser-visible.

## 16. Acceptance Criteria

- [ ] DCP-002 exact FU02 identity remains collision-free and registered once.
- [ ] DCP-005 Registered Presentation/MA owner and reference contract are approved before any code-start.
- [ ] One tenant-owned relationship aggregate stores Legal Entity, verified market, LSKU, Registered Presentation,
  Finished Good and one effective interval; it does not store `GskuId`.
- [ ] No field is added to existing LSKU or Finished Good and no direct LSKU-FG FK exists.
- [ ] Market selection comes only from verified MOD-0048 `market` resolution and is persisted with exact version proof.
- [ ] All references are same-tenant and fail-closed/non-leaking.
- [ ] Only Draft create/update/delete and read are possible; no lifecycle transition endpoint exists.
- [ ] Duplicate tuple, overlap-by-recreation, idempotency drift and stale version fail with 409.
- [ ] Audit intent is atomic with mutation; delivery replay/recovery and stale-claim fencing are proven.
- [ ] Exact four permissions are declared; entitlement/default-role behavior is owner-approved and regression-tested.
- [ ] Golden Slim seven-field page uses `_LayoutTenantShell`, DataTable v2, seven locales and same-origin MVC-to-Gateway.
- [ ] Navigation remains hidden and WorkCenter/MA/approval/Production behavior is absent.
- [ ] Real Mongo and live browser evidence exist before any completion claim.

## 17. Test Expectations

### Unit/domain

- Entity defaults, exact lifecycle enum and one-interval invariant.
- Deterministic length-prefixed relationship key and separator/case/whitespace collision resistance.
- Interval validation, forbidden client fields, shared-GSKU and market equality rules.
- Idempotent replay versus key/fingerprint/relationship conflict.
- Expected-version behavior and audit intent binding.

### Contract/API/authorization

- Exact request/response shape; no tenant, GSKU, RelationshipKey, lifecycle or provider-proof inputs.
- Exact routes and four permission attributes; generic/unentitled access denied.
- 400/401/403/404/409/503/504 mapping and cancellation propagation.
- Legal Entity and Registered Presentation non-leaking reference validation.
- Exact nav-hidden manifest page/action/permission equality and existing manifest regression.

### Real MongoDB localhost:27017

- First Draft create/read/list and one aggregate/intent cardinality.
- Same command/fingerprint replay without duplicate aggregate or intent.
- Same key/different fingerprint, same tuple/different key and stale version conflicts.
- Tenant A/B isolation for reads, writes, indexes and audit delivery.
- Unique `(TenantId, CreationCommandId)` and `(TenantId, RelationshipKey)` indexes.
- Soft delete retention/non-reuse; no physical cleanup.
- Audit checkpoint crash/recovery, lease expiry, claim generation/token fencing, acknowledgement and compaction.
- No mutation after market/regulatory/Legal Entity provider failure.

Fake/in-memory/skip evidence cannot close Mongo acceptance.

### Frontend/browser

- DataTable verifier, JS syntax, locale key parity, focused frontend tests and full frontend suite.
- Admin Draft CRUD/read according to the approved matrix; Viewer read-only.
- Two-tenant non-disclosure; direct URL and API checks.
- Provider-backed cascading options, exact selected values and no hardcoded fallback.
- Same-origin network only, no direct 5057/5059 request, no browser token/tenant fabrication, console error 0.
- Save View load/save/reset regression.

### Regression/build

- Existing Global Product, GSKU, LSKU, Finished Good, ABB, Legal Entity, verified market and audit-intent tests.
- Existing MOD-0048 GSKU/market resolver/provider tests read-only.
- MDM API Release build, frontend Release build, Gateway build when F changes, `git diff --check`, conflict marker,
  trailing whitespace and final newline.

## 18. Ready-for-dev Checklist

- [x] Master 8.1 MOD-0290 parent/SoR evidence recorded.
- [x] Registry collision checked; `MOD-0290-FU02` exact verifier exits 0.
- [x] Follow-up rather than standalone module decision recorded.
- [x] Aggregate owner recommendation and direct-FK prohibition recorded.
- [x] Exact relationship/entity/effective-date/audit/fencing proposal recorded.
- [x] Golden Slim seven-field decision and exact A-H planning allow-lists recorded.
- [ ] Product Data Owner accepts one durable tuple/one interval and `SupplyingLegalEntityId` semantics.
- [ ] Regulatory Information Owner accepts the opaque Registered Presentation boundary.
- [ ] DCP-005 step 5 Registered Presentation/MA pack is approved and its reference contract is code-truth.
- [ ] MOD-0018 owner approves the exact permission/default-role/entitlement model or creates a separate follow-up.
- [ ] Audit owner confirms the new aggregate discriminator and delivery-repository extension.
- [ ] Each A-H step receives separate exact code-start/operational authorization.
- [ ] Pack status is explicitly changed from `draft` to `approved` or `ready-for-dev` by authorized review.

Until every unchecked owner/dependency item required for the requested step is closed, implementation is blocked.

## 19. Implementation Notes

### A-H delivery sequence

| Step | Delivery | Entry gate | Exit evidence |
|---|---|---|---|
| A | Domain/ownership contract | DCP-005 step 5 + owner acceptance + code-start | Entity/enum/repository/interface unit proof |
| B | Mongo/index/audit fencing | A green + code-start | Real-Mongo uniqueness, concurrency and recovery |
| C | CQRS/reference orchestration | B green + regulatory resolver code-truth | Unit/contract/provider failure proof |
| D | API/nav-hidden manifest | C green + permission names accepted | API/auth/manifest focused tests |
| E | Permission onboarding | Separate MOD-0018 owner gate | Catalog/entitlement/default-role real-Mongo evidence |
| F | Gateway routes | Integration-agent code-start | Base/catch-all/build/route regression |
| G | Golden Slim frontend | D-F green + frontend code-start | Focused/full frontend, build and browser contract |
| H | Local Development acceptance | A-G green + separate operational approval | One Draft UI/API/Mongo read-back; no lifecycle mutation |

### WorkCenter-independent foundation

- Domain relationship, persistence, Draft CQRS, verified reference checks, read API, nav-hidden frontend and audit intent
  creation can operate without WorkCenter after the Registered Presentation contract exists.
- `Draft` has no supply authorization effect and is never treated as effective merely because its dates include now.

### WorkCenter-dependent follow-up

- Submit/approve/reject, maker-checker, task ownership, activation/inactivation/retirement, callback/poll recovery and
  workflow SLA belong to a later named step after MOD-0023 integration is approved.
- That step must revalidate Legal Entity, market, LSKU, Registered Presentation/MA and Finished Good at decision time,
  use trusted actor/S2S correlation/idempotency and persist authoritative lifecycle only in MDM.

### Current code-start blockers

1. DCP-005 Registered Presentation/MA owner and exact reference-validation contract are not yet approved/code-truth.
2. Product/regulatory owners have not accepted the exact tuple, single-interval rule or supplying-Legal-Entity meaning.
3. MOD-0018 owner has not approved Market Supply Assignment default-role/entitlement onboarding.
4. Audit owner has not approved addition of the new aggregate type to the shared delivery repository.
5. Pack remains `draft`; no A-H code-start or operational authorization exists.

The active worktree was already materially dirty before this planning task. Existing changes are user-owned and must
not be normalized, staged or overwritten. This preparation adds only this pack and its canonical registry row.

## 20. Follow-up Items

- Prepare/approve the DCP-005 Registered Presentation/MA owner pack and reference resolver before FU02 approval.
- Obtain the Product Data and Regulatory Information owner decisions listed in section 18.
- If generic catalog/entitlement behavior cannot express the approved matrix, prepare a separate DCP-002-proven
  MOD-0018 permission-onboarding follow-up; do not invent an ID in this pack.
- After foundation evidence, prepare a separate WorkCenter-dependent lifecycle/approval named step.
- Decide later whether multiple non-contiguous effective periods are required. Do not add history arrays or a second
  aggregate for the same relationship under this foundation.
- Navigation remains a separate explicit decision; this pack does not open the left menu.
- Production enablement, central audit transport operations, retention/redaction, metrics/runbook and release gates
  remain separate from Local Development foundation acceptance.
