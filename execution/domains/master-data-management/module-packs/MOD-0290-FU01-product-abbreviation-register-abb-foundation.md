---
id: MOD-0290-FU01
name: Product Abbreviation Register (ABB) Foundation
domain: master-data-management
service: Diten.MdmService
shell: tenant
golden_reference: slim
entity_base: EntityBase
status: in-progress
owner: product-data-owner / head-of-r-and-d / master-data-steward / quality-owner
branch: feature/mdm/mod-0290-fu01-product-abbreviation-register
started: "2026-08-04T13:14:25Z"
target: "2026-08-04"
form_field_count: 2
parent_module: MOD-0290
parent_dcp: execution/portfolio/delivery-capability-packs/DCP-005-material-product-master-data-coding-alignment.md
canonical_blueprint: docs/System Capability & Implementation Blueprint - master 8.1.xlsx
---

# MOD-0290-FU01 - Product Abbreviation Register (ABB) Foundation

> **Code-truth guard (2026-08-09):** The internal backend foundation plus the named-step MDM API/controller, Gateway and
> tenant frontend implementation are present. This reconciliation authorizes no runtime change and does not claim
> permission catalog/grant onboarding and Local Development ABB lifecycle smoke are now live-proven. Navigation and
> Production readiness remain open; the internal lifecycle, no-reuse and SoD contract is unchanged.
>
> **Parent guard:** This follow-up is a child of Master 8.1 `MOD-0290 - Product / Item / SKU Master`. It does not
> expand or modify the current in-progress MOD-0290 first-GSKU foundation.

## 1. Module Summary

This follow-up defines the approved internal-backend foundation contract for the Product Abbreviation Register
(ABB). One GMG group boundary is one ERP tenant, and a tenant may contain multiple Legal Entities. Within that tenant,
ABB is a normalized, exactly three-character identifier that is unique tenant-wide and never reusable after durable
allocation.

After successful approved allocation, each Global Product has exactly one active ABB; before allocation it may have
none, and it can never have more than one. A corrected or replaced former ABB is retained as immutable append-only
history evidence and is never another active ABB. An ABB assigned to one Global Product cannot be active for
another Global Product.

ABB is not `CanonicalCode`, the internal UUID, `RevisionIdentifier`, or a legacy alias. MOD-0290 `CanonicalCode`
remains the low-semantic, immutable, tenant-scoped technical/business identifier for the current Global Product and
first-GSKU foundation. This pack neither creates a separate group-wide registry nor brings Material, FPF, FPP or
artwork controlled-code issuance into scope.

The internal foundation and the bounded API/Gateway/tenant frontend exposure are implemented. Section 20's named step records the
`ABB Exposure & Register UI` contract and remains the authority for its bounded command/query behavior. Manifest,
catalog/grant onboarding and Local Development live lifecycle smoke are complete. Navigation and Production readiness
remain open. It is not a new module, FU or Delivery Capability Pack.

## 2. Ownership and Boundaries

| Concern | Authority / owner | Boundary |
|---|---|---|
| Business policy | DCP-005 plus GMG-SCM-SOP-0001 management policy input | Tenant-wide, three-character, controlled and no-reuse ABB is locked. |
| Product definition | Product Data Owner | One active ABB per Global Product; former values are immutable, non-active history/correction evidence. Resolvable legacy aliases are excluded. |
| Allocation and stewardship | Head of R&D / Master Data Steward | Exactly three ASCII `A-Z` characters after culture-invariant trim and uppercase normalization. |
| Independent approval | Quality Owner | Maker-checker/SoD roles and evidence are locked by this pack. |
| Technical delivery | MDM Domain Team | Internal foundation, API/Gateway/frontend exposure, operational permission onboarding and Local Development live smoke are present; navigation and Production gates remain open. |
| Identity authority | Master 8.1 / DCP-002 | `MOD-0290-FU01` is a follow-up child of `MOD-0290`; no new root MOD identity is introduced. |

Legal Entity is not an ABB ownership or uniqueness boundary. `LegalEntityId` must not be carried by the ABB record and
must not participate in ABB uniqueness. Cross-tenant lookup or mutation must fail closed using the repository tenant
contract, including the established not-found/404 behavior at an eventual HTTP boundary.

## 3. Owned Objects

The internal foundation plans these explicit owned objects:

- `ProductAbbreviationRegisterEntry`: tenant-owned ABB aggregate that binds one normalized ABB to one Global Product
  and represents whether that binding is active.
- `ProductAbbreviationAllocationLedger`: tenant-owned, append-only allocation/no-reuse aggregate and idempotency
  evidence. It is the sole namespace authority for every durably allocated ABB, including terminal tombstones.
- `ProductAbbreviationHistoryEntry`: tenant-owned immutable append-only request, decision, correction, retirement and
  recovery evidence. It is not a resolvable lookup alias and never substitutes for an active ABB, `CanonicalCode` or
  `RevisionIdentifier`.
- `IProductAbbreviationRegisterRepository`, `IProductAbbreviationAllocationLedgerRepository` and
  `IProductAbbreviationHistoryRepository`.
- Internal commands: `RequestProductAbbreviationAllocationCommand`, `CancelProductAbbreviationAllocationCommand`,
  `ApproveProductAbbreviationAllocationCommand`, `RejectProductAbbreviationAllocationCommand`,
  `InitiateProductAbbreviationCorrectionCommand`, `RequestProductAbbreviationRetirementCommand`,
  `ApproveProductAbbreviationRetirementCommand` and `RejectProductAbbreviationRetirementCommand`.
- Internal queries: `GetProductAbbreviationByGlobalProductQuery`, `ResolveProductAbbreviationQuery` and
  `GetProductAbbreviationAllocationEvidenceQuery`. Resolution is limited to the currently active normalized ABB;
  former values are audit/history evidence only.
- DTOs in `ProductAbbreviationRegisterModels.cs`: `ProductAbbreviationRegisterEntryDto`,
  `ProductAbbreviationAllocationResultDto`, `ProductAbbreviationResolutionDto` and
  `ProductAbbreviationAllocationEvidenceDto`.

The existing general `CodeReservation` aggregate/ledger is not reused or modified by this delivery. ABB receives its
own aggregate, ledger, repositories and Mongo indexes.

## 4. Entity Fields

This is a contract-level field boundary, not an implementation schema.

| Field / concept | Requirement | State |
|---|---|---|
| `Id` | Immutable technical identifier, distinct from ABB. | Locked |
| `TenantId` | Required tenant partition supplied from trusted server context; absent from client write DTOs. | Locked |
| `Abbreviation` | Raw command value; culture-invariant trim + uppercase is applied before validation and persistence. | Locked |
| `NormalizedAbbreviation` | Exactly three uppercase ASCII letters matching `^[A-Z]{3}$`; tenant-wide unique in the allocation ledger and never reusable after durable allocation. | Locked |
| `GlobalProductId` | Same-tenant MOD-0290 Global Product reference; one active ABB per Global Product. | Locked |
| Lifecycle status | Exactly `REQUESTED`, `ACTIVE`, `REJECTED`, `CANCELLED`, `RETIRED`; `CORRECTED` and reactivation are prohibited. | Locked |
| `IdempotencyKey` | Stable command key; unique tenant-wide for allocation requests and immutable after persistence. | Locked |
| History | Separate immutable append-only `ProductAbbreviationHistoryEntry` evidence; former/corrected values are never active records or resolvable aliases. | Locked |
| Concurrency token | `Version` is required on every mutable aggregate and every mutation uses an expected-version compare-and-swap filter. | Locked |
| Audit metadata | Actor, timestamp, action, reason/correlation and before/after evidence where applicable. | Locked |
| `LegalEntityId` | Prohibited on the ABB record and prohibited from uniqueness partitioning. | Locked |
| `CanonicalCode` / `RevisionIdentifier` | Prohibited as storage substitutes for ABB. | Locked |

