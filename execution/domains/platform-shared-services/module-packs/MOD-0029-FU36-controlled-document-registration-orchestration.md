---
id: MOD-0029-FU36
name: Controlled Document Registration Orchestration
parent: MOD-0029
previous: MOD-0029-FU33
domain: platform-shared-services
service: Diten.Platform
shell: tenant
golden_reference: compact
entity_base: BaseEntity
status: ready-for-dev
owner: platform-shared-services
branch: feature/pss/mod-0029-fu36-controlled-document-registration-orchestration
started: 2026-07-24
target: 2026-08-07
form_field_count: 16
runtime_implementation: implemented-with-runtime-gaps
---

# MOD-0029-FU36 — Controlled Document Registration Orchestration

> Parent canonical module: **MOD-0029 — Controlled Documents (SOPs/Work Instructions)**.
>
> This pack was approved for implementation planning on 2026-07-24 and is now `status: ready-for-dev`.
> Backend orchestration, unified create frontend and FU36C reverse-navigation/legacy-bypass hardening are
> implemented. FU36 remains open until FU36D authenticated runtime smoke and reconciliation are completed.

## 1. Module Summary

MOD-0029-FU36 makes Document Master Register the governed entry point for creating a new controlled document,
while preserving Controlled Documents as the daily file explorer and version library.

The normal operator flow becomes:

```text
Document Master Register
  → New Controlled Document
  → governance metadata + company/folder + first file
  → one registration orchestration
  → Master Register Draft + ControlledDocument + first immutable version + link
  → success only when the complete relationship exists
```

The Controlled Documents page remains available for browse, preview, download, version history, new-version
upload, move, favorite and sharing. Its existing `Add Document` action redirects to the governed Master Register
create flow. Template creation remains on its dedicated template path.

Manual linking remains an exception path for legacy imports, migrations and reconciliation. It is not the normal
daily create flow.

Target users:

- Document Control operators;
- Quality/GxP document administrators;
- process owners authorized to originate controlled documents;
- auditors and support operators reviewing registration failures.

## 2. Ownership and Boundaries

### In scope

- One orchestrated create use case spanning existing `DocumentMasterRegisterEntry`, `ControlledDocument`,
  `ControlledDocumentVersion` and content storage abstractions.
- A durable, tenant-scoped registration-operation record for idempotency, compensation and support visibility.
- Master Register `New Controlled Document` Compact form.
- Controlled Documents `Add Document` redirect into the governed flow.
- Automatic link from the register entry to the created controlled document.
- Reverse read projection from a Controlled Document detail to its linked Master Register entry.
- Clear failure state; no successful UI response for a partially created relationship.
- Reconciliation/retry for an interrupted operation without duplicating register entries, documents or versions.
- Permission, audit, localization and tenant-isolation coverage.

### Out of scope

- Removing the Controlled Documents explorer.
- Template Master/Template Variant creation redesign.
- External Document Register creation.
- Approval-route, electronic-signature or release-gate auto-completion.
- Automatic transition beyond `Draft`.
- Automatic UID/document-code invention outside the existing FU07 allocation engine.
- Editing a binary file in the browser, OCR or full-text indexing.
- Public/anonymous file sharing.
- Cross-tenant document creation or linking.
- Hard deletion of governance or document metadata.
- Changing an existing register entry to point at a different controlled document.

### System-of-entry decision

- Governed controlled-document birth starts in Document Master Register.
- Daily file consumption remains in Controlled Documents.
- Controlled Documents remains the operational explorer/version library; its normal `Add Document` action
  redirects to the governed Master Register unified-create route.
- Template creation remains on its dedicated template route.
- Manual linking is restricted to explicitly authorized legacy, migration and reconciliation scenarios.
- The Master Register aggregate owns `ControlledDocumentId`.
- Reverse navigation is a read projection/query; `MasterRegisterEntryId` is not duplicated into
  `ControlledDocument` unless a later approved migration proves it necessary.

Approved decision:
Document Master Register is the governed system of entry for new controlled documents.
Controlled Documents remains the operational explorer/version library.