## 5. Repo Scope

This authoring revision is limited to this Module Pack. The completed internal-backend implementation allow-list is:

- `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/ProductAbbreviationRegisterEntry.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/ProductAbbreviationAllocationLedger.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/ProductAbbreviationHistoryEntry.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Enums/ProductAbbreviationLifecycleStatus.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Enums/ProductAbbreviationAllocationState.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Enums/ProductAbbreviationHistoryEventType.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/IProductAbbreviationRegisterRepository.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/IProductAbbreviationAllocationLedgerRepository.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/IProductAbbreviationHistoryRepository.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Contracts/IProductAbbreviationActorContext.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductAbbreviationRegister/**`
- `services/Diten.MdmService/src/Diten.MdmService.Application/DependencyInjection.cs` only for internal handler,
  validator and service registration.
- `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/ProductAbbreviationRegisterRepository.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/ProductAbbreviationAllocationLedgerRepository.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/ProductAbbreviationHistoryRepository.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Persistence/DependencyInjection.cs` only for ABB repository and
  Mongo registration.
- `services/Diten.MdmService/src/Diten.MdmService.Infrastructure/Security/ProductAbbreviationActorContext.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Infrastructure/DependencyInjection.cs` only for the internal actor
  context registration; no transport or hosted worker registration.
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ProductAbbreviationRegisterUnitTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ProductAbbreviationRegisterMongoTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ProductAbbreviationRegisterAuthorizationTests.cs`
- Existing MOD-0290 regression test projects may be executed but not modified by this pack.

The original foundation code-start authorized only this internal allow-list. The Section 20 named step records API/controller,
frontend and separately owned Gateway exposure but authorizes none of those paths in this documentation revision.
Workflow, hosted worker, external transport and production enablement remain outside the named step.

## 6. Protected Paths

- The existing `MOD-0290-product-item-sku-master.md`, its Domain Contract and DCP-004.
- DCP-005, product backlog, Master 8.1 and every registry row.
- Existing MOD-0290 `GlobalProduct`, `CodeReservation`, their repositories, handlers, indexes, audit-intent runtime and
  first-GSKU runtime/tests. This pack grants no authority to add an ABB field to `GlobalProduct`, change
  `CanonicalCode`, or adapt the common `CodeReservation` ledger.
- `services/Diten.MdmService/src/Diten.MdmService.Api/**`, including controllers, manifests and permission seed/catalog.
- `frontend/**`, `gateway/**`, workflow definitions/runtimes, hosted workers, external transports and production
  configuration/enablement.
- Material, FPF, FPP and artwork controlled-code namespaces and issuance.
- Other services/domains, frontend, gateway, `.antigravity/**` and the SOP source document.
- Branch, stage, commit, push, reset, stash, restore and deletion operations.

## 7. Dependencies

- Master 8.1 `Blueprint_Data!A291:AG291`: canonical parent `MOD-0290 - Product / Item / SKU Master`.
- DCP-002 identity gate and the canonical module registry.
- DCP-005 locked ABB namespace decision: one GMG group equals one tenant; ABB is tenant-wide and no separate group-wide
  registry is selected.
- The current MOD-0290 Global Product identity boundary, without changing its `CanonicalCode` semantics.
- An approved actor/audit contract and trustworthy server-side tenant context for any future implementation.

The current tenant-scoped `CodeReservation` counter/index and Global Product `CanonicalCode` index are read-only
infrastructure references. They do not prove ABB grammar, ABB no-reuse, ABB ownership or maker-checker policy and are
not implementation extension points for this delivery.

## 8. Runtime Constraints

- The internal ABB runtime foundation exists and is the immutable dependency of the Section 20 named step. No exposure/UI/Gateway or
  permission-provider runtime is authorized by this documentation revision.
- Normalization is culture-invariant trim followed by culture-invariant uppercase; grammar validation runs on the
  normalized result and accepts only `^[A-Z]{3}$`.
- ABB uniqueness is evaluated on `NormalizedAbbreviation` within `TenantId`; Legal Entity differences are irrelevant.
- `ProductAbbreviationAllocationLedger` has a non-partial unique index on
  `TenantId + NormalizedAbbreviation`; terminal allocation/tombstone rows remain in that index and are not soft-deleted.
- The register has a partial unique index permitting at most one `ACTIVE` `TenantId + GlobalProductId` binding. History
  rows cannot be promoted around the allocation ledger.
- Lifecycle transitions are closed: `REQUESTED -> ACTIVE`, `REQUESTED -> REJECTED`,
  `REQUESTED -> CANCELLED`, and `ACTIVE -> RETIRED`. No other transition, `CORRECTED` state or reactivation is allowed.
- Allocation is ledger-first. A durable ledger write consumes the normalized ABB permanently. Validation,
  authentication or authorization failure before the first repository write consumes nothing; after durable allocation,
  reject, cancel, correction, retirement or soft delete cannot release it.
- Correction is not a lifecycle state. `InitiateProductAbbreviationCorrectionCommand` creates an audited replacement
  `REQUESTED` entry and consumes its new ABB. The old entry remains `ACTIVE` while approval is pending. Approval makes
  the replacement `ACTIVE` and the old entry `RETIRED`; rejection or cancellation leaves the old entry `ACTIVE`, while
  the replacement ABB remains unavailable in the ledger. History is immutable and append-only.
- Retirement is maker-checker controlled. A retirement request records immutable pending decision evidence while the
  entry remains `ACTIVE`; approval transitions it to `RETIRED`, while rejection leaves it `ACTIVE`. Direct retirement
  mutation is prohibited.
- The ledger has a unique `TenantId + IdempotencyKey` index. Replay with the same key returns/reconciles the original
  durable outcome and cannot allocate another ABB; payload drift fails closed.
- Allocation and correction multi-write flows make no Mongo transaction assumption. They must be idempotent and
  reconciliation-safe: ambiguous or partial writes retain the original ledger outcome and either reconcile the same
  ledger/register/history set or fail closed for manual audit recovery.
- Every mutable repository operation uses a custom filter containing `TenantId + Id + expected Version`. ABB
  repositories must not use generic `RepositoryBase.UpdateAsync`, whose reference pattern does not enforce expected
  version in the write filter.
- Tenant identity and actor data come only from trusted execution context. The minimum context is `TenantId`,
  `CanonicalHumanSubjectId`, `ActorType`, granted permission keys, and correlation/idempotency identifiers.
- Only a directly authenticated `tenant_user` canonical human subject may mutate ABB. `service`, delegated, workflow,
  `platform_admin` and unknown actor types fail closed. Platform-admin authorization bypass must never bypass ABB SoD.
- Authentication, actor-type, permission, ownership and maker-checker authorization run before any repository write.
- Soft delete hides records but never releases an ABB. Market rebrand preserves ABB. Resolvable legacy-alias lookup is
  outside this slice; active resolution accepts only the currently active normalized ABB.
- Each state-changing internal command records tenant, canonical actor, actor type, permission decision,
  correlation/idempotency key, before/after state, reason and timestamp in immutable local ABB evidence. External audit
  transport and hosted delivery are excluded.
- No existing counter, unique index or reservation collection may be claimed as ABB evidence; the separate ABB ledger
  and ABB-specific real-Mongo tests are mandatory.
- SOP Material, FPF, FPP and artwork codes must not be placed into `CanonicalCode` or `RevisionIdentifier` by this pack.

## 9. Layout & Shell Contract

The original foundation was backend-only. The implemented Section 20 named step changes the pack-level presentation
metadata to `shell: tenant`, `golden_reference: slim`, `form_field_count: 2` without authorizing implementation.

- Every planned Razor page explicitly sets `Layout = "_LayoutTenantShell"`; `_ViewStart.cshtml` is unchanged.
- Planned view folder: `frontend/Diten.Web/Views/MDM/ProductAbbreviationRegister/` (not ASP.NET `Areas/`).
- Planned browser route: `/MDM/ProductAbbreviationRegister`.
- The verified request-allocation fields are `Global Product` and `ABB`. `Reason` is not counted because the existing
  `RequestProductAbbreviationAllocationCommand` has no reason member; silently accepting or discarding a reason would
  violate the existing-command-only boundary. Action reason modals and audit/details fields do not count.
- Two fields are within the `8 and under` rule, so the live `GoldenReferenceSlim` structure is mandatory.

## 10. Backend File Convention

The completed internal foundation uses the MDM five-layer/CQRS convention and the exact allow-list in Section 5.
Application naming is:

```text
Application/Features/ProductAbbreviationRegister/
|-- Commands/
|   |-- RequestProductAbbreviationAllocationCommand.cs
|   |-- CancelProductAbbreviationAllocationCommand.cs
|   |-- ApproveProductAbbreviationAllocationCommand.cs
|   |-- RejectProductAbbreviationAllocationCommand.cs
|   |-- InitiateProductAbbreviationCorrectionCommand.cs
|   |-- RequestProductAbbreviationRetirementCommand.cs
|   |-- ApproveProductAbbreviationRetirementCommand.cs
|   `-- RejectProductAbbreviationRetirementCommand.cs
|-- Queries/
|   |-- GetProductAbbreviationByGlobalProductQuery.cs
|   |-- ResolveProductAbbreviationQuery.cs
|   `-- GetProductAbbreviationAllocationEvidenceQuery.cs
|-- Handlers/
|   |-- CommandHandlers/ (`Request...Handler`, `Cancel...Handler`, `Approve...Handler`, `Reject...Handler`,
|   |   `Initiate...CorrectionHandler`, `Request...RetirementHandler`, `Approve...RetirementHandler`,
|   |   `Reject...RetirementHandler`; no `CommandHandler` suffix)
|   `-- QueryHandlers/ (`Get...Handler`; no `QueryHandler` suffix)
|-- Validators/ (one validator per command above; validator names omit the `Command` suffix)
|-- Services/ProductAbbreviationNormalizer.cs
`-- ProductAbbreviationRegisterModels.cs
```

Handlers use the separate ABB repositories and do not write through `ICodeReservationRepository` or mutate
`IGlobalProductRepository`. Queries are internal MediatR contracts only; this pack defines no controller or transport
DTO. Custom ABB repositories perform every mutation with `TenantId + Id + expected Version`; generic
`RepositoryBase.UpdateAsync` is prohibited. Duplicate-key and stale-version mapping returns stable domain failures
rather than leaking Mongo exceptions.

## 11. Frontend File Contract

No frontend file was authorized by that historical preparation. The Section 20 named step defines the exact Slim file set and interaction
contract. The request surface is request-only even though it retains the Golden Slim
`_CreateEditOffcanvas.cshtml` filename for verifier parity; no generic edit/update operation is introduced. A UI must
never accept writable `TenantId` or `LegalEntityId`, and must never present a former non-active ABB as canonical active.

## 12. Validation Rules

| Rule | Required outcome |
|---|---|
| Normalization | Apply culture-invariant trim, then culture-invariant uppercase; never locale-sensitive casing. |
| ABB grammar | After normalization, match exactly `^[A-Z]{3}$`; lowercase input may normalize, but digits, symbols, spaces inside the value, Turkish/diacritic or any non-ASCII letter fail. |
| Invalid-input consumption | If validation, authentication or authorization fails before persistence, create no register, allocation-ledger, tombstone, history or audit-evidence record. |
| Tenant uniqueness | The normalized ABB value cannot be duplicated anywhere in the same tenant, including across Legal Entities. |
| Cross-tenant isolation | The same value in another tenant is a separate partition; access across tenants fails closed. |
| Active product cardinality | After successful approved allocation a Global Product has exactly one active ABB; before allocation it may have none and can never have more than one. A former/corrected ABB is immutable non-active history evidence. |
| Active ABB cardinality | One ABB cannot be active for another Global Product; durable allocation also prevents later transfer or reuse. |
| No reuse | Before persistence, invalid input consumes nothing. After durable allocation, the ABB remains unavailable after reject, cancel, retire, correction or soft delete. |
| Rebrand | Market/product rebranding alone preserves the ABB. |
| Identifier separation | ABB is never copied into or derived as `CanonicalCode`, internal UUID, `RevisionIdentifier` or legacy alias. |
| Lifecycle | Only `REQUESTED`, `ACTIVE`, `REJECTED`, `CANCELLED`, `RETIRED` exist. Allowed direct transitions are `REQUESTED -> ACTIVE/REJECTED/CANCELLED` and `ACTIVE -> RETIRED`; no reactivation. |
| Actor trust | Tenant and actor context are server-derived. A direct authenticated `tenant_user` subject must be a valid GUID; `NameIdentifier` and `sub`, when both present, must identify the same GUID and are canonicalized to standard GUID format. Invalid, missing or mismatched subjects and service, delegated, workflow, platform-admin and unknown actors fail closed. |
| Maker-checker | The same `CanonicalHumanSubjectId` cannot make and approve the same allocation, correction or retirement; permission aliases, actor aliases and platform-admin bypass cannot defeat this rule. |
| Own cancellation | Requester cancellation is allowed only while its own request is `REQUESTED`, using exact canonical-subject comparison after the existing `cancel` permission check. Steward managed cancellation is prohibited in this internal slice and deferred to a later workflow/authorization follow-up. No `cancel-own` or `cancel-managed` permission key is added. |
| Correction | Correction initiates a replacement request. The old ABB remains `ACTIVE` until approval; approval activates the replacement and retires the old, while reject/cancel preserves the old active value and never releases the replacement ABB. |
| Retirement | Retirement requires separate maker request and checker approval. Request/reject leaves the entry `ACTIVE`; approval alone produces `RETIRED`. |
| History/lookup | Correction and retirement evidence is immutable and append-only. Former values are not resolvable aliases; lookup aliases are outside this slice. |

Approved lifecycle transition table:

| Operation | Source | Target | Guard / side effect |
|---|---|---|---|
| Approve allocation or replacement | `REQUESTED` | `ACTIVE` | Checker differs from maker. For correction approval, the former `ACTIVE` entry becomes `RETIRED` under the same idempotent, reconciliation-safe domain operation. |
| Reject allocation or replacement | `REQUESTED` | `REJECTED` | Checker differs from maker; durable ABB remains unavailable. A correction's former entry remains `ACTIVE`. |
| Cancel allocation or replacement | `REQUESTED` | `CANCELLED` | Exact-subject own cancellation only; durable ABB remains unavailable. A correction's former entry remains `ACTIVE`. Managed cancellation is outside this slice. |
| Approve retirement | `ACTIVE` | `RETIRED` | Separate retirement request evidence exists and checker differs from maker. |
| Reject retirement | `ACTIVE` | `ACTIVE` | No lifecycle transition; append the rejection decision to immutable history. |
| Request retirement | `ACTIVE` | `ACTIVE` | No lifecycle transition; append pending decision evidence. Direct retirement is prohibited. |
| Reactivate or any unlisted operation | Any | Forbidden | No `RETIRED -> ACTIVE`, terminal-to-requested, destructive correction or direct overwrite path. |

## 13. Failure Path to Verify

- Validation, authentication or authorization failure returns a stable failure before any ABB repository write; no
  allocation, reservation, tombstone, history or audit-evidence row exists.
- Authorization failure is evaluated before any ABB repository write and persists no ABB evidence.
- A known duplicate is rejected before allocation persistence. In a race, the unique index produces one winner and the
  losing attempt persists no allocation/register row; it returns a stable duplicate result.
- Concurrent same-tenant attempts for the same normalized ABB produce one durable winner and deterministic loser(s).
- The same ABB requested by different Legal Entities in one tenant is rejected as a duplicate.
- Cross-tenant read/update/approve attempts return the established fail-closed/not-found outcome without existence leak.
- Retry after timeout or ambiguous completion with the same idempotency key returns or reconciles the original outcome
  and cannot allocate a second ABB. A different payload with the same key fails as an idempotency conflict.
- Reject, cancel, correction, retirement and soft delete do not make a durably allocated ABB available again.
- Stale approval, cancellation, correction or retirement fails compare-and-swap concurrency and cannot overwrite newer
  state.
- The maker cannot self-approve through subject aliases, permission escalation, platform-admin bypass or alternate
  authentication context; non-direct-human actors fail before persistence.
- Correction approval atomically at the domain level produces one replacement `ACTIVE` and the former entry `RETIRED`;
  interrupted multi-writes reconcile idempotently without relying on a Mongo transaction.
- Correction reject/cancel and retirement reject preserve the prior `ACTIVE` ABB and immutable decision evidence.
- Active lookup never resolves a former value as an alias.

## 14. Authorization Convention

The existing MDM runtime proves the lowercase dotted format `mdm.{kebab-resource}.{action}` through keys such as
`mdm.legal-entities.read`. ABB uses resource segment `product-abbreviations` and exactly these eight required keys:

`mdm.product-abbreviations.read`, `mdm.product-abbreviations.request`,
`mdm.product-abbreviations.cancel`, `mdm.product-abbreviations.approve`,
`mdm.product-abbreviations.reject`, `mdm.product-abbreviations.correct`,
`mdm.product-abbreviations.retire`, `mdm.product-abbreviations.audit`.

`mdm.product-abbreviations.allocate` is prohibited: allocation is an internal consequence of an authorized request,
never a separately grantable action.

This hardening fix introduces no permission key. In particular, `cancel-own` and `cancel-managed` are prohibited; the
existing `mdm.product-abbreviations.cancel` key is necessary but not sufficient without exact request ownership.

| Actor | Minimum actions | Required permission keys | SoD / evidence |
|---|---|---|---|
| Requester | Read, request, cancel own `REQUESTED` request | `read`, `request`, `cancel` | Own cancellation compares exact canonical subject. Cannot cancel another subject's request or approve/check its own request. |
| Steward / maker | Read, request, initiate correction, request retirement; cancel only a request it owns | `read`, `request`, `correct`, `cancel`, `retire` | Records canonical maker; cannot cancel another subject's request or approve/check the same allocation, correction or retirement. |
| Approver / checker | Approve or reject allocation/correction/retirement decisions | `approve`, `reject` | Canonical human subject must differ from the requester/maker for that decision. |
| Auditor | Read register, ledger and immutable history/audit evidence | `read`, `audit` | No mutation; cross-tenant access fails closed. |

Roles are responsibility labels, not new runtime roles or automatic grants. A combined grant set never authorizes the
same canonical human subject to make and check one decision. Authorization order is fail-closed and pre-write:
authenticated actor, direct `tenant_user`, trusted tenant binding, exact permission, own-request ownership where
applicable, then maker-checker separation.

This follow-up declares the exact permission needs and owns internal authorization/SoD tests. Platform owns permission
catalog declaration/reconciliation; Diten.AuthService remains system of record for permission catalog, seed and grants;
MOD-0018 owns the authorization-policy boundary. Actual seed, role, grant, manifest, catalog mutation or endpoint
exposure is outside this internal-only slice and requires a later exposure/onboarding gate.

## 15. Gateway / API Routing Decision

Gateway/API change was unnecessary for the completed internal foundation. The Section 20 named step records the later exposure
surface at `/api/product-abbreviations`; only `integration-agent` may add the explicit Ocelot route pair after the
named-step gates and code-start are approved. This pack never edits `ocelot.json`. Every write derives `TenantId`
server-side and exposes no `LegalEntityId` partition selector.

## 16. Acceptance Criteria

- [ ] AC-01 — One GMG group is one ERP tenant; ABB uniqueness spans every Legal Entity in that tenant, and ABB records
  and write contracts contain no `LegalEntityId`.
- [ ] AC-02 — Culture-invariant trim + uppercase produces exactly `^[A-Z]{3}$`; non-ASCII letters, digits, symbols and
  internal spaces fail before persistence and consume no ABB.
- [ ] AC-03 — The only lifecycle states are `REQUESTED`, `ACTIVE`, `REJECTED`, `CANCELLED`, `RETIRED`; allowed direct
  transitions are `REQUESTED -> ACTIVE/REJECTED/CANCELLED` and approved `ACTIVE -> RETIRED`, with no reactivation.
- [ ] AC-04 — Two concurrent requests for the same normalized ABB in one tenant yield one durable allocation; the loser
  has no register entry, while a durably written ledger tombstone is never released.
- [ ] AC-05 — Same ABB in different tenants remains isolated; every cross-tenant access fails closed/not-found.
- [ ] AC-06 — Durable no-reuse survives reject, cancel, correction, retirement and soft delete; validation,
  authentication or authorization failures before persistence consume nothing.
- [ ] AC-07 — After approval each Global Product has exactly one `ACTIVE` ABB; before approval it may have none and can
  never have more than one. The same ABB cannot transfer to another Global Product.
- [ ] AC-08 — Correction creates an audited replacement `REQUESTED` entry and never a `CORRECTED` state. The old ABB
  stays `ACTIVE` until approval; approval activates the replacement and retires the old; reject/cancel preserves the old
  active value and the replacement ABB remains unavailable.
- [ ] AC-09 — Retirement uses separate request and checker decision. The ABB remains `ACTIVE` pending decision and on
  rejection; only approval transitions it to `RETIRED`.
- [ ] AC-10 — Former values and decision/recovery history are immutable append-only evidence, never destructive
  overwrite/delete and never resolvable aliases.
- [ ] AC-11 — Only direct authenticated `tenant_user` canonical human subjects may mutate. Service, delegated,
  workflow, platform-admin and unknown actors fail before any repository write.
- [ ] AC-12 — Maker-checker rejects identical `CanonicalHumanSubjectId` for allocation, correction and retirement,
  including permission, alias, alternate-authentication and platform-admin-bypass attempts.
- [ ] AC-13 — Requester cancellation succeeds only for its own `REQUESTED` request by exact canonical-subject comparison
  after the existing `cancel` permission check. Steward managed cancellation is prohibited and deferred; neither
  `cancel-own` nor `cancel-managed` is introduced.
- [ ] AC-14 — Exactly the eight Section 14 permission keys are recognized; `allocate` is not grantable. Roles remain
  responsibility labels and auditor access is read-only.
- [ ] AC-15 — Trusted context supplies `TenantId`, `CanonicalHumanSubjectId`, `ActorType`, granted permission keys and
  correlation/idempotency identifiers; client input cannot bind tenant or actor identity.
- [ ] AC-16 — Every ABB mutation uses a custom repository compare-and-swap filter containing
  `TenantId + Id + expected Version`; generic `RepositoryBase.UpdateAsync` is not used.
- [ ] AC-17 — Allocation/correction/retirement replays and interrupted multi-writes reconcile one durable outcome
  without assuming a Mongo transaction; payload drift and unreconciled ambiguity fail closed with audit evidence.
- [ ] AC-18 — `ProductAbbreviationAllocationLedger` remains separate from `CodeReservation`, with non-partial unique
  `TenantId + NormalizedAbbreviation` and unique `TenantId + IdempotencyKey` indexes.
- [ ] AC-19 — ABB remains separate from `CanonicalCode`, UUID, `RevisionIdentifier`, Material, FPF, FPP and artwork
  namespaces; market rebrand preserves ABB and existing MOD-0290 behavior remains independently deliverable.
- [ ] AC-20 — Delivery is limited to the Section 5 internal-backend allow-list and creates no API/controller, frontend,
  gateway, workflow, hosted worker, external transport, permission seed/role/grant/catalog mutation or enablement.
- [ ] AC-21 — Unit and real-Mongo tests prove lifecycle guards, tenant isolation, both unique indexes, active cardinality,
  pre-write auth denial, canonical-human SoD, expected-version concurrency, immutable history, no-reuse and recovery.

## 17. Test Expectations

Completed internal-foundation regression headings:

1. **Pre-persistence denial does not consume ABB:** normalization/grammar, authentication, actor-type, tenant-binding,
   permission, ownership and SoD tests prove no register, ledger, tombstone, history or audit-evidence write.
2. **Durable allocation is never reusable:** real-Mongo tests cover reject, cancel, retire, correction and soft delete
   while the tenant-wide ledger tombstone remains unique and non-releasable.
3. Three-character ASCII grammar and culture-invariant trim/uppercase boundaries, including Turkish culture execution.
4. Tenant-wide uniqueness across multiple Legal Entities and the non-partial normalized-ABB unique index.
5. Same-value cross-tenant isolation and cross-tenant non-disclosure; eventual HTTP 404 semantics are contract-only.
6. Concurrent allocation single-winner behavior; duplicate loser persists no ABB record.
7. Idempotent request replay, payload-drift conflict and ambiguous-timeout recovery using one stable key.
8. Exact lifecycle transition table, forbidden transition/reactivation matrix and one active ABB per Global Product.
9. Market rebrand ABB preservation and identifier-separation invariants.
10. Active-only normalized ABB resolution; former history values never resolve as aliases.
11. Replacement correction approval/reject/cancel outcomes and immutable local audit/recovery evidence.
12. Requester own-cancel, non-owner cancellation denial, approver, auditor and exact eight-key authorization matrix.
13. Direct-human GUID subject enforcement, `sub`/`NameIdentifier` equivalence, canonical-subject SoD and
    platform-admin/delegated/service/workflow bypass denial.
14. Expected-version custom-repository real-Mongo concurrency for approval, cancellation, correction and retirement.
15. Crash matrix: before ledger insert, ambiguous ledger insert, ledger durable before register write, register durable
    before audit finalization and reconciliation replay; no second ABB or released tombstone.
16. Soft-deleted register history remains discoverable by internal audit/recovery without becoming an active binding.
17. Regression execution proving existing `CodeReservation`, `GlobalProduct`, `CanonicalCode`, audit-intent and
    first-GSKU behavior are unchanged. Existing MOD-0290 test files are not modified by this pack.

Current 2026-08-09 evidence supersedes the earlier documentation-only checkpoint: MDM Release build completed with
`0 warning / 0 error`; focused ABB/manifest tests passed `57/57`; full MDM passed `404/404`, with zero skipped and real
`localhost:27017` Mongo coverage. The Local Development UI/API lifecycle smoke and read-back evidence is recorded in
Section 20.

## 18. Ready-for-dev Checklist

- [x] Master 8.1 parent evidence confirms `MOD-0290` at `Blueprint_Data!A291:AG291`.
- [x] Original reservation evidence found no conflicting `MOD-0290-FU01` or canonical-name mapping; the current
  registry child row is the expected record.
- [x] DCP-002 legacy verifier reports `OK` for the exact ID/name/parent tuple; this is compatibility evidence, not the
  business authority.
- [x] Canonical registry child record exists for this follow-up pack; this revision does not modify the registry.
- [x] ABB-to-Global Product cardinality is owner-approved: one active ABB per Global Product; former values are
  non-active history/evidence and no ABB is active for another Global Product.
- [x] Grammar is owner-approved: culture-invariant trim + uppercase, then exactly three ASCII `A-Z` letters.
- [x] No-reuse boundary is owner-approved: invalid pre-persistence input consumes nothing; durable allocation is never
  released by reject, cancel, retire, correction or soft delete.
- [x] Requester, steward/maker, approver/checker and auditor action matrix plus canonical-human maker-checker is locked.
- [x] Separate tenant-owned ABB aggregate/ledger is selected; existing `CodeReservation` is protected and not adapted.
- [x] Exact lifecycle states and transitions are locked; correction is a replacement request/event, retirement is
  maker-checker controlled, and resolvable legacy aliases are excluded.
- [x] Internal foundation plus the separately approved API/controller, Gateway, frontend, permission onboarding and
  Local Development live-smoke steps are complete; workflow, hosted worker, external transport, navigation and
  Production enablement remain excluded.
- [x] Acceptance criteria and test headings cover the internal foundation implementation plan.
- [x] Exact eight `mdm.product-abbreviations.*` keys and Platform/AuthService/MOD-0018 onboarding ownership are locked.
- [x] Pack remains `in-progress`; the Section 20 named step does not widen current authority.
- [x] Internal-foundation code-start was separately authorized and its implementation completed.
- [x] Trusted context fails closed with `TenantId`, direct-human canonical subject, actor type, granted keys and
  correlation/idempotency data before any repository write.
- [x] Custom expected-version repository operations, idempotent correction/recovery behavior and immutable audit
  evidence are implemented in the internal foundation.
- [x] Unit and non-optional real-Mongo foundation tests are implemented and passing; the exposure named step has separate gates.

## 19. Implementation Notes

- The existing MOD-0290 persistence demonstrates useful tenant-scoped uniqueness, idempotency, expected-version and
  local audit-intent patterns. It is read-only reference evidence and is not an ABB implementation extension point.
- The current `GlobalProduct` tenant-plus-`CanonicalCode` unique index proves only that technical namespace. ABB needs
  its own approved grammar, lifecycle and no-reuse evidence.
- `TenantId` is trusted server context, not write input. Tenant normalization must be consistent with the repository
  isolation contract before uniqueness is evaluated.
- Current MDM `ProductIdentityActorContext` exposes only a subject string, current platform-admin handling permits a
  broad bypass, and generic `RepositoryBase.UpdateAsync` omits expected `Version` from its write filter. They are
  reference evidence, not sufficient ABB primitives; the scoped actor context and custom repositories in Section 5 are
  code-start requirements and do not authorize AuthService changes.
- Soft deletion may hide a record but cannot erase the no-reuse tombstone/evidence.
- No code, API, test, UI, configuration, route or runtime field is produced by this authoring task.

## 20. Follow-up Items

### Code-truth reconciliation evidence — 2026-08-09

- `ProductAbbreviationsController` exposes the bounded ABB query/command contract with the exact eight permission keys.
- Gateway contains the ABB base/catch-all routes and the frontend contains the MVC controller, tenant-shell register,
  scripts, seven-locale resources and focused test file.
- Product Item SKU Master now declares one nav-hidden `PRODUCT_ABBREVIATIONS` page and the exact eight ABB permissions.
  Live Local Development reconciliation proved the global catalog definitions, six system-role matrices, zero automatic
  responsibility assignment and fresh Admin/Viewer read-only tokens.
- Live acceptance created only `QZX` and `VWK`. `QZX` is immutable `CANCELLED` version `1` after non-owner deny and
  owner cancel; `VWK` is `ACTIVE` version `1` after same-subject maker-checker deny and distinct-subject approval.
  Auditor evidence read succeeded, canceled-code reuse returned `409`, and final Mongo cardinality is register `2`,
  ledger `2`, history `4`.
- Temporary responsibility memberships were removed and the two additional Development test users were disabled.
  Browser UI read-back showed `VWK`, its Global Product, `ACTIVE`, version `1`; console warning/error count was zero.
- FU20 Local Development entitlement closure proved both disabled (`NoAccess`) and expired (`Expired`) states with zero
  ABB module grants across all six roles, then restored the same physical entitlement to active/effective with no
  expiry and the exact original role/grant matrix. QZX/VWK and Product Item SKU Master business cardinalities were
  unchanged. The fresh-login gap is now closed: supported Auth credential renewal produced a new login token in every
  state, disabled/expired returned `403` for all eight ABB actions through Gateway -> MDM, and restored Admin/Viewer
  returned `read=200` with every mutation/audit action still `403`. No responsibility assignment was created.
- Identity-hardened operational preflight pinned the tenant and six canonical active role IDs before mutation. The
  initial/final/replay snapshots remained active roles `6`, soft-deleted recovery rows `5`, aggregate active grants
  `54`, responsibility assignments `0`, ABB register/ledger/history `2/2/4`, GSKU/Revision/LSKU/Finished Good
  `1/1/1/1`, and reservations `5`. Soft-deleted recovery rows were retained; no direct Mongo write was used.
- Closure-specific isolated Release builds completed with zero errors; focused FU20 Auth tests passed `34/34` and
  focused ABB MDM tests passed `32/32`, with zero failures and zero skips.
- Verification passed focused MDM `57/57` and full MDM `404/404` with zero skipped and real `localhost:27017` Mongo.
  Production readiness and navigation remain open; no Production completion is claimed.

| Deferred item | Owner | Boundary | Closure gate |
|---|---|---|---|
| Production permission/catalog/role enablement | Platform permission catalog owner + Diten.AuthService owner + MOD-0018 policy owner | Local Development onboarding is complete; Production/Staging was not touched. | Separate Production approval, runbook and production evidence. |
| Steward managed cancellation | Product Data Owner + Auth/Permission Owner | A steward cannot cancel another subject's request in this slice; no permission key is minted here. | Separately approved workflow/authorization follow-up with ownership override, SoD, immutable evidence and negative tests. |
| ABB API, tenant-shell Register UI and Gateway exposure | Product Data Owner + MDM Architect + Integration Owner | Implemented as the single Section 20 named step; Local Development lifecycle smoke is complete. | Production quality gates only; navigation remains separately deferred. |
| Delegated, service, workflow or platform-admin actor support | Quality Owner + Auth/Permission Owner | Mutating ABB fails closed for these actor types in this slice. | Separately approved actor-delegation and SoD contract plus bypass-negative tests. |
| Resolvable legacy ABB aliases | Product Data Owner + Quality Owner | Former values remain immutable audit/history evidence only. | Separately approved lookup semantics, collision rules, authorization and privacy contract. |

### Named Step — ABB Exposure & Register UI

#### Step identity, authority and non-expansion boundary

Named step: **`ABB Exposure & Register UI`**.

This is one delivery step inside `MOD-0290-FU01`, not a new DCP, MOD/FU, registry identity, lifecycle design or SoD
review. It exposes the completed internal foundation without modifying its entities, repositories, indexes, lifecycle,
no-reuse, maker-checker, owner-only cancellation or actor-context rules. API and UI may dispatch only the existing
Section 3 commands and queries. Managed cancellation, former-value lookup aliases, delegated/service/workflow actors,
new permission keys and new code namespaces remain prohibited.

This preparation changes documentation only. All runtime paths remain protected until the Product Data Owner, MDM
Architect, Auth/Permission Owner and Integration Owner accept the contracts below and the user grants explicit
named-step code-start.

#### Verified user surface and Golden Reference decision

Surface: tenant-shell `ABB Register` DataTable at `/MDM/ProductAbbreviationRegister`.

Verified request-allocation fields:

| User field | UI control | Required | Maps to existing contract |
|---|---|---|---|
| `Global Product` | Same-tenant searchable selector/provider; no hardcoded options | Yes | `RequestProductAbbreviationAllocationCommand.GlobalProductId` |
| `ABB` | Text input, maxlength 3; server remains authoritative for normalization and `^[A-Z]{3}$` | Yes | `RequestProductAbbreviationAllocationCommand.Abbreviation` |

Actual `form_field_count` is **2**, therefore `golden_reference: slim`. The initial three-field suggestion is not
adopted: the existing request command has no `Reason`, and the exposure layer must not accept and silently discard one.
`IdempotencyKey` is transport metadata, not a user form field. Action reason modals, identifiers, `Version`, audit
evidence and DataTable action/checkbox columns are also excluded from the field count.

The live `GoldenReferenceSlim` file/ordering contract is mandatory:

```text
frontend/Diten.Web/Views/MDM/ProductAbbreviationRegister/
|-- Index.cshtml
|-- _Filter.cshtml
|-- _DataTable.cshtml
|-- _IndexL10n.cshtml
|-- _CreateEditOffcanvas.cshtml
|-- _DetailsQuickView.cshtml
`-- ProductAbbreviationRegisterIndex.cs

frontend/Diten.Web/wwwroot/assets/js/MDM/ProductAbbreviationRegister/
|-- index.js
`-- index.l10n.js

frontend/Diten.Web/Resources/Views/MDM/ProductAbbreviationRegister/
`-- ProductAbbreviationRegisterIndex.{en|fr|es|zh|ar|ru|tr}.resx
```

`_CreateEditOffcanvas.cshtml` retains the Golden Slim filename but is request-only; there is no ABB update/edit
endpoint. `Index.cshtml` explicitly sets `Layout = "_LayoutTenantShell"`, uses absolute partial paths and renders
filter, shared bulk-selection bar, DataTable, details quick view and request offcanvas in Golden order. The table uses
`data-dt-standard="v2"`, `id="skeleton-loader"`, `DtDefaults.create()`/the shared DataTable wrapper,
`stateSave: false`, accessibility markers and the standard localization bridge. No bulk delete or destructive bulk
action exists; selection may only expose the shared count/clear behavior because ABB has no delete permission or
delete command.

The current internal foundation has no tenant-wide list query and no Global Product list/lookup query. To honor the
existing-query-only decision, the initial DataTable is explicitly **exact-product scoped**: after a Global Product is
selected, it calls `GetProductAbbreviationByGlobalProductQuery` and displays zero or one row. A free-browse register
list, server-side paging query or new Global Product lookup contract must not be invented in this step. Before code
start, an owner-approved same-tenant Global Product selector provider must be identified; absent that provider, the UI
step is blocked and may not fall back to a hardcoded list or raw service-port call.

Planned columns: Global Product display/identifier, ABB, lifecycle, `Version`, retirement-pending indicator and
actions. Former/replaced ABB values may appear only inside clearly labelled read-only evidence/history; they are never
rendered as the product's canonical active ABB. Active resolution remains the only canonical-active display source.

#### UI actions and Premium SweetAlert2 contract

Request uses the Slim offcanvas. Approval, rejection, owner cancellation, correction, retirement request and
retirement decision are row actions gated by the current lifecycle plus the actor's granted ABB permission. Backend
authorization remains authoritative; hiding/disabling an action is UX only and never substitutes for handler checks.

| UI action | Existing permission | Existing command | Reason modal |
|---|---|---|---|
| Request allocation | `mdm.product-abbreviations.request` | `RequestProductAbbreviationAllocationCommand` | None; request command has no reason |
| Approve allocation/correction | `mdm.product-abbreviations.approve` | `ApproveProductAbbreviationAllocationCommand` | Premium confirm; optional reason |
| Reject allocation/correction | `mdm.product-abbreviations.reject` | `RejectProductAbbreviationAllocationCommand` | Premium confirm; required reason |
| Cancel own request | `mdm.product-abbreviations.cancel` | `CancelProductAbbreviationAllocationCommand` | Premium confirm; optional reason; handler enforces exact owner |
| Initiate correction | `mdm.product-abbreviations.correct` | `InitiateProductAbbreviationCorrectionCommand` | Replacement ABB plus required reason |
| Request retirement | `mdm.product-abbreviations.retire` | `RequestProductAbbreviationRetirementCommand` | Required reason |
| Approve retirement | `mdm.product-abbreviations.approve` | `ApproveProductAbbreviationRetirementCommand` | Premium confirm; optional reason |
| Reject retirement | `mdm.product-abbreviations.reject` | `RejectProductAbbreviationRetirementCommand` | Premium confirm; required reason |

All confirmation/reason interactions use the shared MOD-0013 Premium SweetAlert2 wrapper, localized text,
`buttonsStyling: false`, Sneat button classes and safe error rendering. Native `alert`, `confirm`, hardcoded fallback
text and manually duplicated modal CSS are prohibited. A stale-version `409` tells the user to reload; permission or
ownership `403` shows a localized denied state and performs no optimistic local mutation.

Audit evidence is lazy-loaded into the read-only details quick view/side panel only when the actor has
`mdm.product-abbreviations.audit`. Canonical subject IDs, hashes and correlation/idempotency evidence are displayed as
read-only values and never rebound into mutation payloads.

#### Exact MDM API and CQRS mapping

Planned controller: `services/Diten.MdmService/src/Diten.MdmService.Api/Controllers/ProductAbbreviationsController.cs`.
It is `[Authorize]`, `[ApiController]`, `[Route("api/product-abbreviations")]`, derives from `CustomBaseController`,
injects only `IMediator`, contains no validation/business authorization, and returns every handler envelope through
`CreateActionResultInstance(response)`.

Every mutation requires an `Idempotency-Key` header, mapped to the existing command `IdempotencyKey`; the body never
contains tenant or actor identity. Exact endpoints:

| HTTP and route | Permission | Transport request | Existing CQRS mapping | Response envelope |
|---|---|---|---|---|
| `GET /api/product-abbreviations/by-global-product/{globalProductId:guid}` | `read` | Route ID only | `GetProductAbbreviationByGlobalProductQuery(globalProductId)` | `Response<ProductAbbreviationRegisterEntryDto>` |
| `GET /api/product-abbreviations/resolve/{abbreviation}` | `read` | Route ABB only | `ResolveProductAbbreviationQuery(abbreviation)` | `Response<ProductAbbreviationResolutionDto>` |
| `GET /api/product-abbreviations/{registerEntryId:guid}/evidence` | `audit` | Route ID only | `GetProductAbbreviationAllocationEvidenceQuery(registerEntryId)` | `Response<ProductAbbreviationAllocationEvidenceDto>` |
| `POST /api/product-abbreviations/requests` | `request` | `RequestAllocationRequest(GlobalProductId, Abbreviation)` | `RequestProductAbbreviationAllocationCommand(GlobalProductId, Abbreviation, idempotencyKey)` | `Response<ProductAbbreviationAllocationResultDto>` |
| `PATCH /api/product-abbreviations/{registerEntryId:guid}/cancel` | `cancel` | `CancelAllocationRequest(ExpectedVersion, Reason?)` | `CancelProductAbbreviationAllocationCommand(registerEntryId, ExpectedVersion, idempotencyKey, Reason)` | `Response<ProductAbbreviationRegisterEntryDto>` |
| `PATCH /api/product-abbreviations/{registerEntryId:guid}/approve` | `approve` | `ApproveAllocationRequest(ExpectedVersion, ExpectedFormerVersion?, Reason?)` | `ApproveProductAbbreviationAllocationCommand(registerEntryId, ExpectedVersion, idempotencyKey, ExpectedFormerVersion, Reason)` | `Response<ProductAbbreviationRegisterEntryDto>` |
| `PATCH /api/product-abbreviations/{registerEntryId:guid}/reject` | `reject` | `RejectAllocationRequest(ExpectedVersion, Reason)` | `RejectProductAbbreviationAllocationCommand(registerEntryId, ExpectedVersion, idempotencyKey, Reason)` | `Response<ProductAbbreviationRegisterEntryDto>` |
| `POST /api/product-abbreviations/{registerEntryId:guid}/corrections` | `correct` | `InitiateCorrectionRequest(ExpectedVersion, ReplacementAbbreviation, Reason)` | `InitiateProductAbbreviationCorrectionCommand(registerEntryId, ExpectedVersion, ReplacementAbbreviation, idempotencyKey, Reason)` | `Response<ProductAbbreviationAllocationResultDto>` |
| `POST /api/product-abbreviations/{registerEntryId:guid}/retirement-requests` | `retire` | `RequestRetirementRequest(ExpectedVersion, Reason)` | `RequestProductAbbreviationRetirementCommand(registerEntryId, ExpectedVersion, idempotencyKey, Reason)` | `Response<ProductAbbreviationRegisterEntryDto>` |
| `PATCH /api/product-abbreviations/{registerEntryId:guid}/retirement-requests/{retirementRequestId}/approve` | `approve` | `ApproveRetirementRequest(ExpectedVersion, Reason?)` | `ApproveProductAbbreviationRetirementCommand(registerEntryId, ExpectedVersion, retirementRequestId, idempotencyKey, Reason)` | `Response<ProductAbbreviationRegisterEntryDto>` |
| `PATCH /api/product-abbreviations/{registerEntryId:guid}/retirement-requests/{retirementRequestId}/reject` | `reject` | `RejectRetirementRequest(ExpectedVersion, Reason)` | `RejectProductAbbreviationRetirementCommand(registerEntryId, ExpectedVersion, retirementRequestId, idempotencyKey, Reason)` | `Response<ProductAbbreviationRegisterEntryDto>` |

Transport DTOs are sealed records in
`services/Diten.MdmService/src/Diten.MdmService.Api/Contracts/ProductAbbreviations/`. None contains `TenantId`,
`LegalEntityId`, canonical subject, actor type, permission keys, lifecycle target, ledger ID, history, correlation ID or
idempotency key. `Idempotency-Key`, `X-Correlation-Id`, JWT and trusted tenant headers are transport context. Controller
code does not normalize ABB, decide lifecycle, repeat FluentValidation, evaluate owner/maker-checker rules or translate
stable domain failures into alternate business outcomes.

#### Authorization and catalog dependency gate

The API uses exactly the eight Section 14 keys with `[HasPermission("mdm.product-abbreviations.{action}")]`; no key is
renamed, aliased or added. `[Authorize]` plus per-action permission is required, and the existing application
authorization continues to reject every mutating actor except a direct `tenant_user` with a valid canonical GUID.

Ownership chain:

1. MDM declares the ABB page/action descriptors and the existing eight keys through the owner-accepted module-provider
   contract; it does not seed or grant them.
2. Platform module catalog reconciles provider descriptors.
3. Diten.AuthService remains the permission catalog/seed/grant system of record; its internal catalog sync is the
   consumer boundary.
4. MOD-0018 remains the authorization-policy boundary.

Planned MDM provider name: `ProductAbbreviationRegisterManifestProvider`, with tenant route
`/MDM/ProductAbbreviationRegister`, read/audit page actions and the eight verbatim keys. Provider shape, module-code
attribution and owner acceptance are an explicit pre-code gate. This pack does not authorize changes under
`services/Diten.AuthService/**`, `services/Diten.Platform/**`, seed data, role templates or grants. Until provider
reconciliation is proven and the intended tenant roles can receive the required keys, the endpoint/UI must remain
disabled and must not be described as production ready.

#### Gateway and frontend transport contract

Browser JavaScript uses the same-origin MVC proxy profile at
`/MDM/ProductAbbreviationRegister/api/...` on frontend port `5001`. The MVC controller reads the HttpOnly access token
server-side and forwards JWT, trusted tenant, correlation and idempotency headers only to Gateway `5000`. Browser code
must not read the token, construct a Bearer header, call MDM `5059`, or use another service port.

Planned frontend controller: `frontend/Diten.Web/Controllers/ProductAbbreviationRegisterController.cs`. It renders
Index and forwards the exact MDM API subsection resource/action paths without implementing ABB validation, authorization or
lifecycle rules. It safely preserves `Response<T>` status/envelope and never turns a failed mutation into local success.

Gateway route contract, to be implemented only by `integration-agent` after approval:

- base and catch-all pair:
  `/api/product-abbreviations` and `/api/product-abbreviations/{everything}`;
- identical upstream/downstream path templates;
- downstream MDM host `localhost`, current repo-established MDM port `5059`;
- methods `GET`, `POST`, `PATCH`, `OPTIONS` (`OPTIONS` is mandatory);
- explicit routes precede any catch-all route.

`gateway/Diten.ApiGateway/ocelot.json` remains protected and is never edited by the MDM pack author or MDM
implementation agent.

#### Named-step repo scope and protected paths

Planned implementation allow-list after all gates and explicit code-start:

- the named-step MDM controller and transport-contract folder;
- MDM API module-provider/registration files only for declaring this surface;
- `frontend/Diten.Web/Controllers/ProductAbbreviationRegisterController.cs`;
- the exact named-step view, script and seven-locale resource paths;
- new ABB exposure/controller/frontend tests in the owning MDM and Web test locations;
- a separately assigned `integration-agent` change limited to the two named-step Ocelot route entries.

Protected/out-of-scope even after named-step code-start:

- all existing ABB Domain/Application/Persistence/Infrastructure implementation except read-only consumption through
  existing commands/queries;
- existing `GlobalProduct`, `CodeReservation`, first-GSKU and their tests;
- Diten.AuthService and Platform catalog/seed/grant implementations;
- managed cancellation, lookup aliases, delegated/service/workflow actor support, hosted workers and workflows;
- new lifecycle states, permission keys, delete/bulk-delete endpoints and Material/FPF/FPP/artwork code surfaces;
- `.antigravity/**`, frozen/archive paths and every registry/DCP file.

#### Named-step acceptance criteria

- [ ] NS-01 — The only create/request fields are `Global Product` and `ABB`; `TenantId`, `LegalEntityId`, actor identity,
  permission data and idempotency are absent from body/form payloads.
- [ ] NS-02 — The tenant page explicitly uses `_LayoutTenantShell`, the complete Slim partial set, DataTable v2 marker,
  skeleton loader, shared state/personalization behavior and `GoldenReferenceSlim` ordering.
- [ ] NS-03 — The exact-product-scoped DataTable dispatches only `GetProductAbbreviationByGlobalProductQuery` and
  returns zero/one row; no unapproved list/paging or Global Product lookup query is invented.
- [ ] NS-04 — Every named-step endpoint derives from `CustomBaseController`, dispatches exactly one existing MediatR
  request, uses `Response<T>` and carries the stated existing permission key.
- [ ] NS-05 — Request, approve, reject, owner cancel, correction and retirement actions preserve every internal
  lifecycle, SoD, expected-version, no-reuse and direct-human authorization result without controller/UI duplication.
- [ ] NS-06 — Non-owner cancellation and maker-checker denials remain `403`; stale versions remain `409`; UI performs
  no optimistic mutation after failure and reloads authoritative state after success.
- [ ] NS-07 — Audit evidence is read-only and audit-permission gated; former non-active ABB values are never labelled or
  resolved as canonical active ABBs.
- [ ] NS-08 — All action confirmations/reasons use localized Premium SweetAlert2 behavior; native dialogs, hardcoded
  fallback strings and page-level modal CSS are absent.
- [ ] NS-09 — Exactly seven tenant locales (`en`, `fr`, `es`, `zh`, `ar`, `ru`, `tr`) have parity for marker class
  `ProductAbbreviationRegisterIndex`, `_IndexL10n` JSON and `index.l10n.js`; non-English values are not placeholders.
- [ ] NS-10 — Browser network traffic targets the same-origin MVC proxy, which calls Gateway `5000`; no browser request
  targets MDM `5059` or another service port.
- [ ] NS-11 — Only `integration-agent` adds the two explicit Ocelot routes with `OPTIONS`, and route smoke proves JWT,
  tenant and correlation propagation without direct-service bypass.
- [ ] NS-12 — Provider reconciliation exposes exactly the Section 14 keys with correct tenant scope and owner-accepted
  module attribution; no AuthService seed/grant code is changed by this pack.
- [ ] NS-13 — Without provider reconciliation and tenant-role grantability evidence, API/UI remains disabled and is not
  marked production ready.
- [ ] NS-14 — Managed cancellation, aliases, non-direct actors, new lifecycle/code namespaces and new permissions remain
  absent from controller, UI, provider and Gateway contracts.

#### Named-step test and quality gates

- MDM controller contract tests pin route, HTTP verb, `[Authorize]`, each `[HasPermission]`, exact DTO-to-command/query
  mapping, `Response<T>` mapping and absence of `TenantId`/`LegalEntityId` in every write DTO.
- Existing ABB unit/authorization/real-Mongo tests and the full MDM regression suite remain green; exposure tests do not
  replace real-Mongo evidence.
- Frontend tests cover exact-product zero/one-row load, action visibility, Premium reason validation, safe
  `403/404/409` rendering, authoritative reload, audit-only details and no former-as-active presentation.
- Permission/provider integration proves exact eight-key reconciliation, tenant-scope attribution and no implicit
  production readiness before grantability acceptance.
- Gateway smoke covers base/catch-all route, `GET/POST/PATCH/OPTIONS`, JWT/token passthrough, tenant header and
  correlation/idempotency propagation through Gateway `5000` to MDM `5059`.
- Build gates: MDM API, Diten.Web and Gateway.
- DataTable gate:
  `python3 .antigravity/scripts/verify_datatable_page.py . --area MDM --module ProductAbbreviationRegister --reference slim`.
- Localization parity gate covers all seven tenant RESX files and rejects English placeholders in non-English files.
- Browser smoke covers request offcanvas, exact-product filter/load, each lifecycle action/modal, details/evidence,
  responsive behavior and absence of direct service-port calls.

#### Named-step ready-for-code checklist

- [x] Existing ABB command/query and DTO signatures inspected; no internal behavior change planned.
- [x] Live GoldenReferenceSlim views/scripts/resources and the two-field count inspected; Slim selected.
- [x] Existing MDM `CustomBaseController`, `Response<T>`, permission attribute and controller patterns inspected.
- [x] Existing Ocelot MDM/Golden route pairs inspected; MDM downstream port `5059` confirmed from repo configuration.
- [x] Auth catalog seed/sync examples inspected; declaration/reconciliation/SoR ownership recorded.
- [ ] Product Data Owner accepts exact-product-scoped zero/one-row DataTable and the absence of request `Reason`.
- [ ] MDM Architect identifies and accepts a same-tenant Global Product selector provider without a new query in this
  step; otherwise implementation is blocked.
- [ ] Auth/Permission Owner accepts `ProductAbbreviationRegisterManifestProvider` shape, module attribution and exact
  eight-key reconciliation path.
- [ ] Integration Owner accepts the explicit Gateway route pair and assigns it to `integration-agent`.
- [ ] Seven-locale institutional translations are approved; placeholder translation is prohibited.
- [ ] Explicit user named-step code-start is granted after the above gates. Current `in-progress` status alone does not
  authorize API, UI, provider, Gateway or test code.

Material, FPF, FPP and artwork controlled-code namespace scopes remain governed outside this follow-up. No follow-up may
use this approved design/scope pack to authorize their issuance or to place those codes in `CanonicalCode` or
`RevisionIdentifier`.