## 3. Owned Objects

### New object

`ControlledDocumentRegistrationOperation`:

- durable idempotency and orchestration record;
- tenant-scoped `BaseEntity`;
- append-safe state transitions;
- contains identifiers/references only, never raw file bytes;
- no hard delete.

### Existing objects consumed

- `DocumentMasterRegisterEntry`
- `ControlledDocument`
- `ControlledDocumentVersion`
- `CollectionInstance` through the existing read-only reader contract
- `IContentStorageGateway`
- existing Master Register identifier/lifecycle/release-gate services as boundaries only

### Application contracts

- `CreateControlledDocumentRegistrationCommand`
- `RetryControlledDocumentRegistrationCommand`
- `GetControlledDocumentRegistrationOperationQuery`
- `GetMasterRegisterByControlledDocumentQuery`
- `ControlledDocumentRegistrationService`
- `IControlledDocumentRegistrationRepository`
- request/response models in one feature models file

### API endpoints

```text
POST /api/v1/document-management/controlled-document-registrations
GET  /api/v1/document-management/controlled-document-registrations/{operationId}
POST /api/v1/document-management/controlled-document-registrations/{operationId}/retry
GET  /api/v1/document-management/controlled-documents/{controlledDocumentId}/master-register
```

No DELETE endpoint is allowed.

### Frontend routes

```text
GET  /DocumentManagementMasterRegister/CreateControlledDocument
POST /DocumentManagement/MasterRegister/api/controlled-document-registrations
GET  /DocumentManagement/MasterRegister/api/controlled-document-registrations/{operationId}
POST /DocumentManagement/MasterRegister/api/controlled-document-registrations/{operationId}/retry
GET  /DocumentManagementControlledDocuments/master-register/{controlledDocumentId}
```

### Permissions

Dedicated permissions approved by this pack:

```text
platform.document-management.master-register.registration.view
platform.document-management.master-register.registration.create
platform.document-management.master-register.registration.reconcile
```

Existing permissions remain authoritative for downstream resources:

```text
platform.document-management.master-register.view
platform.document-management.master-register.manage
platform.document-management.master-register.link
platform.document-management.controlled-documents.view
platform.document-management.controlled-documents.create
```

Backend authorization requires the dedicated registration permission and the relevant existing downstream
permissions. Frontend hiding/disabling is UX only.

The keys are added to the AuthService catalog during implementation. Existing full-catalog behavior grants them
to SuperAdmin. Tenant Admin and Viewer do not receive them automatically; tenant-role grants remain an explicit
deployment/operations action.

Approved decision:
Dedicated registration permission keys are approved as proposed.

## 4. Entity Fields

### `ControlledDocumentRegistrationOperation`

| Field | Type | Required | Rule |
|---|---|---:|---|
| `Id` | `Guid` | yes | Server generated |
| `TenantId` | `Guid` | yes | Server-side tenant context; never accepted from payload |
| `IdempotencyKey` | `string` | yes | Trimmed, max 128; unique per tenant |
| `Status` | enum | yes | `Pending`, `ContentStored`, `DocumentCreated`, `RegisterCreated`, `Linked`, `Completed`, `CompensationPending`, `Failed` |
| `ControlledDocumentId` | `Guid?` | no | Set once document metadata exists |
| `ControlledDocumentVersionId` | `Guid?` | no | Set once first immutable version exists |
| `MasterRegisterEntryId` | `Guid?` | no | Set once Draft register entry exists |
| `ContentRef` | `string?` | no | Provider reference only; no public URL/raw bytes |
| `ContentSha256` | `string?` | no | Lowercase hex, 64 chars |
| `FailureReasonCode` | `string?` | no | Controlled code, max 120 |
| `FailureDetail` | `string?` | no | Sanitized support detail, max 1000; no stack trace |
| `LastAttemptAt` | `DateTimeOffset?` | no | UTC |
| `AttemptCount` | `int` | yes | Starts at 1; bounded retry policy |
| `CorrelationId` | `string` | yes | Existing correlation contract |
| audit/soft-delete fields | inherited | yes | Existing `BaseEntity` contract |

### Indexes

- unique partial index: `(TenantId, IdempotencyKey)` where `IsDeleted == false`;
- index: `(TenantId, Status, UpdatedAt)`;
- unique partial index on non-null `(TenantId, ControlledDocumentId)`;
- unique partial index on non-null `(TenantId, MasterRegisterEntryId)`.

### Create-form fields counted for Golden Reference

1. Document Title
2. Document Class
3. Criticality
4. Document Type
5. Description
6. Tags
7. Governing Language
8. Owner Function
9. Owner Company
10. Process Owner Role
11. Process Owner User
12. Review Cycle Months
13. Retention Class
14. Company / Legal Entity
15. Collection Instance / Folder
16. Initial File

`Id`, `TenantId`, audit fields, status, UID/code and derived effective/review dates are not counted.

## 5. Repo Scope

Implementation may touch only:

- `services/Diten.Platform/src/Diten.Platform.API/Controllers/DocumentManagementControlledDocumentRegistrationController.cs`
- `services/Diten.Platform/src/Diten.Platform.API/Models/DocumentManagement/*Registration*`
- `services/Diten.Platform/src/Diten.Platform.Application/Features/DocumentManagementControlledDocumentRegistration/**`
- `services/Diten.Platform/src/Diten.Platform.Application/DependencyInjection.cs`
- `services/Diten.Platform/src/Diten.Platform.Domain/Entities/DocumentManagement/ControlledDocumentRegistrationOperation.cs`
- `services/Diten.Platform/src/Diten.Platform.Domain/Enums/DocumentManagement/ControlledDocumentRegistrationEnums.cs`
- `services/Diten.Platform/src/Diten.Platform.Domain/Repositories/IControlledDocumentRegistrationRepository.cs`
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Repositories/ControlledDocumentRegistrationRepository.cs`
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Configurations/MongoDbIndexConfigurations.cs`
- existing MOD-0029 ControlledDocument/MasterRegister application services only where a small reusable method or
  orchestration port is required; no aggregate rewrite
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/DocumentManagement/*Registration*`
- `services/Diten.AuthService/src/Diten.AuthService.Persistence/Seed/DataSeeder.cs` only for the three approved
  permission keys, with targeted tests
- `services/Diten.AuthService/tests/**/Authorization/*DocumentManagement*`
- `frontend/Diten.Web/Controllers/DocumentManagementMasterRegisterController.cs`
- `frontend/Diten.Web/Controllers/DocumentManagementControlledDocumentsController.cs`
- `frontend/Diten.Web/Views/DocumentManagement/MasterRegister/**`
- `frontend/Diten.Web/Views/DocumentManagement/ControlledDocuments/**`
- `frontend/Diten.Web/wwwroot/assets/js/DocumentManagement/MasterRegister/**`
- `frontend/Diten.Web/wwwroot/assets/js/DocumentManagement/ControlledDocuments/**`
- corresponding 7-language MasterRegister/ControlledDocuments RESX resources
- a new targeted verifier under `scripts/verify-mod0029-fu36-*.ps1`
- `execution/registries/module-implementation-status.md` during implementation lifecycle updates
- `docs/audits/mod-0029-fu36-*.md`

## 6. Protected Paths

- `.antigravity/**`
- `gateway/Diten.ApiGateway/**/ocelot.json` (integration-agent owned)
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml`
- `frontend/Diten.Web/Controllers/Archive/**`
- `frontend/Diten.Web/Views/Archive/**`
- other domains' service folders, including `services/Diten.CrmService/**`,
  `services/Diten.DevEnablementService/**`, `services/Diten.EnterpriseStrategyService/**`
- MOD-0028 baseline/definition/instantiation mutation paths
- external-document, retention, signature, quality-event and approval aggregates except read-only integration
- existing user data and document content outside explicit test fixtures

## 7. Dependencies

- MOD-0029-FU01 controlled-document/version/content-storage foundation.
- MOD-0029-FU04 folder/document authorization.
- MOD-0029-FU06 Master Register foundation.
- MOD-0029-FU07 identifier allocation boundary.
- MOD-0029-FU08 lifecycle policy.
- Existing Gateway catch-all `/api/v1/document-management/{everything}`.
- Existing `Response<T>`, `CustomBaseController`, MediatR and four pipeline behaviors.
- Existing TenantShell localization bridge and Golden Reference Compact Details/Create patterns.

## 8. Runtime Constraints

- MongoDB single logical database, tenant-scoped collections.
- `TenantId` is resolved only from authenticated server context.
- Cross-tenant document, folder, operation or register lookup returns non-leaking `404`.
- Browser calls only same-origin MVC proxy; server forwards through Gateway port 5000.
- Raw bytes are accepted only through the upload boundary, passed to `IContentStorageGateway`, and never persisted
  in MongoDB.
- No public content URL.
- No hard delete of register/document/version/operation metadata.
- Registration response returns success only when status is `Completed`.
- `Draft` is the only initial register/lifecycle state; no approval/effective/signature automation.
- Existing FU07 engine exclusively owns UID/document-code allocation. FU36 does not invoke allocation during
  registration. The new register entry remains Draft with identifiers unallocated until an authorized operator
  uses the governed FU25 Identifiers-tab flow.

### Consistency and compensation decision

The implementation must not assume MongoDB transactions are available. It uses a durable orchestration record and
idempotent step boundaries:

1. Validate tenant, permissions, folder access, metadata and upload before writes.
2. Reserve/create the registration operation by tenant-scoped idempotency key.
3. Store content through the existing storage gateway.
4. Create ControlledDocument and first immutable version.
5. Create Draft Master Register entry.
6. Link the register to the ControlledDocument.
7. Mark operation `Completed`.

If a later step fails:

- no success response is returned;
- operation becomes `CompensationPending` or `Failed`;
- retry resumes from recorded IDs rather than creating duplicates;
- unreferenced stored content is deleted through the storage abstraction when safe;
- content-storage cleanup and metadata compensation are distinct: stored content may be cleaned up through the
  abstraction where safe, while created document/register/version metadata is never hard-deleted;
- partial metadata is soft-archived, hidden from normal operational surfaces and kept reconciliation-visible
  only through a dedicated compensation policy;
- `FailureDetail` remains sanitized and contains neither stack traces nor sensitive content;
- a reconciliation query exposes unresolved operations to authorized support users.

The operation record remains support/audit evidence and never replaces the business document or audit events.
It has no hard-delete path and is retained for the document lifetime plus configured audit retention. Until a
formal operation-retention policy receives governance/legal approval, operation history is retained indefinitely.

Approved decision:
Compensation uses durable operation state, idempotent retry, storage cleanup where safe, and
soft-archive/reconciliation visibility for partial metadata. Hard delete is not allowed.

Approved decision:
Registration operation history is retained as support/audit evidence. No hard delete. Until a formal
operation-retention policy exists, retain indefinitely.

## 9. Layout & Shell Contract

- `shell: tenant`
- Every new Razor page explicitly uses `Layout = "_LayoutTenantShell"`.
- Unified create view lives under `Views/DocumentManagement/MasterRegister/`.
- Golden Reference Compact Create/Details visual hierarchy is mandatory.
- No create/edit offcanvas.
- Controlled Documents remains an explorer; its add action navigates to the unified create route.

## 10. Backend File Convention

The feature follows the existing Diten.Platform document-management slice plus Golden Compact naming:

```text
Features/DocumentManagementControlledDocumentRegistration/
├── Commands/
│   ├── CreateControlledDocumentRegistrationCommand.cs
│   └── RetryControlledDocumentRegistrationCommand.cs
├── Queries/
│   ├── GetControlledDocumentRegistrationOperationQuery.cs
│   └── GetMasterRegisterByControlledDocumentQuery.cs
├── Handlers/
│   ├── CommandHandlers/
│   │   ├── CreateControlledDocumentRegistrationHandler.cs
│   │   └── RetryControlledDocumentRegistrationHandler.cs
│   └── QueryHandlers/
│       ├── GetControlledDocumentRegistrationOperationHandler.cs
│       └── GetMasterRegisterByControlledDocumentHandler.cs
├── Validators/
│   ├── CreateControlledDocumentRegistrationValidator.cs
│   └── RetryControlledDocumentRegistrationValidator.cs
├── Services/
│   └── ControlledDocumentRegistrationService.cs
└── DocumentManagementControlledDocumentRegistrationModels.cs
```

No `*CommandHandler` filename suffix and no controller business logic.

## 11. Frontend File Contract

Master Register additions:

```text
Views/DocumentManagement/MasterRegister/
├── CreateControlledDocument.cshtml
├── _ControlledDocumentForm.cshtml
└── existing Index/Details/L10n files

wwwroot/assets/js/DocumentManagement/MasterRegister/
├── controlled-document-create.js
└── existing index/details scripts
```

Controlled Documents changes:

- Existing `Add Document` action navigates to
  `/DocumentManagementMasterRegister/CreateControlledDocument`.
- Template action remains `/DocumentManagementControlledDocuments/Create?kind=template`.
- Existing direct document-create view may remain only as a legacy/migration path and must not be reachable from
  normal navigation; it must be explicitly permission-gated. It may alternatively be removed from normal
  navigation or constrained to non-controlled use.
- Normal direct creation of `Controlled=true` documents outside registration orchestration is blocked.
- Controlled Document Details shows a read-only Master Register card and an authorized `Open Master Register`
  action.

Approved decision:
Normal direct creation of controlled documents outside the registration orchestration is blocked.
Template creation and explicitly authorized legacy/migration paths remain separate.

All visible strings use the existing 7-language RESX/L10n bridge. No hardcoded UI messages.

## 12. Validation Rules

| Field | Required | Format/Rule | DB-level | Pre-check |
|---|---:|---|---|---|
| Idempotency Key | yes | max 128 | tenant unique | existing operation lookup |
| Document Title | yes | trim, max 512 | — | — |
| Document Class | yes | valid enum/reference value | — | policy validation |
| Criticality | yes | valid enum/reference value | — | policy validation |
| Document Type | yes | supported controlled type | — | type mapping |
| Description | no | max 4000 | — | — |
| Tags | no | normalized distinct list; bounded count/length | — | — |
| Governing Language | yes | published reference value | — | reference-data lookup |
| Owner Company | yes | non-empty GUID | — | tenant-scoped legal entity |
| Process Owner Role | conditional | required by document class/policy | — | route resolution |
| Process Owner User | no | non-empty GUID if supplied | — | tenant user lookup |
| Review Cycle Months | conditional | 1–120 when required | — | class/criticality policy |
| Retention Class | conditional | published reference value | — | reference-data lookup |
| Company | yes | tenant-scoped legal entity | — | resource existence |
| Folder | yes | active CollectionInstance for selected company | — | Layer 1 AND Layer 2 upload access |
| Initial File | yes | non-empty, allowed extension/MIME/size | storage key unique | content validation + SHA-256 |

`EffectiveDate`, lifecycle status, register status, UID and code are not client-controlled fields in this create
flow.

Approved decision:
UID/document-code allocation is deferred to the governed Identifiers tab. FU36 must not auto-allocate UID/code
during registration.

## 13. Failure Path to Verify

- **Duplicate submission**
  - Same tenant + idempotency key returns the existing completed result or resumes the existing incomplete
    operation; no duplicate register/document/version.
- **Unauthorized folder**
  - `403`; no content write and no metadata row.
- **Cross-tenant company/folder/user**
  - non-leaking `404`; no write.
- **Content storage failure**
  - controlled failure response; operation records sanitized reason; no document/register metadata.
- **ControlledDocument creation failure after storage**
  - storage cleanup attempted; operation remains reconcilable; no success response.
- **Master Register creation failure after document creation**
  - document/version is not presented as a successfully registered controlled document; operation is
    compensation-pending and retry-safe.
- **Link failure**
  - retry reuses both existing IDs and only repeats the link step.
- **Stale retry/concurrency**
  - optimistic/idempotency guard prevents two workers from advancing the same operation incompatibly; `409`.
- **Unsupported type/template**
  - validation failure directs templates to their dedicated flow; no partial write.
- **Existing relationship conflict**
  - `409 ALREADY_LINKED`; no relationship overwrite.

## 14. Authorization Convention

Policy: `[Authorize]` with tenant actor context.

Permission enforcement:

- list/view operation: `master-register.registration.view`;
- create: `master-register.registration.create` AND existing Master Register manage/link AND Controlled Documents
  create permissions;
- retry/reconcile: `master-register.registration.reconcile`;
- open linked document: existing Controlled Documents view plus resource-level access;
- open reverse Master Register link: existing Master Register view.

The three new keys must be:

- added to AuthService catalog;
- granted to SuperAdmin by existing full-catalog behavior;
- not automatically granted to tenant Admin/Viewer;
- covered by exact-literal seed tests;
- explicitly granted to authorized tenant roles by deployment/operations.

## 15. Gateway / API Routing Decision

Gateway configuration change is **not required**.

- Existing `/api/v1/document-management/{everything}` route covers the new endpoints.
- Frontend uses Gateway 5000 through same-origin MVC proxies.
- Direct 5057 browser calls are prohibited.
- `gateway/Diten.ApiGateway/**/ocelot.json` remains protected.
- Route coverage test must confirm the new endpoint family resolves through the existing catch-all and produces
  authorization response rather than `404`.

## 16. Acceptance Criteria

- [ ] Master Register Index exposes an authorized `New Controlled Document` action.
- [ ] Unified Compact form contains the 16 declared user fields and explicitly uses `_LayoutTenantShell`.
- [ ] Successful submit creates exactly one Draft Master Register entry, one ControlledDocument, one first
  immutable version and one completed registration operation.
- [ ] The created register entry contains the new ControlledDocument ID before success is returned.
- [ ] Controlled Document Details resolves and displays the linked Master Register entry.
- [ ] Master Register Details opens the actual document and version history.
- [ ] Controlled Documents `Add Document` navigates to the unified Master Register create route.
- [ ] Template creation remains on the existing template flow.
- [ ] Direct normal creation of a `Controlled=true` document cannot bypass registration orchestration.
- [ ] Manual link remains permission-gated for legacy/reconciliation use.
- [ ] Duplicate submit/retry does not create duplicate content, document, version or register entry.
- [ ] Partial failure returns no success and creates an inspectable/retryable operation state.
- [ ] Cross-tenant references return non-leaking `404`.
- [ ] No request DTO contains `TenantId`.
- [ ] No hard delete or public content URL is introduced.
- [ ] Lifecycle/Register defaults remain `Draft`; no automatic Effective/approval/signature action occurs.
- [ ] All new UI text has identical key parity across `ar,en,es,fr,ru,tr,zh`.
- [ ] All browser calls remain same-origin; direct `5057` and client `X-Tenant-Id` are absent.
- [ ] Golden Reference Compact structure verifier passes for the unified form/details surfaces.

## 17. Test Expectations

### Unit/application tests

- orchestration happy path and exact created IDs;
- each step failure and compensation state;
- idempotent replay at every status;
- concurrent duplicate submission;
- retry reuses existing IDs;
- tenant isolation and cross-tenant `404`;
- permission denial before write;
- unsupported type/template rejection;
- no lifecycle auto-transition;
- reverse Master Register lookup.

### Infrastructure/integration tests

- unique idempotency and relationship indexes;
- repository tenant filters;
- content cleanup adapter behavior;
- no raw bytes stored in Mongo;
- role permission seed exact literals.

### Frontend/static verification

- Compact view/partial contract;
- Controlled Documents redirect and template-route preservation;
- same-origin proxy and anti-forgery;
- no direct port/TenantId/header leakage;
- permission-gated actions;
- 7-language RESX parity;
- linked/retry/failure states render safely without raw stack traces.

### Commands

```text
dotnet build services/Diten.Platform/src/Diten.Platform.API/Diten.Platform.API.csproj -c Debug
dotnet test services/Diten.Platform/tests/Diten.Platform.Application.Tests
dotnet build services/Diten.AuthService/src/Diten.AuthService.Api/Diten.AuthService.Api.csproj -c Debug
dotnet test services/Diten.AuthService
dotnet build frontend/Diten.Web/Diten.Web.csproj -c Debug
dotnet build gateway/Diten.ApiGateway/Diten.ApiGateway.csproj -c Debug
python3 .antigravity/scripts/verify_datatable_page.py . --area DocumentManagement --module MasterRegister --reference compact
powershell -File scripts/verify-mod0029-fu36-controlled-document-registration.ps1
git diff --check
```

Browser smoke requires an authenticated tenant user with all declared permissions and a writable active folder.

## 18. Ready-for-dev Checklist

- [x] DCP-002 parent/child preflight passed:
  `py .antigravity/scripts/verify_module_id.py . --check-id MOD-0029-FU36 --name "Controlled Document Registration Orchestration" --parent MOD-0029`.
- [x] FU34 and FU35 prior candidate reservations were inspected; FU36 selected to avoid semantic collision.
- [x] `golden_reference: compact` selected from 16 form fields.
- [x] Tenant shell and `BaseEntity` decisions recorded.
- [x] User reviewed and approved the system-of-entry decision on 2026-07-24.
- [x] User approved the three dedicated permission keys on 2026-07-24.
- [x] User approved the direct Controlled Document create bypass rule on 2026-07-24.
- [x] Compensation/archive policy was approved on 2026-07-24.
- [x] UID/document-code allocation decision selected: `deferred to Identifiers tab`.
- [x] Registration operation retention approved: document lifetime plus configured audit retention; retain
  indefinitely until an approved formal policy exists.
- [x] Module pack status changed to `ready-for-dev` on 2026-07-24.

### Approval Decision Log — 2026-07-24

1. **System of entry:** Document Master Register is the governed system of entry for new controlled documents;
   Controlled Documents remains the operational explorer/version library.
2. **Permissions:** The three dedicated registration permission keys are approved as proposed.
3. **Direct-create bypass:** Normal direct creation of controlled documents outside the orchestration is blocked;
   template creation and explicitly authorized legacy/migration paths remain separate.
4. **Compensation:** Durable operation state, idempotent retry, safe storage cleanup and
   soft-archive/reconciliation visibility are approved; metadata hard delete is prohibited.
5. **Identifiers:** UID/document-code allocation is deferred to the governed Identifiers tab and is not run by
   FU36 registration.
6. **Retention:** Registration-operation history is support/audit evidence with no hard delete; retain
   indefinitely until a formally approved operation-retention policy exists.
7. **Status:** The pack is approved at `ready-for-dev`; runtime implementation remains `not-started`.

## 19. Implementation Notes

- This is orchestration over existing aggregates, not a merger of Master Register and ControlledDocument.
- Existing services are consumed through application-level methods/ports; handlers/controllers are not invoked
  from other handlers/controllers.
- The normal create UI does not expose lifecycle/effective/approval fields.
- The first version remains immutable under FU01 rules.
- The operation state is support evidence, not a business document and not a replacement for audit events.
- Manual linking remains visible only to explicitly authorized legacy/reconciliation operators.
- Existing records are not bulk migrated by this pack.
- Working tree contains other active streams; implementation must preserve unrelated changes and stage by hunk.

## 20. Follow-up Items

1. Bulk legacy reconciliation/matching assistant.
2. Optional document-code/UID allocation immediately after registration, if deferred in FU36.
3. Operator dashboard for failed/compensation-pending registrations beyond the minimum detail view.
4. Scheduled reconciliation sweep after safe tenant-enumeration policy is approved.
5. Template Master/Variant unified registration policy.
6. External Document Register unified intake.
7. Content malware scanning and provider-level quarantine.
8. Event-driven notification after successful registration.
