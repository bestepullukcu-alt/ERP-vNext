---
id: MOD-0290
name: Product / Item / SKU Master
domain: master-data-management
service: Diten.MdmService
shell: tenant
golden_reference: slim
entity_base: EntityBase
status: in-progress
owner: product-data-owner / mdm-domain-team
branch: feature/mdm/mod-0290-product-item-sku-master
started: "2026-08-01T12:32:42Z"
target: "Local Development code-truth: Global Product end-to-end; Product Definition Revision/First GSKU; verified GSKU provider/publication/resolver; GSKU and LSKU A-G; Finished Good A-E; four-register Save View hardening; lifecycle, regulatory/master-data, deferred-navigation and Production-readiness gates remain open"
form_field_count: 2
parent_dcp: execution/portfolio/delivery-capability-packs/DCP-004-mod-0290-sku-coding-foundation-readiness.md
domain_contract: execution/domains/master-data-management/domain-contracts/MOD-0290-sku-coding-foundation-domain-contract.md
canonical_blueprint: docs/System Capability & Implementation Blueprint - master 8.1.xlsx
---

# MOD-0290 - Product / Item / SKU Master

> **2026-09-10 FG proposal authority:** Section 21 records the actual integration HEAD and a Phase 1.5 planning-only
> revision. Its proposed future paths and decisions do not grant runtime code-start, change this pack's status or
> revalidate historical live/test claims. Existing unrelated approvals remain intact; current FG code truth is in 21.1-21.3.

> **In-progress/code-truth guard (2026-08-09):** This pack records the implemented Local Development scope proved in
> Section 19: Global Product end-to-end, Product Definition Revision/First GSKU, verified GSKU provider/publication,
> GSKU A-G, LSKU A-G, Finished Good A-E and shared Save View hardening. It grants no new runtime authority and makes no
> Production-readiness claim. WorkCenter lifecycle, the separately listed master-data/regulatory backlogs, remaining
> navigation decisions and Production enablement retain their own gates.
>
> **Branch guard:** Implementation is restricted to `feature/mdm/mod-0290-product-item-sku-master`; no stage, commit or
> push is authorized by this pack status.
>
> **Finished Good named-step guard:** A-E and the authorized Local Development live create/read smoke are implemented.
> `FG-000000000005` is the retained pilot proof. Navigation remains hidden and Production enablement remains open.
>
> Subwork `A — MDM Global Product API/read-selector` was explicitly authorized on 2026-08-04. Permission-provider,
> Gateway, frontend and ABB-consumption subwork remains planning-only and creates no new DCP/FU/registry identity.
>
> **Superseding Global Product status (2026-08-09):** Backend/API, Gateway, frontend, permission onboarding and Local
> Development create/read smoke are complete. ABB consumption, lifecycle and Production gates remain open.

> **GSKU Register named-step guard:** A-G are implemented and evidenced, including permissions, verified provider
> publication/resolution and Local Development create/read/replay. `GS-000000000003` is the retained pilot proof.
> The current manifest makes `GSKUS` visible; this reconciliation does not authorize any further navigation mutation.

> **LSKU Register named-step guard:** A-G are implemented and evidenced, including FU19 permissions, the verified
> 249-value market catalog and Local Development create/read smoke. `LS-000000000004` with market `TR` is the retained
> pilot proof. H remains deliberately deferred and `LSKUS` stays navigation-hidden.

> **Superseding GSKU reference decision (2026-08-07):** The user selected MOD-0048's global, code-owned,
> deployment-versioned lookup for the exact `pack-applicability` and `uom` families. GSKU still resolves and persists
> provider evidence through the existing authenticated Platform contract, but these two sets no longer require a
> reference tenant, consumer assignment, Mongo catalog rows, seed/load/publish operation or operational governance
> eligibility. All later GSKU sections that describe those items as mandatory predecessors are superseded. The exact
> values remain closed to clients and tenants; any catalog change requires a new MOD-0048 deployment version and
> regression evidence. Tenant-owned business reference families are unaffected.

## 1. Module Summary

MOD-0290 establishes the tenant-scoped Product/SKU identity system of record in `Diten.MdmService`. Its first phase
owns Global Product, Product Definition Revision, GSKU, LSKU, Finished Good, MarketTradeName, LegacyAlias and
CodeReservation. It provides low-semantic canonical codes, explicit parent-child identity, controlled approval,
historical market-trade-name replacement and manual legacy alias onboarding.

The implemented Local Development surface is no longer backend-only. Global Product, GSKU, LSKU and Finished Good have
bounded MDM APIs, Gateway delivery and tenant frontend surfaces. The Global Product slice retains its one-field create
contract; LSKU retains exactly two user-entered fields and GSKU retains three. Frontmatter records the LSKU Slim count
without changing those independently locked contracts.

The repository contains Product Definition Revision + First GSKU, the completed GSKU and LSKU A-G slices, and Finished
Good A-E. Local Development smoke proves the retained pilot records and Admin/Viewer plus tenant-isolation behavior.
None of this certifies Production readiness. Legal Entity binding, MarketTradeName and later lifecycle transitions
remain absent or separately gated.

### Authority and references

- Formal delivery authority: approved [DCP-004](../../../portfolio/delivery-capability-packs/DCP-004-mod-0290-sku-coding-foundation-readiness.md).
- Detailed supporting design: draft [MOD-0290 Domain Contract](../domain-contracts/MOD-0290-sku-coding-foundation-domain-contract.md).
- For this named-step revision, the user-locked Domain Contract field/cardinality decisions govern the Module Pack;
  the pack is aligned to them and may not weaken them because the supporting document remains `draft`.
- Canonical Blueprint authority for MOD-0290: Master 8.1 only.
  - `Blueprint_Data!A291:AG291` - MOD-0290, `Product / Item / SKU Master`.
  - `Dependencies!A1281:D1285` - MOD-0003, MOD-0040, MOD-0021, MOD-0252 and MOD-0253 direct dependencies.
  - `SoR_Map!A256:E256` - Product master, item master, SKU and UoM mapping ownership.
- Registry identity: `MOD-0290 - Product / Item / SKU Master`, owner `master-data-management`.
- Master 7 is legacy verifier/tool compatibility input only and is not Master 8.1 alignment evidence.

## 2. Ownership and Boundaries

### In scope

- Tenant-scoped Product/SKU identity and tenant-scoped uniqueness.
- The eight aggregate roots listed in this pack.
- Shared Product identity lifecycle and maker-checker enforcement.
- Common canonical-code reservation ledger and permanent no-reuse evidence.
- Explicit Product Definition Revision references; no inferred current revision.
- The implemented-in-repository Product Definition Revision + First GSKU foundation as predecessor code truth; this
  pack does not infer production readiness from its presence.
- The planned `Finished Good Draft Foundation` named step, subject to its separate A-E entry gates, exact allow-list
  and explicit user code-start authorization.
- The planned `LSKU Draft Identity Foundation` named step, limited to one Draft LSKU identity per GSKU and verified
  market, subject to its separate provider contract, exact allow-list and explicit user code-start authorization.
- The planned `Global Product Register Exposure & UI` named step: Global Product list/detail/create exposure, minimum
  same-tenant read-only selector, Gateway contract and tenant register, subject to its separate gates and owners.
- Finished Good to exactly one GSKU relationship.
- LSKU-owned MarketTradeName proposal, approval, replacement and historical lookup.
- Manual legacy onboarding by attaching a separately governed LegacyAlias.
- Six controlled-reference families as fail-closed consumer dependencies; their exact contracts remain G2/G3 gates.
- MDM-local durable critical-mutation evidence and reliable delivery boundary to MOD-0021.
- Backend create, correction, search/list/detail, submit, decision, retirement, alias lookup and export contracts.

### Out of scope and prohibited shortcuts

- Composition/active-substance SoR, Composition ID/FK/reference/placeholder and complex-strength approval: BL-015.
- Revision effective dating, automatic current revision, `IsCurrent`, overlap or parallel-revision policy: BL-016.
- Packaging hierarchy or `PackagingLevelCode`: BL-017.
- Direct LSKU-Finished Good relationship or FK; future Market Supply Assignment: BL-018.
- MA and Registered Presentation: BL-019.
- Artwork, label and leaflet lifecycle: BL-020.
- BOM, manufacturing version, quality specification, batch or release: BL-021.
- GTIN lifecycle or an unmanaged GTIN text field: BL-022.
- Bulk legacy import, staging, migration or migration-success claims: BL-023.
- Synthetic MarketTradeName `IsUsed`: BL-024.
- ERP/PLM clients, feeds, workers, ingestion, distribution or gateway routes: BL-025 and DCP G7.
- Runtime external contract publication: BL-026 and DCP G7.
- Provider-owned PSS-012 bulk quarantine, reapproval or migration: BL-027.
- Frontend, DataTable, Razor views, navigation and gateway implementation remain prohibited until the planned exposure
  named step receives explicit code-start; this preparation changes none of them.
- Provider-domain code changes or provider follow-up packs.
- For the Product Definition Revision/GSKU named step: API/controller, gateway, frontend, workflow, hosted worker or transport activation,
  submit/approval, LSKU, Finished Good, MA, artwork, GTIN, Composition, ProductType, DosageForm,
  RouteOfAdministration, Strength, revision effective dates and `IsCurrent`.
- A standalone Product Definition Revision create/edit command or an externally successful empty revision shell.

### GMG-SCM-SOP-0001 delivery guard

The currently authorized and planned slices allocate only the MOD-0290 low-semantic internal `CanonicalCode` and the
Product Definition parent-scoped `RevisionIdentifier` described by this pack. They do not issue SOP-controlled
material, FPF, FPP, box, label, foil or leaflet codes/revisions, and they do not equate those identities with
Product Definition, GSKU, LSKU or Finished Good.

SOP-controlled code/revision issuance is outside this slice and requires DCP-005 approval, a resolved real
Master 8.1 owner or DCP-002 candidate identity, an approved owner Module Pack/domain contract, and explicit code-start
authorization. No `CanonicalCode`/`RevisionIdentifier` overload, new runtime field, placeholder FK, Composition, MA,
Registered Presentation, artwork or Market Supply scope may enter through this note. The current first-GSKU internal
foundation remains allowed only within its existing named-step gates.

## 3. Owned Objects

### Aggregate and storage boundaries

| Aggregate root | MOD-0290 SoR responsibility | Logical storage boundary |
|---|---|---|
| Global Product | Stable product identity and parent of explicit revisions | Separate tenant-owned aggregate collection |
| Product Definition Revision | Presentation identity for one Global Product | Separate tenant-owned aggregate collection |
| GSKU | Global SKU identity and pack applicability for one explicit revision | Separate tenant-owned aggregate collection |
| LSKU | Market-context SKU identity for one GSKU | Separate tenant-owned aggregate collection |
| Finished Good | Finished-Good identity linked to exactly one GSKU | Separate tenant-owned aggregate collection |
| MarketTradeName | LSKU-owned proposal and approved historical timeline row | Separate tenant-owned historical-row collection |
| LegacyAlias | Raw legacy identifier, normalized lookup key and target reference | Separate tenant-owned alias collection |
| CodeReservation | Entity-type-independent canonical-code reserve/consume ledger | One common tenant-owned ledger collection with a required unique persistence invariant on `TenantId + ReservedCode` |

Physical Mongo collection names, helper/performance indexes and the approved G4 atomicity representation remain
authorized-implementation design decisions whose proof is required for later readiness. The common ledger's entity-type-independent unique persistence/index invariant on
`TenantId + ReservedCode` is not open: it is the base namespace-enforcement requirement. Implementation must not
collapse SoR boundaries, rely on cross-collection unique-index behavior or weaken that invariant.

### Cardinality

| Relationship | Cardinality / invariant |
|---|---|
| Global Product -> Product Definition Revision | `1 -> 0..*`; every revision has exactly one Global Product |
| Product Definition Revision -> GSKU | `1 -> 0..*`; every GSKU has exactly one explicit revision |
| GSKU -> LSKU | `1 -> 0..*`; every LSKU has exactly one GSKU |
| GSKU -> Finished Good | `1 -> 0..*`; every Finished Good has exactly one GSKU |
| LSKU -> MarketTradeName | `1 -> 0..*`; market/language/timeline rules apply |
| Canonical identity -> LegacyAlias | `1 -> 0..*`; target is same-tenant |
| CodeReservation -> identity | `1 -> 0..1`; a code-bearing identity -> exactly one matching consumed reservation |
| LSKU -> Finished Good | No direct relationship in phase one |
| Product Definition/GSKU -> Composition | No relationship or placeholder in phase one |

### Conceptual application objects

- Commands: reserve/cancel/expire code; create and correct Draft identities; submit; apply approval/rejection;
  controlled cancel; retire; propose/approve MarketTradeName replacement; attach/retire LegacyAlias.
- Queries: list, filter, get-by-ID, canonical-code lookup, alias lookup, parent/child inspection,
  MarketTradeName current/history timeline, reservation status and internal export.
- DTOs: write DTOs exclude `TenantId`, direct `CanonicalCode`, trusted actor identity and system lifecycle fields.
- Repositories: aggregate-specific repositories plus the common CodeReservation ledger contract; every operation is
  tenant-filtered and soft-delete aware.

For `Global Product Register Exposure & UI`, Global Product owns required `GlobalProductName` as its market-independent
internal/global product-family name. The create request adds that one business field; list, detail, create result and ABB
selector projections expose it. The named step adds no edit/update/rename object.

For `Product Definition Revision + First GSKU Draft Foundation`, the conceptual write surface is deliberately
narrower than the full-module list above:

- `CreateFirstGskuDraft` is the only revision-creation behavior. One stable command identity drives parent validation,
  parent-scoped revision allocation, matching GSKU reservation consumption, revision/GSKU persistence and audit intent.
- The normalized immutable `CreationCommandId` is persisted on both the Revision and GSKU. It is the pair-recovery key;
  it does not replace the CodeReservation ledger's reservation/consume command identities.
- An idempotent replay returns or completes the same Revision/GSKU outcome; it never allocates a second revision ordinal,
  consumes another reservation or creates a second GSKU.
- A GSKU Draft correction uses expected-version conditional mutation. `GlobalProductId`,
  `ProductDefinitionRevisionId`, `CodeReservationId` and `CanonicalCode` are immutable after create.
- Because no cross-collection transaction topology is assumed, a partial/ambiguous write is not reported as success.
  It remains recoverable under the same command identity, and reconciliation must complete or surface the same
  non-reusable reservation/pending outcome without exposing an independent empty-revision workflow.

## 4. Entity Fields

### Shared `EntityBase` fields

`Id`, `TenantId`, `IsDeleted`, `DeletedAt`, `CreatedAt`, `UpdatedAt` and technical `Version` are inherited. They are
not redeclared on aggregate entities. `CreatedBy`/`UpdatedBy`, where retained for a user-driven aggregate, come from
trusted actor context and never from a DTO.

| Shared field | Rule |
|---|---|
| `TenantId` | Server-assigned from authenticated tenant context; absent from write DTOs; supplied client value is rejected fail-closed |
| `Version` | Expected-version conditional mutation; last-write-wins is prohibited |
| Soft delete fields | Technical deletion only; never substitute for retirement and never free a code or alias decision |
| Product identity lifecycle | `DRAFT -> PENDING_IDENTITY_APPROVAL -> IDENTITY_APPROVED -> RETIRED` |

### Global Product

| Field | Decision | Validation/lifecycle rule |
|---|---|---|
| `Id` / `GlobalProductId` | System-derived identity | Immutable; one technical ID, no duplicate identity property |
| `CanonicalCode` | Required, system-derived | Matching consumed reservation; immutable; shared tenant namespace; no-reuse |
| `GlobalProductName` | Required business text | User-approved market-independent internal/global product-family name; required at create; not MarketTradeName, an LSKU/market/authorization name or a replacement for `CanonicalCode` |
| Lifecycle and technical fields | System-derived | Shared lifecycle, tenant, audit, soft-delete and concurrency rules |

`GlobalProductName` ownership and requiredness are closed user decisions. Empty or whitespace-only input is rejected.
Maximum length, uniqueness, normalization and post-create mutability are not proven by the current runtime/domain
contract and remain implementation-time decisions; they must not be guessed. This named step includes no update/edit,
rename, name-versioning or name-approval behavior.

### Product Definition Revision

| Field | Decision | Validation/lifecycle rule |
|---|---|---|
| `GlobalProductId` | Required | Same tenant; immutable after create; retired parent not referenceable |
| `RevisionIdentifier` | Required, system-derived | Immutable parent-scoped ordinal `REV-001`, `REV-002`, ...; never client supplied; does not imply current/effective state |
| `CreationCommandId` | Required, command-derived | Same immutable normalized idempotency identity as the first GSKU; used only for replay/reconciliation; never a mutable business field |
| `ProductTypeCode` | Conditional-open | Controlled reference only if approved; not an implementation requirement while open |
| `DosageFormCode` | Controlled reference required when applicable | Published, owner-approved and ProductType-compatible contract |
| `RouteOfAdministrationCodes` | Controlled reference required when applicable | Cardinality/applicability remains an owner decision |
| `StrengthRepresentationType` | Controlled reference required when applicable | Only scalar SIMPLE_STRENGTH/SIMPLE_CONCENTRATION direction may reach approval |
| `StrengthValue` | Conditional-open decimal | Positive and paired with UoM when scalar descriptor applies |
| `StrengthUomCode` | Controlled reference required with value | MOD-0290 owns mapping/business semantics |
| Effective dates / `IsCurrent` | Deferred / prohibited | BL-016; consumers use explicit RevisionId |
| Composition reference | Prohibited | Any ID/FK/reference/placeholder is rejected |

The scalar strength tuple does not identify ingredients, active moieties, formulae or per-ingredient quantities.
Complex or multi-active input may remain Draft only and cannot be submitted for identity approval.

The named first-GSKU step does not expose Product Definition Revision as an independently mutable shell. Its revision
stores only the immutable Global Product parent/revision boundary and technical lifecycle/concurrency/audit fields;
the first meaningful mutable presentation is the GSKU created by the same idempotent command. ProductType,
DosageForm, RouteOfAdministration, Strength, Composition and temporal/current fields remain outside this step.

### GSKU

| Field | Decision | Validation/lifecycle rule |
|---|---|---|
| `ProductDefinitionRevisionId` | Required | Same tenant; immutable after create; approved parent required at approval |
| `CanonicalCode` | Required, system-derived | Matching consumed reservation; immutable; shared tenant namespace; no-reuse |
| `CreationCommandId` | Required, command-derived | Must equal its Revision's immutable value; pair recovery/replay key; cannot be changed after create |
| `PackApplicabilityCode` | Required controlled reference | Explicit published value; silent null is rejected |
| `PackQuantity` | Required positive decimal | Required for every first-phase GSKU because the only initial applicability value is `SCALAR_QUANTITY_APPLIES` |
| `PackUomCode` | Required controlled reference | Required with `PackQuantity`; initial codes are `C62`, `GRM`, `KGM`, `MLT`, `LTR`; incompatible, missing or unvalidated values fail closed |
| `PackApplicabilitySelection` | Required embedded `ReferenceCatalogSelection` | Server-controlled SetCode `pack-applicability`; ValueCode must equal `PackApplicabilityCode`; provider evidence follows the lifecycle below |
| `PackUomSelection` | Required embedded `ReferenceCatalogSelection` | Server-controlled SetCode `uom`; ValueCode must equal `PackUomCode`; provider evidence follows the lifecycle below |
| Packaging level/hierarchy, GTIN, Composition | Deferred/prohibited | BL-017, BL-022 and BL-015 |

Each embedded `ReferenceCatalogSelection` has exactly this persisted shape:

| Field | Source | Lifecycle rule |
|---|---|---|
| `SetCode` | Server-controlled contract | Immutable; `pack-applicability` or `uom`; client override is rejected |
| `ValueCode` | Business selection validated through provider contract | Draft-correctable only through the owning GSKU mutation; free text is forbidden |
| `CatalogVersionId` | Provider-derived identifier | Never accepted from client payload; refreshable while mode is `LATEST`; immutable when `PINNED` |
| `CatalogVersionNumber` | Provider-derived positive integer | Never accepted from client payload; must match the provider version identity |
| `ResolutionMode` | Server-derived enum | `LATEST` in Draft resolution/refresh; transitions to `PINNED` only in future submit/approval flow |
| `ResolvedAtUtc` | Server-derived UTC instant | Records successful provider resolution; never client-authored |

The catalog-selection field shape is closed. A Draft `LATEST` refresh re-resolves the same SetCode/ValueCode and may
replace only provider-derived version/mode/timestamp evidence under expected-version concurrency. Once the future
submit/approval flow changes a selection to `PINNED`, the complete selection is immutable and historical pinned
resolution uses the stored version identity. This embedded value object is not a provider system, Composition FK or
placeholder. Provider-integrated create/lookup/latest refresh/pin validation and submit/approval code remain blocked
until provider B runtime readiness and the separately authorized delivery step close.

### LSKU

| Field | Decision | Validation/lifecycle rule |
|---|---|---|
| `GskuId` | Required | Same tenant; immutable after create; approved parent required at approval |
| `CanonicalCode` | Required, system-derived | Matching consumed reservation; immutable; shared tenant namespace; no-reuse |
| `MarketCode` | Controlled reference required | First LSKU phase uses the universal MOD-0048-FU01 `market` catalog and exact ISO 3166-1 alpha-2 `^[A-Z]{2}$` country codes; no request normalization, free text or country-external region code |
| `LegalEntityId` | Conditional-open | Nullability/applicability and validation topology remain G6 owner decisions |
| `FinishedGoodId` | Prohibited | Direct LSKU-Finished Good relationship is rejected |
| MA / Registered Presentation | Prohibited | BL-019 |

If `LegalEntityId` is approved for an LSKU use case, only the ID is stored. Legal Entity master data is not copied.

### Finished Good

| Field | Decision | Validation/lifecycle rule |
|---|---|---|
| `GskuId` | Required, exactly one | Same tenant; immutable after create; approved parent required at approval |
| `CanonicalCode` | Required, system-derived | Matching consumed reservation; immutable; shared tenant namespace; no-reuse |
| `StewardLabel` | Conditional-open | Not implemented until owner approval; no regulatory/manufacturing meaning |
| `LskuId` | Prohibited | Direct relationship is rejected |
| Composition/manufacturing/quality/GTIN fields | Prohibited/deferred | BL-015, BL-021 and BL-022 |

### MarketTradeName

| Field | Decision | Validation/lifecycle rule |
|---|---|---|
| `LskuId` | Required | Same tenant; immutable after create |
| `MarketCode` | Controlled reference required | Same universal MOD-0048-FU01 `market` catalog and exact ISO 3166-1 alpha-2 country code as the owning LSKU |
| `LanguageCode` | Controlled reference required | Must be an approved language for the market |
| `Name` | Required Unicode text | Draft-editable; an approved value is never overwritten |
| `ProposedEffectiveFrom` | Required for Draft proposal | Not part of approved timeline before approval |
| `EffectiveFrom`, `EffectiveTo` | System-derived on approval | Approved half-open interval `[from,to)` |
| `ReplacesMarketTradeNameId` | Conditional-open | Persistence requires Product Data/Audit owner decision |
| `IsCurrent` | Derived query result | Never persisted |
| `IsUsed` | Prohibited | BL-024 |

### LegacyAlias

| Field | Decision | Validation/lifecycle rule |
|---|---|---|
| `TargetEntityType`, `TargetEntityId` | Required | Allowed MOD-0290 type; target exists in same tenant |
| `RawAliasValue` | Required | Stored unchanged; normalization never overwrites raw evidence |
| `SourceSystemCode` | Conditional-open | Requirement/catalog needs Product Data and Migration Steward approval |
| `AliasTypeCode` | Conditional-open | Target-compatible contract remains open |
| `NormalizedLookupKey` | System-derived | Case/space/punctuation normalization for lookup/duplicate analysis only |
| `AliasStatus` | System-derived | Separate `ACTIVE -> RETIRED`; no Product identity approval lifecycle |
| `CanonicalCode` | Prohibited | Alias is never canonical output |

### CodeReservation

| Field | Decision | Validation/lifecycle rule |
|---|---|---|
| `EntityType` | Required internal type | Allocator/prefix selector only; never narrows uniqueness |
| `ReservedCode` | Required, system-derived | Common ledger unique by tenant + code across entity types; immutable and no-reuse |
| `ReservationState` | System-derived | `RESERVED`, `CONSUMED`, `CANCELLED`, `EXPIRED` |
| `CommandId` / idempotency key | Required, system-derived | Stable command replay cannot allocate or consume twice |
| Reserve/expiry timestamps and trusted actor | System-derived | Actor comes from authenticated context |
| `ConsumedEntityId` | System-derived `0..1` | At most one matching same-tenant code-bearing identity |
| Cancel reason | Conditional-open | Authorization/reason policy must close before cancel is enabled |
| Soft-delete fields | Technical only | Never free a reserved or terminal code |

## 5. Repo Scope

Future implementation under this pack is restricted to MDM-owned paths:

- `services/Diten.MdmService/src/Diten.MdmService.Domain/**`
  - MOD-0290 entities, enums, value objects and repository abstractions.
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/**`
  - Commands, queries, handlers, validators, models and internal orchestration abstractions.
- `services/Diten.MdmService/src/Diten.MdmService.Application/Contracts/**`
  - Only MOD-0290-owned audit/workflow/reference-data consumer abstractions required by accepted G2-G5 contracts.
- `services/Diten.MdmService/src/Diten.MdmService.Persistence/**`
  - MOD-0290 repositories, common ledger, indexes and selected/proven G4 persistence mechanism.
- `services/Diten.MdmService/src/Diten.MdmService.Infrastructure/**`
  - Only approved MOD-0290 consumer adapters/workers within Class C responsibility after G5 selects authenticated
    callback or secure pull/poll decision ingestion.
- `services/Diten.MdmService/src/Diten.MdmService.Api/**`
  - Controllers, DI registration and authorization owned by MOD-0290. An inbound callback endpoint is conditional
    on the G5 owner decision and is not authorized by this draft.
- `services/Diten.MdmService/tests/**`
  - Unit, contract, real-Mongo integration, authorization, tenancy, concurrency, crash/recovery and API tests.

The existing `in-progress` status authorizes only the previously named steps in the opening guard. This authoring
revision does not authorize any additional path or the new named step.

### Named delivery-step allow-list

The full-module scope above is not the allow-list for `Product Definition Revision + First GSKU Draft Foundation`.
If and only if the named step receives separate explicit code-start authorization after its entry gates close, changes
are restricted to these exact paths:

- `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/ProductDefinitionRevision.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/Gsku.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/ValueObjects/ReferenceCatalogSelection.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Enums/AuditAggregateType.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Enums/ProductAuditOperation.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Enums/ReferenceCatalogResolutionMode.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/IProductDefinitionRevisionRepository.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/IGskuRepository.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Commands/CreateFirstGskuDraftCommand.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Commands/UpdateGskuDraftCommand.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Handlers/CommandHandlers/CreateFirstGskuDraftHandler.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Handlers/CommandHandlers/UpdateGskuDraftHandler.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Validators/CreateFirstGskuDraftValidator.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Validators/UpdateGskuDraftValidator.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/ProductItemSkuMasterModels.cs`,
  named-step DTO additions only.
- `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/ProductDefinitionRevisionRepository.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/GskuRepository.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/AuditIntentDeliveryRepository.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Persistence/DependencyInjection.cs`, registration changes only.
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ProductItemSkuMasterMongoTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/AuditIntentDeliveryMongoTests.cs`

No wildcard expansion from the full-module scope is implied. In particular, `Diten.MdmService.Api/**`,
`Diten.MdmService.Infrastructure/**`, provider-domain code, configuration, hosted-service registration, workflow,
frontend and gateway paths are not in this named-step allow-list.

## 6. Protected Paths

- `.antigravity/**`
- `AGENTS.md`
- `docs/System Capability & Implementation Blueprint - master 8.1.xlsx`
- `docs/product-backlog.md`
- `execution/registries/**`
- `execution/portfolio/delivery-capability-packs/**`
- `execution/domains/master-data-management/domain-contracts/**`
- `execution/domains/platform-shared-services/**`
- All other domains' `execution/domains/**` and `services/**`
- `services/Diten.Platform/**`, including Reference Data, Workflow and Audit provider code
- `services/Diten.AuthService/**`
- `services/Diten.DevEnablementService/**`
- `frontend/**`
- `gateway/**`; Ocelot routes remain integration-agent owned and are not part of this pack
- `services/Diten.MdmService/src/Diten.MdmService.Infrastructure/Middleware/TenantResolutionMiddleware.cs` unless a
  separately owned and approved shared-infrastructure delivery explicitly authorizes a change
- Archive/frozen paths

Provider Class B work requires a separately approved owner-domain artifact after ownership decisions; this pack does
not authorize or create one.

## 7. Dependencies

### Direct Master 8.1 dependencies

| Dependency | MOD-0290 boundary | Gate treatment |
|---|---|---|
| MOD-0003 | Data Contract Registry | G7 approved scoped deferral for internal-only phase-one work |
| MOD-0040 | Technical Canonical ID & Correlation Standard | Reconciliation or approved scoped waiver before a dependent implementation step starts and before readiness |
| MOD-0021 | Central audit append/query/retention | MDM-local atomicity is MOD-0290 C; provider B only if central contract changes |
| MOD-0252 ERP | External SoR/feed | G7 scoped deferral; no ERP runtime scope |
| MOD-0253 PLM | External SoR/feed | G7 scoped deferral; no PLM runtime scope |

### Delivery-derived dependencies

| Dependency | Delivery classification and closure |
|---|---|
| MOD-0048 / reconciled Reference Data owner | Shared provider gaps are B; six-family MOD-0290 consumption is C; G2/G3 must close |
| MOD-0023 Workflow | Provider trusted-actor/S2S/idempotency/recovery gaps are B; MOD-0290 lifecycle/selected decision ingestion/reconciliation is C; G5 must close |
| MOD-0220 Legal Entity | In-process A is possible; HTTP/durable provider change is B; LSKU binding/revalidation is C; only the LegalEntityId slice is gated by G6 |

A/B/C/D classification changes delivery responsibility only. It does not waive any dependency or technical gate.

### Delivery-derived authorization and onboarding dependencies

| Owner boundary | Required onboarding evidence before endpoint enablement/exposure/readiness | MOD-0290 boundary |
|---|---|---|
| Auth owner | Approved candidate permission keys are present in the permission catalog/seed and are assignable through the authorized role model | MOD-0290 declares and enforces endpoint permissions; it does not seed or change Auth provider code |
| Platform owner | A matching `ModuleCatalogItem` and tenant module entitlement/onboarding path make the module reachable only to entitled tenants | MOD-0290 consumes the effective entitlement decision; it does not change Platform catalog or entitlement provider code |

These are delivery-derived onboarding prerequisites, not direct Master 8.1 edges and not provider implementation ACs
owned by this pack. Missing permission/catalog/entitlement onboarding can leave otherwise authorized service endpoints
unreachable. Exact owner-approved evidence must therefore close before entitlement-dependent endpoint enablement,
gateway/public exposure or readiness; it is not a blanket prerequisite for an unrelated authorized implementation step.

### G5 inbound workflow-decision boundary

The current MDM tenant middleware has no safe inbound workflow-callback/tenant-resolution contract. Before decision
ingestion code starts, the Workflow, Product Data, MDM and Security owners must select either:

- an authenticated inbound callback with trusted service identity, distinct delegated human-decision identity,
  trusted tenant binding, least-privilege authorization, idempotency, replay protection and reconciliation; or
- secure pull/poll plus reconciliation using an approved least-privilege S2S credential and workflow/version binding.

Raw request headers or self-declared body values never establish trusted tenant context. Adding an `/api/internal`
bypass is not selected by this pack. If shared MDM middleware must change, it requires a separate owner,
classification and approved shared-infrastructure delivery decision; it is not automatic MOD-0290 Class C scope.

## 8. Runtime Constraints

- MongoDB, single database and tenant-owned `EntityBase` conventions apply.
- Every repository read/write/reference/index includes trusted `TenantId`; cross-tenant access returns non-leaking 404.
- Write payloads cannot contain `TenantId`; attempted supply is rejected before command handling.
- Soft delete never substitutes for retirement, releases a code or removes historical evidence.
- Every mutable aggregate uses expected-version conditional updates; last-write-wins is prohibited.
- The existing generic MDM repository does not yet satisfy that rule. Module Pack approval requires the mechanism and
  test plan to be specified; readiness requires an implemented expected-version mechanism and real-Mongo concurrency proof.
- Canonical code is allocated only by the common ledger. Direct/manual assignment and reservation bypass are rejected.
- Canonical-code namespace is tenant-wide across Global Product, GSKU, LSKU and Finished Good.
- The common ledger enforces one unique persistence/index invariant on `TenantId + ReservedCode`, independent of
  `EntityType`; `EntityType` may select a fixed prefix/allocator only and is not an index partition.
- No standalone, replica-set, sharded or transaction-ready Mongo topology is assumed without evidence.
- Reference-data, workflow and Legal Entity provider failures follow the owner-approved fail-closed contracts.
- External ERP/PLM and runtime publication behavior is prohibited under the current G7 deferral.
- Product Definition Revision ordinal allocation is tenant- and parent-scoped. Persistence enforces a unique
  `TenantId + GlobalProductId + RevisionIdentifier` invariant plus a tenant-first parent-list index. Deleted ordinals
  remain unavailable; soft delete never permits ordinal reuse.
- Concurrent/retried `CreateFirstGskuDraft` calls use one atomic/idempotent parent-scoped allocator that cannot issue
  the same ordinal twice and cannot advance into a second revision for the same command replay. The canonical-code
  counter is not reused for revision ordinals. Atomicity here is limited to the allocator's conditional Mongo mutation;
  no cross-collection transaction is assumed.
- Revision and GSKU each enforce a unique `TenantId + CreationCommandId` recovery index. Replay first resolves both
  records by this shared immutable key, verifies that parent, ordinal, reservation and pair identities agree, and then
  resumes the incomplete stage. It never creates a replacement parent/child pair for the same command.
- GSKU persistence includes a tenant-first non-unique parent index on
  `TenantId + ProductDefinitionRevisionId`, a unique `TenantId + CodeReservationId` binding index, immutable
  parent/code references and expected-version conditional mutation. A collection-local unique
  `TenantId + CanonicalCode` index is defense in depth only; the common CodeReservation ledger remains the shared
  namespace authority.
- Existing repository implementations are reuse evidence, not copy templates. The named step must prove tenant
  enforcement, soft-delete behavior and expected-version predicates in the actual Mongo mutation filters.
- `CreationCommandId` coordinates the combined Revision/GSKU recovery while CodeReservation retains its own durable
  `ReservationCommandId`, `ConsumeCommandId`, expected-version and binding-state rules. The GSKU consume identity is
  deterministically linked to the creation command; replay reuses the existing consumed/pending/confirmed reservation
  outcome and never reserves or consumes a second code.
- A partial or ambiguous combined write is `FIRST_GSKU_DRAFT_RECONCILIATION_REQUIRED`, never success. Recovery under
  the same command may complete the missing Revision/GSKU/binding/audit stage only after all already-persisted facts
  match; any mismatch fails closed and cannot manufacture a duplicate ordinal, Revision or GSKU.
- MOD-0048-FU01 pack approval does not itself prove provider runtime readiness. Until real provider B evidence and
  consumer delivery authorization close, provider-dependent GSKU create/lookup, Draft `LATEST` resolution/refresh,
  `PINNED` validation, submit/approval and production reference validation remain prohibited.
  Hardcoded/free-text/Mock/Disabled fallback is never allowed.
- `AuditAggregateType` and `ProductAuditOperation` are extended append-only for Product Definition Revision and GSKU;
  existing numeric values are never reordered, renumbered or reused. The delivery repository must explicitly add the
  new collections to discovery, claim, acknowledgement, failure/dead-letter and compaction paths.
- Every successful Revision/GSKU mutation persists its local audit intent in the same approved local consistency
  boundary as the aggregate mutation. Merely implementing `IAuditIntentAggregate` does not make the existing worker
  support the new aggregate.

## 9. Layout & Shell Contract

- The completed/authorized internal foundation remains backend-only. The planned `Global Product Register Exposure & UI`
  named step uses `_LayoutTenantShell`; this preparation does not authorize its code-start.
- The current runtime `CreateGlobalProductDraftRequest` contains only reservation/version/idempotency transport facts;
  the planned named step adds the user-approved required `GlobalProductName`. It is the only user-entered business
  field. `TenantId`, system-generated `CanonicalCode`, reservation, version, idempotency and audit facts do not count,
  so `form_field_count: 1` and `golden_reference: slim` are the locked decision (`1 <= 8`).
- The real `GoldenReferenceSlim` contract is the applicable baseline: DataTable v2 marker, skeleton loader, inline
  filters, Index-hosted create surface, same-origin MVC proxy, localized script bridge and tenant shell. Compact's
  separate Create/Edit/Details form is not selected because the field threshold is not met.
- The initial register plans list, paging/search/filter, read-only details and a one-field create surface for
  `GlobalProductName`. Premium SweetAlert2 confirms create before submission; `CanonicalCode` is displayed read-only
  only after server allocation. Edit is deliberately unavailable because post-create name mutability is unresolved and
  the foundation has no update command. No UI may synthesize one.
- Destructive or lifecycle confirmation, if a later authorized command exists, must use the Premium SweetAlert2
  contract. The named step adds no delete, bulk-delete, submit, approval or retirement action.

## 10. Backend File Convention

Future backend work must retain the service's five-layer architecture and action-based CQRS separation:

```text
Application/Features/ProductItemSkuMaster/
|-- Commands/                    # one sealed command record per file
|-- Queries/                     # one sealed query record per file
|-- Handlers/
|   |-- CommandHandlers/         # one sealed handler per file
|   `-- QueryHandlers/           # one sealed handler per file
|-- Validators/                  # one validator per command type
`-- ProductItemSkuMasterModels.cs
```

- Command: `{Verb}{Aggregate}Command`; query: `Get{Aggregate}{Qualifier}Query`.
- Handler: `{Verb}{Aggregate}Handler`; no `Command`, `Query` or `Request` suffix.
- Controllers contain no business logic and delegate through MediatR.
- Commands/queries return the service-standard `Response<T>` envelope; mutation commands use
  `Response<NoContent>` where no representation is returned.
- Domain has no Mongo driver dependency; Persistence owns Mongo implementation.
- Provider calls are behind Application abstractions and MDM-owned Infrastructure adapters.

## 11. Frontend File Contract

No frontend files are authorized by the completed internal foundation or by this preparation. The planned named step,
after its gates close and explicit code-start is granted, is limited to:

```text
frontend/Diten.Web/Controllers/GlobalProductsController.cs
frontend/Diten.Web/Models/GlobalProducts/**
frontend/Diten.Web/Views/MasterDataManagement/GlobalProducts/**
frontend/Diten.Web/Views/Shared/Components/**                 # reuse only; no global contract rewrite
frontend/Diten.Web/wwwroot/assets/js/MasterDataManagement/GlobalProducts/**
frontend/Diten.Web/Resources/Views/MasterDataManagement/GlobalProducts/**
frontend/Diten.Web/Resources/SharedResource.*.resx           # keys only when shared ownership accepts them
```

The implementation must use seven-locale RESX resources plus `window.L10n`, DataTable v2 with skeleton/error/empty
states, localized filters/details and a one-field `GlobalProductName` create offcanvas with Premium SweetAlert2
confirmation. `_DetailsQuickView.cshtml` is read-only and no edit action is rendered. The browser calls only same-origin MVC
actions; the MVC proxy forwards the bearer token and trusted tenant header to Gateway `5000`. JavaScript never calls
the Gateway or MDM service port directly, and lookup input is never hardcoded.

Outside a separately authorized named-step implementation, the following remain prohibited:

- `frontend/Diten.Web/Views/**`
- `frontend/Diten.Web/Controllers/**`
- `frontend/Diten.Web/wwwroot/**`
- menu/navigation, DataTable, form, localization resource or same-origin proxy work

The API must nevertheless return stable machine-readable failure codes so a later localized UI can map messages
without parsing English text.

## 12. Validation Rules

| Field/operation | Required | Rule | Persistence/pre-check |
|---|---:|---|---|
| Client `TenantId` | No; forbidden | Reject when supplied; never ignore | Pre-handler payload/contract validation |
| Direct `CanonicalCode` | No; forbidden | Only reservation consume may assign | DTO absence plus unknown/forbidden-field validation |
| `GlobalProductName` on Global Product create | Yes | Market-independent internal/global product-family name; reject missing, empty or whitespace-only input | Persist on the Global Product created from the matching reservation; maximum length, normalization and uniqueness are implementation-time decisions, not inferred constraints |
| `ExpectedVersion` | Yes on mutation | Must match stored version | Conditional update; mismatch = 409 |
| Parent ID | Yes for child | Same tenant, exists, non-deleted and not retired | Repository referenceability check |
| Revision creation surface | Combined only | No standalone create/edit; revision is created only by idempotent `CreateFirstGskuDraft` | Command/handler absence plus contract tests |
| Revision identifier | System-derived | Parent-scoped immutable `REV-001`, `REV-002`, ...; no client assignment or reuse | Atomic allocator plus unique tenant/parent/identifier index |
| `CreationCommandId` | Yes | Same normalized immutable value on Revision and GSKU; replay/recovery key; conflicting pair fails closed | Unique tenant + command indexes and pair reconciliation |
| Approval parent | Yes at approval | Required parent is `IDENTITY_APPROVED` | Revalidate in approval command |
| Code reservation | Yes for code-bearing create | Same tenant/type/code; one stable command; at most one identity | Common-ledger check and G4 consistency proof |
| `PackApplicabilityCode` | Yes for GSKU | User-approved initial SetCode `pack-applicability`, ValueCode `SCALAR_QUANTITY_APPLIES`; no null or local fallback | G2/G3 provider contract still required |
| Pack quantity/UoM | Yes for first-phase GSKU | Positive quantity plus compatible UoM from user-approved `uom` catalog | G2/G3 provider contract and MOD-0290 compatibility validation |
| Draft parent lifecycle | Yes | Same-tenant existing non-deleted non-retired parent; `IDENTITY_APPROVED` is not required for Draft create | Non-leaking parent lookup; approval revalidation deferred |
| Selection SetCode | Server-controlled | Exact `pack-applicability` or `uom`; client input/override is forbidden | DTO absence plus unknown/forbidden-field validation |
| Selection provider evidence | Server-derived | `CatalogVersionId`, positive `CatalogVersionNumber`, `ResolutionMode`, `ResolvedAtUtc` come only from provider result | Client-supplied evidence rejected before handling |
| Draft selection lifecycle | Draft | `LATEST` may refresh the same SetCode/ValueCode evidence under expected-version concurrency | Real provider required; no local fallback |
| Pinned selection lifecycle | Submit/approval | Complete selection becomes `PINNED` and immutable; pinned version must remain historically resolvable | Future provider-ready submit/approval step |
| UoM precision | Yes | `C62` maximum 0 decimals; `GRM`, `KGM`, `MLT`, `LTR` maximum 3; excess precision is rejected, never silently rounded | Provider metadata plus MOD-0290 semantic validation |
| ProductType | Conditional-open | Do not require or implement until owner approval | G2 field-contract decision |
| Scalar strength tuple | Conditional | Positive value + compatible UoM + approved representation | Complex/multi-active submit is blocked |
| Market/language | Yes where specified | Published approved code; language valid for market | Owner-approved contract; fail closed |
| MarketTradeName period | On approval | `[from,to)` and no overlap; no-gap validation applies only if the owner approves a no-gap policy | Timeline query, approved temporal granularity and atomic transition proof |
| Legacy raw alias | Yes | Preserve raw value exactly; normalization separate | Exact and normalized collision checks |
| LegalEntityId | Conditional-open | Current same-tenant referenceability at approved validation points | G6 selected contract |
| Composition/LSKU-FG/GTIN fields | Forbidden | Reject request and do not persist | Schema/DTO/validator negative tests |

## 13. Failure Path to Verify

| Failure code | Trigger | Expected behavior |
|---|---|---|
| `TENANT_ID_CLIENT_INPUT_FORBIDDEN` | Client supplies `TenantId` | 400; command not executed |
| `CANONICAL_CODE_ASSIGNMENT_FORBIDDEN` | Client/manual path supplies code | 400; no reservation or identity mutation |
| `GLOBAL_PRODUCT_NAME_REQUIRED` | Global Product create omits `GlobalProductName` or supplies empty/whitespace-only text | 400 before reservation/consume; no Global Product, audit intent or reservation mutation |
| `CODE_RESERVATION_REQUIRED` | Identity create lacks matching consumed flow | 409; identity not committed |
| `CODE_RESERVATION_MISMATCH` | Tenant/type/code/command mismatch | 409; no identity; evidence retained |
| `CODE_RESERVATION_ALREADY_TERMINAL` | Invalid consume/cancel/expire replay | Idempotent replay or 409 according to same-command versus conflicting-command rules; no reuse |
| `CONCURRENCY_CONFLICT` | Expected version stale | 409; no partial mutation or lost audit intent |
| `PARENT_NOT_FOUND` | Global Product or Revision is missing, cross-tenant or soft-deleted | Same non-leaking 404 for all three cases; no existence disclosure or mutation |
| `PARENT_NOT_IDENTITY_APPROVED` | Child approval before parent approval | 409; child remains pending/draft as applicable |
| `PARENT_RETIRED_NOT_REFERENCEABLE` | New child/approval references retired parent | 409; no change |
| `REVISION_ORDINAL_CONFLICT` | Concurrent allocation cannot complete the stable command outcome | Idempotent retry/reconciliation or stable 409; no duplicate/reused ordinal and no second revision for replay |
| `FIRST_GSKU_DRAFT_RECONCILIATION_REQUIRED` | Combined Revision/GSKU write is ambiguous or partial | Non-success recovery state; same command resumes the same pair; no independent empty-revision success |
| `CREATION_COMMAND_PAIR_CONFLICT` | Same command resolves to mismatched parent, Revision, GSKU or reservation facts | 409; fail closed; no replacement pair, ordinal or code is created |
| `REFERENCE_CATALOG_EVIDENCE_CLIENT_OVERRIDE_FORBIDDEN` | Client supplies SetCode, version identity/number, mode or resolved timestamp | 400; command not executed; provider evidence is never trusted from payload |
| `REFERENCE_SELECTION_MODE_INVALID` | Draft attempts `PINNED`, or a pinned selection is refreshed/changed | 409; selection and aggregate remain unchanged |
| `REFERENCE_VERSION_PIN_CONFLICT` | Required pinned version/value cannot resolve historically | 409; never falls back to `LATEST` |
| `PACK_QUANTITY_PRECISION_EXCEEDED` | Quantity scale exceeds the resolved UoM maximum | 400; no silent rounding or mutation |
| `DEPENDENT_IDENTITIES_EXIST` | Parent retirement with approved children | 409; no cascade |
| `DRAFT_CHILD_CANCELLATION_REQUIRED` | Parent retirement with open Draft child | 409 until controlled cancellation |
| `SELF_APPROVAL_FORBIDDEN` | Maker and approver are same canonical human subject | 403/409; no lifecycle change |
| `REQUIRED_WORKFLOW_NOT_APPROVED` | Missing workflow or non-approved terminal decision | 409/503; approval fails closed |
| `REFERENCE_DATA_CONTRACT_UNAVAILABLE` | Required published version/value cannot resolve | 503 or stable dependency failure; submit/approval blocked |
| `PACK_APPLICABILITY_REQUIRED` | GSKU applicability is null | 400 |
| `COMPLEX_STRENGTH_APPROVAL_FORBIDDEN` | Complex/multi-active Draft is submitted | 409; remains Draft |
| `DIRECT_LSKU_FINISHED_GOOD_FORBIDDEN` | Direct relation supplied | 400; no persistence |
| `COMPOSITION_REFERENCE_FORBIDDEN` | Composition field supplied | 400; no persistence |
| `MARKET_TRADE_NAME_IMMUTABLE` | Approved `Name` is overwritten | 409; old row unchanged |
| `MARKET_TRADE_NAME_PERIOD_OVERLAP` | Approved timeline would overlap | 409; replacement transaction has no effect |
| `MARKET_TRADE_NAME_REPLACEMENT_NOT_APPROVED` | Draft proposal tries to close old row | 409; old row unchanged |
| `MARKET_TRADE_NAME_GAP_FORBIDDEN` (conditional candidate) | Approved transition creates a gap only after an owner-approved no-gap policy exists | 409; timeline unchanged; the code/behavior is not mandatory while the policy remains open |
| `MARKET_TRADE_NAME_USAGE_DEFINITION_DEFERRED` | Caller depends on synthetic usage flag | 409/not-supported; no `IsUsed` persisted |
| `LEGACY_ALIAS_COLLISION` | Exact/normalized collision requires stewardship | 409 with collision class; raw evidence preserved |
| `LEGAL_ENTITY_NOT_REFERENCEABLE` | Applicable LSKU reference fails G6 validation | Stable non-leaking failure; LSKU binding/approval blocked |

Provider unavailable, timeout, invalid credential, malformed response, wrong tenant, duplicate callback, crash before
delivery and central-accepted-before-local-ack paths must also be covered by G2-G6 contract tests.

## 14. Authorization Convention

- Policy: tenant API controllers require `[Authorize]`.
- Repository-aligned direction: lowercase dotted `mdm.{resource}.{action}`; the current MDM evidence is
  `mdm.legal-entities.{action}`. Exact MOD-0290 keys below are onboarding candidates, not final seeded permissions.
- Candidate resource/action matrix, to be approved and onboarded before entitlement-dependent endpoint enablement or readiness:

  | Resource | Meaningful candidate actions |
  |---|---|
  | `mdm.global-products` | `read`, `create`, `update`, `submit`, `approve`, `retire` |
  | `mdm.product-definition-revisions` | `read`, `create`, `update`, `submit`, `approve`, `retire` |
  | `mdm.gskus` | `read`, `create`, `update`, `submit`, `approve`, `retire` |
  | `mdm.lskus` | `read`, `create`, `update`, `submit`, `approve`, `retire` |
  | `mdm.finished-goods` | `read`, `create` only for `Finished Good Draft Foundation`; later actions require a separate approved slice and are not current candidates |
  | `mdm.market-trade-names` | `read`, `create`, `update`, `submit`, `approve`, `replace`, `retire` |
  | `mdm.legacy-aliases` | `read`, `attach`, `retire` |
  | `mdm.code-reservations` | `read`, `reserve`, `cancel` |
  | `mdm.product-item-sku-master` | `read`, `export` |

- No broad `manage` permission is introduced. Reservation consume is an internal part of identity creation and expiry
  is a controlled system action; neither is exposed as a general user permission by this draft.
- `delete` and `bulk-delete` are deliberately absent. Technical soft-delete is not a user business action; governed
  business lifecycle termination uses `retire` (or the aggregate-specific controlled cancellation rule).
- Permission catalog/seed ownership remains with the Auth owner. Final key names, role mapping and Platform
  module-entitlement onboarding are external readiness evidence, not provider-code implementation scope of this pack.
- Product Data Steward may create/correct/submit but may not approve its own record.
- Product Identity Approver may approve/reject only with trusted authenticated human-subject evidence.
- Maker-checker/SoD is enforced by comparing canonical human subjects in the domain transition, not merely by
  permission assignment. The current `platform_admin` permission bypass cannot override this domain invariant.
- Transport service identity is distinct from delegated human decision identity.
- Reservation-cancel authorization/reason policy, controlled system-expiry policy and break-glass/override behavior
  remain open Security/Product Data decisions; no break-glass behavior is implied by this draft.

### Named step permission gate — `Global Product Register Exposure & UI`

The existing pack declares `mdm.global-products.read` and `mdm.global-products.create` only as onboarding candidates;
repository inspection does not prove that either is currently cataloged, seeded or grantable. This preparation therefore
does not invent or treat a key as live. The exact endpoint mapping, once Auth/Platform owners accept those names, is:

| Endpoint class | Required accepted key | Notes |
|---|---|---|
| list, detail, selector | `mdm.global-products.read` | Same key for the minimum read-only selector; no separate lookup key is introduced |
| reservation-for-create, create draft | `mdm.global-products.create` | Both steps are one user create capability; no general reservation permission is exposed |
| edit | Not mapped | No update command or approved mutable field exists, so no endpoint/action is enabled |

MDM declares endpoint needs and enforces accepted keys. Platform reconciles module catalog/tenant entitlement. The
Diten.AuthService catalog/seed/grant path remains the permission system of record. No MDM change may seed, grant or
silently substitute a permission, and no `platform_admin` bypass may defeat tenant or domain invariants. Provider-owner
catalog/seed/grant and assignability evidence is not an MDM backend/UI implementation-start blocker for this named
step. It is a hard fail-closed endpoint production/user-enablement gate: without that evidence, the UI/API must not be
declared production-ready or enabled for users.

## 15. Gateway / API Routing Decision

Gateway change is not authorized by this backend-only pack. No Ocelot catch-all route may expose MOD-0290, and no
gateway route may be added or changed under this pack. The surfaces below are service-contract drafts only; public,
browser and gateway exposure is out of scope.

### Draft internal service surface

The table below remains the eventual full-module API design only. It is not part of `Product Definition Revision +
First GSKU Draft Foundation`: that named step authorizes no controller or endpoint, and its combined
`CreateFirstGskuDraft` application behavior must not be split into standalone Revision/GSKU API operations.

| Resource | Draft API surface | Commands/queries |
|---|---|---|
| Global Product | `/api/global-products` | create through reservation flow, update Draft, get/list/search, submit, apply decision through the selected G5 boundary, retire |
| Product Definition Revision | `/api/product-definition-revisions` | create, update Draft, get/list, submit, apply decision through the selected G5 boundary, retire |
| GSKU | `/api/gskus` | create through reservation flow, update Draft, get/list, submit, apply decision through the selected G5 boundary, retire |
| LSKU | `/api/lskus` | create through reservation flow, update Draft, get/list, submit, apply decision through the selected G5 boundary, retire |
| Finished Good | `/api/finished-goods` | current planned named step: Draft create, get/list and GSKU selector only; update/rebind/submit/decision/retire require a separate approved slice |
| MarketTradeName | `/api/market-trade-names` | propose, update Draft, history/timeline, submit, apply replacement decision through the selected G5 boundary, retire |
| LegacyAlias | `/api/legacy-aliases` | attach, retire, exact/normalized lookup |
| CodeReservation | `/api/code-reservations` | reserve, status, consume through identity create, cancel; expiry is controlled system action |
| Cross-identity lookup/export | `/api/product-item-sku-master` | permissioned canonical-code lookup, alias lookup and bounded internal export; no gateway/public exposure |

Exact service routes, verbs and request/response schemas for the eventual full-module table above remain open; the
narrow Global Product exposure plan below is exact but still requires its named code-start gates.
The callback-versus-pull/poll decision boundary must be finalized before any workflow-dependent slice starts.
Any later browser/public exposure and explicit Ocelot route require separately authorized integration/UI delivery;
this pack never modifies `gateway/**` and does not rely on a catch-all route.

### Named step API and selector contract — `Global Product Register Exposure & UI`

This subsection closes the delivery plan only; it does not authorize controllers, queries or routes. The controller is
`GlobalProductsController : CustomBaseController`, rooted at `/api/global-products`, decorated with `[Authorize]` and
the accepted action permission. It maps MediatR `Response<T>` through `CustomBaseController`; it does not repeat tenant,
validation, reservation or business authorization logic.

| Verb and exact route | Request contract | `Response<T>` data contract | CQRS mapping | Permission |
|---|---|---|---|---|
| `GET /api/global-products?pageNumber={n}&pageSize={n}&search={text}&lifecycleStatus={value}` | Query string only; bounded paging and allow-listed filter | `GlobalProductPageResponse` containing `Items: GlobalProductListItemResponse[]`, `PageNumber`, `PageSize`, `TotalCount`; item = `Id`, `CanonicalCode`, `GlobalProductName`, `LifecycleStatus` | new `GetGlobalProductsPageQuery` / `GetGlobalProductsPageHandler`; new tenant-scoped repository page/search operation | accepted `mdm.global-products.read` |
| `GET /api/global-products/{id:guid}` | Route ID | `GlobalProductDetailResponse`: `Id`, `CanonicalCode`, `GlobalProductName`, `LifecycleStatus`, read-only `Version` and audit timestamps | new `GetGlobalProductByIdQuery` / handler over existing tenant-scoped get-by-ID behavior | accepted `mdm.global-products.read` |
| `GET /api/global-products/selector?pageNumber={n}&pageSize={n}&search={text}` | Query string only; bounded search | `GlobalProductSelectorPageResponse`; item is exactly `Id`, `CanonicalCode`, `GlobalProductName` | new `GetGlobalProductSelectorQuery` / handler and minimum-projection repository operation | accepted `mdm.global-products.read` |
| `POST /api/global-products/code-reservations` | Empty body; idempotency key supplied by the same-origin server flow, never as a user form field | existing reservation result: `ReservationId`, system `CanonicalCode`, `Version` | existing `ReserveCanonicalCodeCommand` fixed server-side to `GlobalProduct`; controller accepts no entity type or code | accepted `mdm.global-products.create` |
| `POST /api/global-products/drafts` | `CreateGlobalProductDraftApiRequest`: required `GlobalProductName` plus server-held `ReservationId`, `ExpectedReservationVersion`, `IdempotencyKey`; no `TenantId` or `CanonicalCode` | Global Product draft result including `Id`, server `CanonicalCode`, `GlobalProductName`, lifecycle/binding result | extended `CreateGlobalProductDraftCommand` / `CreateGlobalProductDraftRequest`; existing reservation/consume/no-reuse behavior remains unchanged | accepted `mdm.global-products.create` |

The list/page and selector queries are new named-step scope because neither the current application nor
`IGlobalProductRepository` provides paging/search/projection. They must apply tenant and soft-delete predicates in Mongo,
use deterministic ordering and return the same non-disclosing 404 for missing, deleted and cross-tenant detail IDs.
Search is server-side and limited to `GlobalProductName` plus exact/prefix `CanonicalCode`; no unbounded scan or client
filtering is accepted.

The user-approved `GlobalProductName` is the required persisted display field and is not a placeholder or contract alias.
It never replaces or relabels `CanonicalCode`, and it carries no market, authorization, LSKU or MarketTradeName meaning.
The same-origin MVC create flow first validates its sole user field, then obtains the Global Product reservation
server-side, invokes the create-draft command with the server-held reservation facts and returns the server-generated
`CanonicalCode`. Reservation/version/idempotency facts are never rendered as user fields. A validation failure occurs
before reservation; an ambiguous post-reservation failure retains the existing reconciliation/no-reuse behavior.

The ABB UI consumes only `GET /api/global-products/selector` through its same-origin proxy. It does not copy Global
Product query logic or business rules, and it renders only `Id`, `CanonicalCode` and `GlobalProductName`. UUID entry,
fake selectors and hardcoded product lists are forbidden.

Gateway delivery is owned only by `integration-agent`. The planned Ocelot pair maps upstream
`/api/global-products` to downstream `/api/global-products` and upstream
`/api/global-products/{everything}` to downstream `/api/global-products/{everything}`. Both allow only `GET`, `POST`
and `OPTIONS`, use the current MDM route host/scheme convention and downstream port `5059`. `PUT`, `PATCH`, `DELETE`
and a general CodeReservation route are not enabled. Frontend calls Gateway `5000`; this pack never modifies
`gateway/Diten.ApiGateway/**`.

## 16. Acceptance Criteria

### Identity, tenant and aggregate invariants

- [ ] Master 8.1 ranges and the canonical registry identity are referenced directly; Master 7 output is not used as alignment proof.
- [ ] All eight aggregate roots are implemented only within MDM ownership; excluded field groups are absent from schema and write contracts.
- [ ] Every create obtains `TenantId` only from trusted server context; a supplied `TenantId` fails before handling.
- [ ] Every get/list/search/update/reference/index is tenant-scoped; cross-tenant ID/code/alias access is non-leaking 404.
- [ ] Technical soft delete and business retirement remain distinct; deletion never frees a code or erases required history.
- [ ] Expected-version conditional updates cover edit, submit, decision, retirement, timeline, alias and reservation transitions.
- [ ] Finished Good has exactly one GSKU; GSKU permits zero-to-many Finished Goods.
- [ ] Direct LSKU-Finished Good and Composition references are rejected at DTO/schema/validator boundaries.
- [ ] `RevisionIdentifier` is required/system-derived, immutable and parent-scoped as `REV-001`, `REV-002`, ...;
      standalone Revision create/edit does not exist in the named step.
- [ ] `CreateFirstGskuDraft` idempotently creates one explicit Revision and its first GSKU as one reported outcome;
      replay never allocates another ordinal, reservation or GSKU, and partial writes are recoverable under the same command.
- [ ] Revision and GSKU persist the same immutable normalized `CreationCommandId`; unique tenant + command indexes
      resolve replay to the original pair and mismatched persisted facts fail closed.

### Canonical code and reservation ledger

- [ ] Global Product, GSKU, LSKU and Finished Good share one tenant-wide code namespace in the common ledger.
- [ ] The common ledger has one required unique persistence/index constraint on `TenantId + ReservedCode` across all
      entity types; helper/performance indexes may be finalized during authorized implementation and proven before readiness.
- [ ] Every successful code-bearing identity has exactly one matching consumed reservation; each reservation has at most one identity.
- [ ] Identity creation and matching consume are one stable, idempotent success flow; no identity commits without durable consume.
- [ ] Direct assignment, manual override and every reservation-bypass path fail closed.
- [ ] Entity type can select prefix/allocator but cannot narrow tenant + code uniqueness.
- [ ] Duplicate reserve/consume replay returns the original outcome; conflicting command cannot allocate or bind again.
- [ ] An ambiguous identity write after durable consume remains reconciliation-pending and is never automatically
      burned from an absence lookup; its code is never reused. A terminal burned reservation without identity requires
      a deterministically proven pre-insert failure or a separately owner-approved persistent fence/transaction path;
      reason, critical audit, retry/reconciliation and recovery disposition are durable.
- [ ] Cancelled, expired, consumed, retired and soft-deleted codes remain permanently unavailable.
- [ ] First-GSKU creation consumes only a matching same-tenant `CodeBearingEntityType.Gsku` reservation and preserves
      the existing pending-write/confirm reconciliation rules; revision ordinal allocation never uses the canonical-code counter.
- [ ] `CreationCommandId` coordinates pair recovery without replacing `ReservationCommandId`, `ConsumeCommandId`,
      expected reservation version or binding-state reconciliation; replay never reserves or consumes a second code.

### Product Definition Revision + First GSKU Draft Foundation

- [ ] The named step has no new ID and uses only the exact Section 5 allow-list; full-module scope does not widen it.
- [ ] Global Product and Revision parents are same-tenant, existing, non-deleted and non-retired at Draft create;
      parent approval is deferred to future submit/approval revalidation.
- [ ] Missing, cross-tenant and soft-deleted parents return the same non-leaking `404 PARENT_NOT_FOUND`; retired parents
      return stable `409 PARENT_RETIRED_NOT_REFERENCEABLE`.
- [ ] Persistence enforces unique `TenantId + GlobalProductId + RevisionIdentifier`, a tenant-first parent-list index,
      ordinal no-reuse after soft delete and concurrent/idempotent parent-scoped allocation.
- [ ] GSKU parent/reservation indexes, immutable references and expected-version conditional mutations are proven in Mongo.
- [ ] Every first-phase GSKU has explicit `SCALAR_QUANTITY_APPLIES`, positive `PackQuantity` and one of `C62`, `GRM`,
      `KGM`, `MLT`, `LTR`; no quantity-free, kit or hierarchy placeholder exists.
- [ ] GSKU persists exactly two embedded selections, `PackApplicabilitySelection` and `PackUomSelection`, each with
      `SetCode`, `ValueCode`, `CatalogVersionId`, `CatalogVersionNumber`, `ResolutionMode` and `ResolvedAtUtc`.
- [ ] SetCode and all provider evidence fields are server-controlled/server-derived; client override fails before handling.
- [ ] Draft selections use refreshable `LATEST`; the future submit/approval transition produces immutable `PINNED`
      selections whose stored versions remain historically resolvable and never fall back to latest.
- [ ] Provider-integrated create/lookup, `LATEST` resolution/refresh, `PINNED` validation and submit/approval remain
      blocked until provider B runtime evidence and a later explicit delivery authorization close.
- [ ] No hardcoded, free-text, Mock or Disabled runtime fallback satisfies reference validation.
- [ ] Product Definition Revision and GSKU audit aggregate/operation values are append-only additions; delivery
      discovery, claim, acknowledgement, failure/dead-letter and compaction explicitly support both collections.
- [ ] Each successful Revision/GSKU mutation and its local audit intent share the approved local consistency boundary;
      soft-deleted records with pending intents remain internally deliverable without business-data disclosure.
- [ ] API/controller, gateway, frontend, workflow, hosted worker, provider-domain code, submit/approval, LSKU, Finished
      Good, Composition and the other excluded field families remain absent.

### Reference data, strength and pack

External Reference Data owner readiness prerequisites (promotion evidence, not provider implementation ACs owned by
MOD-0290):

- [ ] G2 owner-approved contracts exist for all six families without hardcoded/free-text fallback or unapproved example SetCodes.
- [ ] G3 provider evidence proves Disabled/Mock cannot publish or satisfy production consumption for the six families.

MOD-0290 consumer ACs:

- [ ] The consumer binds only to the exact owner-approved SetCode/scope/version/access/failure contract.
- [ ] Missing/unpublished/wrong-scope/wrong-version/retired required values fail submit/approval closed.
- [ ] Disabled/Mock output is never interpreted as production-ready evidence or used as a fallback by MOD-0290.
- [ ] ProductType remains non-required until its applicability contract is approved.
- [ ] Scalar SIMPLE_STRENGTH/SIMPLE_CONCENTRATION tuples are validated; complex/multi-active records cannot submit.
- [ ] `PackApplicabilityCode` is non-null and equals the initial approved `SCALAR_QUANTITY_APPLIES` value; every
      first-phase GSKU has a positive quantity and compatible UoM. Quantity-free/kit/hierarchy cases have no
      placeholder applicability and remain deferred to BL-017.
- [ ] `C62` rejects fractional quantity; `GRM`, `KGM`, `MLT` and `LTR` reject scale above three without silent rounding.
- [ ] A `PINNED` selection is immutable as a complete value object; a Draft `LATEST` refresh changes only
      provider-derived evidence for the same SetCode/ValueCode under expected-version concurrency.

### Lifecycle, workflow and retirement

- [ ] Lifecycle is exactly `DRAFT -> PENDING_IDENTITY_APPROVAL -> IDENTITY_APPROVED -> RETIRED`; reject returns to Draft with reason.
- [ ] Maker and approver are distinct trusted canonical human subjects; self-approval and delegation back to maker fail.
- [ ] Permission bypass, including `platform_admin`, cannot bypass the domain-level canonical-human-subject SoD check.
- [ ] Parent approval prerequisites are revalidated at decision time.
- [ ] Missing/NotApplicable workflow can never authorize identity approval; MOD-0290 applies its own fail-closed check.
- [ ] Retired parent cannot accept a new child or child approval.
- [ ] Approved children block parent retirement with `DEPENDENT_IDENTITIES_EXIST`; there is no automatic cascade.
- [ ] Draft children require controlled cancellation before parent retirement.
- [ ] Duplicate/stale workflow decision observations cannot transition an aggregate twice; orphan and approved-without-state cases reconcile.
- [ ] G5 owners select authenticated callback or secure pull/poll before decision-ingestion implementation; the
      selected model proves trusted tenant binding, service/human actor separation, authorization, replay protection,
      idempotency and reconciliation without trusting raw header/body tenant assertions.
- [ ] Any required shared MDM tenant-middleware change has a separately approved owner/classification/delivery and is
      not silently implemented as MOD-0290 Class C.

### MarketTradeName

- [ ] Draft replacement does not close or alter the approved name.
- [ ] Approval closes the previous approved interval and adds the new interval in one controlled consistency boundary.
- [ ] Approved periods use `[EffectiveFrom, EffectiveTo)` and reject overlap for one LSKU + market + language; gap
      rejection and its failure code apply only if the Product Data owner approves a no-gap policy and temporal granularity.
- [ ] Rejection/cancellation leaves the old approved row unchanged; former names remain searchable and auditable.
- [ ] Approved `Name` is immutable; no persisted `IsCurrent` or `IsUsed` field exists.
- [ ] Nitop to Nitopin in one market/language is a replacement, not a new Global Product or GSKU.

### LegacyAlias

- [ ] Attach requires an authorized steward and same-tenant existing target; alias starts `ACTIVE`.
- [ ] Only authorized `ACTIVE -> RETIRED` transition exists; Product identity lifecycle states are rejected.
- [ ] Raw value is byte/character-preserved; normalized lookup key is stored/derived separately.
- [ ] Exact and normalized collisions are distinguishable; alias never becomes canonical output.
- [ ] No bulk migration, staging, rollback or migration-success behavior is implemented.

### G4-G8A and delivery boundary

- [x] The user selected topology-independent Candidate B for the authorized first implementation step; real atomicity, concurrency, recovery, retention and operational evidence still closes before readiness/release. Candidate C remains an unselected alternative pending production/CI transaction-topology proof.
- [ ] Critical mutation and local durable audit intent share the approved local consistency boundary; post-handler best-effort or synchronous remote-only append is insufficient.
- [ ] G5 provider B, the owner-selected inbound-callback or pull/poll boundary and MOD-0290 consumer C contracts close before approval workflow code-start.
- [ ] G6 topology, applicability and stable failures close before the affected LSKU LegalEntityId slice; unrelated aggregate work is not blanket-blocked.
- [ ] G7 delivery contains no ERP/PLM feed/client/worker/gateway route or runtime external publication and makes no readiness claim for them.
- [ ] G8A Auth seed/catalog, Platform module catalog/tenant entitlement and MOD-0290 endpoint enforcement evidence
      close before entitlement-dependent endpoint enablement, gateway/public exposure or readiness; these are external onboarding preconditions, not provider implementation ACs.
- [ ] The currently authorized implementation remains backend-only; this document-only named-step preparation changes
      no frontend or gateway path and grants no exposure code-start.

### `Global Product Register Exposure & UI` acceptance criteria

- [x] `GlobalProductName` is persisted as the required market-independent internal/global product-family name; missing,
      empty and whitespace-only create values fail with `GLOBAL_PRODUCT_NAME_REQUIRED` before reservation or writes.
- [x] New list/page, detail and minimum selector queries are tenant-scoped, soft-delete aware, deterministically ordered,
      bounded and backed by real Mongo repository operations; cross-tenant detail is the same 404 as missing/deleted.
- [ ] List/detail include `GlobalProductName`; selector items expose exactly technical `Id`, system `CanonicalCode` and
      `GlobalProductName`. ABB consumes
      this contract only and never accepts typed UUID or hardcoded products.
- [ ] The same-origin server flow validates `GlobalProductName`, reserves a Global Product code server-side, invokes the
      create-draft command and returns its server-generated `CanonicalCode` without changing allocation/no-reuse
      behavior; write DTOs reject client `TenantId`, `CanonicalCode` and unknown fields.
- [ ] Register uses `_LayoutTenantShell`, Golden Slim, DataTable v2, skeleton/error/empty states, server paging/search,
      read-only details and localized one-field `GlobalProductName` create with Premium SweetAlert2 confirmation.
      `CanonicalCode` is read-only.
- [x] No edit control or update endpoint is present; post-create `GlobalProductName` mutability/versioning/approval and
      rename behavior remain outside this named step, and no lifecycle/delete/bulk action is inferred.
- [ ] Browser traffic is same-origin MVC proxy -> Gateway `5000` -> MDM `5059`; JavaScript has no direct Gateway or
      service-port URL and no tenant identifier in form/body.
- [ ] MDM backend/UI implementation may start after explicit authorization without completed provider onboarding, but
      accepted `mdm.global-products.read`/`create` permissions must be cataloged, seeded, grantable and enforced before
      production endpoint/user enablement; MDM does not implement Auth/Platform ownership.
- [ ] Only `integration-agent` supplies the reviewed base/catch-all Gateway route with the bounded methods in Section 15.
- [ ] This preparation changes only this pack, keeps status `in-progress` and does not authorize code-start.

## 17. Test Expectations

### Unit and contract tests

- Field validators, conditional applicability, forbidden-field rejection and stable failure codes.
- All lifecycle transition tables, parent approval, self-approval, reject-to-Draft and child-first retirement.
- Code allocation/consume/cancel/expire idempotency, shared namespace collisions and no-reuse.
- MarketTradeName proposal, approval, overlap, rejection/cancellation and historical lookup; gap tests are conditional
  on an owner-approved no-gap policy and temporal granularity.
- LegacyAlias raw preservation, normalization, collision class, lookup and `ACTIVE -> RETIRED`.
- Reference-data consumer failures and scalar strength/pack cross-field rules.
- Authorization attributes and permission ownership for every endpoint.
- Cross-identity `read`/`export` authorization, bounded internal export and denial of public/gateway exposure.
- No user-facing `delete`/`bulk-delete` permission; domain SoD remains effective under permission bypass principals.
- `CreateFirstGskuDraft` request forbids TenantId, CanonicalCode, RevisionIdentifier, Composition and every named-step
  excluded field; no standalone Revision create/edit command is present.
- Parent failure mapping is exact: missing/cross-tenant/soft-deleted are indistinguishable 404; retired is stable 409;
  Draft create does not require parent approval.
- PackApplicability/quantity/UoM pure validation covers required positive scalar quantity and the five locked codes,
  without claiming provider publication/latest/pin validation or introducing a runtime fallback.
- Audit enum compatibility tests prove append-only values and preserve all existing numeric assignments.
- ReferenceCatalogSelection contract tests cover exact six-field shape, server-controlled SetCode, provider-derived
  version/mode/timestamp evidence, client override rejection, Draft `LATEST` refresh and complete `PINNED` immutability.
- Quantity precision tests reject fractional `C62`, scale above three for `GRM`/`KGM`/`MLT`/`LTR`, and silent rounding.

### Real-Mongo and concurrency tests

- Tenant-filtered CRUD, cross-tenant non-disclosure, soft-delete and historical evidence retention.
- Expected-version concurrent edit/submit/decision/retire/alias/reservation tests; no last-write-wins.
- The approved repository mechanism includes expected `Version` in the real Mongo mutation predicate and returns a
  stable concurrency failure when the stored version changed.
- Tenant + reserved-code uniqueness across all four entity types and duplicate concurrent reservation attempts.
- Identity-without-consumed-reservation rejection and at-most-one identity per reservation.
- Crash before consume, after durable consume/before identity, after identity/local intent and before/after central acknowledgement.
- Reconciliation-pending ambiguous writes without code reuse; any burned-reservation evidence only under the
  separately owner-approved safe fence/transaction mechanism.
- Selected G4 model atomicity; if Candidate C is selected, replica-set transaction/session/failover/retry tests.
- Soft-deleted aggregate with pending audit intent remains deliverable.
- Document-growth/16 MB, compaction/receipt, retention/redaction and tenant-isolated worker claim tests.
- Concurrent first-GSKU commands for one parent prove unique parent-scoped ordinals, stable command replay, no ordinal
  reuse after soft delete and no reuse of the canonical-code counter.
- Replay by the shared immutable `CreationCommandId` returns the same Revision/GSKU pair and reservation binding;
  unique tenant + command indexes prevent duplicate Revision, ordinal, GSKU and code consumption.
- GSKU reservation crash/replay covers before consume, after durable consume/before Revision/GSKU completion, after
  Revision persistence/before GSKU persistence and after GSKU/local-intent persistence/before binding confirmation.
- Ambiguous combined writes return reconciliation-required, then resume the same persisted pair; mismatched pair facts
  return `CREATION_COMMAND_PAIR_CONFLICT` and never create replacements.
- Selection persistence round-trips both embedded value objects; `LATEST` refresh is expected-version guarded,
  `PINNED` mutation is rejected, and stored historical pinned version identity remains unchanged.
- GSKU Draft correction includes expected `Version` in the Mongo predicate; stale mutation changes neither aggregate
  state nor embedded audit intents.
- Product Definition Revision and GSKU pending intents are discovered, tenant-filtered, single-winner claimed,
  generation/token fenced, acknowledged and compacted; soft-deleted aggregates remain internally deliverable and
  business `Version` is unchanged by worker bookkeeping.

### Provider/consumer integration tests

- MOD-0290 Reference Data consumer: wrong scope/version, unpublished/retired value, provider
  unavailable/timeout/unauthorized/malformed response and Disabled/Mock fail-closed behavior. Provider-owned usage
  registration and publish/deprecate/pointer recovery are external G2/G3 readiness evidence, not implementation tests
  owned by this pack.
- G5 for the selected callback or pull/poll model: trusted subject/tenant binding, maker/approver mismatch, invalid
  credential, raw-header/body tenant spoofing, wrong tenant, mandatory start/decision idempotency, replay, partial
  start, timeout, duplicate/stale decision observation, orphan workflow and reconciliation.
- G6 where applicable: valid, missing, invalid GUID, wrong tenant, inactive/suspended/archived/deleted, unauthorized,
  unavailable/timeout/malformed response, approval-time revalidation, stale cache/race, historical retirement/reactivation.
- MOD-0021: timeout/4xx/5xx, retry/dead-letter/stale claim and central accepted-before-local-ack duplicate acceptance.

Real MOD-0048 provider integration, Draft `LATEST` provider resolution/refresh, historical `PINNED` provider lookup and
submit/approval tests are explicitly outside `Product Definition Revision + First GSKU Draft Foundation`. They enter
only through a later provider-ready named step after provider B runtime evidence and explicit delivery authorization close.

### Build and quality gates

- `dotnet build services/Diten.MdmService/Diten.MdmService.sln` passes.
- `dotnet test services/Diten.MdmService` passes, including non-optional real-Mongo G4 suites in the approved topology.
- API contract/open-api snapshot, error-code and permission tests pass.
- No frontend, DataTable verifier, browser smoke or RESX gate applies to the currently authorized backend-only slices.
- For a later explicitly authorized `Global Product Register Exposure & UI` implementation, unit/contract tests cover
  missing/empty/whitespace `GlobalProductName`, paging bounds, filter allow-list, deterministic ordering, projection
  minimization, permission mapping, client `TenantId`/`CanonicalCode` override rejection and `Response<T>`/error mapping.
  Create tests prove validation precedes reservation, reservation is server-side, returned `CanonicalCode` is generated by
  the existing allocator and idempotent/no-reuse/reconciliation behavior is unchanged. Real-Mongo tests cover persisted
  `GlobalProductName` in same-tenant list/detail/selector, cross-tenant 404, soft-delete exclusion and paging stability.
- That later frontend delivery must pass the DataTable verifier with `--reference slim`, seven-locale RESX/`window.L10n`
  checks, MVC proxy tests proving Gateway-only routing, browser smoke for skeleton/filter/details/one-field create/
  SweetAlert2 confirmation/error states and negative tests proving no edit, typed UUID, hardcoded lookup, direct service
  URL or client `TenantId`/`CanonicalCode`. The Slim contract asserts `form_field_count: 1` and explicit
  `Layout = "_LayoutTenantShell"`.
- Gateway tests must prove base/catch-all matching, `GET`/`POST`/`OPTIONS` only, MDM `5059` downstream routing and auth/
  tenant-header preservation. Auth/Platform evidence must prove both accepted permissions are assignable and enforced.
- Operational metrics and runbooks exist for audit-intent backlog, stale processing, dead-letter, workflow pending
  start/decision-ingestion/reconciliation and terminal failures before integration/release or production readiness.

## 18. Ready-for-dev Checklist

### Stage 1 — Module Pack approval (design and scope)

- [x] DCP-004 is `approved`; this does not imply technical proof completion.
- [x] MOD-0290 registry identity exists with exact Master 8.1 name and MDM owner.
- [x] Master 8.1 evidence ranges are directly recorded; legacy verifier output is not authority.
- [x] Scope, eight aggregate roots, cardinalities and BL-015-BL-027 exclusions are recorded.
- [x] The authorized internal foundation was backend-only; the later exposure plan records tenant shell,
      `golden_reference: slim` and the user-approved field count `1` without granting code-start.
- [x] Product Data Owner and MDM owner approve the Stage 1 design, owned objects, protected paths and field/failure contracts.
- [x] Product/SKU code policy, tenant isolation, lifecycle, cardinality, parent/child and common-ledger reservation invariants are approved as testable ACs.
- [x] Every applicable G2-G8A gate has a named owner, closure artifact and delivery step; no dependency is silently waived.
- [x] G4 has the user-approved Candidate B path, consistency design and executable real-Mongo/concurrency/crash test plan. Completed implementation evidence is not required at this stage.
- [x] G2, G5, G6 and G8A step-entry boundaries are explicit, including the work that remains prohibited while each gate is open.
- [x] Open field decisions outside the authorized first step remain explicitly gated and are not implementation requirements for this slice.
- [x] API/controller and permission/entitlement work remain outside the authorized first step.
- [x] The test plan identifies the real-Mongo evidence needed for later readiness; no optional result is accepted as proof when Mongo is available.
- [x] The user approved Stage 1 design/scope and authorized the named first implementation step.

Module Pack approval is design/scope approval. It does not by itself authorize implementation, merge, integration,
endpoint exposure or production use.

### Stage 2 — Authorized implementation start

- [x] The user approved this pack's Stage 1 design/scope and explicitly authorized `CodeReservation common ledger + Global Product draft foundation`.
- [x] The authorized implementation branch exists and is recorded in frontmatter.
- [x] This slice does not depend on MOD-0040 technical correlation; canonical business-code ownership remains separate and no waiver is inferred.
- [x] This slice implements no Reference Data consumption; G2 remains closed to later reference-data-dependent slices. The user selected PSS-012 only as the provider direction for PackApplicability and UoM contract authoring; this is not canonical-owner reconciliation, publication readiness or implementation authorization.
- [x] This slice implements no workflow/approval transition; G5 remains closed to later workflow-dependent slices.
- [x] This slice implements no LSKU or LegalEntityId behavior; G6 remains closed to that later slice.
- [x] This slice implements no API/controller, entitlement-dependent endpoint or public/gateway exposure; G8A remains closed to those later slices.
- [x] G7 scoped deferral is recorded; the authorized step contains no external feed/publication work.
- [x] No unresolved gate is bypassed through Mock, Disabled, hardcoded, best-effort or other insecure fallback behavior.

An explicitly authorized G4 step may implement the persistence/audit mechanism and tests that create readiness evidence.
Only the named delivery step may start, and implementation start must not be described as production-ready.

The user-authorized second G4 step is `MDM-local audit-intent discovery, fenced claim, retry/recovery and compaction
foundation`. It is limited to MDM-owned embedded-intent contracts, internal persistence, test-invoked worker logic and
real-Mongo evidence. It must keep runtime hosted-service registration disabled, never synthesize a transport
acknowledgement, preserve aggregate business `Version`, require opaque token + lease + generation fencing, and compact
only an acknowledged delivered intent. Future transport uses service-specific credentials plus server-side tenant grants,
durable-outbox-accepted acknowledgement and `SourceService + TenantId + IntentId + ContractVersion` idempotency;
numeric operation passthrough, shared-key/raw-tenant trust and unapproved dead-letter requeue are prohibited.

Repository code truth now contains the Product Definition Revision + First GSKU Draft foundation, including the exact
six-field embedded selection shape and shared-`CreationCommandId` combined-recovery model. This documentation task
does not reconstruct or retroactively grant its code-start authority, and the predecessor's provider/audit/real-Mongo
evidence must be reconciled with this pack and the implementation tracker before it is used as a readiness claim.

`Finished Good Draft Foundation` A-D code truth and evidence are recorded in Section 19. Subwork E remains a separate
permission/production-enablement gate; A-D implementation is not production-readiness evidence.

`Global Product Register Exposure & UI` remains an ordered delivery step. Subwork A, `MDM Global Product
API/read-selector`, was explicitly authorized and implemented on 2026-08-04. Subwork B-E remains fail-closed until its
own explicit authorization and applicable owner gates close. Permission catalog/seed/grant onboarding is not an MDM
backend implementation-start blocker, but endpoint/user enablement and any production-ready claim still require Auth
onboarding and Gateway delivery.

### Stage 3 — Ready-for-dev / integration / production readiness

- [ ] G2 exact six-family SetCode/scope/catalog/schema/version/retirement/access/failure contracts and consumer evidence close for the enabled fields.
- [ ] G3 production-safe provider delivery and Disabled/Mock, actor, SoD, tenant, usage and recovery proofs close.
- [ ] User-selected Candidate B readiness closes. The authorized steps now prove real-Mongo common-ledger uniqueness, tenant/counter/idempotency and ambiguous-write safeguards plus tenant-isolated embedded-intent discovery, soft-deleted aggregate discovery without business-read exposure, single-winner opaque claims, lease/generation reclaim, stale-token fencing, retry/dead-letter transitions, acknowledgement-gated compact receipts, cross-tenant non-disclosure and unchanged aggregate business `Version`. Ambiguous `PendingIdentityWrite` is conservatively reconciliation-pending, never automatically burned from an absence lookup; a fenced/race-safe burn procedure requires a separately owner-approved persistent fence or transaction mechanism. Platform transport, real central acknowledgement, active hosted worker, production scheduling, full crash matrix, retention/purge/redaction, metrics and runbook proof remain open.
- [ ] G5 provider B trusted actor/S2S/idempotency/recovery and MOD-0290 C fail-closed/idempotency/reconciliation evidence close for the workflow-dependent slice.
- [ ] G6 provider/consumer, approval-time revalidation, race/cache and historical-reference evidence close for the affected LSKU slice.
- [ ] Auth permission catalog/seed, Platform `ModuleCatalogItem`/tenant entitlement and MOD-0290 endpoint enforcement evidence close before exposure or production readiness.
- [ ] The user-approved `GlobalProductName` persistence, list/detail/selector API, base/catch-all Gateway route, Golden Slim
      tenant UI and ABB consumption contract close in delivery order A-E before Global Product/ABB user enablement.
- [ ] All other technical, security and operational evidence applicable to the slice is approved by the named owner.
- [ ] Real-Mongo, concurrency, crash/recovery, tenant-isolation and authorization suites pass in the approved topology.
- [ ] Real-Mongo evidence proves shared `CreationCommandId` replay returns the same Revision/GSKU pair, partial writes
      reconcile without duplicate ordinal/GSKU/code, and mismatched persisted facts fail closed.
- [ ] Selection contract/persistence evidence proves server-derived catalog metadata, client override rejection,
      expected-version `LATEST` refresh, complete `PINNED` immutability and historical pinned-version preservation.

No integration, merge/release, public exposure or production-readiness claim is permitted while an applicable gate remains open.
“Code can start” and “production-ready” are separate lifecycle states.

## 19. Implementation Notes

- The user-approved `GlobalProductName` decision replaces the Domain Contract's conditional-open Global Product
  `StewardLabel` planning placeholder for this pack. The Domain Contract is not modified by this task. This replacement
  applies only to Global Product and does not rename or expand GSKU/LSKU/MarketTradeName concepts.
- The repository contains an existing `Diten.MdmService` runtime despite stale MDM README/domain-config statements
  that the service does not exist. Implementation planning must use code truth without editing those documents here.
- Existing MDM post-handler audit forwarding is reuse/gap evidence only and does not close G4.
- Existing `RepositoryBase.UpdateAsync` increments `Version` but does not include the expected version in its Mongo
  replace filter. Current generic updates therefore permit last-write-wins and do not close the optimistic-concurrency gate.
- Existing MDM tenant middleware bypasses only OPTIONS, health, Swagger and favicon. It provides no safe inbound
  workflow-callback tenant-resolution contract; neither an `/api/internal` bypass nor callback topology is selected here.
- Existing PSS-012 runtime is a provider candidate, not canonical identity or production-readiness proof.
- MOD-0048-FU01 pack approval is planning/design authority, not runtime readiness proof for this named step. The
  provider-integrated create, lookup, `LATEST` refresh and `PINNED` validation behaviors remain closed until runtime
  readiness evidence and a separately authorized delivery exist.
- The final catalog-selection persistence shape is closed as the two embedded six-field value objects in Section 4.
  This decision creates no Composition FK, placeholder or new provider system.
- Combined recovery is closed on one immutable shared `CreationCommandId`, same-pair replay and fail-closed
  reconciliation without a cross-collection transaction assumption. Runtime proof remains outstanding.
- The named step intentionally combines Revision creation with first-GSKU creation. It adds no standalone empty
  Revision endpoint/command and no ProductType, DosageForm, Route, Strength, Composition or temporal behavior.
- Existing MOD-0220 reference validation can support an owner-approved in-process A path; HTTP/S2S remains a
  conditional B provider decision. G6 gates only the LSKU `LegalEntityId` slice.
- The implementation status tracker opens with this user-authorized first slice. It must not imply implementation of
  any other aggregate, API/controller, provider integration or readiness gate.
- Authorized first-slice evidence on 2026-08-01: the latest
  `dotnet test services/Diten.MdmService/Diten.MdmService.sln --no-restore -c Debug` run built the solution and passed
  105/105 tests with no skipped tests against reachable local Mongo. The suite includes stable, non-leaking tombstone
  conflict mapping plus the MDM-local discovery/fenced-claim/retry/dead-letter/receipt-compaction foundation for
  CodeReservation and Global Product. One existing obsolete GUID-representation warning remains. This is implementation
  evidence, not Platform transport, real central acknowledgement, hosted-worker activation, production-topology or
  overall G4 readiness proof.
- Authorized subwork-A evidence on 2026-08-04: `GlobalProductName` is stored trimmed, with a separate
  FormKC/invariant-case normalized tenant duplicate key and a unique Mongo index that includes soft-deleted rows.
  The MDM API now exposes authorized list, detail, selector, reservation and draft-create actions with
  `mdm.global-products.read`/`create` fail-closed attributes. Real-Mongo tests cover Unicode preservation, normalized
  duplicates, tombstone no-reuse, cross-tenant reuse/non-disclosure, duplicate concurrency, paging/search/order and
  minimal selector projection. Gateway, permission onboarding, frontend and Local Development create/read smoke are now
  complete; ABB selector consumption remains open.
- `IDENTITY_APPROVED` guarantees identity, code, duplicate and basic master-data integrity only. It does not claim
  regulatory, market, manufacturing, quality or commercial readiness.
- The G7 deferral ends for a triggered scope at the first approved external-feed use case, external consumer/runtime
  publication need, cross-module consumer or breaking schema/version change. That scope cannot start until its
  source/consumer, direction, objects, SoR/conflict, credentials, security, idempotency, retry, reconciliation,
  observability and delivery artifact close.

### Ordered named step — `Finished Good Draft Foundation`

This named step is an ordered A-E delivery contract. The user separately authorized and implemented subworks A-D on
2026-08-06. This sentence is historical: E and the separately authorized Local Development live smoke are now complete;
navigation and Production/operational enablement remain separate gates.

#### Slice contract

- First slice: Draft create, list, detail and GSKU selector only.
- Shell and UI pattern: tenant shell, `golden_reference: slim`, `form_field_count: 1`.
- The only user-entered business field is `GskuId`, selected through the bounded same-tenant GSKU selector. Typed UUID,
  free text and a cached or hardcoded GSKU list are prohibited.
- `CanonicalCode` is tenant-scoped, immutable, low-semantic and system-generated through the existing common
  CodeReservation ledger. The user never enters or overrides it.
- A Finished Good references exactly one GSKU. One GSKU may be referenced by zero-to-many Finished Goods.
- `GskuId` is immutable after create. This slice has no edit, update, rebind, delete, bulk-delete, submit, approval,
  rejection or retirement surface.
- A direct LSKU-Finished Good relationship is prohibited. A future relationship may exist only through an approved
  Market Supply Assignment contract, which this step does not create.
- Draft create may use only an existing, same-tenant, non-deleted GSKU whose lifecycle is `DRAFT` or
  `IDENTITY_APPROVED`. `PENDING_IDENTITY_APPROVAL`, `RETIRED`, missing, cross-tenant and soft-deleted GSKUs fail closed
  with the same non-disclosing referenceability failure. This referenceability decision applies only to Finished Good
  Draft creation and does not imply approval or production usability.

Read-only list/detail/quick-view projections contain only:

- Finished Good `Id`;
- Finished Good `CanonicalCode`;
- linked GSKU `CanonicalCode`;
- owner-approved GSKU display information;
- Finished Good `LifecycleStatus`;
- technical concurrency `Version`;
- `CreatedAt` and `UpdatedAt` audit timestamps.

No new persisted `GskuDisplay`, label or denormalized GSKU name is implied. For this first backend slice, GSKU display
information is exactly the linked GSKU `CanonicalCode`. It must not synthesize `StewardLabel`, MarketTradeName, market,
regulatory, packaging, manufacturer or site meaning.

List/search is tenant-scoped, active-record-only, bounded and deterministic:

- searchable keys are exactly Finished Good `CanonicalCode` and linked GSKU `CanonicalCode`;
- search over display text or any excluded semantic field is prohibited;
- ordering is deterministic by Finished Good `CanonicalCode`, then `Id` as the tie-breaker;
- paging and selector limits reuse the existing MDM bounded-query contract: default `PageSize=20`,
  `PageNumber=1..1,000,000`, `PageSize=1..100` and `Search` maximum length `200`;
- cross-tenant, deleted or otherwise non-referenceable GSKU records never appear in the selector.

#### Create and recovery contract

The logical create sequence is fixed:

1. Resolve the authenticated tenant and trusted actor; reject client tenant/actor/audit input.
2. Resolve `GskuId` inside the same tenant and revalidate the owner-approved referenceability rule.
3. Reserve one `FinishedGood` canonical code through the existing common ledger.
4. Consume the reservation for one preallocated Finished Good identity under the same stable idempotent operation.
5. Persist the Finished Good Draft and its local G4 audit intent.
6. Confirm the reservation-to-identity binding. An ambiguous write remains reconciliation-pending under the same
   identity and reservation; it is never treated as success and the code is never returned to the available pool.

Replay of the same stable operation returns or completes the same Finished Good/reservation outcome. Conflicting facts
fail closed and never allocate a second code or identity. The business request contains only `GskuId`;
`IdempotencyKey` is technical request metadata, normalized with the existing MDM `Trim().ToUpperInvariant()` convention,
and is not a form field. No new header/protocol or public Finished Good reservation endpoint is introduced. Reservation,
consume, identity binding and reconciliation are managed inside the server-side create flow.

Client write contracts and UI payloads must reject or structurally exclude all of the following:

- `TenantId`, `CanonicalCode`, `CodeReservationId` and every reservation/consume/binding evidence field;
- `StewardLabel`;
- `LskuId`, `MarketSupplyAssignmentId`, `MarketCode` and `LegalEntityId`;
- packaging, packaging hierarchy, site, manufacturer, MA, Registered Presentation, artwork, GTIN, batch and
  Composition/formulation fields;
- lifecycle, version assignment, actor, audit intent, timestamps, soft-delete and other technical/audit fields.

`StewardLabel` remains conditional-open in the supporting Domain Contract and is not owner-approved for this slice;
it is not a DTO, UI, search or persistence addition.

#### Why adjacent concepts remain outside this slice

- **LSKU:** LSKU is a market-context identity. The locked first-phase model prohibits a direct LSKU-Finished Good FK;
  introducing it would bypass BL-018.
- **MarketTradeName:** it is LSKU-owned, market/language/effective-period data. It cannot supply a Finished Good or
  GSKU label and is not a search key here.
- **Market Supply Assignment:** it is the only possible future LSKU/Registered Presentation-to-Finished-Good route,
  but its owner, identity, lifecycle and effective-dating contract remain outside this pack and DCP-005-gated.
- **MA / Registered Presentation:** these are regulatory/market identities with unresolved candidate ownership under
  DCP-005. Identity approval is not regulatory or commercial readiness, so no MA/Registered Presentation field,
  selector, placeholder or inferred mapping is permitted.
- Packaging, site, manufacturer, artwork, GTIN, batch and Composition would import manufacturing, regulatory,
  labeling or formulation semantics explicitly excluded by BL-015 and BL-017 through BL-022.

#### Authorization boundary

The only permission candidates for this step are:

- `mdm.finished-goods.read` — list, detail and read-only Finished Good projections;
- `mdm.finished-goods.create` — the create form's GSKU selector and Draft create operation.

No `update`, `delete`, `bulk-delete`, `submit`, `approve`, `retire`, reservation-management or broad `manage` key is
introduced. These two strings are candidates only. Catalog/seed/grant/role and tenant-entitlement onboarding remains a
separately approved MOD-0018 owner delivery. This named step neither grants the permissions nor authorizes Auth or
Platform runtime changes. Endpoint/user enablement is blocked until onboarding and end-to-end enforcement evidence
close.

#### Exact repo allow-list

Rows A-D were separately authorized and implemented on 2026-08-06. Row E remains evidence-only and open; all edits
remain limited to the named Finished Good concern.

**A — Backend/domain/persistence foundation**

- `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/FinishedGood.cs` — new aggregate only.
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Enums/AuditAggregateType.cs` — append-only Finished Good value.
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Enums/ProductAuditOperation.cs` — append-only Draft-created value.
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/IFinishedGoodRepository.cs` — new contract only.
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/IGskuRepository.cs` — same-tenant
  referenceability/selector reads only; no GSKU mutation expansion.
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Commands/CreateFinishedGoodDraftCommand.cs`.
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Queries/GetFinishedGoodsQuery.cs`.
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Queries/GetFinishedGoodByIdQuery.cs`.
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Queries/GetFinishedGoodGskuSelectorQuery.cs`.
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Handlers/CommandHandlers/CreateFinishedGoodDraftHandler.cs`.
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Handlers/QueryHandlers/GetFinishedGoodsHandler.cs`.
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Handlers/QueryHandlers/GetFinishedGoodByIdHandler.cs`.
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Handlers/QueryHandlers/GetFinishedGoodGskuSelectorHandler.cs`.
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Validators/CreateFinishedGoodDraftValidator.cs`.
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Validators/GetFinishedGoodsValidator.cs`.
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Validators/GetFinishedGoodByIdValidator.cs`.
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Validators/GetFinishedGoodGskuSelectorValidator.cs`.
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/ProductItemSkuMasterModels.cs` — Finished Good DTO additions only.
- `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/FinishedGoodRepository.cs` — new custom
  tenant-scoped repository with conditional writes and tombstone-preserving indexes.
- `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/GskuRepository.cs` — referenceability and
  selector reads only.
- `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/AuditIntentDeliveryRepository.cs` — add
  Finished Good discovery/claim/fencing/acknowledgement/compaction support only.
- `services/Diten.MdmService/src/Diten.MdmService.Persistence/DependencyInjection.cs` — repository registration only.
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/FinishedGoodDraftFoundationUnitTests.cs`.
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/FinishedGoodDraftFoundationMongoTests.cs`.
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/AuditIntentDeliveryMongoTests.cs` — Finished Good
  audit-delivery cases only.

**A implementation evidence — 2026-08-06:** The authorized A allow-list is implemented. The custom tenant-scoped
repository preserves unique canonical-code, reservation and creation-command tombstones; validates the same-tenant
`DRAFT | IDENTITY_APPROVED` GSKU and consumed Finished Good reservation again at the persistence boundary; and exposes
no GSKU mutation or Finished Good edit surface. Create performs GSKU validation before server-side reserve/consume,
persists one Draft plus its local audit intent, then confirms or reports reconciliation under the same normalized
idempotency operation. Finished Good audit delivery is append-only and uses the existing discovery/claim/fencing/
acknowledgement/compaction lifecycle without changing business `Version`. `dotnet build` completed with 0 warnings and
0 errors. Targeted hardening classifies only persistence-reported `MongoConnectionException`,
`MongoExecutionTimeoutException` and `MongoWriteConcernException` outcomes as ambiguous; cancellation and unexpected
exceptions propagate to the global pipeline, while duplicate/idempotency conflicts retain deterministic 409 behavior.
The full MDM suite passed 271/271 with 0 skipped against reachable real MongoDB. This A evidence did not itself
authorize later rows; their separate authority and evidence are recorded below.

**B — MDM API contract**

- `services/Diten.MdmService/src/Diten.MdmService.Api/Controllers/FinishedGoodsController.cs` — proposed
  `GET /api/finished-goods`, `GET /api/finished-goods/{id}`, `GET /api/finished-goods/gsku-selector` and
  `POST /api/finished-goods/drafts` only; no public reservation endpoint.
- `services/Diten.MdmService/src/Diten.MdmService.Api/ModuleRegistration/ProductItemSkuMasterManifestProvider.cs` —
  declare only the two candidate Finished Good permissions/page actions after provider-owner acceptance; no grant/seed.
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/FinishedGoodApiContractTests.cs`.
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/FinishedGoodAuthorizationTests.cs`.

**B implementation evidence — 2026-08-06:** The API exposes exactly list, detail, bounded GSKU selector and Draft create
under `/api/finished-goods`; there is no public reservation, update, rebind, delete, bulk or lifecycle route. Controller
permissions are exactly `mdm.finished-goods.read` for list/detail and `mdm.finished-goods.create` for selector/create.
Strict extension-data validation returns 400 before dispatch, while the existing create responses preserve 201, 202,
409 and non-disclosing 404 envelopes. The manifest preserves `GLOBAL_PRODUCTS` and adds nav-hidden `FINISHED_GOODS`,
with an exact four-permission union across both controllers and only `ADD_NEW`/`VIEW_DETAILS` actions per page. Focused
B controller/authorization/manifest tests passed 26/26; the full MDM suite passed 271/271 with 0 skipped and the solution
build completed with 0 warnings and 0 errors. Permission declaration is not Auth onboarding, grant, entitlement or
production enablement.

**C — Gateway delivery**

- `gateway/Diten.ApiGateway/ocelot.json` — integration-agent only, restricted to the reviewed Finished Good base/catch-all
  route pair and the bounded `GET`/`POST`/`OPTIONS` method set. No general CodeReservation or GSKU mutation route.

**C implementation evidence — 2026-08-06:** The reviewed Finished Good base and catch-all routes are present in
`ocelot.json`, map Gateway `5000` to MDM `5059`, and retain the bounded method set. Later Local Development smoke proved
the Gateway-to-MDM path; Production enablement is not inferred.

**D — Tenant frontend (Golden Slim, create-only/read-only variance)**

- `frontend/Diten.Web/Controllers/FinishedGoodsController.cs`.
- `frontend/Diten.Web/Models/FinishedGoods/**`.
- `frontend/Diten.Web/Views/MasterDataManagement/FinishedGoods/Index.cshtml`.
- `frontend/Diten.Web/Views/MasterDataManagement/FinishedGoods/_Filter.cshtml`.
- `frontend/Diten.Web/Views/MasterDataManagement/FinishedGoods/_DataTable.cshtml`.
- `frontend/Diten.Web/Views/MasterDataManagement/FinishedGoods/_IndexL10n.cshtml`.
- `frontend/Diten.Web/Views/MasterDataManagement/FinishedGoods/_CreateEditOffcanvas.cshtml` — create mode only; no edit
  action or update request.
- `frontend/Diten.Web/Views/MasterDataManagement/FinishedGoods/_DetailsQuickView.cshtml` — read-only.
- `frontend/Diten.Web/Views/MasterDataManagement/FinishedGoods/FinishedGoodsIndex.cs`.
- `frontend/Diten.Web/wwwroot/assets/js/MasterDataManagement/FinishedGoods/index.js`.
- `frontend/Diten.Web/wwwroot/assets/js/MasterDataManagement/FinishedGoods/index.l10n.js`.
- `frontend/Diten.Web/Resources/Views/MasterDataManagement/FinishedGoods/FinishedGoodsIndex.en.resx`.
- `frontend/Diten.Web/Resources/Views/MasterDataManagement/FinishedGoods/FinishedGoodsIndex.fr.resx`.
- `frontend/Diten.Web/Resources/Views/MasterDataManagement/FinishedGoods/FinishedGoodsIndex.es.resx`.
- `frontend/Diten.Web/Resources/Views/MasterDataManagement/FinishedGoods/FinishedGoodsIndex.zh.resx`.
- `frontend/Diten.Web/Resources/Views/MasterDataManagement/FinishedGoods/FinishedGoodsIndex.ar.resx`.
- `frontend/Diten.Web/Resources/Views/MasterDataManagement/FinishedGoods/FinishedGoodsIndex.ru.resx`.
- `frontend/Diten.Web/Resources/Views/MasterDataManagement/FinishedGoods/FinishedGoodsIndex.tr.resx`.
- `frontend/Diten.Web/tests/finished-good-draft-foundation.test.js`.

Every Razor surface explicitly sets `Layout = "_LayoutTenantShell"`. Browser code uses the same-origin MVC proxy,
which calls Gateway `5000`; it never calls MDM `5059` or a provider service directly. The Slim verifier remains
authoritative, with only the same create-only/read-only checks that are structurally inapplicable (edit, delete,
bulk-delete and selection/bulk-action controls) eligible for an explicitly reviewed local variance. Fake endpoints,
hidden edit markup or inert bulk controls may not be added to satisfy the verifier.

#### Subwork D controlled DataTable verifier variance — Finished Good

The Finished Good tenant UI uses the proxy profile and the canonical same-origin browser surface
`/MasterDataManagement/FinishedGoods/api`. The verifier must be run with
`--area MasterDataManagement --module FinishedGoods --reference slim --api-profile proxy`. This first slice is
deliberately create-only and read-only, so only these structurally inapplicable checks are accepted variances:

- `Active`
- `Passive`
- `Edit`
- `BulkDelete`
- `BulkDeleteConfirm`
- `AreYouSure`
- `Import`
- `ShowAll`
- Index-file direct `offcanvasDetailsPreview` lookup; the real offcanvas remains in `_DetailsQuickView.cshtml`
- select-all checkbox
- bulk config
- bulk selection
- `/bulk` endpoint
- bulk trigger
- bulk reload lifecycle
- clear selection

No fake endpoint, hidden edit markup, inert checkbox or non-functional bulk control may be added to satisfy these
checks. Any verifier failure outside this exact list blocks D completion.

**Verifier hardening evidence — 2026-08-06:** The official proxy-profile verifier improved from `63 passed / 29 failed`
to `76 passed / 16 failed`. All remaining failures map one-to-one to the exact controlled variance list above; no
additional verifier failure remains. The tenant-shell Finished Good Slim surface, same-origin MVC proxy, bounded GSKU
selector, create-only offcanvas, read-only quick view, seven locale resources and frontend tests are present. Navigation
remains hidden. The later authorized live pilot smoke supersedes this historical no-smoke statement.

**Local UI targeted hotfix evidence — 2026-08-06:** All five Finished Good partials now use explicit application-root
view paths, eliminating the local HTTP 500 without moving or duplicating partials. Global Product and Finished Good
toolbar create actions consume the existing server-built `IPermissionSnapshot` only as a UX visibility flag: tenant
admins with the exact create permission see the canonical `DtDefaults.exportButtons` action, while Viewer does not;
Gateway/MDM permission enforcement remains authoritative. Both module localization bridges now normalize Razor's
camel-case JSON payload to the Pascal-case keys consumed by module scripts. Focused frontend tests passed `19/19`, JS
syntax checks passed, the Frontend build completed with zero errors, and the official proxy verifiers retained only
their previously approved create-only/read-only variances. Local browser smoke proved both pages return 200, Admin can
open both create offcanvases and create a one-field Global Product, Viewer sees neither create action, and Viewer
Global Product create plus Finished Good selector/create remain 403. This is local smoke evidence only; it does not
close First GSKU, provider/catalog, ABB or production-operational gates.

**E — Integration and enablement evidence**

- No Auth or Platform provider file is allow-listed by this pack. MOD-0018 permission onboarding runs only under its
  separately approved artifact and owner authorization.
- No additional production source path is allow-listed. E consumes the frozen A-D contracts and produces test/smoke
  evidence only: Auth catalog/grant/tenant-entitlement allow/deny proof, Gateway-to-MDM routing, frontend `5001` through
  Gateway `5000`, tenant isolation, and audit delivery compatibility.
- Any test-code addition not named under A-D requires a separate allow-list revision and explicit authorization.

Everything outside the exact A-E list is protected for this named step. In particular, `.antigravity/**`, `AGENTS.md`,
the Domain Contract, DCP-004/DCP-005, registries, Blueprint files, product backlog, other modules/domains/services,
existing Global Product/Product Definition Revision/GSKU behavior except the two narrowly listed GSKU read contracts,
`CodeReservationRepository.cs`, middleware, configuration/secrets, hosted-service activation, workflow, LSKU,
MarketTradeName, ABB, MA/Registered Presentation, Market Supply Assignment, Composition, archive/frozen views and
unrelated Gateway/frontend paths are protected.

#### Acceptance criteria

- [x] Finished Good Draft create accepts exactly one business field, `GskuId`; unknown/forbidden DTO fields fail before
      reservation or aggregate mutation.
- [x] The GSKU referenceability rule's allowed lifecycle states and the deterministic GSKU display projection are
      owner-approved; no `ACTIVE` enum or display label is invented.
- [x] Same-tenant referenceable GSKU creates a Draft Finished Good with exactly one immutable `GskuId` and one
      system-generated immutable `CanonicalCode` backed by exactly one consumed and confirmed reservation.
- [x] One GSKU can own zero, one or multiple Finished Goods; each successful create receives its own permanently
      non-reusable code and reservation proof.
- [x] Missing, cross-tenant, soft-deleted, `PENDING_IDENTITY_APPROVAL` and retired GSKU inputs return the same
      non-leaking not-found/referenceability
      failure class before reservation allocation.
- [x] Direct `LskuId`, Market Supply Assignment, market, Legal Entity, packaging/site/manufacturer/MA/Registered
      Presentation/artwork/GTIN/batch/Composition and `StewardLabel` inputs are rejected and never persisted.
- [x] Same-operation replay returns/completes the same Finished Good; conflicting replay does not allocate a second
      identity, code or reservation. The stable idempotency transport is approved outside the one-field business DTO.
- [x] An ambiguous post-consume identity write remains reconciliation-pending, never reports create success and never
      makes the code reusable; binding confirmation is idempotent and fact-checked.
- [x] Soft-delete or retirement never frees `CanonicalCode`, reservation, consume-command or identity-binding evidence.
- [x] List/detail/selector reads are tenant-scoped and active-record-only; list search uses only Finished Good code and
      linked GSKU code, enforces approved server bounds and orders by Finished Good code then `Id`.
- [x] Detail/quick view exposes only the approved read-only fields; no edit/rebind/update UI or endpoint exists.
- [x] Custom Finished Good persistence uses expected-version/expected-state conditional filters where mutation or
      reconciliation occurs. Generic `RepositoryBase.UpdateAsync` is not optimistic-concurrency proof and is not used
      for a stale-write-sensitive Finished Good transition.
- [x] Finished Good audit enum additions are append-only compatible, and the existing G4 delivery repository discovers,
      claims, fences, acknowledges and compacts Finished Good intents without changing aggregate business `Version`.
- [x] Only `mdm.finished-goods.read` and `mdm.finished-goods.create` appear as permission candidates; FU17 and the
      Local Development entitlement/grant smoke prove the bounded Admin/Viewer behavior.
- [x] The Golden Slim tenant UI has one selector field, explicit tenant layout, DataTable v2 list, read-only quick view,
      no edit/bulk/delete controls, seven-locale resources, Premium SweetAlert2 create confirmation and Gateway-only
      network behavior.
- [x] A-E and Local Development live smoke are accepted and implemented; Production enablement remains open.

#### Test expectations

Non-optional real-Mongo evidence is required when the repository's configured Mongo test topology is reachable:

- exactly-one GSKU enforcement, including absence/null/empty rejection at every DTO/entity persistence boundary;
- one GSKU to multiple Finished Goods with distinct immutable codes and no cardinality cap invented by this slice;
- same-tenant success plus indistinguishable cross-tenant, missing, soft-deleted and retired GSKU rejection;
- schema/DTO/validator negative proof that no direct LSKU relationship can be supplied or persisted;
- code no-reuse after successful create, ambiguous failure, retirement and technical soft-delete;
- duplicate/replay/idempotency tests for same facts, conflicting facts and tombstoned command/identity evidence;
- concurrent creates and reservation allocation prove single-winner identity binding, tenant-wide ledger uniqueness and
  no duplicate code or identity;
- stale expected reservation/binding/reconciliation writes return conflict with no partial aggregate/audit mutation;
- deterministic paging/search/order tests using only Finished Good and linked GSKU canonical codes, including bounds;
- tenant-isolated audit discovery, including pending intent on a soft-deleted Finished Good without business-read
  exposure;
- single-winner claim, lease expiry/reclaim, increasing generation, opaque claim token, stale-token/generation fencing,
  retry and dead-letter behavior;
- durable acknowledgement validation using `SourceService + TenantId + IntentId + ContractVersion`, followed by
  acknowledgement-gated compaction to one receipt and idempotent compaction replay;
- aggregate business `Version` remains unchanged by discovery, claim, retry, acknowledgement and compaction;
- API contract tests prove strict forbidden-field rejection, `Response<T>`/`CustomBaseController`, authorization
  attributes, non-leaking failure mapping and absence of update/delete/bulk/reservation endpoints;
- DataTable Slim verifier, seven-locale RESX parity, frontend unit tests and browser smoke prove selector/create/list/
  detail behavior through frontend `5001` and Gateway `5000`, with negative assertions for direct `5059`, typed UUID,
  hardcoded GSKU options, edit/rebind and excluded fields;
- Auth/Gateway integration proves permission deny/allow, tenant entitlement, route/method restrictions and preservation
  of auth/tenant headers. These tests do not convert candidate permissions into onboarding authority.

#### Code-start gates

- [x] The user separately authorized and implemented subworks A-D on 2026-08-06; E remains gated.
- [x] Product Data Owner defined `DRAFT | IDENTITY_APPROVED` as the Finished Good Draft referenceability set.
- [x] Product Data and UX owners fixed the non-persisted GSKU display projection to `CanonicalCode` only.
- [x] The technical `IdempotencyKey` normalization/replay contract was approved without a new header or protocol.
- [x] Existing MDM numeric search, page-size and selector bounds were approved for reuse.
- [x] Existing Product Definition Revision + First GSKU code truth, Module Pack wording and implementation tracker drift
      are reconciled as predecessor evidence; no production-readiness claim is inferred.
- [x] Audit enum append-only compatibility and the Finished Good extension plan for
      `AuditIntentDeliveryRepository` are accepted; hosted delivery/transport scope remains unchanged.
- [ ] Before E or any user enablement, the two permission candidates must be onboarded under the separate MOD-0018
      owner artifact and all producer contracts plus required live owner evidence must be accepted.

### Ordered named step — `Global Product Register Exposure & UI`

This is one delivery step with ordered subwork A-E. Teams may prepare contracts in parallel, but consumers follow the
accepted producer contract. No row below is code-start authority, and B+C are mandatory before production/user
enablement.

| Order / subwork | Owner and exact repo scope | Protected paths / non-scope | Code-start gate | Acceptance criteria | Test expectations |
|---|---|---|---|---|---|
| **A — MDM Global Product API/read-selector** | MDM owner; `GlobalProduct.cs`; Global Product create request/result, command/handler/validator; `services/Diten.MdmService/src/Diten.MdmService.Api/Controllers/GlobalProductsController.cs`; Global Product-only queries/DTOs/handlers; `IGlobalProductRepository` and its Mongo implementation; corresponding MDM unit/integration/real-Mongo tests | Existing reservation allocation/consume/no-reuse behavior; Product Definition Revision, GSKU, LSKU, Finished Good, ABB and Composition; no Gateway/Auth/Platform/frontend files | **Authorized and implemented 2026-08-04.** Maximum length 200 Unicode scalars; visible trim; duplicate key FormKC + invariant case-folding; tenant unique index retains tombstones; no edit/rename semantics | Required `GlobalProductName` is persisted and returned; Section 15 routes/DTOs/CQRS mappings work through `Response<T>` + `CustomBaseController`; same-tenant/non-disclosure, bounded search and minimal selector contract hold; no update endpoint | Passed unit/API-contract and non-optional real-Mongo create/tenant/search/paging/soft-delete/cross-tenant tests; full MDM count recorded in the implementation tracker |
| **B — Auth permission catalog/onboarding provider work** | Auth/Platform owners under their separately authorized artifacts; provider-owned `services/Diten.AuthService/**` catalog/seed/grant and `services/Diten.Platform/**` catalog/entitlement scopes only | This MDM pack, MDM runtime and role-bypass inventions; no permission key beyond owner-accepted existing candidates | Provider-owner acceptance, MOD-0018/policy-boundary compliance and their own code-start authority | `read` and `create` are cataloged, seedable, grantable, tenant-entitled and testable; MDM remains declarer/consumer, not SoR | Catalog uniqueness/seed idempotency, grant/deny, tenant entitlement, token-claim and endpoint allow/deny evidence |
| **C — Gateway route** | `integration-agent` only; the exact MDM Ocelot configuration file under `gateway/Diten.ApiGateway/**` for the Section 15 base/catch-all pair | No frontend/MDM/Auth/Platform changes; no general CodeReservation route or extra methods | A's route contract frozen, integration-agent authorization and route review | Gateway `5000` maps the two upstream patterns and bounded methods to MDM `5059`; auth and tenant headers are preserved | Ocelot route-match/method negative tests and Gateway-to-MDM smoke with unauthorized/forbidden/not-found mapping |
| **D — Global Product tenant UI** | MDM frontend owner; only the Global Product paths listed in Section 11 and narrowly accepted shared localization keys | Archive views/controllers, frozen `_Layout.cshtml`, Gateway config, service/Auth/Platform code, other MDM aggregates and ABB views | A contract frozen, localization keys approved, explicit named-step/frontend code-start; B+C are enablement rather than implementation-start gates | Tenant-shell Golden Slim register provides DataTable v2 list/filter/details and one-field `GlobalProductName` create with SweetAlert2 confirmation through same-origin proxy; read-only code; no edit/fake lookup/direct port | DataTable Slim verifier, one-field/controller/proxy/unit tests, seven-locale checks and browser smoke/negative network assertions |
| **E — ABB consumes the same selector** | MOD-0290-FU01 MDM frontend owner under that pack's separately authorized ABB UI scope | This pack's Global Product rules/runtime; no duplicate selector, UUID input, cached/hardcoded list or ABB lifecycle change | A selector contract is frozen; MOD-0290-FU01 separately authorizes ABB UI code-start; B+C remain user/production-enablement gates | ABB Global Product input uses only the same-origin proxy for `/api/global-products/selector` and renders exactly `Id`, `CanonicalCode`, `GlobalProductName` | ABB form/browser tests for search/select, empty/error/403, tenant isolation and no UUID/manual/hardcoded/direct-port fallback |

Dependencies are strict: D and E may implement against the frozen A contract after their explicit code-start approvals,
but neither can be user/production-enabled without B+C. E also requires its own pack authority. This named step neither
copies nor changes ABB business rules.

#### Subwork D controlled DataTable verifier variance

The Global Product tenant UI uses the proxy profile and the canonical same-origin browser surface
`/MasterDataManagement/GlobalProducts/api`. The Global Product DataTable verifier must be run with
`--area MasterDataManagement --module GlobalProducts --reference slim --api-profile proxy`. After the proxy-route
contract is satisfied, every applicable verifier check must pass.

This first UI slice is intentionally read-only plus create: edit, delete, bulk delete, selection checkboxes and a bulk
action bar are prohibited. Therefore only the verifier checks named below are accepted variances for Subwork D:

- `Edit`
- `BulkDelete`
- `BulkDeleteConfirm`
- select-all checkbox
- bulk config
- bulk selection
- `/bulk` endpoint
- bulk trigger
- bulk reload lifecycle
- clear selection

No fake endpoint, unused localization bridge, inert checkbox or non-functional UI may be added to satisfy these checks.
This variance is local to the MOD-0290 Global Product read-only/no-bulk slice; it changes no global standard and creates
no precedent for future CRUD modules. Any verifier failure outside these ten named checks blocks completion.

### Ordered named step - `Product Definition Revision + First GSKU Register Exposure`

This is an additive named step inside canonical `MOD-0290`; it is not a new MOD, FU or DCP. The user approved the
page, field, permission and initial-navigation direction on 2026-08-06. Pack status remains `in-progress`; this
authoring revision alone grants no code-start authority.

#### Locked visible scope

- Tenant route: `/MasterDataManagement/Gskus`.
- Shell: `tenant`; every Razor surface explicitly sets `Layout = "_LayoutTenantShell"`.
- UI baseline: Golden Reference Slim, `form_field_count: 3`.
- User-entered fields are exactly:
  1. `GlobalProductId` selected by AJAX.
  2. `PackQuantity`.
  3. `PackUomCode` selected only from a verified published provider contract.
- `PackApplicabilityCode` is server-resolved as `SCALAR_QUANTITY_APPLIES` and is not a form field.
- `CanonicalCode` and `RevisionIdentifier` are server-generated, read-only results.
- Product Definition Revision is created only by the combined first-GSKU command. No independent empty Revision
  create/edit page, endpoint or command is added.
- First visible phase has list, detail, Global Product selector and create-first-GSKU only. Existing internal
  `UpdateGskuDraftCommand` remains unexposed until a later explicit approval.
- No edit, update, delete, bulk, checkbox/select-all, lifecycle, submit, approval or retirement action exists.
- Composition, MA, LSKU, Finished Good, artwork, site, manufacturer, GTIN, regulatory and additional packaging fields
  are outside this step.
- Initial rollout is direct URL only. Manifest/navigation declaration remains `IsNavigationVisible: false` until the
  final navigation decision.

#### Public transport, idempotency and reservation decision

Selected model: **same-origin MVC/BFF transport idempotency plus MDM application-owned reservation orchestration**.

1. Browser posts only the three business values to the same-origin MVC action and never generates tenant, token,
   reservation, canonical/revision code, catalog-evidence or idempotency facts.
2. The MVC/BFF creates one random operation ID and protects it with the application's stable ASP.NET Core Data
   Protection key ring when it issues the form attempt. The browser only round-trips the resulting opaque, signed
   form-attempt token as transport metadata; JavaScript neither creates nor interprets it, and it is not part of the
   three-field MDM request DTO. MVC rejects a missing, expired or invalid token with `400`, derives the same operation
   key on retry and rotates the token only after terminal `201`. A `202` keeps the same token for reconciliation/replay.
3. `GskusController` accepts the three-field public body plus trusted transport metadata and dispatches one MDM
   application facade command. It contains no reserve/consume/create multi-write sequence.
4. The application facade reserves `CodeBearingEntityType.Gsku` with a stable derivative of the operation key, then
   adapts to the existing internal `CreateFirstGskuDraftCommand` by supplying `GskuReservationId`,
   `ExpectedReservationVersion` and normalized `CreationCommandId` server-side.
5. Existing `CreateFirstGskuDraftCommand` / handler / validator remain the internal pair-creation boundary unless an
   implementation-blocking reuse defect is separately demonstrated and approved. Its internal response is sanitized;
   public output never exposes reservation ID/version, creation command ID or catalog evidence/version fields.
6. There is no public CodeReservation endpoint and no general reservation permission. A controller-level two-call
   reservation/create pattern is prohibited.

The selected model follows the current Finished Good application-owned orchestration precedent and avoids copying the
more fragile Global Product MVC multi-write flow.

Data Protection behavior is frozen as follows:

- The key-ring must be shared, persisted and verified across process restart and multiple frontend instances; an
  instance-local or ephemeral key-ring is not acceptable evidence.
- A missing, expired or tampered form-attempt token returns exact `400`, and no MDM mutation, reservation or provider
  call starts.
- Every replay within the same form attempt derives the same server-owned operation key. The browser cannot derive,
  choose, inspect or generate either the token contents or the operation key.
- After `202`, the same opaque token remains replayable for reconciliation and continues to derive the same operation
  key.
- After terminal `201`, that attempt is closed; a subsequent create requires a newly issued attempt token and derives
  a new operation key.

Public create body:

```text
GlobalProductId
PackQuantity
PackUomCode
```

Public successful result:

```text
GskuId
CanonicalCode
GlobalProductId
ProductDefinitionRevisionId
RevisionIdentifier
PackQuantity
PackUomCode
LifecycleStatus
Version
```

#### Exact API and HTTP contract

Frozen core route family:

| Verb / route | Permission | Contract |
|---|---|---|
| `GET /api/gskus` | `mdm.gskus.read` | Bounded, tenant-scoped, soft-delete-aware list using the approved projection below. |
| `GET /api/gskus/{id}` | `mdm.gskus.read` | Same projection with non-disclosing missing/deleted/cross-tenant 404. |
| `GET /api/gskus/create-options` | `mdm.gskus.create` | Bounded create-options envelope; excludes retired/non-referenceable Global Products before paging and consumes verified UoM enumeration. |
| `POST /api/gskus/drafts` | `mdm.gskus.create` | Three-field public request; MDM facade owns reservation and delegates to the existing internal combined command. |

UoM enumeration is provider-owned work in `MOD-0048-FU01`. Its bounded universal enumeration contract, Platform
query/handler/controller/static-catalog implementation and provider tests must close there before MOD-0290 A starts.
MOD-0290 neither owns nor allow-lists those Platform files. The MDM delta is consumer-only: an additive enumeration
method on `IVerifiedGskuReferenceResolver`, `PlatformVerifiedGskuResolverClient`, the MDM create-options
query/handler/facade and their MDM contract/client/facade tests. Hardcoded UoM values, generic PSS-012 results, cache
fallback and browser-supplied metadata are prohibited availability or trust sources.

`GET /api/gskus/create-options` returns exactly this envelope:

```text
GlobalProducts[]:
  Id
  CanonicalCode
  GlobalProductName
Uoms[]:
  Code
  DisplayText                  # provider display text
  SortOrder
  MaximumDecimalPrecision
```

`CatalogVersionId`, `CatalogVersionNumber`, `ResolutionMode`, `ResolvedAtUtc` and credential data remain server-side
and are never returned to the browser. There is no assignment/reference-tenant/publication evidence for these two
universal families. Create
re-resolves and revalidates `PackUomCode` through the verified provider; it never trusts option metadata or prior
create-options results round-tripped by the browser.

Status mapping for `POST /api/gskus/drafts` and dependency-backed selectors is exact:

| HTTP | Meaning |
|---:|---|
| `200` | Successful list, detail or create-selector read; never a create success. |
| `201` | Revision + GSKU created and reservation binding confirmed. |
| `202` | Reconciliation required; `IsSuccessful=false`; never presented as UI success and never closes/resets the form as success. |
| `400` | Validation, precision, forbidden/unknown client field or malformed public request. |
| `401` | Unauthenticated MDM caller. |
| `403` | Authenticated caller missing the required GSKU permission. |
| `404` | Missing/deleted/cross-tenant GSKU or Global Product, without disclosure. |
| `409` | Idempotency/pair/reservation/concurrency/lifecycle/reference-contract conflict. |
| `503` | Provider unavailable, provider configuration/credential failure or malformed provider evidence. |
| `504` | Verified provider timeout. |

An exact same-operation replay returns the original terminal `201` facts; it does not create a second reservation,
Revision ordinal or GSKU. No `200` create-success variant and no `PUT`, `PATCH` or `DELETE` GSKU endpoint is part of
this step.

#### List/detail projection contract

List and detail expose at least:

| Field | Source / persistence decision |
|---|---|
| `Id` / GSKU ID | Persisted on GSKU. |
| `CanonicalCode` | Persisted on GSKU; system generated. |
| `GlobalProductId` | Persisted on Product Definition Revision; joined read projection. |
| `GlobalProductCanonicalCode` | Persisted on Global Product; joined display projection, not copied to GSKU. |
| `GlobalProductName` | Persisted on Global Product; joined display projection, not copied to GSKU. |
| `ProductDefinitionRevisionId` | Persisted on GSKU. |
| `RevisionIdentifier` | Persisted on Product Definition Revision; joined display projection, not copied to GSKU. |
| `PackQuantity`, `PackUomCode` | Persisted on GSKU. |
| `LifecycleStatus`, `Version`, `CreatedAt`, `UpdatedAt` | Persisted GSKU/base fields. |

Joined values are query-time projections only. Query/repository implementation must be tenant-safe, deterministic and
batch/aggregation based; N+1 parent reads are not accepted. No synthetic display field, MarketTradeName, Composition,
regulatory, site, manufacturer, GTIN or Finished Good projection is added.

#### UI contract

- Slim file pattern is create-only/read-only variance: Index-hosted create offcanvas plus read-only quick view.
- One same-origin AJAX create-options action unwraps
  `GET /api/gskus/create-options`: bounded Global Product options plus versioned universal UoM options.
  Typed UUID, free text, stale/cached or hardcoded lists and browser-direct provider/Gateway/service calls are
  prohibited.
- Quantity is positive and obeys the provider-backed UoM precision contract; no silent rounding.
- Successful `201` displays `CanonicalCode` and `RevisionIdentifier` as read-only result values.
- `202` displays a localized reconciliation-pending state, does not show success toast and does not treat the record as
  safely created.
- A Viewer with `mdm.gskus.read` sees list/detail only. The create button, Global Product selector, UoM selector and
  create transport are absent/denied without `mdm.gskus.create`.
- Browser JS calls only `/MasterDataManagement/Gskus/...`; MVC forwards HttpOnly authentication and trusted tenant
  context to Gateway `5000`. Browser code never creates or forwards raw bearer, `TenantId`, operation key,
  reservation, catalog metadata/evidence, reference-tenant identity or provider credentials.
- Seven locales (`en`, `fr`, `es`, `zh`, `ar`, `ru`, `tr`) and `_IndexL10n.cshtml` / `index.l10n.js` are required.
- There is no bulk action bar, checkbox, edit/delete/lifecycle control or fake endpoint to appease the verifier.

#### Permission and entitlement contract

- Exact permission keys: `mdm.gskus.read`, `mdm.gskus.create`.
- Both belong to the existing `product-item-sku-master` ModuleCode/entitlement.
- `ProductItemSkuMasterManifestProvider` declares a nav-hidden `GSKUS` page with only `ADD_NEW` and `VIEW_DETAILS`.
- These keys must not be replaced by or mixed with `mdm.global-products.*`, `mdm.finished-goods.*` or broad `manage`.
- MDM declares/enforces the keys. Catalog/seed/grant/token/role/tenant-entitlement onboarding belongs to a separately
  approved MOD-0018 owner follow-up. This task creates no FU identity and changes no Auth/Platform runtime.
- MDM API/frontend implementation may start only after explicit named-step code-start; production/user enablement is
  impossible until permission onboarding and end-to-end allow/deny evidence close.

#### Provider and BRD readiness gate

- `MOD-0048-FU01` owns the universal `GSKU-UNIVERSAL-V1` catalog and bounded UoM enumeration. MOD-0290 consumes that
  authenticated contract and does not duplicate or hardcode the values.
- The exact universal values are `SCALAR_QUANTITY_APPLIES` and `C62`, `GRM`, `KGM`, `MLT`, `LTR` with precision
  `0,3,3,3,3`. Tenants cannot add, edit, retire or override them.
- No `ReferenceTenantId`, consumer assignment, Mongo publication, loader, publisher, operational runner or BRD
  provisioning is required for these two families.
- Resolver credential, independently validated delegated tenant JWT, bounded timeout and strict response evidence
  remain mandatory. Unauthenticated/forbidden calls remain `401/403`; malformed reference contract remains `409`;
  provider unavailable is `503`; timeout is `504`.
- A new catalog value or semantic change requires a new MOD-0048 deployment version and deterministic version
  identity. Browser metadata and tenant-provided evidence remain prohibited.
- Runtime code completion does not itself authorize GSKU mutation, navigation or production enablement; read-only
  provider/MDM smoke remains required after updated binaries restart.

#### Exact A-H delivery order and allow-list

The control-tower delivery sequence is revised as seven ordered entries:

1. `MOD-0048-FU01` universal lookup implementation and focused regression evidence.
2. MOD-0290 A backend/facade/create-options consumer.
3. MOD-0290 B API/manifest.
4. C Gateway.
5. D Frontend.
6. E MOD-0018-FU18 permission onboarding followed by G live read-only/integration smoke.
7. H Navigation decision.

Rows A-E, G and H retain execution ownership and evidence boundaries. Former F reference-tenant/catalog provisioning
is removed for these universal sets. Every row requires its stated authorization; completion of one row does not
authorize the next. Provider predecessor entry 1 is wholly owned by `MOD-0048-FU01`.

**A - Backend facade, queries and repository projections**

Exact files:

- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/ProductItemSkuMasterModels.cs` - public GSKU DTO additions only.
- New `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Commands/CreateFirstGskuDraftFacadeCommand.cs`.
- New `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Handlers/CommandHandlers/CreateFirstGskuDraftFacadeHandler.cs`.
- New `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Validators/CreateFirstGskuDraftFacadeValidator.cs`.
- New exact query files:
  `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Queries/GetGskusQuery.cs`,
  `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Queries/GetGskuByIdQuery.cs`
  and
  `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Queries/GetGskuCreateOptionsQuery.cs`;
  the last query owns the create-options envelope and calls the accepted verified UoM enumeration contract.
- New exact query-handler files:
  `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Handlers/QueryHandlers/GetGskusHandler.cs`,
  `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Handlers/QueryHandlers/GetGskuByIdHandler.cs`
  and
  `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Handlers/QueryHandlers/GetGskuCreateOptionsHandler.cs`.
- New exact query-validator files:
  `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Validators/GetGskusValidator.cs`
  and
  `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Validators/GetGskuCreateOptionsValidator.cs`.
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/IGskuRepository.cs`.
- `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/GskuRepository.cs`.
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/IProductDefinitionRevisionRepository.cs`.
- `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/ProductDefinitionRevisionRepository.cs`.
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/IGlobalProductRepository.cs`.
- `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/GlobalProductRepository.cs`.
- Exact MDM consumer allow-list for the additive enumeration delta, after both MOD-0048-FU01 predecessor gates close:
  - `services/Diten.MdmService/src/Diten.MdmService.Application/Contracts/ReferenceData/IVerifiedGskuReferenceResolver.cs` - additive bounded enumeration contract only.
  - `services/Diten.MdmService/src/Diten.MdmService.Infrastructure/ReferenceData/PlatformVerifiedGskuResolverClient.cs` - typed consumer implementation only.
  - `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Queries/GetGskuCreateOptionsQuery.cs`.
  - `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Handlers/QueryHandlers/GetGskuCreateOptionsHandler.cs`.
  - `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/GskuCreateOptionsFacade.cs`.
- Exact MDM contract/client/facade test allow-list for that delta:
  `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ReferenceData/VerifiedGskuReferenceResolverContractTests.cs`,
  `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ReferenceData/PlatformVerifiedGskuResolverClientTests.cs`,
  `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ReferenceData/VerifiedGskuDelegatedTokenForwardingTests.cs`
  `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ReferenceData/VerifiedGskuResolverDependencyInjectionTests.cs`
  and `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/GskuCreateOptionsFacadeTests.cs`.
- New exact test files:
  `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/GskuRegisterFacadeTests.cs` and
  `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/GskuRegisterMongoTests.cs`.

Protected: every `services/Diten.Platform/**` file, including provider query/handler/controller/repository and tests;
existing internal Create/Update command/handler/validator; CodeReservation repository behavior; other aggregates;
API/frontend/Gateway/Auth/config/hosted-worker files. The existing hardcoded internal UoM checks must not be copied
into the public facade/create-options path or used as an availability fallback; generic PSS-012 results, cache values
and browser metadata are equally prohibited. Changing those existing internal files requires a separately approved,
evidenced defect scope.

Acceptance/test gate: three-field public contract; stable server idempotency; one GSKU reservation; unchanged internal
pair behavior; same-operation replay; 202 reconciliation; 409 fact conflict; tenant-safe N+1-free projections;
non-referenceable Global Product exclusion; universal enumeration and precision evidence; real-Mongo
paging/order/soft-delete/cross-tenant/concurrency proof.

Test plan: facade unit tests cover protected-token replay facts, reservation/combined-command delegation,
`PackUomCode` provider revalidation at create, strict three-field mapping and `201/202/400/404/409/503/504`; resolver
tests cover bounded enumeration, absence of forbidden metadata, delegated auth, identical cross-tenant universal
values, provider unavailable/configuration `503` and timeout `504`; Mongo tests prove deterministic batch projections, no
N+1, soft-delete, tenant isolation and concurrency. **Code-start gate:** MOD-0048-FU01 operational publication
universal lookup and bounded UoM enumeration are green; the MDM DTO/failure/test contract is frozen; the user
then separately authorizes exact subwork A.

**B - MDM API and manifest declaration**

Exact files:

- New `services/Diten.MdmService/src/Diten.MdmService.Api/Controllers/GskusController.cs`.
- `services/Diten.MdmService/src/Diten.MdmService.Api/ModuleRegistration/ProductItemSkuMasterManifestProvider.cs`.
- New `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/GskuApiContractTests.cs` and
  `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/GskuAuthorizationTests.cs`; update
  `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ModuleRegistration/ProductItemSkuMasterManifestProviderTests.cs`
  only for the additive GSKU page/permission union.

Protected: public reservation/update/delete/lifecycle routes; Auth seed/grant; Platform entitlement; Gateway/frontend.

Acceptance/test gate: exact approved routes/statuses; `Response<T>` + `CustomBaseController`; strict unknown/technical
field rejection; exact permissions; nav false; no technical response leakage; no update/reservation endpoint.

Test plan: API contract tests assert the exact four-route allow-list, the exact create-options envelope, create replay
`201`, reconciliation `202` with `IsSuccessful=false`, strict body rejection and no technical response fields;
authorization tests cover anonymous,
Viewer and creator paths; manifest tests prove `GSKUS`, two actions, entitlement union and navigation false.
**Code-start gate:** A is green and the user separately authorizes exact subwork B.

**A-B implementation evidence - 2026-08-07:** The user explicitly authorized exact subworks A and B. MDM now exposes
the bounded GSKU list/detail/create-options application surfaces, consumes Platform's authenticated bounded universal
UoM enumeration without a local/browser fallback, and performs server-owned reserve -> existing combined
Revision/GSKU create -> binding confirmation/reconciliation through the public facade. The API contains exactly the
four frozen routes with `mdm.gskus.read`/`mdm.gskus.create`; at that A-B checkpoint `GSKUS` was navigation-hidden and
declared only `ADD_NEW` and `VIEW_DETAILS`. Release build passed with 0 warnings/errors. Focused unit/contract/real-
Mongo evidence passed 41/41 with 0 skipped, and the full MDM suite passed 298/298 with 0 skipped. Later separately
authorized C-G delivery and the current visible manifest supersede that checkpoint status.

**C - Gateway**

Exact file: `gateway/Diten.ApiGateway/ocelot.json`, integration-agent only.

Acceptance/test gate: base `/api/gskus` and catch-all `/api/gskus/{everything}` map to MDM `5059`; methods are exactly
`GET`, `POST`, `OPTIONS`; routes precede fallback; auth/tenant/correlation headers survive. `PUT`, `PATCH`, `DELETE` and
general CodeReservation routes are absent.

Protected: all MDM/frontend/Auth/Platform files and unrelated Gateway routes.

Test plan: parse `ocelot.json`; assert exactly two GSKU templates, port `5059`, method allow-list and route order; smoke
OPTIONS plus authorized GET/POST header forwarding without exposing a reservation route. **Code-start gate:** B route
contract is frozen, the user separately authorizes C and `integration-agent` owns the edit.

**D - Golden Slim tenant frontend**

Exact files:

- New `frontend/Diten.Web/Controllers/GskusController.cs`.
- New `frontend/Diten.Web/Models/Gskus/GskuViewModels.cs`.
- New `frontend/Diten.Web/Views/MasterDataManagement/Gskus/Index.cshtml`, `_Filter.cshtml`, `_DataTable.cshtml`,
  `_IndexL10n.cshtml`, `_CreateEditOffcanvas.cshtml`, `_DetailsQuickView.cshtml`, `GskusIndex.cs`.
- New `frontend/Diten.Web/wwwroot/assets/js/MasterDataManagement/Gskus/index.js` and `index.l10n.js`.
- New `frontend/Diten.Web/Resources/Views/MasterDataManagement/Gskus/GskusIndex.{en,fr,es,zh,ar,ru,tr}.resx`.
- New `frontend/Diten.Web/tests/gsku-register.test.js`.

Protected: `_Layout.cshtml`, archive/frozen paths, navigation visibility changes, shared CSS/JS contract rewrites,
Gateway/service/Auth/Platform code and all unrelated frontend modules.

Acceptance/test gate: explicit tenant layout; direct route; three inputs; verified AJAX selectors; read-only result and
details; Viewer create hidden/denied; 202 non-success; proxy-only traffic; no technical/forbidden fields; Slim verifier
with reviewed create-only/read-only variances only; seven-locale parity and browser smoke.

Test plan: frontend/controller tests cover a persisted shared key-ring across process restart and multiple frontend
instances; missing/expired/tampered token `400` with zero MDM mutation; same-attempt stable operation key; replay with
the same token after `202`; token rotation/new operation key only after terminal `201`; browser inability to create or
inspect token contents/operation keys; anti-forgery; four-route proxying; Viewer/creator rendering; three fields;
create-options envelope; positive/provider-precision quantity; `201` read-only results and `202` non-success. Run the
Slim verifier, seven-RESX parity and browser network smoke.
**Code-start gate:** C is green, verified enumeration is available and the user separately authorizes exact subwork D.

**E - MOD-0018-FU18 permission onboarding**

Exact allow-list: none in this pack. Work is owned by existing `MOD-0018-FU18` and only the exact Auth/Platform files
that follow-up names. Protected paths: this MOD-0290 pack, all MDM runtime/frontend/Gateway files
and every Auth/Platform file not expressly owned by that follow-up. Acceptance criteria: both exact keys are cataloged
under `product-item-sku-master`, seed/grant operations are idempotent, the tenant entitlement carries them and token
claims distinguish Viewer read from creator create. Test plan: catalog uniqueness, repeated seed, grant/revoke,
entitlement sync, token claim and endpoint allow/deny evidence. **Code-start gate:** MOD-0018-FU18 is separately
approved and its owner explicitly authorizes E; this pack neither creates that FU nor grants its start.

**F - BRD local readiness and catalog publication**

Removed for the exact universal `pack-applicability` and `uom` families by the 2026-08-07 decision. No reference
tenant, assignment, seed/load/publish, governance-mode eligibility, Mongo mutation or catalog provisioning operation
is permitted or required. Existing generic BRD provisioning remains protected and unrelated to this GSKU step.

**G - Integration and live smoke**

Exact allow-list: no production source/config file; read-only use of the frozen A-F binaries, existing test commands
and existing smoke harness only. Protected paths: the whole worktree from implementation edits, test-data mutation
outside the named pilot tenants and any production tenant. Acceptance criteria: frontend `5001` -> Gateway `5000` ->
MDM `5059`; `201` and same-fact replay; `202` non-success; `400/401/403/404/409/503/504`; tenant isolation; provider
failure/timeout; Viewer/creator permissions; manifest entitlement; no direct port or technical browser field. Test
plan: capture HTTP/network, logs and database facts for every listed path and prove one reservation/Revision/GSKU pair
after replay. **Code-start gate:** A-F acceptance evidence is green and the user separately authorizes the exact smoke
environment and pilot tenants.

**H - Navigation decision**

Default remains `IsNavigationVisible: false`. Exact conditional allow-list after a later affirmative decision:
`services/Diten.MdmService/src/Diten.MdmService.Api/ModuleRegistration/ProductItemSkuMasterManifestProvider.cs` and
`services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ModuleRegistration/ProductItemSkuMasterManifestProviderTests.cs`,
limited to the GSKUS visibility/order/display assertion. Protected paths: every other file and every other manifest
page/action/permission. Acceptance criteria: the user explicitly chooses visibility after the direct-URL pilot and no
other page ordering or entitlement changes. Test plan: manifest serialization/registration plus authorized/unauthorized
tenant navigation smoke. **Code-start gate:** G evidence is accepted and the user separately authorizes navigation;
pilot success alone never enables it.

#### Open decisions and A code-start gates

There is no open route-shape, envelope, provider-ownership, fallback or browser-trust decision in this named step.
The API is frozen to four endpoints, the create-options envelope is frozen, MOD-0048-FU01 owns provider enumeration,
and MOD-0290 owns only the MDM consumer delta. The remaining gates are evidence/authorization gates, not permission
to improvise a fifth route or Platform implementation inside this pack.

Before A code-start, all four conditions must be true:

1. MOD-0048-FU01 universal GSKU lookup is implemented and its focused regressions pass.
2. MOD-0048-FU01 has closed its provider-owned versioned universal UoM enumeration contract and evidence.
3. MOD-0290 MDM DTO, exact failure mapping and contract/client/facade test contract are frozen.
4. The user gives a separate, explicit code-start authorization for exact subwork A.

#### Named-step ready-for-dev checklist

- [x] User approved separate GSKU Register, URL, three fields, Slim pattern, permissions and nav-hidden pilot on 2026-08-06.
- [x] Existing internal Create/Update, reservation, resolver, Global Product/Finished Good patterns and manifest were inspected.
- [x] Public reservation endpoint is rejected; facade/idempotency boundary is selected.
- [x] List/detail projection and non-persisted joins are explicit.
- [x] The revised seven-entry control-tower order and A-E/G-H ownership, allow-lists, acceptance criteria and tests are recorded.
- [x] The API is frozen to `GET /api/gskus`, `GET /api/gskus/{id}`, `GET /api/gskus/create-options` and
  `POST /api/gskus/drafts`; no fifth endpoint is introduced.
- [x] Create-options exposes only `GlobalProducts` (`Id`, `CanonicalCode`, `GlobalProductName`) and `Uoms` (`Code`,
  provider `DisplayText`, `SortOrder`, `MaximumDecimalPrecision`).
- [x] Platform provider query/handler/controller/static-catalog files remain MOD-0048-owned; MOD-0290 allow-lists only the exact
  MDM resolver contract/client, create-options query/handler/facade and MDM contract/client/facade tests.
- [x] MOD-0048-FU01 operational publication/assignment is not required for the two universal GSKU sets.
- [x] MOD-0048-FU01 universal lookup and bounded UoM enumeration implementation is present with focused evidence.
- [x] MDM DTO, exact `404/503/504` failure mapping and contract/client/facade tests are frozen.
- [x] Exact A code-start was separately authorized and A passed its focused/full MDM evidence on 2026-08-07.
- [x] Exact B code-start was separately authorized and its API/authorization/manifest evidence passed on 2026-08-07.
- [x] C-G received separate owner/code-start or operational authorization and closed their evidence.
- [x] MOD-0018-FU18 permission onboarding and Local Development provider/consumer smoke are accepted.
- [x] Local Development integration/smoke evidence is recorded; it is not Production-readiness evidence.
- [x] The current manifest makes `GSKUS` visible; this reconciliation performs no navigation mutation.

### Ordered named step - `LSKU Draft Identity Foundation`

This is an additive backend-only step inside canonical `MOD-0290`. The user accepted the narrow direction on
2026-08-07 and explicitly authorized this exact named-step code-start on 2026-08-08. The authorization and resulting
implementation do not imply LSKU API, Gateway, frontend, permission onboarding, workflow, production enablement or
readiness.

#### Locked first-slice identity contract

- An LSKU belongs to exactly one immutable `GskuId` and one immutable, provider-verified `MarketCode`.
- One GSKU may have zero-to-many LSKUs, but at most one non-reusable identity may ever be allocated for the same
  `TenantId + GskuId + MarketCode`. Soft delete, retirement or tombstoning never frees that identity key.
- Draft create accepts only a same-tenant, non-deleted GSKU in `DRAFT` or `IDENTITY_APPROVED`. Missing,
  cross-tenant, soft-deleted, `PENDING_IDENTITY_APPROVAL` and `RETIRED` parents fail closed with the same
  non-disclosing referenceability result. A later LSKU approval step, which is outside this slice, must require an
  `IDENTITY_APPROVED` parent.
- `CanonicalCode` is generated only by the existing common reservation -> consume -> identity-write -> binding-confirm
  flow using the LSKU code family. It is immutable, tenant-wide unique across all code-bearing identities and never
  client supplied or reused.
- `MarketCode` is not free text or an enum embedded in MDM. For the first LSKU phase it is an exact ISO 3166-1
  alpha-2 country code matching `^[A-Z]{2}$`, selected from the versioned, active universal `market` set owned by the
  existing MOD-0048-FU01 Business Reference Data provider boundary and re-resolved server-side at create. Request-time
  trimming, uppercasing, case-folding, alias or fuzzy conversion is prohibited. Country-external commercial or
  regulatory groupings are deferred. The client never supplies catalog version, reference-tenant, assignment,
  credential or resolution evidence.
- The first slice persists the provider-resolved market evidence using the existing six-field
  `ReferenceCatalogSelection` shape: `SetCode`, `ValueCode`, `CatalogVersionId`, `CatalogVersionNumber`,
  `ResolutionMode` and `ResolvedAtUtc`. `SetCode` is server-controlled as `market`; Draft resolution uses `LATEST`.
- `LegalEntityId` is intentionally absent from the first-slice entity and DTO. No null placeholder, copied Legal Entity
  data or synthetic default is persisted. G6 therefore does not block this slice; any later Legal Entity binding is an
  additive, separately approved LSKU step and stores only the MOD-0220 reference.
- `MarketTradeName`, `FinishedGoodId`, MA/Registered Presentation, Market Supply Assignment, artwork, packaging,
  manufacturer, site, GTIN, Composition, regulatory and supply-readiness fields are prohibited.
- There is no create/list/detail controller, public reservation endpoint, update, delete, submit, decision, retirement
  or UI surface in this backend-only foundation.

#### Provider/consumer boundary and code-start gate

MOD-0048-FU01 owns the `market` catalog, publication/readiness, bounded active-market resolution and its Platform
runtime/tests. MOD-0290 owns only the MDM consumer contract, adapter, fail-closed mapping and persisted selection.
No Platform path is authorized by this pack. The first-phase source/grammar decision closed on 2026-08-07. Before
LSKU code-start, the provider owner must provide:

1. implemented/tested exact-code resolve for `SetCode=market` and ISO 3166-1 alpha-2 `^[A-Z]{2}$` country codes;
2. latest resolution and historical version evidence using the existing verified provider security boundary;
3. stable non-leaking missing/not-assigned, unavailable/configuration and timeout outcomes mapping to `404`, `503`
   and `504`; and
4. focused provider contract evidence without hardcoded MDM fallback, direct Mongo provisioning or test-only seams.

The current official source snapshot, complete rows, usage/license basis, immutable provider version and artifact hash
remain `MARKET-ARTIFACT-01` operational-provisioning evidence. They do not reopen the runtime design but must close
before real catalog load/publication. After the provider code contract is evidenced, this exact LSKU allow-list still
requires a separate explicit user code-start authorization.

#### Exact runtime allow-list

- `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/Lsku.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Enums/AuditAggregateType.cs`, LSKU append-only member only.
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Enums/ProductAuditOperation.cs`, LSKU append-only members only.
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/ILskuRepository.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Contracts/ReferenceData/IVerifiedMarketReferenceResolver.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/ProductItemSkuMasterModels.cs`,
  LSKU foundation DTO additions only.
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Commands/CreateLskuDraftCommand.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Handlers/CommandHandlers/CreateLskuDraftHandler.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Validators/CreateLskuDraftValidator.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/LskuRepository.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/AuditIntentDeliveryRepository.cs`, LSKU
  routing/discovery/claim/acknowledgement/compaction extension only.
- `services/Diten.MdmService/src/Diten.MdmService.Persistence/DependencyInjection.cs`, LSKU repository registration only.
- `services/Diten.MdmService/src/Diten.MdmService.Infrastructure/ReferenceData/VerifiedMarketResolverOptions.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Infrastructure/ReferenceData/PlatformVerifiedMarketResolverClient.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Infrastructure/DependencyInjection.cs`, resolver registration only.
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/LskuDraftFoundationUnitTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/LskuDraftFoundationMongoTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ReferenceData/VerifiedMarketReferenceResolverContractTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/AuditIntentDeliveryMongoTests.cs`, LSKU-only
  append-only audit-delivery cases.

Everything else is protected for this named step. In particular, `CodeReservationRepository.cs`, existing
Global Product/Revision/GSKU/Finished Good behavior, Legal Entity code, API/controllers, manifests, configuration and
secrets, hosted workers, Auth, Platform, Gateway, frontend, Domain Contract, DCP-004/DCP-005, registries, backlog and
`.antigravity/**` are outside the allow-list.

#### Acceptance criteria and test expectations

- Same-tenant verified-market create persists exactly one LSKU, one LSKU reservation binding and one six-field market
  selection; client-authored technical/provider fields are rejected before mutation.
- Same-command replay returns or completes the same LSKU. Payload drift and cross-reservation command reuse fail with
  stable conflicts and never allocate a second code.
- Concurrent different-command creates for the same `TenantId + GskuId + MarketCode` have exactly one `201` winner
  and one exact `202 LSKU_BINDING_RECONCILIATION_REQUIRED` loser. The repository result contract distinguishes this
  identity-key collision from command/payload conflict without exposing Mongo types outside Persistence. The losing
  reservation remains `Consumed + PendingIdentityWrite`, is never burned or reused, and replay of the losing command
  returns the same pending reconciliation result without allocating another reservation, code or identity.
- Missing, cross-tenant, deleted or non-referenceable GSKU inputs are indistinguishable and fail before reservation.
- Missing/inactive/unassigned market returns the provider-owned non-leaking `404`; provider configuration/unavailable
  returns `503`; timeout returns `504`; no hardcoded, cached-success or free-text fallback exists.
- Mongo unique indexes retain both canonical-code and GSKU/market tombstones. Expected-version predicates protect any
  later internal reconciliation mutation; generic last-write-wins repository updates are not used.
- LSKU audit intents participate in existing discovery, claim fencing, acknowledgement and compaction without changing
  LSKU business `Version` during delivery bookkeeping.
- Unit/contract tests cover validation, forbidden fields, exact provider mapping and exception propagation. Real Mongo
  tests cover tenant isolation, index races, replay/drift, ambiguous recovery, soft-delete no-reuse and audit delivery.
- MDM build, focused LSKU tests, full real-Mongo MDM regression, `git diff --check`, whitespace, conflict-marker and
  final-newline checks must pass before the step may move to review.

#### Implementation evidence - 2026-08-08

- [x] Exact allow-list only: LSKU entity/repository, command/handler/validator/DTO, verified-market MDM consumer,
      repository/client DI, LSKU audit routing and the four allow-listed test files were changed.
- [x] Exact `POST /api/internal/v1/reference-data/verified-market/resolve` typed-client contract persists only the
      six-field `ReferenceCatalogSelection`; provider/client evidence fields remain absent from the business request.
- [x] Targeted identity-key race hardening classifies repository outcomes as `CommandOrPayload` versus `IdentityKey`;
      two real-Mongo concurrent commands produce exactly `1 x 201` and `1 x 202
      LSKU_BINDING_RECONCILIATION_REQUIRED`, with one LSKU, one confirmed winner reservation and one consumed
      `PendingIdentityWrite` loser reservation. Losing-command replay remains the same `202` and does not increase the
      reservation, reserved-code or identity counts. Same-command replay remains `201`; payload drift remains exact
      `409 IDEMPOTENCY_KEY_CONFLICT`; tombstone/soft-delete no-reuse tests remain green.
- [x] Real-Mongo focused Release evidence: `83/83` passed, `0` failed, `0` skipped.
- [x] Full real-Mongo MDM Release regression: `362/362` passed, `0` failed, `0` skipped.
- [x] Isolated-output MDM Release build: `0` errors; `5` pre-existing warnings remain in GSKU/Product Definition
      nullable annotations and the existing Mongo GUID configuration line.
- [x] `git diff --check` plus exact-file trailing-whitespace, conflict-marker and final-newline checks passed.
- [x] No API/controller, manifest, Gateway, frontend, Auth, Platform, configuration/data, provider catalog or
      `MARKET-ARTIFACT-01` provisioning change was made. This evidence is foundation-only and is not production
      readiness or LSKU API/UI enablement evidence.

### Ordered named step - `LSKU Register Exposure & UI`

This is an additive planning-only exposure step inside canonical `MOD-0290`; it is not a new MOD, FU or DCP. It builds
only on the completed LSKU Draft Identity Foundation evidence above. Pack status remains `in-progress`, and this
documentation revision grants no code-start authority for any A-H substep.

#### Locked user surface and scope

- Tenant route: `/MasterDataManagement/Lskus`.
- Shell: `tenant`; every Razor page explicitly sets `Layout = "_LayoutTenantShell"`.
- UI baseline: Golden Reference Slim with `form_field_count: 2`.
- The two and only two user-entered fields are:
  1. `GskuId`, rendered as a bounded AJAX GSKU selector.
  2. `MarketCode`, rendered only from provider-backed active-market enumeration.
- Browser labels may say `GSKU` and `Market`; transport/property names remain `GskuId` and `MarketCode`.
- `CanonicalCode` is server-generated and is displayed read-only only after terminal create or in list/detail.
- The page supports list, detail quick view and Draft create only. There is no edit mode even though the Slim partial
  filename remains `_CreateEditOffcanvas.cshtml` for Golden Reference structural compatibility.
- No edit, update/rebind, delete, bulk, checkbox/select-all, approval, retirement or lifecycle action exists. The
  DataTable omits selection controls and the bulk-action bar.
- Manifest page code is `LSKUS`, starts with `IsNavigationVisible: false`, and declares only `ADD_NEW` and
  `VIEW_DETAILS`.
- Exact out-of-scope concepts are `LegalEntityId`, `MarketTradeName`, `FinishedGoodId`, Market Supply Assignment,
  MA/Registered Presentation, artwork, packaging, manufacturer/site, GTIN, Composition, approval, retirement,
  update/rebind, workflow and production enablement. No placeholder, nullable future field or hidden browser field is
  introduced for them.

#### Frozen projection and create-options contract

The bounded LSKU list/detail projection contains only existing identity facts plus owner-approved display joins:

```text
Id
CanonicalCode
GskuId
GskuCanonicalCode
MarketCode
LifecycleStatus
Version
CreatedAt
UpdatedAt
```

The GSKU display join is a tenant-filtered batch projection; per-row repository calls and N+1 behavior are prohibited.
List/detail do not call the market provider per row and do not synthesize or persist market display text.

`GET /api/lskus/create-options` is bounded and returns exactly:

```text
Gskus[]:
  Id
  CanonicalCode
  GlobalProductCanonicalCode
  GlobalProductName
  RevisionIdentifier
  PackQuantity
  PackUomCode
Markets[]:
  Code
  DisplayText
  SortOrder
```

Every GSKU option is same-tenant, non-deleted and referenceable before paging. The listed GSKU fields are a strict
subset of the already owner-approved `GskuListItemDto` projection; this step invents no new GSKU business field.
Markets come only from the existing provider-owned active-market enumeration. `CatalogVersion`,
`ReferenceTenantId`, credentials, publication state, assignment evidence and resolution evidence never cross the
browser boundary. Hardcoded ISO lists, cached-success/browser fallback and free-text market entry are prohibited.
Create always re-resolves the submitted exact `MarketCode` through the verified provider; create-options is not
authorization or freshness evidence for mutation.

#### Create transport and idempotency contract

Selected model: **same-origin MVC/BFF form-attempt protection plus MDM-owned LSKU creation**.

1. Browser posts only `GskuId` and `MarketCode` to the same-origin MVC action. It never creates or sends `TenantId`,
   UUID/identity ID, reservation ID/version, canonical code, provider credential, catalog/publication/assignment
   evidence or a client-authored idempotency key.
2. MVC issues an opaque time-limited form-attempt token protected by ASP.NET Core Data Protection and backed by the
   application's stable shared key ring. The token is bound to the authenticated subject. Missing, expired, tampered
   or wrong-subject tokens return `400` before Gateway/MDM/provider activity.
3. MVC derives the same stable server-owned `Idempotency-Key` for every replay of that form attempt and forwards it as
   trusted transport metadata. The token and operation key remain opaque to JavaScript.
4. The MDM API accepts a strict two-field body, reads `Idempotency-Key` from the trusted header and dispatches the
   existing `CreateLskuDraftCommand`; it exposes no public reserve/consume/confirm sequence.
5. Terminal `201` closes the attempt and the MVC response returns a newly protected token for a future create.
   `202 LSKU_BINDING_RECONCILIATION_REQUIRED` is a warning/non-success, preserves the same token and operation key,
   keeps the form open and permits exact replay/reconciliation.
6. Exact fail-closed create mapping is `404` for non-referenceable GSKU or market, `409` for idempotency/fact/
   reservation conflict, `503` for provider configuration/unavailability/malformed evidence and `504` for provider
   timeout. `400/401/403` retain their standard validation/auth meanings. No fallback converts these outcomes into a
   success.

Public create body:

```text
GskuId
MarketCode
```

Sanitized create result:

```text
LskuId
CanonicalCode
GskuId
GskuCanonicalCode
MarketCode
LifecycleStatus
Version
```

`ReferenceCatalogSelection`, `CodeReservationBindingState`, reservation/command IDs and all provider evidence remain
server-side. A `202` envelope may carry only the safe identity facts already known; it remains
`IsSuccessful=false` and must never be rendered as created.

#### Exact API - CQRS - permission matrix

No fifth route, public reservation route or `PUT`/`PATCH`/`DELETE` route is permitted.

| Verb / exact route | CQRS/application target | Required permission | Result contract |
|---|---|---|---|
| `GET /api/lskus` | `GetLskusQuery` -> `GetLskusHandler` | `mdm.lskus.read` | Bounded tenant list projection; `200` only. |
| `GET /api/lskus/{id}` | `GetLskuByIdQuery` -> `GetLskuByIdHandler` | `mdm.lskus.read` | Same projection; missing/deleted/cross-tenant is indistinguishable `404`. |
| `GET /api/lskus/create-options` | `GetLskuCreateOptionsQuery` -> `GetLskuCreateOptionsHandler` -> `LskuCreateOptionsFacade` | `mdm.lskus.create` | Bounded referenceable GSKUs plus provider-backed active Markets; `200/404/409/503/504`. |
| `POST /api/lskus/drafts` | strict two-field body + trusted header -> existing `CreateLskuDraftCommand` -> `CreateLskuDraftHandler` | `mdm.lskus.create` | `201` terminal success; exact `202` reconciliation warning; `400/404/409/503/504` fail closed. |

Canonical ModuleCode is `product-item-sku-master`. Tenant Admin role onboarding is `read + create`; Viewer is
`read` only. A creator may also read only when the actor has the read key; create does not imply read. Missing tenant
module entitlement or missing permission denies access. MDM declares/enforces the two keys but does not seed, grant,
revoke, synchronize entitlement or mint claims. Permission onboarding is a separately approved MOD-0018 FU owner
step; none is created or authorized here.

#### Exact A-H delivery order, allow-lists and gates

The execution order is exactly A -> B -> C -> D -> E -> F -> G -> H. Completion of one step never authorizes the
next. Each step requires its own explicit code-start or operational authorization and accepted predecessor evidence.

**A - MDM application/query/repository projection and verified-market enumeration consumer**

Exact path allow-list:

- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/ProductItemSkuMasterModels.cs`, LSKU public projection/create-options DTO additions only.
- New `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/LskuCreateOptionsFacade.cs`.
- New `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Queries/GetLskusQuery.cs`.
- New `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Queries/GetLskuByIdQuery.cs`.
- New `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Queries/GetLskuCreateOptionsQuery.cs`.
- New `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Handlers/QueryHandlers/GetLskusHandler.cs`.
- New `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Handlers/QueryHandlers/GetLskuByIdHandler.cs`.
- New `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Handlers/QueryHandlers/GetLskuCreateOptionsHandler.cs`.
- New `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Validators/GetLskusValidator.cs`.
- New `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Validators/GetLskuCreateOptionsValidator.cs`.
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/ILskuRepository.cs`, bounded list/detail projection methods only.
- `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/LskuRepository.cs`, matching tenant/soft-delete projections only.
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/IGskuRepository.cs`, only if the existing referenceable page/batch contract cannot supply the frozen option projection.
- `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/GskuRepository.cs`, only the matching bounded/batch projection delta.
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/IProductDefinitionRevisionRepository.cs` and
  `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/ProductDefinitionRevisionRepository.cs`, batch display join only if existing methods are insufficient.
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/IGlobalProductRepository.cs` and
  `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/GlobalProductRepository.cs`, batch display join only if existing methods are insufficient.
- `services/Diten.MdmService/src/Diten.MdmService.Application/Contracts/ReferenceData/IVerifiedMarketReferenceResolver.cs`, additive bounded `EnumerateActiveAsync` contract and option/result records only.
- `services/Diten.MdmService/src/Diten.MdmService.Infrastructure/ReferenceData/PlatformVerifiedMarketResolverClient.cs`, typed `enumerate-active` consumer only.
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ReferenceData/VerifiedMarketReferenceResolverContractTests.cs`, additive enumeration contract cases only.
- New `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ReferenceData/PlatformVerifiedMarketResolverClientTests.cs`.
- New `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/LskuRegisterQueryTests.cs`.
- New `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/LskuRegisterMongoTests.cs`.

Protected paths: existing LSKU entity/create handler/validator and reservation semantics except a separately evidenced
blocking defect; all API/manifest/Gateway/frontend/Auth/Platform/configuration/artifact files; every other aggregate;
`.antigravity/**`, registries, DCPs and domain contracts.

Acceptance criteria: bounded deterministic paging; cross-tenant/deleted records absent; exact list/detail projection;
referenceable GSKUs filtered before paging; batch joins with no N+1; active-market items contain only `Code`,
`DisplayText`, `SortOrder`; provider status mapping is exact; no browser/provider evidence leakage or fallback.

**Code-start gate:** the provider's active-market enumeration contract is present and green; exact MDM DTO/failure
mapping and tests are frozen; then the user separately authorizes A.

**A implementation evidence (2026-08-08):** the user separately authorized A and approved the Phase 1.5 architecture.
The exact allow-list now supplies tenant/soft-delete-safe bounded list/detail queries, deterministic paging/search,
referenceable-GSKU create options with bounded batch display joins, and a typed provider-only active-market consumer.
No entity/create/race/reservation/reconciliation behavior, public API, manifest, Gateway, frontend, Auth, Platform,
configuration, artifact or B-H path changed. Focused Exposure A tests pass `24/24`; existing LSKU foundation/provider
regression tests pass `63/63`; the complete MDM application/real-Mongo suite passes `386/386`, all with zero skipped.
The isolated-output Release API build succeeds with zero errors and five pre-existing persistence warnings. B remains
not started and not authorized.

**B - MDM API and manifest**

Exact path allow-list:

- New `services/Diten.MdmService/src/Diten.MdmService.Api/Controllers/LskusController.cs`.
- `services/Diten.MdmService/src/Diten.MdmService.Api/ModuleRegistration/ProductItemSkuMasterManifestProvider.cs`, additive `LSKUS` page and two permission constants only.
- New `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/LskuApiContractTests.cs`.
- New `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/LskuAuthorizationTests.cs`.
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ModuleRegistration/ProductItemSkuMasterManifestProviderTests.cs`, additive `LSKUS` assertions only.

Protected paths: public reservation/update/rebind/delete/lifecycle routes; existing foundation semantics; all Gateway,
frontend, Auth, Platform and configuration files; all other manifest pages/actions/visibility values.

Acceptance criteria: `CustomBaseController` + `Response<T>`; exactly four routes; strict two-field body and required
trusted idempotency header; exact permissions; `LSKUS` navigation false; only `ADD_NEW`/`VIEW_DETAILS`; no technical
response leakage; entitlement absence denies access.

**Code-start gate:** A is accepted green and the user separately authorizes B.

**B implementation evidence - 2026-08-08:** The user explicitly authorized Exposure B after approving the Phase
1.5 architecture. The MDM API now declares exactly `GET /api/lskus`, `GET /api/lskus/{id:guid}`,
`GET /api/lskus/create-options` and `POST /api/lskus/drafts`, gated respectively by
`mdm.lskus.read`, `mdm.lskus.read`, `mdm.lskus.create` and `mdm.lskus.create`. The create body is fail-closed to
`GskuId` and `MarketCode`; the required `Idempotency-Key` is accepted only as trusted transport metadata and is
server-side mapped to the existing command. Public success output is a sanitized projection; reconciliation returns
the existing non-success `202 LSKU_BINDING_RECONCILIATION_REQUIRED` envelope without reservation/provider evidence.
`GskuCanonicalCode` is obtained only from the existing tenant-safe `GetLskuByIdQuery` projection. The additive
`LSKUS` manifest page is navigation-hidden and has only `ADD_NEW` and `VIEW_DETAILS`. Focused B controller,
authorization and manifest tests pass `20/20` with zero skipped (isolated Release output); five pre-existing
persistence warnings remain. This is historical B-checkpoint evidence; later separately authorized C-G delivery is
recorded in the authoritative matrix, while H remains closed.

**No-code regression verification - 2026-08-08:** With Release output rooted at
`work/lsku-b-regression` beneath the repository, the three `LegalEntityL10nContractTests` and the previously failing
`ProductItemSkuMasterMongoTests.Domain_layer_has_no_mongodb_driver_or_bson_imports` pass. The complete MDM Release
suite passes `403/403`, zero failed and zero skipped. This proves the earlier four failures were output-path/repository-
root discovery failures from the temporary output location, not an LSKU Exposure B regression. No source, runtime,
configuration or scope change was made by this verification.

**C - Gateway**

Exact path allow-list: `gateway/Diten.ApiGateway/ocelot.json`, integration-agent only, limited to base
`/api/lskus` and catch-all `/api/lskus/{everything}` routes to MDM `5059` with methods exactly `GET`, `POST`,
`OPTIONS`.

Protected paths: every non-LSKU route and every MDM/frontend/Auth/Platform/configuration file. `PUT`, `PATCH`,
`DELETE` and CodeReservation routes are prohibited.

Acceptance criteria: both templates precede fallback; tenant/auth/correlation headers and `Idempotency-Key` survive;
no direct service-port browser path; route parse/order/method evidence passes.

**Code-start gate:** B contract is frozen and green, the user separately authorizes C, and integration-agent owns the edit.

**D - Golden Slim frontend**

Exact path allow-list:

- New `frontend/Diten.Web/Controllers/LskusController.cs`.
- New `frontend/Diten.Web/Models/Lskus/LskuViewModels.cs`.
- New `frontend/Diten.Web/Views/MasterDataManagement/Lskus/Index.cshtml`.
- New `frontend/Diten.Web/Views/MasterDataManagement/Lskus/_Filter.cshtml`.
- New `frontend/Diten.Web/Views/MasterDataManagement/Lskus/_DataTable.cshtml`.
- New `frontend/Diten.Web/Views/MasterDataManagement/Lskus/_IndexL10n.cshtml`.
- New `frontend/Diten.Web/Views/MasterDataManagement/Lskus/_CreateEditOffcanvas.cshtml`, create-only despite the canonical Slim filename.
- New `frontend/Diten.Web/Views/MasterDataManagement/Lskus/_DetailsQuickView.cshtml`, read-only.
- New `frontend/Diten.Web/Views/MasterDataManagement/Lskus/LskusIndex.cs`.
- New `frontend/Diten.Web/wwwroot/assets/js/MasterDataManagement/Lskus/index.js`.
- New `frontend/Diten.Web/wwwroot/assets/js/MasterDataManagement/Lskus/index.l10n.js`.
- New `frontend/Diten.Web/Resources/Views/MasterDataManagement/Lskus/LskusIndex.en.resx`.
- New `frontend/Diten.Web/Resources/Views/MasterDataManagement/Lskus/LskusIndex.fr.resx`.
- New `frontend/Diten.Web/Resources/Views/MasterDataManagement/Lskus/LskusIndex.es.resx`.
- New `frontend/Diten.Web/Resources/Views/MasterDataManagement/Lskus/LskusIndex.zh.resx`.
- New `frontend/Diten.Web/Resources/Views/MasterDataManagement/Lskus/LskusIndex.ar.resx`.
- New `frontend/Diten.Web/Resources/Views/MasterDataManagement/Lskus/LskusIndex.ru.resx`.
- New `frontend/Diten.Web/Resources/Views/MasterDataManagement/Lskus/LskusIndex.tr.resx`.
- New `frontend/Diten.Web/tests/lsku-register.test.js`.

Protected paths: `_Layout.cshtml`, `_ViewStart.cshtml`, Archive/frozen paths, shared DataTable/SweetAlert contracts,
navigation files, existing GSKU/Finished Good/Global Product pages, Gateway/services/Auth/Platform and configuration.

Acceptance criteria: explicit `_LayoutTenantShell`; route exactly `/MasterDataManagement/Lskus`; v2 DataTable and
skeleton; no checkbox/bulk/edit/delete action; exact two-field form; verified selectors only; Viewer hides create and
can view details; Admin read+create; stable shared Data Protection key-ring across restart/instances; anti-forgery;
same-origin proxy only; `201` rotates token and shows read-only CanonicalCode; `202` preserves token/form and warns;
exact safe mappings for `404/409/503/504`; seven-locale parity; only `ADD_NEW`/`VIEW_DETAILS` UI actions.

**Code-start gate:** C is green, A's enumeration consumer is green and the user separately authorizes D.

**D implementation and Golden Slim verifier evidence - 2026-08-08:** The authorized 19-file LSKU frontend
allow-list is implemented as a tenant-shell, server-side DataTable v2 surface using only the same-origin
`/MasterDataManagement/Lskus/api` MVC proxy. The create form contains only `GskuId` and `MarketCode` plus
anti-forgery and the opaque Data Protection form-attempt token; `201` rotates that token, while `202` preserves the
form and token. Save View uses the shared personalization client and tracks applied search, base ordering, column
visibility and ColReorder state. Factory reset restores the factory search/filter, visibility, column order and base
order rather than a saved user view. The single real quick-view offcanvas renders the approved read-only detail
projection from `GET /MasterDataManagement/Lskus/api/{id}`. No edit, delete, bulk, checkbox, lifecycle mutation,
direct-Gateway, browser tenant/token/idempotency generation or reservation surface was added.

The official command without an API-profile override progressed from the recorded baseline `40 passed / 50 failed`,
through `50 passed / 41 failed`, to the final `75 passed / 16 failed`. Every final failure is an exact controlled
variance:

- missing generic/inapplicable localization expectations: `Active`, `Passive`, `Edit`, `BulkDelete`,
  `BulkDeleteConfirm`, `AreYouSure`, `Import`, `ShowAll`;
- `direct-gateway profile uses window.API service base`, because this page intentionally uses the approved same-origin
  MVC proxy profile;
- `_DataTable.cshtml has select-all checkbox header (dt-checkboxes-select-all)`;
- `index.js declares bulk action config (bulkOptions / bulkBarSelector)`;
- `index.js wires bulk selection (getSelectedIds(...) or onBulkAction)`;
- `index.js calls bulk endpoint (.../bulk)`;
- `index.js wires bulk delete trigger (#btnBulkDelete | .bulk-delete-btn | [data-bulk-action])`;
- `index.js uses shared reload-with-toast lifecycle (DitenDataTable.reloadWithToast)`;
- `index.js wires clear-selection (clearSelectionSelector or clearSelection())`.

This variance is local to the MOD-0290 LSKU create-only/read-only first slice and is not a precedent for other
DataTable modules or later LSKU scopes. It cannot authorize inert localization keys, hidden markup, fake endpoints,
bulk/select controls, edit/delete actions or a direct-Gateway browser profile. Focused LSKU Vitest passed `10/10`;
both JavaScript syntax checks passed; all seven locale XML files parsed with exact 35-key parity; the forbidden
browser scan was clean; and the isolated-output Frontend Release build completed with zero errors and 13 pre-existing
unrelated warnings. That run was pre-smoke evidence; later authorized E-G delivery supersedes its historical no-live
statement. H remains closed and navigation-hidden.

**E - Permission onboarding**

Exact path allow-list in this pack: **none**. MOD-0018-FU19 owns this work. Its implementation tests and the authorized
Local Development entitlement/grant/token smoke prove Admin read+create and Viewer read-only; Production onboarding
remains fail closed.

Protected paths: all `services/Diten.AuthService/**`, `services/Diten.Platform/**`, MDM/frontend/Gateway files and
governance files under this pack.

Acceptance criteria: only `mdm.lskus.read` and `mdm.lskus.create` are cataloged under
`product-item-sku-master`; Admin gets read+create, Viewer gets read only; seed/reconciliation/grant/revoke is
idempotent; tenant entitlement and JWT claims carry no broader LSKU permission; entitlement absence denies; endpoint
allow/deny evidence matches the matrix.

**Current gate:** FU19 is implemented and in review; this MOD-0290 pack supplies no further permission authority.

**F - Market artifact/provisioning**

Exact path allow-list in this pack: **none**. Provider code, catalog artifact, configuration and provisioning are
MOD-0048/provider-owner work. The provider owner must freeze the official source snapshot, complete active rows,
usage/license basis, immutable catalog version, artifact hash, exact repository artifact path and exact provisioning
command/runbook in its own approved authority before any mutation. No guessed seed filename or Platform path is
authorized here.

Protected paths: all `services/Diten.Platform/**`, provider data/configuration, Mongo publication/assignment state,
credentials and every MDM/frontend/Gateway/Auth file.

Acceptance criteria: repeatable artifact validation/load/publish; exact active-market enumeration and exact resolve
agree on code/version; credentials and tenant boundary remain server-side; unavailable/configuration/timeout remain
`503/504`; no hardcoded/browser fallback; immutable source/version/hash and provisioning evidence are recorded.

**Current evidence:** `MARKET-ARTIFACT-01` artifact authoring and the separately authorized Local Development operational
provisioning are closed: version `UNSD-M49-2026-08-08`, 249 active values and exact `TW` delta. Production provisioning
remains a separate prohibited gate.

**G - Live create smoke**

Exact path allow-list: no source, config or governance file changes; read-only execution of accepted A-F binaries and
existing smoke/test harnesses against an explicitly authorized non-production pilot tenant only.

Protected paths: the entire worktree from edits, all production tenants, and test-data mutation outside named pilot
records.

Acceptance criteria: browser `5001` -> Gateway `5000` -> MDM `5059`; exact two-field create; server-owned tenant,
operation key, UUID and reservation; `201` plus same-fact replay; exact `202` warning/replay; `404/409/503/504` paths;
tenant isolation; Admin/Viewer/entitlement deny matrix; provider enumeration + create-time exact resolve; no direct
port, fifth endpoint or technical browser field; Mongo evidence shows permanent no-reuse and one identity for the
winning key.

**Code-start gate:** A-F acceptance evidence is green and separately accepted; the user authorizes the exact smoke
environment, tenant and test data.

**H - Navigation enablement**

Default remains `IsNavigationVisible: false`. Conditional exact allow-list after a later affirmative decision:

- `services/Diten.MdmService/src/Diten.MdmService.Api/ModuleRegistration/ProductItemSkuMasterManifestProvider.cs`, only the `LSKUS` visibility/order/display value.
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ModuleRegistration/ProductItemSkuMasterManifestProviderTests.cs`, matching `LSKUS` assertion only.

Protected paths: every other manifest page/action/permission, frontend navigation hardcoding, Auth/Platform
entitlement logic and all other files.

Acceptance criteria: G evidence is accepted, the user explicitly enables navigation, authorized entitled tenants see
the entry, Viewer/Admin visibility follows read permission and unauthorized/unentitled tenants do not see it. Direct
URL pilot success never enables navigation implicitly.

**Code-start gate:** G is accepted and the user separately authorizes H.

#### A-H test matrix

| Step | Unit | Contract | Real Mongo / integration | Browser |
|---|---|---|---|---|
| A | Query validation, mapping, bounded options, provider failure normalization | Typed active-market enumeration shape/auth/no forbidden metadata | Tenant isolation, paging/order, soft-delete/no-reuse visibility, batch joins/no N+1 | N/A; no surface yet |
| B | Controller dispatch and strict header/body mapping | Exact four routes, status/envelope, permissions, manifest nav false/two actions | API host with real Mongo for `201/202/404/409` and cross-tenant non-disclosure | N/A; no frontend yet |
| C | JSON parse/order/method assertions | Gateway route/header/idempotency forwarding and OPTIONS | Gateway -> MDM smoke with test Mongo | Network-only gateway smoke; no UI |
| D | MVC token protection, permission rendering, safe error mapping | Same-origin proxy, anti-forgery, exact payload/header and seven-RESX parity | MVC -> Gateway -> MDM integration against test Mongo | Slim verifier; list/detail/create; no checkbox/bulk/edit/delete; `201/202/404/409/503/504` |
| E | Role-template exact sets and idempotent reconciliation | Catalog/entitlement/JWT claim allow-deny matrix | Auth/Platform persistence and sync if owner pack requires it | Admin read+create, Viewer read-only, no entitlement deny |
| F | Artifact schema/hash/version validation | Enumeration/resolve agreement and server-only evidence | Repeatable load/publish in authorized non-production Mongo | Selector population and provider failure only after F is green |
| G | No new unit code | Frozen end-to-end HTTP contract | One winner/one reconciliation path, replay, tenant isolation and tombstone no-reuse evidence | Full live pilot network/UI smoke |
| H | Manifest visibility-only assertion | Registration/entitlement/navigation contract | Registration readback if owner environment requires it | Authorized menu visibility and unauthorized absence |

Every implementing step also runs its focused suite plus the applicable full MDM/Auth/Platform/frontend regression,
build, `git diff --check`, conflict-marker, trailing-whitespace and final-newline checks. D additionally runs
`verify_datatable_page.py` for area `MasterDataManagement`, module `Lskus`, reference `slim` and documents the
create-only/no-bulk/no-edit controlled variances without weakening the v2/skeleton/layout contract.

#### Historical code-start blockers — superseded by current evidence

The earlier B-G code-start blockers are closed by their separately authorized implementations and evidence. H remains
closed: navigation stays false and no Production/readiness claim is permitted. Historical planning text above remains
as an authorization record, but the current-state matrix below is authoritative where status statements conflict.

#### Named-step ready-for-dev checklist

- [x] Existing LSKU Draft Identity Foundation and its `83/83` focused plus `362/362` full MDM evidence are recorded.
- [x] Route, tenant shell, Slim reference and exactly two user fields are frozen.
- [x] The exact four API endpoints and no-mutation/no-reservation-route boundary are frozen.
- [x] Create-options GSKU projection and provider-backed Market shape are frozen; forbidden metadata/fallback is explicit.
- [x] Data Protection form-attempt and stable server `Idempotency-Key` behavior are frozen.
- [x] Exact API-CQRS-permission matrix and `201/202/404/409/503/504` mapping are frozen.
- [x] A-H order, per-step allow-list/protected paths/gates/acceptance criteria/test matrix are documented.
- [x] ModuleCode, Admin/Viewer behavior, entitlement deny, manifest nav false and two actions are frozen.
- [x] The user separately authorized exact A code-start and approved its Phase 1.5 architecture.
- [x] A closed its implementation, focused, regression, full-suite and Release-build evidence.
- [x] B-D closed their separately authorized implementation and evidence.
- [x] MOD-0018-FU19 closed the Development permission implementation and evidence for E.
- [x] Provider owner closed the verified market artifact and Local Development provisioning evidence for F.
- [x] G Local Development live smoke is accepted; it is not Production/readiness evidence.
- [ ] H remains false until a separate explicit navigation decision.

### Authoritative code-truth reconciliation — 2026-08-09

This matrix supersedes older preparation-only, backend-only, `B-H unstarted`, `permission open` and `smoke open`
status sentences in this pack. It does not erase their historical authorization context.

| Surface | A | B | C | D | E | F | G | H / navigation |
|---|---|---|---|---|---|---|---|---|
| Global Product | Backend/query complete | MDM API/manifest complete | Gateway complete | Frontend complete | FU16 permission + Local Development role evidence complete | Not a separate provider step | Live create/read + isolation complete | `GLOBAL_PRODUCTS` currently visible; no change authorized here |
| GSKU | Backend/application complete | MDM API/manifest complete | Gateway complete | Frontend complete | FU18 permission + role evidence complete | Verified provider, publication, two assignments and resolver complete | `GS-000000000003` create/read/replay + isolation complete | `GSKUS` currently visible; no change authorized here |
| LSKU | Backend/application complete | MDM API/manifest complete | Gateway complete | Frontend complete | FU19 permission + role evidence complete | Verified market `UNSD-M49-2026-08-08`, 249 values, `TW` present | `LS-000000000004` / `TR` create/read + isolation complete | `LSKUS` remains hidden and deliberately deferred |
| Finished Good | Backend/domain complete | MDM API/manifest complete | Gateway complete | Frontend complete | FU17 permission + role evidence complete | Existing GSKU selector contract; no new provider | `FG-000000000005` create/read + isolation complete | `FINISHED_GOODS` remains hidden and deliberately deferred |

Product Definition Revision + First GSKU foundation is complete. Universal market consumption creates no consumer
tenant assignment. Admin holds the exact four read/create pairs; Viewer holds only the four reads. Live evidence shows
Viewer reads allowed, create/create-options denied, and no cross-tenant disclosure.

#### Completed/open boundary

| Class | Current truth |
|---|---|
| Completed | Global Product end-to-end; Product Definition Revision + First GSKU; verified GSKU provider/publication/two assignments/resolver; GSKU A-G; LSKU A-G; Finished Good A-E; four-register same-origin Save View hardening |
| WorkCenter-dependent open | Submit/approve/reject/retire lifecycle orchestration; maker-checker workflow tasks; durable callback/poll and workflow recovery; workflow-owned Production operations |
| WorkCenter-independent open | Market Supply Assignment; MA/Registered Presentation; packaging; artwork/label/leaflet; GTIN; Composition/strength and remaining master-data slices; Production audit transport/central acknowledgement, finite-expiry scheduling, retention/purge/redaction, metrics/runbook and overall Production readiness |
| Navigation | No navigation mutation is authorized by this reconciliation. Existing code truth is Global Product/GSKU visible and LSKU/Finished Good hidden; remaining decisions are deliberately deferred. |

#### Shared personalization evidence

The shared Save View contract is browser-relative `/api/personalization/views` -> authenticated MVC proxy -> Gateway
`5000` -> Platform. Load/save/reload/reset passed on Global Product, GSKU, LSKU and Finished Good with zero browser
console errors and no direct `5057`/`5059` call. Focused Vitest passed `44/44`; JavaScript syntax and seven-locale parity
passed; the Release frontend build completed with zero errors and 13 pre-existing warnings. The full frontend suite
discovered 152 tests: 143 passed and nine unrelated Enterprise Strategy/Planning tests failed; no MOD-0290 or
personalization test failed. Existing pilot records remained unchanged.

### Global Product lifecycle common Workflow amendment — approved code-start (2026-09-08)

This narrowly authorizes the missing MDM common layer required before the separately approved Global Product
draft-edit, submit/withdraw, correction-request and retirement-request implementations. It does not authorize a new
module identity, GSKU/LSKU/ABB/Finished Good lifecycle behavior, operational provisioning, appsettings values,
credential material, browser acceptance or a WorkCenter provider change.

The audit delivery identity and the Product Identity Workflow identity remain distinct audiences and contracts. The
common Workflow client accepts only a canonical gateway tenant header matching the single JWT `tenant_id`; malformed,
duplicate or mismatched tenant/subject/actor/bearer evidence fails closed. Human delegated authorization is used only
for delegated start/cancel calls; the Auth-issued service identity remains the service-to-service authority.

Runtime exact allow-list:

- `services/Diten.MdmService/src/Diten.MdmService.Application/Contracts/IProductIdentityLifecycleActorContext.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Contracts/Workflow/IProductIdentityDelegatedTokenAccessor.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Contracts/Workflow/IProductIdentityWorkflowServiceIdentityProvider.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Contracts/Workflow/IProductIdentityWorkflowClient.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Contracts/Workflow/ProductIdentityWorkflowTransportModels.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Infrastructure/Security/ProductIdentityLifecycleActorContext.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Infrastructure/Workflow/AuthProductIdentityWorkflowServiceIdentityProviderOptions.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Infrastructure/Workflow/AuthProductIdentityWorkflowServiceIdentityProvider.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Infrastructure/Workflow/ProductIdentityWorkflowClientOptions.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Infrastructure/Workflow/HttpContextProductIdentityDelegatedTokenAccessor.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Infrastructure/Workflow/PlatformProductIdentityWorkflowClient.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Infrastructure/DependencyInjection.cs`, limited to the above
  service registrations, options binding, named HTTP clients and redacted headers.

Test exact allow-list:

- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ProductIdentityLifecycleActorContextTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/Workflow/ProductIdentityDelegatedTokenAccessorTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/Workflow/AuthProductIdentityWorkflowServiceIdentityProviderTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/Workflow/PlatformProductIdentityWorkflowClientTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/Workflow/ProductIdentityWorkflowDependencyInjectionTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/Workflow/ProductIdentityWorkflowNoCredentialPersistenceTests.cs`

Required evidence: audience separation from G4 audit, strict actor/tenant/header/bearer rejection, service-token
expiry/rotation failure handling, delegated-token forwarding only where required, timeout/cancellation classification,
idempotency and terminal-evidence validation, and no credential/token persistence or logging. The common layer is
default-fail-closed when its operator-owned options are absent; this amendment creates no configuration values.

The follow-on Global Product backend/UI/Gateway slices remain constrained to their separately approved exact paths.
Gateway may add only `PUT` to `/api/global-products/{everything}`; the root route remains `GET, POST, OPTIONS`.

### Global Product lifecycle backend amendment — approved code-start (2026-09-08)

This amendment authorizes only the Global Product backend lifecycle slice already separately approved by the user:
draft update; submit, withdraw, correction request and retirement request; terminal decision reconciliation and
read-back; repository persistence; explicit, default-disabled recovery; manifest projection; and the focused tests
listed below. It does not authorize frontend, localization, Gateway, other product lifecycle flows, provisioning,
configuration values, real tenant data, service startup or operational recovery execution.

Runtime exact allow-list:

- `services/Diten.MdmService/src/Diten.MdmService.Api/Controllers/GlobalProductsController.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Api/ModuleRegistration/ProductItemSkuMasterManifestProvider.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Api/Program.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Handlers/QueryHandlers/GetGlobalProductByIdHandler.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/ProductItemSkuMasterModels.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/GlobalProduct.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Enums/ProductAuditOperation.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/IGlobalProductRepository.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Persistence/DependencyInjection.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/GlobalProductRepository.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Api/Configuration/GlobalProductCorrectionWorkflowOptions.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Api/Configuration/GlobalProductCorrectionWorkflowWorkerOptions.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Api/Configuration/GlobalProductRetirementRequestWorkflowOptions.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Api/Configuration/GlobalProductRetirementRequestWorkflowWorkerOptions.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Api/Configuration/ProductIdentityWorkflowOptions.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Api/Configuration/ProductIdentityWorkflowWorkerOptions.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Api/Services/ProductItemSkuMaster/GlobalProductCorrectionRecoveryCommandLine.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Api/Services/ProductItemSkuMaster/GlobalProductCorrectionRecoveryRunner.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Api/Services/ProductItemSkuMaster/GlobalProductCorrectionRecoveryWorker.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Api/Services/ProductItemSkuMaster/GlobalProductRetirementRequestRecoveryCommandLine.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Api/Services/ProductItemSkuMaster/GlobalProductRetirementRequestRecoveryRunner.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Api/Services/ProductItemSkuMaster/GlobalProductRetirementRequestRecoveryWorker.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Api/Services/ProductItemSkuMaster/ProductIdentityWorkflowRecoveryCommandLine.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Api/Services/ProductItemSkuMaster/ProductIdentityWorkflowRecoveryRunner.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Api/Services/ProductItemSkuMaster/ProductIdentityWorkflowRecoveryWorker.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Lifecycle/Commands/ReconcileGlobalProductIdentityDecisionCommand.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Lifecycle/Commands/RetireGlobalProductIdentityCommand.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Lifecycle/Commands/SubmitGlobalProductIdentityCommand.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Lifecycle/Commands/UpdateGlobalProductDraftCommand.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Lifecycle/Commands/WithdrawGlobalProductIdentityApprovalCommand.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Lifecycle/GlobalProductCorrectionAuditIntentFactory.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Lifecycle/GlobalProductRetirementRequestAuditIntentFactory.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Lifecycle/Handlers/CommandHandlers/ReconcileGlobalProductIdentityDecisionHandler.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Lifecycle/Handlers/CommandHandlers/RetireGlobalProductIdentityHandler.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Lifecycle/Handlers/CommandHandlers/SubmitGlobalProductIdentityHandler.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Lifecycle/Handlers/CommandHandlers/UpdateGlobalProductDraftHandler.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Lifecycle/Handlers/CommandHandlers/WithdrawGlobalProductIdentityApprovalHandler.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Lifecycle/ProductIdentityLifecycleAuditIntentFactory.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Lifecycle/ProductIdentityLifecycleModels.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Lifecycle/Validators/ReconcileGlobalProductIdentityDecisionValidator.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Lifecycle/Validators/RetireGlobalProductIdentityValidator.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Lifecycle/Validators/SubmitGlobalProductIdentityValidator.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Lifecycle/Validators/UpdateGlobalProductDraftValidator.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Lifecycle/Validators/WithdrawGlobalProductIdentityApprovalValidator.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Workflow/Commands/StartGlobalProductCorrectionWorkflowCommand.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Workflow/Commands/StartGlobalProductIdentityWorkflowCommand.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Workflow/Commands/StartGlobalProductRetirementRequestWorkflowCommand.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Workflow/GlobalProductCorrectionWorkflowProcessor.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Workflow/GlobalProductCorrectionWorkflowStartRequestFactory.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Workflow/GlobalProductIdentityWorkflowProcessor.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Workflow/GlobalProductRetirementRequestWorkflowProcessor.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Workflow/GlobalProductRetirementRequestWorkflowStartRequestFactory.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Workflow/Handlers/CommandHandlers/StartGlobalProductCorrectionWorkflowHandler.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Workflow/Handlers/CommandHandlers/StartGlobalProductIdentityWorkflowHandler.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Workflow/Handlers/CommandHandlers/StartGlobalProductRetirementRequestWorkflowHandler.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Workflow/ProductIdentityWorkflowStartRequestFactory.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Workflow/Validators/StartGlobalProductCorrectionWorkflowValidator.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Workflow/Validators/StartGlobalProductIdentityWorkflowValidator.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Workflow/Validators/StartGlobalProductRetirementRequestWorkflowValidator.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/GlobalProductCorrectionOperation.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/GlobalProductIdentityWorkflowOperation.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/GlobalProductRetirementRequestOperation.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Enums/GlobalProductCorrectionCheckpoint.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Enums/GlobalProductIdentityWorkflowCheckpoint.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Enums/GlobalProductLifecycleOperationKind.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Enums/GlobalProductRetirementRequestCheckpoint.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Enums/ProductIdentityDecisionKind.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Enums/ProductIdentityWorkflowRecoveryDisposition.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/GlobalProductCorrectionOperationResults.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/GlobalProductIdentityWorkflowOperationResults.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/GlobalProductRetirementRequestOperationResults.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/IGlobalProductCorrectionOperationRepository.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/IGlobalProductIdentityWorkflowOperationRepository.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/IGlobalProductIdentityWorkflowTenantPartitionDiscovery.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/IGlobalProductRetirementRequestOperationRepository.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/ValueObjects/GlobalProductActiveLifecycleOperationBinding.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/ValueObjects/ProductChildCreationAdmission.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/ValueObjects/ProductIdentityWorkflowBinding.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/GlobalProductCorrectionOperationRepository.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/GlobalProductIdentityWorkflowOperationRepository.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/GlobalProductIdentityWorkflowTenantPartitionDiscoveryRepository.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/GlobalProductRetirementRequestOperationRepository.cs`

Test exact allow-list:

- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/GlobalProductApiMongoTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ModuleRegistration/ProductItemSkuMasterManifestProviderTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/GlobalProductAvailableActionsTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/GlobalProductChildAdmissionMongoTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/GlobalProductCorrectionOperationMongoTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/GlobalProductCorrectionRecoveryRunnerTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/GlobalProductCorrectionRecoveryWorkerMongoTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/GlobalProductCorrectionUnitTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/GlobalProductCorrectionWorkflowOptionsTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/GlobalProductCorrectionWorkflowProcessorTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/GlobalProductCorrectionWorkflowStartRequestFactoryTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/GlobalProductIdentityWorkflowOperationMongoTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/GlobalProductIdentityWorkflowProcessorTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/GlobalProductIdentityWorkflowRecoveryRunnerTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/GlobalProductIdentityWorkflowRecoveryWorkerMongoTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/GlobalProductLifecycleAuthorizationTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/GlobalProductLifecycleMongoTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/GlobalProductLifecycleOperationAdmissionMongoTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/GlobalProductLifecycleUnitTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/GlobalProductRetirementRequestOperationMongoTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/GlobalProductRetirementRequestRecoveryRunnerTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/GlobalProductRetirementRequestRecoveryWorkerMongoTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/GlobalProductRetirementRequestUnitTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/GlobalProductRetirementRequestWorkflowProcessorTests.cs`

Required boundaries: existing Auth 16/7/10 grants and Platform trusted cancellation are consumed unchanged; no
workflow/audit common layer rewrite; each recovery runner is explicit-command only, default-disabled and fails closed
on failed processing; server-side tenant/scope/idempotency/concurrency/audit receipt controls remain authoritative.


### Global Product lifecycle frontend and Gateway amendment — approved code-start (2026-09-08)

This amendment authorizes the Global Product tenant-shell frontend slice and the one required Gateway method delta.
The browser calls only same-origin MVC routes; MVC performs the authenticated Gateway proxy. Lifecycle actions keep
anti-forgery, Data Protection-derived idempotency, expected-version and permission boundaries server-side. A success
notice is emitted only after response-shape validation and a fresh source-product detail read-back.

Frontend runtime exact allow-list:

- `frontend/Diten.Web/Controllers/GlobalProductsController.cs`
- `frontend/Diten.Web/Views/MasterDataManagement/GlobalProducts/Index.cshtml`
- `frontend/Diten.Web/Views/MasterDataManagement/GlobalProducts/_CreateEditOffcanvas.cshtml`
- `frontend/Diten.Web/Views/MasterDataManagement/GlobalProducts/_DetailsQuickView.cshtml`
- `frontend/Diten.Web/Views/MasterDataManagement/GlobalProducts/_IndexL10n.cshtml`
- `frontend/Diten.Web/wwwroot/assets/js/MasterDataManagement/GlobalProducts/index.js`
- `frontend/Diten.Web/Resources/Views/MasterDataManagement/GlobalProducts/GlobalProductsIndex.ar.resx`
- `frontend/Diten.Web/Resources/Views/MasterDataManagement/GlobalProducts/GlobalProductsIndex.en.resx`
- `frontend/Diten.Web/Resources/Views/MasterDataManagement/GlobalProducts/GlobalProductsIndex.es.resx`
- `frontend/Diten.Web/Resources/Views/MasterDataManagement/GlobalProducts/GlobalProductsIndex.fr.resx`
- `frontend/Diten.Web/Resources/Views/MasterDataManagement/GlobalProducts/GlobalProductsIndex.ru.resx`
- `frontend/Diten.Web/Resources/Views/MasterDataManagement/GlobalProducts/GlobalProductsIndex.tr.resx`
- `frontend/Diten.Web/Resources/Views/MasterDataManagement/GlobalProducts/GlobalProductsIndex.zh.resx`

Frontend test exact allow-list:

- `frontend/Diten.Web/tests/global-products-register.test.js`

Gateway exact allow-list, owned under the integration-agent contract:

- `gateway/Diten.ApiGateway/ocelot.json`, only add `PUT` to `/api/global-products/{everything}`.
- `gateway/Diten.ApiGateway.Tests/OcelotConfigurationTests.cs`, assert nested `PUT` and root `PUT` absence.

Protected paths: all shared layout/theme and WorkCenter paths, all SharedResource files, other product register
screens, Auth/Platform/MDM runtime, every Gateway route except the named nested Global Product template, and all
configuration/provisioning paths. Root `/api/global-products` remains `GET, POST, OPTIONS`; GSKU and all other
route method sets remain untouched.

### GSKU lifecycle FU01 mapping prerequisite — approved code-start (2026-09-08)

The GSKU lifecycle backend may depend only on the FU01 amendment's 17 exact Revision/GSKU audit-map pairs and its
negative strict-mapping tests. It must not add LSKU, ABB, wildcard or numeric fallback mappings. This is a bounded
provider acceptance prerequisite; it does not authorize Platform transport, service identity, configuration,
provisioning or operational activation.

### GSKU backend B/C/D/E and E1A/E1B integration evidence (2026-09-08)

This is the user-authorized source reconciliation of the retained final checkpoint `ffdd280a`:
Section 19.17 B/C/D/E and the E1A/E1B/E1C amendments are applied to the current integration branch,
not a claim that the historical checkpoint is current main or that live acceptance has run.
The following exact paths expand those approved backend boundaries; no frontend, Gateway, other-product
lifecycle, shared authentication rewrite, configuration or provisioning is included.

Prerequisites are the integrated strict 17-pair FU01 audit mapping, trusted reference-data identity
`284a79374c832fa1666df1cfdaae2497f7975518`, and the separate FU21 exact-pair amendment
`edf004f646a600574d372dbc7ada39658407dd91`. FU21 preserves its original 22 pairs and adds six GSKU
pairs (28 total); correction and retirement requests resolve their own exact mutation permission,
not the older Global Product read substitute. Direct pair retirement remains a hidden System action.

Semantic reconciliation:
- Preserve existing creation, Global Product, tenant/scope, G4 audit and temporal foundations.
- Keep original Revision/GSKU audit ordinals; do not add LSKU/ABB mappings or numeric fallback.
- Enforce the approved closed manual-resume set: only `FIRST_GSKU_IDENTITY_PARENT_NOT_APPROVED`
  and `REFERENCE_UNAUTHENTICATED` can resume via exact same-maker replay without persisted approval proof.
  The retained source's extra `REFERENCE_FORBIDDEN` case is not authorized and is rejected in both
  processor and Mongo CAS regression tests; background/manual bypass is not introduced.
- Wire the pair, correction and retirement repositories/processors and default-disabled recovery workers
  additively. Explicit recovery commands report failures; no operational command was invoked.
- Preserve pair versions, terminal evidence read-back, maker-checker, exact replay, verified reference
  revalidation and admission/retirement fencing. Detail action projection additionally fails closed for
  inconsistent pair state/binding and active retirement fences.
- Correction factory and Mongo admission CAS both reject an existing pair-retirement fence, including a
  caller that has read the newer fenced version. The real-Mongo regression verifies no correction audit
  or active-operation write occurs in that state.
- E1B changes to LSKU/Finished Good are child-admission contracts only, not their lifecycle implementation.
  Existing draft-create fixtures now establish the required approved pair. The child-blocker test uses a
  test-owned retired-child fact; it does not claim to test Finished Good retirement.
- Replica-set ping uses typed BSON in the existing LSKU register test; no global serializer or test
  harness change. Current schema and tenant-owned cleanup are retained.

Verification evidence (overlapping runs are not additive):
- Platform Release build: success; FU21 scope/authorization regression 77 passed, 0 failed, 0 skipped;
  the separately selected real-Mongo scope subset was 6 passed, 0 failed, 0 skipped.
- MDM full Release test run on the isolated single-node replica set: 1023 passed, 0 failed, 0 skipped.
- Focused GSKU Mongo-class selection: 29 passed, 0 failed, 0 skipped (includes factory assertions;
  it must not be reported as 29 distinct database-mutation tests).
  Excluding the two factory assertions, the actual persistence selection is 27 passed, 0 failed, 0 skipped.
- Six Enforced-scope tests exercise the actual MDM HTTP client/consumer guard and actual Platform
  authenticator/JWT context/executor via an in-process HTTP adapter. They check exact permission,
  rejected read substitution, rejected service actor, and tenant echo mismatch. Only candidate facts
  are test-supplied; the permission decision is not mocked. This requires the Platform Release assembly.
- This is isolated code/contract evidence, not live Auth provisioning, real WorkCenter decisions,
  browser acceptance, operational recovery, or tenant activation. Frontend/Gateway remain a next slice.

**Previously blocked transport and Auth gates closed by separately authorized amendments (2026-09-08):**
`de099875236454313f4f4142ce95c268c3bdc089` adds only the exact transport profiles
`GskuCorrection` and `GskuRetirementRequest` and real Platform owner-boundary contract tests.
`7c3b71ebaede1d69e3ff03b8dc08d6c0c132ece8` adds the four exact GSKU Auth action keys;
Steward/Approver/Retirement role totals are 19/7/11, while the lifecycle key count is 16.
No alias, alternate audience, wildcard grant or operational provisioning was introduced.
FU21 exact-pair authorization and FU01 strict GSKU audit mappings remain unchanged.

Final bounded backend verification on the existing isolated test-owned replica set:
- MDM and Auth Release builds: 0 errors; Platform Release build: 0 errors (existing warnings).
- MDM full test project: 1037 passed / 0 failed / 0 skipped.
- Within that run: GSKU Mongo-class selection 29/0/0, comprising 27 persistence/processor tests
  and 2 factory-only assertions; six real client-to-Platform Enforced-scope contract tests also pass.
- Separate overlapping transport selection 36/0/0; Auth role/entitlement/login/scope selection
  132/0/0 including one real-Mongo reconciliation test; Platform security/FU01/FU21 selection 143/0/0.
- Draft edit, submit, approval/rejection pair read-back, withdrawal, correction and retirement
  request have focused unit/security/API and applicable Mongo replay/CAS/recovery evidence.
  Transport/security dispatch boundaries and test-supplied terminal evidence are not live Workflow acceptance.

One intermediate full test run was 1022 passed / 1 failed / 0 skipped at
`ProductItemSkuMasterMongoTests.Concurrent_first_gsku_commands_allocate_unique_parent_ordinals_and_soft_delete_never_reuses`.
Its immediate isolated repeat was 1/0/0 and the subsequent full run was 1023/0/0. This is retained as an
intermittent regression observation, not classified as an environment issue without assertion evidence.
The 2026-09-08 controlled sequence of five isolated repeats each passed 1/0/0, and the current full run
passed 1037/0/0. The initial failure's cause remains unexplained: flakiness risk is open, not disproven.
TRX evidence and initial staged paths/diff are preserved locally under
`.testoutput/gsku-amendment-preflight-20260908/` outside source commits.
Frontend/Gateway, live browser/WorkCenter acceptance and operator provisioning remain separate work.

Exact backend runtime/test reconciliation paths:

- `services/Diten.MdmService/src/Diten.MdmService.Api/Controllers/GskusController.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Api/ModuleRegistration/ProductItemSkuMasterManifestProvider.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Api/Program.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Commands/UpdateGskuDraftCommand.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Handlers/CommandHandlers/CreateFinishedGoodDraftHandler.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Handlers/CommandHandlers/CreateLskuDraftHandler.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Handlers/CommandHandlers/UpdateGskuDraftHandler.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Handlers/QueryHandlers/GetGskuByIdHandler.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Handlers/QueryHandlers/GetGskusHandler.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/ProductItemSkuMasterModels.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Queries/GetGskusQuery.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Validators/GetGskusValidator.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/Gsku.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/ProductDefinitionRevision.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Enums/ProductAuditOperation.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/IFinishedGoodRepository.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/IGskuRepository.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/ILskuRepository.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/IProductDefinitionRevisionRepository.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Persistence/DependencyInjection.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/FinishedGoodRepository.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/GskuRepository.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/LskuRepository.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/ProductDefinitionRevisionRepository.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/FinishedGoodDraftFoundationMongoTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/FinishedGoodDraftFoundationUnitTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/GskuApiContractTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/GskuAuthorizationTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/LskuDraftFoundationMongoTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/LskuRegisterMongoTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ModuleRegistration/ProductItemSkuMasterManifestProviderTests.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Api/Configuration/FirstGskuIdentityWorkflowOptions.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Api/Configuration/FirstGskuIdentityWorkflowWorkerOptions.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Api/Configuration/GskuCorrectionWorkflowOptions.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Api/Configuration/GskuCorrectionWorkflowWorkerOptions.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Api/Configuration/GskuRetirementRequestWorkflowOptions.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Api/Configuration/GskuRetirementRequestWorkflowWorkerOptions.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Api/Services/ProductItemSkuMaster/FirstGskuIdentityWorkflowRecoveryCommandLine.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Api/Services/ProductItemSkuMaster/FirstGskuIdentityWorkflowRecoveryRunner.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Api/Services/ProductItemSkuMaster/FirstGskuIdentityWorkflowRecoveryWorker.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Api/Services/ProductItemSkuMaster/GskuCorrectionRecoveryCommandLine.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Api/Services/ProductItemSkuMaster/GskuCorrectionRecoveryRunner.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Api/Services/ProductItemSkuMaster/GskuCorrectionRecoveryWorker.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Api/Services/ProductItemSkuMaster/GskuRetirementRequestRecoveryCommandLine.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Api/Services/ProductItemSkuMaster/GskuRetirementRequestRecoveryRunner.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Api/Services/ProductItemSkuMaster/GskuRetirementRequestRecoveryWorker.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Lifecycle/Commands/RetireGskuIdentityPairCommand.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Lifecycle/Commands/WithdrawFirstGskuIdentityApprovalCommand.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Lifecycle/FirstGskuIdentityLifecycleAuditIntentFactory.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Lifecycle/FirstGskuIdentityLifecycleModels.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Lifecycle/FirstGskuIdentityRetirementAuditIntentFactory.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Lifecycle/FirstGskuIdentityRetirementProcessor.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Lifecycle/FirstGskuIdentityRetirementRecoveryRunner.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Lifecycle/GskuCorrectionAuditIntentFactory.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Lifecycle/GskuCorrectionModels.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Lifecycle/GskuPairRetirementModels.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Lifecycle/GskuRetirementRequestAuditIntentFactory.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Lifecycle/GskuRetirementRequestModels.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Lifecycle/Handlers/CommandHandlers/RetireGskuIdentityPairHandler.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Lifecycle/Handlers/CommandHandlers/WithdrawFirstGskuIdentityApprovalHandler.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Lifecycle/Validators/RetireGskuIdentityPairValidator.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Lifecycle/Validators/WithdrawFirstGskuIdentityApprovalValidator.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Workflow/Commands/StartFirstGskuIdentityWorkflowCommand.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Workflow/Commands/StartGskuCorrectionWorkflowCommand.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Workflow/Commands/StartGskuRetirementRequestWorkflowCommand.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Workflow/FirstGskuIdentityWorkflowProcessor.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Workflow/FirstGskuIdentityWorkflowStartRequestFactory.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Workflow/GskuCorrectionWorkflowProcessor.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Workflow/GskuCorrectionWorkflowStartRequestFactory.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Workflow/GskuRetirementRequestWorkflowProcessor.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Workflow/GskuRetirementRequestWorkflowStartRequestFactory.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Workflow/Handlers/CommandHandlers/StartFirstGskuIdentityWorkflowHandler.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Workflow/Handlers/CommandHandlers/StartGskuCorrectionWorkflowHandler.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Workflow/Handlers/CommandHandlers/StartGskuRetirementRequestWorkflowHandler.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Workflow/Validators/StartFirstGskuIdentityWorkflowValidator.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Workflow/Validators/StartGskuCorrectionWorkflowValidator.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Workflow/Validators/StartGskuRetirementRequestWorkflowValidator.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/FirstGskuIdentityRetirementOperation.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/FirstGskuIdentityWorkflowOperation.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/GskuCorrectionWorkflowOperation.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/GskuRetirementRequestOperation.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Enums/FirstGskuIdentityRetirementCheckpoint.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Enums/FirstGskuIdentityWorkflowCheckpoint.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Enums/GskuCorrectionWorkflowCheckpoint.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Enums/GskuRetirementRequestCheckpoint.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/FirstGskuIdentityRetirementResults.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/FirstGskuIdentityWorkflowOperationResults.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/GskuCorrectionWorkflowOperationResults.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/GskuRetirementRequestOperationResults.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/IFirstGskuIdentityRetirementOperationRepository.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/IFirstGskuIdentityWorkflowOperationRepository.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/IFirstGskuIdentityWorkflowTenantPartitionDiscovery.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/IGskuCorrectionWorkflowOperationRepository.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/IGskuCorrectionWorkflowTenantDiscoveryRepository.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/IGskuRetirementRequestOperationRepository.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/IGskuRetirementRequestTenantDiscoveryRepository.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/ValueObjects/FirstGskuIdentityWorkflowBinding.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/ValueObjects/GskuActiveLifecycleOperationBinding.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/ValueObjects/GskuChildCreationAdmission.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/FirstGskuIdentityRetirementOperationRepository.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/FirstGskuIdentityWorkflowOperationRepository.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/FirstGskuIdentityWorkflowTenantPartitionDiscoveryRepository.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/GskuCorrectionWorkflowOperationRepository.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/GskuCorrectionWorkflowTenantDiscoveryRepository.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/GskuRetirementRequestOperationRepository.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/GskuRetirementRequestTenantDiscoveryRepository.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/FirstGskuIdentityLifecycleAuthorizationTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/FirstGskuIdentityLifecycleMongoTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/FirstGskuIdentityLifecycleUnitTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/FirstGskuIdentityRetirementUnitTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/FirstGskuIdentityWithdrawalUnitTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/FirstGskuIdentityWorkflowOperationMongoTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/FirstGskuIdentityWorkflowProcessorTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/FirstGskuIdentityWorkflowRecoveryRunnerTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/GskuAvailableActionsTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/GskuChildAdmissionRetirementMongoTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/GskuCorrectionMongoTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/GskuCorrectionUnitTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/GskuDraftEditLifecycleTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/GskuRetirementRequestMongoTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/GskuRetirementRequestUnitTests.cs`

## 20. Follow-up Items

These are references to existing backlog or owner decisions; this pack creates no new identity or provider pack.

| Backlog/gate | Deferred or external work | Re-entry condition |
|---|---|---|
| BL-015 | Composition/active substance and complex strength | Approved SoR and Composition contract |
| BL-016 | Revision temporal/current/parallel behavior | First approved temporal revision use case |
| BL-017 | Packaging hierarchy | Approved multi-level packaging use case |
| BL-018 | Market Supply Assignment | Approved market-supply/regulatory boundary |
| BL-019 | MA / Registered Presentation | Approved Regulatory Information contract |
| BL-020 | Artwork/label/leaflet lifecycle | Approved labeling/document use case |
| BL-021 | BOM/manufacturing/quality/batch/release | Approved manufacturing/quality integration |
| BL-022 | GTIN lifecycle | Approved issuer/GS1 lifecycle contract |
| BL-023 | Bulk legacy migration | Real legacy export plus approved migration pack |
| BL-024 | Official MarketTradeName downstream usage | First approved official consumer/event owner |
| BL-025 | ERP/PLM feeds | First approved external-feed use case and G7 exit evidence |
| BL-026 | Runtime external contract publication | First approved external consumer/publication use case |
| BL-027 | Provider-owned PSS-012 legacy risk | Reference Data owner assessment and approved provider artifact |
| G2/G3 | Shared Reference Data provider changes | Canonical owner/identity closure and separately approved owner-domain delivery if B remains required |
| G5 | Workflow provider changes | Workflow owner accepts and delivers the proven B contract changes |
| G6 | Legal Entity provider change, only if required | Selected HTTP/durable-contract topology classifies and closes B |
| Global Product name evolution | Post-create mutability, rename/version history and approval are outside the named step | Re-enter only through an existing applicable backlog/approved delivery boundary after Product Data owner decision; no new backlog identity is created here |
| G8A / exposure | Global Product permission onboarding, Gateway route and tenant UI | Explicit named-step code-start permits A/D preparation; Auth/Platform acceptance and integration-agent route evidence close before endpoint/user enablement |

No provider follow-up Module Pack or new MOD/FU/PSS/DCP identity is created by this reconciliation.

## GSKU transport amendment — approved 2026-09-08

User-authorized bounded integration; no operational authorization is implied.
Exact additional runtime/test paths:
- `services/Diten.MdmService/src/Diten.MdmService.Infrastructure/Workflow/PlatformProductIdentityWorkflowClient.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/Workflow/PlatformProductIdentityWorkflowClientTests.cs`

Only the ordinal profiles `GskuCorrection` and `GskuRetirementRequest` extend the existing client set.
They use separate explicitly selected templates, existing trusted Workflow consumer endpoints and
`TRUSTED_WORKFLOW_CONSUMER`; no audit-audience substitution. Platform's configured authorization
remains exact client/service/audience/object/template matching; no fixed template name is invented.
Test-only owner transport runs actual Platform named token validation, strict parser and configured
start authorization. A fixture response is not proof of durable Workflow execution.
Unknown/case-drift profiles, mismatched tenant/audience and cross-profile templates fail closed.
No production template, grant or credential is provisioned. Validation results are recorded separately.

Validation 2026-09-08: workflow client contract suite 36 passed / 0 failed / 0 skipped;
Platform security/FU01/FU21 regressions 143/0/0; Platform Release build succeeded.
Ten new cases exercise real owner RS256/tenant/delegated validation, strict parsing or exact policy;
four new invalid-profile cases reject before HTTP. Existing client behavior remains covered.
## GSKU operation-bound options amendment — approved 2026-09-08

The user authorizes separate target-bound draft-edit and correction UoM options. Create
retains its existing create-only selector and trusted scope contract. Immutable parent
selection is not fetched for edit/correction. No client-supplied permission is accepted.
GET `{id}/edit-options` requires `mdm.gskus.update`; GET `{id}/correction-options`
requires `mdm.gskus.request-correction`. The server binds the target, resolves the exact
operation permission, verifies tenant/product scope and persisted pair/action fences,
then returns only target/version evidence and verified UoM choices. No grant, config,
data mutation or operation execution is authorized by fetching choices.

Exact backend runtime/test allow-list for this amendment:
- `services/Diten.MdmService/src/Diten.MdmService.Api/Controllers/GskusController.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Handlers/QueryHandlers/GetGskuCreateOptionsHandler.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Queries/GetGskuCreateOptionsQuery.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Validators/GetGskuCreateOptionsValidator.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/ProductItemSkuMasterModels.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/GskuAuthorizationTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/GskuApiContractTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/GskuMutationOptionsTests.cs`

The existing pack is the only governance path. Frontend/Gateway integration remains
a separate approved commit.

Validation 2026-09-08: MDM Release build succeeded (0 errors); focused mutation-options,
API/authorization, existing create facade and scope consumer tests: 66 passed / 0 failed /
0 skipped. These are isolated unit/contract tests with repository/provider doubles,
not real-Mongo or live acceptance evidence. The two exact route methods select the
operation server-side; the handler uses the real scope guard and candidate facade.
The existing first-GSKU concurrent ordinal allocation flakiness remains open; these
option tests do not diagnose or close it.
## GSKU frontend/L10n/Gateway integration — approved 2026-09-08

Bounded adaptation of retained `ffdd280a` GSKU UI to current backend and the operation-bound
options amendment. No other product UI/runtime or shared layout/theme is included.
Exact runtime/test paths (existing GSKU A/G locale ownership is retained):

- `frontend/Diten.Web/Controllers/GskusController.cs`
- `frontend/Diten.Web/Models/Gskus/GskuViewModels.cs`
- `frontend/Diten.Web/Resources/Views/MasterDataManagement/Gskus/GskusIndex.ar.resx`
- `frontend/Diten.Web/Resources/Views/MasterDataManagement/Gskus/GskusIndex.en.resx`
- `frontend/Diten.Web/Resources/Views/MasterDataManagement/Gskus/GskusIndex.es.resx`
- `frontend/Diten.Web/Resources/Views/MasterDataManagement/Gskus/GskusIndex.fr.resx`
- `frontend/Diten.Web/Resources/Views/MasterDataManagement/Gskus/GskusIndex.ru.resx`
- `frontend/Diten.Web/Resources/Views/MasterDataManagement/Gskus/GskusIndex.tr.resx`
- `frontend/Diten.Web/Resources/Views/MasterDataManagement/Gskus/GskusIndex.zh.resx`
- `frontend/Diten.Web/Views/MasterDataManagement/Gskus/Index.cshtml`
- `frontend/Diten.Web/Views/MasterDataManagement/Gskus/_CreateEditOffcanvas.cshtml`
- `frontend/Diten.Web/Views/MasterDataManagement/Gskus/_Filter.cshtml`
- `frontend/Diten.Web/Views/MasterDataManagement/Gskus/_IndexL10n.cshtml`
- `frontend/Diten.Web/tests/gsku-register.test.js`
- `frontend/Diten.Web/wwwroot/assets/js/MasterDataManagement/Gskus/index.js`
- `gateway/Diten.ApiGateway/ocelot.json`
- `gateway/Diten.ApiGateway.Tests/OcelotConfigurationTests.cs`

Create keeps its Data Protection form-attempt contract and create-only options. Edit/correction
fetch only target-bound verified UoMs; the immutable parent comes from detail, never a create
selector. Option target/GSKU/revision versions must match the editor snapshot. Exact action keys
come from fresh server detail; unknown, duplicate, case-drift or malformed actions fail closed.
The direct-retire MVC surface and its unused locale keys are not carried. Retirement is a request.
Canonical operation GUID checks do not impose an unsupported UUID version/variant restriction.
Correction admission increments GSKU version; read-back verifies the returned increment and
closed action fence while keeping revision unchanged. Draft edit validates post-save detail;
submit/withdraw/retirement require source read-back before reporting the bounded outcome.
WorkCenter's current `diten-field` icon composition replaces the retained input-group styling.
Gateway adds only PUT on `/api/gskus/{everything}`; root PUT, ports, auth and other routes stay unchanged.

Validation 2026-09-08 (non-live):

- MDM amendment suite: 66 passed / 0 failed / 0 skipped; recorded separately above.
- GSKU frontend: 49 passed / 0 failed / 0 skipped (20 source contracts and 29 executed
  JS/DOM contracts using synthetic HTTP responses; not authenticated browser acceptance).
- Combined GSKU/Global Product/personalization: 75/0/0. This includes the 49, not an additive total.
- Gateway Ocelot configuration/route tests: 30/0/0, including eight GSKU nested method/path cases.
- Frontend and Gateway Release builds succeeded; final incremental runs: 0 errors / 0 warnings.
  Initial frontend rebuild emitted existing out-of-scope warnings; they were not fixed here.
- Both GSKU JS syntax checks passed. Seven locale XML files: 69 keys each, exact parity,
  nonempty localized bridge values; all 54 literal JS-used keys have bridge entries.
- Packaged Python Golden Reference verifier: **72 pass / 19 fail**, exit 1. It is not a green
  verifier run and focused tests do not substitute for it. No verifier/shared client was modified.

Each verifier failure is reconciled against the approved GSKU G constraints:

| # | Verifier failure | Existing approved constraint / variance |
|---|---|---|
| 1 | Save View hardcoded Default fallback | G disallows the hardcoded fallback; localized SaveView is present in seven locales. Executed persistence test verifies nonempty context/payload. |
| 2 | Browser personalization tenant header | Actual same-origin `/api/personalization/views` goes through `PersonalizationProxyController.TryApplySecurityHeaders`; it adds tenant header for tenant_user and omits it for platform actors. Browser-header expectation is a verifier/proxy-profile mismatch, not missing header transport. Six shared client tests pass; no live request is claimed. |
| 3 | Active bridge key | G forbids Active/Passive toggle; uses lifecycle states. |
| 4 | Passive bridge key | Same explicit lifecycle-only constraint; unused key not injected. |
| 5 | Edit bridge key | Actual action uses localized EditDraft/UpdateDraft, not generic Edit. |
| 6 | BulkDelete bridge key | G forbids bulk/delete. |
| 7 | BulkDeleteConfirm bridge key | G forbids bulk/delete; no inert confirmation added. |
| 8 | AreYouSure bridge key | Actual bounded lifecycle confirmations use their specific localized messages; shared confirmation owns generic title. |
| 9 | Import bridge key | Import is forbidden and no import button is supplied to exportButtons. |
| 10 | ShowAll bridge key | Used directly by SharedLocalizer in the filter Razor; JS does not consume it. |
| 11 | Status bridge key | Explicit LifecycleStatus field and lifecycle label keys replace generic Active/Passive status. |
| 12 | Direct Gateway window.API base | Approved same-origin MVC proxy profile; browser must not construct service/Gateway authority. |
| 13 | Select-all checkbox | G explicitly forbids select-all. |
| 14 | Bulk action configuration | G explicitly forbids bulk operations. |
| 15 | Bulk selection wiring | No row selection/bulk action surface is authorized. |
| 16 | Bulk endpoint | Backend has no authorized bulk endpoint. |
| 17 | Bulk delete trigger | G explicitly forbids delete; no fake trigger. |
| 18 | Delete reloadWithToast helper | No single/bulk delete lifecycle exists. Typed lifecycle actions validate API result and source detail, then reload the table and report the actual bounded result. Executed JS tests cover this sequencing. |
| 19 | Bulk clear-selection | No bulk selection exists; lifecycle filter clear/reset is real and remains separate. |

Open risk preserved: `ProductItemSkuMasterMongoTests.Concurrent_first_gsku_commands_allocate_unique_parent_ordinals_and_soft_delete_never_reuses`
failed in an earlier concurrency run and later passed; cause remains undiagnosed. No result here
closes that flakiness. No Mongo migration, provisioning, credential/config/grant or real tenant
data mutation was performed. Live two-user browser/WorkCenter approval/rejection, service configuration,
durable audit/recovery acceptance and operational readiness remain outside this evidence.

### LSKU shared transport extraction — approved 19.18 B/C integration, 2026-09-09

Exact paths for this independent prerequisite:
- `services/Diten.MdmService/src/Diten.MdmService.Infrastructure/Workflow/PlatformProductIdentityWorkflowClient.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/Workflow/PlatformProductIdentityWorkflowClientTests.cs`

Add only ordinal `LskuRetirementRequest` to the client object-profile set. Source
`ffdd280a` binds retirement to the retirement operation ID (not the LSKU ID); identity workflow retains
`lsku` with the source LSKU ID. Template/object/client/audience authorization remains the current
Platform exact configured policy, not a wildcard or a second workflow bridge.

Client tests: 45 passed / 0 failed / 0 skipped. The added positive and negative cases execute actual
Platform Release named-token validation, delegated-human validation, parser and exact authorization.
They reject cross-tenant, audit-audience substitution, mixed templates, wrong client, missing grant,
case/prefix/wildcard profiles. The terminal dispatch deliberately returns a missing test-owned workflow;
this is transport/security evidence, not a successful live workflow or terminal LSKU read-back claim.

Backend integration scope gate: the protected retirement implementation additionally requires
`services/Diten.MdmService/src/Diten.MdmService.Domain/ValueObjects/LskuActiveLifecycleOperationBinding.cs`.
The new value-object path is absent from the approved E2A / 19.18 exact new-file lists. It is used by
`Lsku.ActiveLifecycleOperation`, `ILskuRepository.AcquireLifecycleOperationAsync`,
`ApplyRetirementDecisionAsync`, the persistence CAS and retirement processor. No substitute, inlining
workaround or bypass is introduced. This exact path needs explicit allow-list authorization before
the complete backend retirement slice can be integrated. No backend/UI completion is claimed.

### LSKU backend integration exact paths — renewed user approval 2026-09-09

The user explicitly authorizes the missing operation binding below and continuation of approved E2A/19.18
backend integration. This supersedes the preceding scope gate only; no live acceptance is implied.
Source reference is protected `ffdd280a`, reconciled with the current target. No edit/correction or new selector.
### LSKU frontend integration exact paths — approved 2026-09-09

This extracts protected `ffdd280a` section 19.18.6 under the renewed user approval, without
navigation, edit/correction, direct-retire UI, bulk/delete/import or new selector endpoints.
Gateway routing is already sufficient; only its existing configuration test is extended.
Exact touched runtime/test files:

- `frontend/Diten.Web/Controllers/LskusController.cs`
- `frontend/Diten.Web/Models/Lskus/LskuViewModels.cs`
- `frontend/Diten.Web/Views/MasterDataManagement/Lskus/Index.cshtml`
- `frontend/Diten.Web/Views/MasterDataManagement/Lskus/_Filter.cshtml`
- `frontend/Diten.Web/Views/MasterDataManagement/Lskus/_IndexL10n.cshtml`
- `frontend/Diten.Web/wwwroot/assets/js/MasterDataManagement/Lskus/index.js`
- `frontend/Diten.Web/tests/lsku-register.test.js`
- `frontend/Diten.Web/Resources/Views/MasterDataManagement/Lskus/LskusIndex.ar.resx`
- `frontend/Diten.Web/Resources/Views/MasterDataManagement/Lskus/LskusIndex.en.resx`
- `frontend/Diten.Web/Resources/Views/MasterDataManagement/Lskus/LskusIndex.es.resx`
- `frontend/Diten.Web/Resources/Views/MasterDataManagement/Lskus/LskusIndex.fr.resx`
- `frontend/Diten.Web/Resources/Views/MasterDataManagement/Lskus/LskusIndex.ru.resx`
- `frontend/Diten.Web/Resources/Views/MasterDataManagement/Lskus/LskusIndex.tr.resx`
- `frontend/Diten.Web/Resources/Views/MasterDataManagement/Lskus/LskusIndex.zh.resx`
- `gateway/Diten.ApiGateway.Tests/OcelotConfigurationTests.cs`

### LSKU frontend verification evidence — 2026-09-09

- Reconciled the approved 19.18.6 source, preserving immutable create/read and same-origin MVC.
  Submit, withdrawal and retirement request retain exact permissions, antiforgery, server-owned
  idempotency and fresh state/version read-back. Direct-retire MVC exposure and its three unused
  localization entries were not imported. No navigation or Gateway runtime change was needed.
- Corrected the source retirement-click dispatch, post-action detail/version refresh and non-advancing
  version detection. AvailableActions is matched case-exactly and intersected with lifecycle state.
  Save View fails instead of claiming success when its client/receipt/name is absent.
- Frontend Release build: 0 errors / 14 existing warnings. Gateway Release build: 0 errors / 0 warnings.
  OcelotConfigurationTests: 37 passed / 0 failed / 0 skipped, including seven new LSKU route cases;
  these are shipped-config assertions, not live Gateway forwarding.
- Final frontend run: 102 passed / 0 failed / 0 skipped across LSKU (27), Global Product (20),
  GSKU (49) and shared personalization (6). The 27 LSKU checks comprise executable DOM/fetch-double
  contracts and source-contract checks; they are not live MVC/browser acceptance. Earlier 23/25-case
  runs overlap and are not added. Both LSKU JavaScript syntax checks passed.
- Seven RESX documents parsed with 57 unique, nonempty, identical keys each. Used page/controller
  localization literals are present. Shared ShowAll is rendered server-side by the filter.
- Bundled Python, unchanged verifier, default profile: initial 72 pass / 19 fail; after fixing the
  actual missing px-3 filter alignment: **73 pass / 18 fail (exit 1)**. The following classification
  records each failure, not a blanket verifier waiver. Authority is the separately approved protected
  19.18.6 contract extracted above; historical first-slice variances alone do not authorize this slice.

| # | Remaining verifier failure | Exact contract / inspection result |
|---|---|---|
| 1 | Nonempty default Save View name | 19.18.6 forbids hardcoded English Default; localized name is validated nonempty or fails. Executable save/reload and missing-response tests pass. |
| 2 | Browser personalization tenant header | 19.18.6 forbids browser tenant authority. Unchanged personalization-client calls same-origin /api/personalization/views; PersonalizationProxyController applies the cookie bearer and X-Tenant-Id only for tenant_user. This is an outdated browser-profile expectation, not missing server forwarding. Shared client tests: 6/0/0. |
| 3 | Active L10n | 19.18.6 forbids Active/Passive; the real lifecycle enum is used. |
| 4 | Passive L10n | Same explicit Active/Passive prohibition. |
| 5 | Edit L10n | 19.18.6 and current approval forbid LSKU edit/correction. |
| 6 | BulkDelete L10n | 19.18.6 forbids bulk/delete; no inert label is added. |
| 7 | BulkDeleteConfirm L10n | Same explicit bulk/delete prohibition. |
| 8 | AreYouSure L10n | Generic delete dialog is absent; real lifecycle actions use their localized confirmations under 19.18.6. |
| 9 | Import L10n | 19.18.6 forbids Import. |
| 10 | ShowAll JavaScript bridge key | The actual lifecycle selector renders SharedLocalizer ShowAll in Razor; JavaScript never consumes it. Adding an unused bridge entry would not test that contract. |
| 11 | Direct-Gateway window.API base | 19.18.6 mandates same-origin MVC; Gateway authority stays server-side. |
| 12 | Select-all checkbox | 19.18.6 forbids select-all/bulk. |
| 13 | Bulk action config | Same explicit bulk prohibition. |
| 14 | Bulk selection wiring | Same explicit bulk/select-all prohibition. |
| 15 | Bulk endpoint call | Same prohibition; no such backend endpoint is invented. |
| 16 | Bulk-delete trigger | Same explicit bulk/delete prohibition. |
| 17 | Shared delete reloadWithToast helper | No delete path exists. 19.18.6 lifecycle success follows real response and detail state/version read-back, then table reload; executable tests cover this ordering and failure cases. |
| 18 | Bulk clear-selection | No row selection exists. The separate lifecycle filter supports Select2 clear, Apply and factory Reset; executable filter/reset tests pass. |

No runtime configuration, user grant, workflow template, real business record, live WorkCenter action or
browser acceptance was performed. Navigation remains hidden. Existing GSKU flakiness remains open:
`ProductItemSkuMasterMongoTests.Concurrent_first_gsku_commands_allocate_unique_parent_ordinals_and_soft_delete_never_reuses`;
later green runs do not establish its cause. The common Windows DisposableMongoReplicaSet helper debt is
also unchanged; backend evidence used the separately owned replica-set fixture, not application Mongo.

### LSKU backend reconciliation evidence — 2026-09-09

- FU21 `f29f845ec48f27d3f67c6928263b04fcd4c4eaf4` preserves 30 pairs and adds only submit/retire (32 exact pairs). It grants no user permission.
- The explicitly approved retirement binding fences admission and terminal decisions by operation ID, base version and kind. Repository tests reject stale/conflicting bindings, expired leases, stale generations, wrong tenants and fingerprint drift.
- Withdraw and retirement-request use their exact permission in Enforced scope through the real MDM client and Platform credential/JWT/parser/executor. Out-of-scope and cross-tenant targets stop before workflow, operation or audit mutation.
- Source reconciliation repairs withdrawal observation validation (object reference is evidence, not a rewritten preflight); retirement claim tenant/fingerprint/expiry fences; stable admission audit timestamp; fail-closed terminal source read-back and crash replay. Pending submit read-back accepts the existing exact compacted G4 receipt contract. No global serializer, audience or human-JWT change.
- MDM Release build: 0 errors (5 existing nullable/obsolete API warnings in the final build).
- `lsku-backend-final-20260909.trx`: 168 passed, 0 failed, 0 skipped; 38 of these are real-Mongo tests, not an additional total.
- `lsku-backend-regressions-20260909.trx`: 615 passed, 0 failed, 0 skipped (Global Product/GSKU/scope/audit); separate overlapping regression run, not added to focused counts.
- Initial focused run: 117 passed / 1 failed, obsolete create-only repository assertion reconciled with approved append-only lifecycle contract. Initial real-Mongo run: 32 passed / 2 failed, retirement failure classification repaired; these were code/assertion mismatches, not transaction topology failures.
- Real Mongo: isolated test-owned single-node `lsku_itest`, loopback port 27129, primary; fixed test database and per-test tenant cleanup. Application port 27017/config/data unchanged. No operational workflow, migration, provisioning, navigation activation or live acceptance.
- Known GSKU concurrency flakiness remains open: `ProductItemSkuMasterMongoTests.Concurrent_first_gsku_commands_allocate_unique_parent_ordinals_and_soft_delete_never_reuses`. A subsequent green regression does not establish its cause or close it.
- Frontend follows in a separate slice; no LSKU edit/correction/new selector or direct-retire UI is authorized.

Exact new and existing LSKU-specific backend/test paths for this extraction:

- `services/Diten.MdmService/src/Diten.MdmService.Domain/Enums/ProductAuditOperation.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/ProductItemSkuMasterModels.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Persistence/DependencyInjection.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Api/Program.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Api/ModuleRegistration/ProductItemSkuMasterManifestProvider.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/LskuDraftFoundationUnitTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ModuleRegistration/ProductItemSkuMasterManifestProviderTests.cs`

- `services/Diten.MdmService/src/Diten.MdmService.Api/Configuration/LskuIdentityWorkflowOptions.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Api/Configuration/LskuIdentityWorkflowWorkerOptions.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Api/Configuration/LskuRetirementRequestWorkflowOptions.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Api/Configuration/LskuRetirementRequestWorkflowWorkerOptions.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Api/Services/ProductItemSkuMaster/LskuIdentityWorkflowRecoveryCommandLine.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Api/Services/ProductItemSkuMaster/LskuIdentityWorkflowRecoveryRunner.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Api/Services/ProductItemSkuMaster/LskuIdentityWorkflowRecoveryWorker.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Api/Services/ProductItemSkuMaster/LskuRetirementRequestRecoveryCommandLine.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Api/Services/ProductItemSkuMaster/LskuRetirementRequestRecoveryRunner.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Api/Services/ProductItemSkuMaster/LskuRetirementRequestRecoveryWorker.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Lifecycle/Commands/RetireLskuIdentityCommand.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Lifecycle/Handlers/CommandHandlers/RetireLskuIdentityHandler.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Lifecycle/LskuIdentityLifecycleAuditIntentFactory.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Lifecycle/LskuIdentityLifecycleModels.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Lifecycle/LskuRetirementRequestModels.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Lifecycle/Validators/RetireLskuIdentityValidator.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Workflow/Commands/StartLskuIdentityWorkflowCommand.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Workflow/Commands/StartLskuRetirementRequestWorkflowCommand.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Workflow/Commands/WithdrawLskuIdentityApprovalCommand.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Workflow/Handlers/CommandHandlers/StartLskuIdentityWorkflowHandler.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Workflow/Handlers/CommandHandlers/StartLskuRetirementRequestWorkflowHandler.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Workflow/Handlers/CommandHandlers/WithdrawLskuIdentityApprovalHandler.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Workflow/LskuIdentityWorkflowProcessor.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Workflow/LskuIdentityWorkflowStartRequestFactory.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Workflow/LskuRetirementRequestWorkflowProcessor.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Workflow/LskuRetirementRequestWorkflowStartRequestFactory.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Workflow/Validators/StartLskuIdentityWorkflowValidator.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Workflow/Validators/StartLskuRetirementRequestWorkflowValidator.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Workflow/Validators/WithdrawLskuIdentityApprovalValidator.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/LskuIdentityWorkflowOperation.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/LskuRetirementRequestOperation.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Enums/LskuIdentityWorkflowCheckpoint.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Enums/LskuRetirementRequestCheckpoint.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/ILskuIdentityWorkflowOperationRepository.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/ILskuIdentityWorkflowTenantPartitionDiscovery.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/ILskuRetirementRequestOperationRepository.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/LskuIdentityWorkflowOperationResults.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/LskuRetirementRequestOperationResults.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/ValueObjects/LskuActiveLifecycleOperationBinding.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/LskuIdentityWorkflowOperationRepository.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/LskuIdentityWorkflowTenantPartitionDiscoveryRepository.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/LskuRetirementRequestOperationRepository.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/LskuIdentityApprovalWithdrawalUnitTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/LskuIdentityLifecycleContractTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/LskuIdentityLifecycleMongoTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/LskuIdentityWorkflowOperationMongoTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/LskuIdentityWorkflowProcessorTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/LskuIdentityWorkflowRecoveryRunnerTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/LskuRetirementRequestOperationMongoTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/LskuRetirementRequestRecoveryContractTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/LskuRetirementRequestWorkflowProcessorTests.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/Lsku.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/ILskuRepository.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/LskuRepository.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Handlers/QueryHandlers/GetLskuByIdHandler.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Handlers/QueryHandlers/GetLskusHandler.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Queries/GetLskusQuery.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Validators/GetLskusValidator.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Api/Controllers/LskusController.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/LskuApiContractTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/LskuAuthorizationTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/LskuRegisterQueryTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/LskuRegisterMongoTests.cs`

### 2026-09-09 approved ABB tenant-discovery amendment

The owner explicitly adds only `mdm_product_abbreviation_register`, verified against
`ProductAbbreviationRegisterRepository`, to the existing eight-collection bounded tenant-discovery set.
Exact implementation/test paths:
- `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/AuditIntentTenantPartitionDiscoveryRepository.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/Audit/AuditIntentDeliveryWorkerMongoTests.cs`

No wildcard discovery, grant/configuration change, worker activation, schema migration or operational cutover.
Existing temporal-state validation, exact intent tenant/source/type checks and paging limits are unchanged.
Test-owned replica-set run `AuditIntentDeliveryWorkerMongoTests`: 11 passed, 0 failed, 0 skipped.
Evidence covers ABB-only tenant discovery followed by real delivery discovery/claim/payload read, cross-tenant denial,
duplicate/cursor paging across ABB plus all original eight collections, and rejection of foreign intent tenant,
wrong aggregate/source, legacy temporal storage, future retry, active lease, delivered and dead-letter records.
The independent audit storage prerequisite run has 2 passed tests; these are distinct from the 11 discovery tests.
Producer, WorkCenter and UI integration are separate gates; neither run proves live worker acceptance.

## 21. Finished Good Phase 1.5 proposal — planning only (2026-09-10)

### 21.0 Decision summary — one-page recommendation, not runtime approval

FG remains a retained identity register with current multiplicity; no Inventory/Material/Brand redesign.
Recommend **O2: narrow FG-local continuation authority** over existing Auth-issued identities, native Workflow evidence,
current MDM scope/fence and G4. Unchanged reuse is insufficient; a new central audience/LE-grant/permit platform is not
shown necessary. Keep the earlier central proposal deferred and retain the current Platform 5/20 schema budget.

Two small shared gaps remain selected: read-only recovery of a committed cancellation whose response was lost, and a
named-service own-tenant status/context read. Neither grants a service the right to cancel a task. Draft-cancel
finalization is mechanical completion of an already authorized local decision: use its receipt and current audit
executor gate, not a fabricated WorkCenter decision or new Workflow grant.

Recommend admission-time maker authority and decision-time checker authority, with **current** executor/tenant/source
checks at application. Later offboarding alone should not strand an authorized decision. Remote revocation is observed,
not globally atomic; approve the bounded-observation model only after its visibility/deadline assumptions and physical-
CAS expiry are tested. The 300-second token lifetime is not a permit window; no measured global bound exists here.

For orphan Drafts, recommend exact cancel-draft permission + current product/LE scope + mandatory reason, even for
another creator's Draft. This is not yet approved and does not permit withdrawing another requester's pending process.
Keep immutable original creation provenance and actual cancelling actor separate.

The original 239 paths are retained and classified once. O2 selects 212 of them and adds 12 exact paths: 46 reuse,
23 semantic source ports, 86 existing-file amendments, 69 genuinely new proposed files across runtime/tests.
The other 27 new central files are deferred, not required. Details and contract-on-existing-file distinctions are in 21.7.
First runtime step, if separately authorized: prove the pre-insert changed-GSKU replay risk using the existing real
repositories before proposing a binder redesign. Exact keys/role deltas, 72–76 ordinal candidates, O2 contracts,
orphan cancellation policy and revocation acceptance remain final owner approvals. No code-start/status/Git mutation.

### 21.1 Authority, baseline and evidence limits

**ARCHITECTURAL DIRECTIONS APPROVED; RUNTIME CODE-START NOT AUTHORIZED.** This remains a planning revision of existing
MOD-0290, not a new ID, runtime/test amendment approval or status promotion. Only Section 21 may change in this turn;
frontmatter, preceding sections, historical approvals and the opening guard remain untouched. Every proposed exact
permission, role delta, operation name/ordinal, authority protocol and future path below still needs explicit review
and code-start approval. Cross-domain Auth/Platform amendments are proposals, not permissions for this MDM pack to edit
other owners' runtime or governance.

The user's selected architectural directions now supersede the first proposal's alternatives:

| Selected direction | Boundary still not granted |
|---|---|
| Preserve current GSKU → zero-or-more FG model and existing records | No assertion that multiplicity has a proven business need; no uniqueness migration or invented discriminator |
| Audited Draft cancellation, no physical deletion or code reuse; safe admission/fence release | Exact permission, cancellation representation, audit allocation and implementation remain proposed |
| Authorized own withdrawal for both undecided identity approval and retirement request | Exact separate permission keys and role grants remain proposed; terminal decisions cannot be overwritten |
| Background applies authenticated terminal decisions without waiting for maker return | Requires explicit narrow audited tenant/scope/revocation/fence authority; no current service audience or human JWT is an automatic substitute |
| Fixed CanonicalCode ordering aligned with UI and Save View | No new server-sort feature or arbitrary saved order |
| WorkCenter retirement request with safe direct-retire grant transition | No direct-retire endpoint, automatic assignment or premature claim that historical grants are all revoked |

Target worktree is `C:/dev/ERP-vNext/.worktrees/product-pv-delivery-integration-20260907`, branch
`codex/product-pv-delivery-integration-20260907`, HEAD
`dc6857d03cdafde5628affdcd0e5efc25bae3f7f`.
The first proposal started with clean tracked/index state and 432 retained .testoutput files.
This revision starts with that expected MOD-0290 pack modification, an empty index, the same HEAD and retained evidence;
it does not reset or replace another worktree's work. No build/test, browser, Mongo or service operation runs here.

Verified source `d58368d0e30f26b2e032d9f70f20bd7fc8712c77` is an ancestor of
`bb9ce94d0ca3d4520f3d1555d284cbe3f8219f25`. Their FG implementation has identity submit/terminal reconciliation and
direct-retire, but no Draft cancellation, identity withdrawal or WorkCenter retirement-request/withdrawal.
Preserve the later immutable start/terminal evidence separation, not the old maker-return recovery restriction:
`AwaitingMakerReplay` is source history to reconcile under the newly selected background-authority design, not a future
requirement. Source code is not an overwrite instruction or approval of new contracts. Historical Section 19 live
evidence and retained test artifacts do not prove current lifecycle runtime or Production readiness.

### 21.2 Business meaning, fields and cardinality decision

A user selects a referenceable approved GSKU and receives a separately allocated FG canonical identity code.
Today this is an identity register entry, not a stock balance, manufacturing order, lot, BOM, material, plant/site
assignment or regulatory licence. A proposed future submit opens an identity approval task in existing WorkCenter;
a checker decision becomes FG state only after verified MDM read-back. A future retirement request is another
controlled decision, not a direct status update.

| Field or link | Actual source and meaning | Phase 1.5 boundary |
|---|---|---|
| `GskuId` | One user selection, server revalidated against tenant, parent state and Product Legal Entity Scope | Preserve the current E1B approved GSKU + approved Product Definition Revision prerequisite; do not restore historical Draft-parent acceptance |
| `CanonicalCode` | System allocation/reservation, not editable input | Preserve immutable code, reservation binding and no reuse |
| `CodeReservationId`, `CreationCommandId` | Allocation and durable create-attempt identity | Technical evidence, not form fields; one logical retry retains identity |
| `LifecycleStatus` and proposed FG cancellation disposition | Server-owned Draft today; future workflow transitions plus FG-specific immutable cancellation evidence | No arbitrary update, direct-retire, Retired alias, edit/correction or physical delete endpoint |
| `Id`, `TenantId`, `Version`, timestamps, soft-delete markers | EntityBase and authenticated server context | Tenant isolation and CAS; Draft cancellation uses retained business evidence, not IsDeleted/DeletedAt or erase |
| Audit intents/receipts | Local producers, durable G4 acceptance and compaction | Not user-entered business data; no sensitive payload display |
| GSKU code / revision / Global Product | Display or validation reached through GSKU and its revision/parent relationships | Do not duplicate those identities or editable names in FG |
| Legal Entity access | Current Global Product scope policy reached through the parent chain | No invented FG LegalEntityId or browser-generated tenant/scope authority |
| LSKU, Market, composition, Material, Inventory | No direct FG association in the inspected model | Deferred dependencies, not fabricated fields; FG does not need the LSKU verified-Market resolver |
| Proposed operation, maker, workflow binding, terminal/cancellation/execution evidence | New or source-candidate technical records | Immutable human provenance versus authenticated service executor; exact reason/authority fields require owner review |

Blueprint master 8.1 `Blueprint_Data!A291:AG291`, `SoR_Map!A256:E256` and
`Dependencies!A1281:D1285` support Product/Item/SKU SoR, UoM/lifecycle/downstream boundaries and MOD-0003,
MOD-0040, MOD-0021, MOD-0252, MOD-0253 dependencies. Read-only searches of all workbook sheets found no explicit
Finished Good/Bitmiş Ürün wording establishing a multiple-FG business purpose. This is limited evidence, not proof
that no implicit need exists. The current locked domain contract Sections 4 and 7.5 allows GSKU → zero-or-more FG,
each FG → exactly one GSKU; the repository's non-unique parent index and tests implement this existing approved model.
Current consumers found are parent-retirement admission, scope inventory, code ledger and audit delivery, not a
manufacturing/inventory consumer that explains why siblings need distinct identity. No business database was queried.

**Selected:** preserve the existing zero-or-more model and data. Its differentiating business need remains unvalidated,
not a code-start blocker requiring a cardinality change. Any future zero-or-one proposal would need a new explicit
business decision, assessment of historical siblings/conflicts, and separately approved schema/data work. Do not add a
unique index, migrate records or add `StewardLabel` merely to distinguish them.
Material/Inventory/Composition linkage and Brand/Product redesign remain excluded.

The current parent-retirement blocker includes every non-retired FG, including Draft. The selected Draft-cancellation
flow addresses that trap without deleting the identity. Proposed FG-specific immutable cancellation facts/disposition
must distinguish `CancellationPending` from audit-finalized `Cancelled`; do not relabel cancellation as Retired or
set IsDeleted. A pending identity approval must first complete valid withdrawal back to Draft. Only a cancellation
whose durable audit receipt and operation-bound finalization are verified may cease blocking parent retirement.
The exact target GSKU blocker must exclude only that terminal case under the existing parent admission/fence contract;
unknown outcomes, missing receipts and other children still block. Canonical code and confirmed reservation remain
consumed/bound permanently. No broad fence clear, reservation reset, code burn/reuse or implicit parent retirement occurs.
The shared four-value lifecycle enum need not be expanded for every product; the proposed FG-specific representation
and DTO projection are an explicit owner-review item. Retained cancelled FG remains in scope inventory and audit discovery.


### 21.3 Current versus selected future end-to-end contracts

All new routes/actions in this table remain implementation proposals. No new FG lifecycle endpoint is present at the
inspected target merely because an architectural direction is selected.

| Flow | Target / source truth | Selected future behavior and required closure |
|---|---|---|
| Create/read/selector | Existing read/create backend and MVC | Preserve approved GSKU/revision, scope, allocation and retry identity; close pre-insert payload-binding proof in 21.6 |
| Draft cancellation | Absent target and old source | Authorized Draft-only audited terminal cancellation, immutable code/history, PendingAudit then verified receipt/finalization; no delete/Retired alias; only finalized cancellation releases parent blocker |
| Submit | Absent target FG API/handler; source candidate exists | Draft without any cancellation record → workflow-backed Pending; maker identity, current permission/scope, CAS and audit |
| Approve/reject read-back | Generic WorkCenter exists; target FG processor absent | Native checker decision → unattended authorized MDM terminal application and actual source read-back, without maker return |
| Identity withdrawal | Absent both target and source | Authorized own undecided request, native cancellation proof before Draft restoration; then a separate Draft-cancel command if desired |
| Retirement request | Absent both target and source | Approved FG → native WorkCenter request; rejection preserves approved identity, approval eventually finalizes Retired through the same background authority |
| Retirement-request withdrawal | Absent both target and source | Authorized own undecided retirement request; exact operation/profile cancellation; FG remains approved and active request binding is released only on verified outcome |
| Direct retire | Old source only | Not ported; safely supersede only module-owned grant, preserve other-source rights without exposing a direct-retire route |
| Edit/correction/delete/import/navigation | Not selected | No mutable FG business field established; no fake action, physical erase or unrelated UI expansion |

Current routes remain `GET /api/finished-goods`, `GET /api/finished-goods/{id}`,
`GET /api/finished-goods/gsku-selector`, `POST /api/finished-goods/drafts`.
Read uses `mdm.finished-goods.read`; selector/create uses `mdm.finished-goods.create`.
The source `POST /api/finished-goods/{id}/submit` is a reconciliation candidate.
Proposed new routes are `POST /api/finished-goods/{id}/cancel-draft`,
`POST /api/finished-goods/{id}/withdraw`, `POST /api/finished-goods/{id}/retirement-requests`,
and `POST /api/finished-goods/{id}/retirement-requests/{operationId}/withdraw`.
Each mutation requires exact permission, strict request shape, immutable operation identity and expectedVersion;
unknown fields cannot select an authority or override stored actor/profile. Background reconciliation is not a public
human `lifecycle/reconcile` endpoint and must not require a maker JWT or maker revisit.

Browser → same-origin MVC → existing Gateway GET/POST/OPTIONS → MDM port 5059 remains sufficient.
No root/nested PUT, route, port or auth change is proposed; only route-contract tests. No browser-generated tenant,
bearer or service identity. CSRF and protected form/action identities remain required.

| Chain boundary | Current delta / explicit future gate |
|---|---|
| Auth → manifest | Current Auth FG submit/retire profile disagrees with actual read/create-only FG manifest. Exact coordinated catalog/profile/action/endpoint delta is required; publishing keys cannot stand in for implemented endpoints |
| User permission → FU21 | Human commands use independently validated delegated JWT + exact `product-item-sku-master` pairs. New action keys are proposals in 21.4; background service authority is separate, not a fake human pair |
| Scope → repository | Current parent-chain scope and local LE/policy validation, short-held admission, physical-write CAS/fence; cancellation/withdrawal cannot clear unrelated operations |
| Audit → durable state | Exact FG aggregate/operation/ordinal, local intent, G4 acceptance/receipt, compaction/replay; cancellation PendingAudit is not final and does not free the parent |
| MDM → workflow | Reuse identity profile `finished-good`; proposed retirement profile `FinishedGoodRetirementRequest` must be validated against exact client/service/audience/object/template contract |
| Platform → WorkCenter | Reuse native provider/action dispatcher and transaction/concurrency behavior. WorkCenter decides; MDM alone owns FG state |
| Terminal → MDM background | New explicit machine execution authorization + current tenant/scope/revocation/fence + exact terminal proof; no inference from evidence-read permission or original human JWT |
| Terminal read-back → UI | Actual server FG disposition, lifecycle and version; accepted/unknown/pending is never displayed as finalized |

Exact existing audience boundaries: workflow `TRUSTED_WORKFLOW_CONSUMER`; audit
`TRUSTED_AUDIT_SOURCE_INGEST` with source `Diten.MDM` and contract `mod-0290.audit-intent.v1`;
human FU21 `TRUSTED_LEGAL_ENTITY_SCOPE_RESOLVE`. None currently authorizes autonomous FG source mutation.
A separate proposed authority contract in 21.4 must be reviewed rather than silently widening these audiences.
Runtime client/template IDs and tenant provisioning remain outside this plan; proposed retirement object-ID meaning
must distinguish workflow operation ID from source FG ID.

### 21.4 Proposed exact permission matrix, safe supersession and background authority

The architecture approves these flow responsibilities, **not these exact new strings or grants**. Existing FG keys are
`mdm.finished-goods.read`, `mdm.finished-goods.create`, and Auth's existing `mdm.finished-goods.submit`/
`mdm.finished-goods.retire` (the latter two are absent from current FG manifest/API). Proposed new keys are
`mdm.finished-goods.cancel-draft`, `mdm.finished-goods.withdraw`,
`mdm.finished-goods.request-retirement`, and `mdm.finished-goods.withdraw-retirement-request`.
Separate withdrawal keys prevent identity-steward scope from automatically granting retirement-request withdrawal.

| Existing responsibility role | Current FG-specific grants | Proposed FG-specific grants / exact delta |
|---|---|---|
| `ProductDataSteward` | read, create, submit | Keep those three; add `mdm.finished-goods.cancel-draft` and `mdm.finished-goods.withdraw`; retain native `platform.workflow.instances.start` |
| `ProductIdentityApprover` | read | No FG delta; native `platform.work-aggregation.inbox.view`, `platform.workflow.tasks.approve`, `platform.workflow.tasks.reject` unchanged |
| `ProductIdentityRetirementSteward` | read, retire | Keep read; remove only module-owned `mdm.finished-goods.retire`; add `mdm.finished-goods.request-retirement` and `mdm.finished-goods.withdraw-retirement-request`; retain native workflow start |
| Admin / Viewer | Existing base-template behavior | No lifecycle mutation addition, no responsibility membership or automatic extra read permission |

Other products' permissions are unchanged. Roles are grant templates, not authorization checks by role name.
Each withdraw additionally requires the operation's same canonical requester, undecided exact workflow binding and
current permission/scope. Draft cancellation ownership is addressed separately below: recommend exact cancel-draft permission + current scope
with mandatory reason, including another creator's Draft; this broadened stewardship choice is not yet approved. Human maker/checker separation
continues through native WorkCenter. No new `mdm.finished-goods.approve/reject` keys or direct-retire exposure.

Proposed new human FU21 pairs (existing read/create and every unrelated pair remain unchanged):

| Exact resource | Exact permission proposal |
|---|---|
| `product-item-sku-master` | `mdm.finished-goods.submit` |
| `product-item-sku-master` | `mdm.finished-goods.cancel-draft` |
| `product-item-sku-master` | `mdm.finished-goods.withdraw` |
| `product-item-sku-master` | `mdm.finished-goods.request-retirement` |
| `product-item-sku-master` | `mdm.finished-goods.withdraw-retirement-request` |

No automatic retire/background pair, wildcard, prefix, case-insensitive match or read fallback. Pair acceptance does not
grant user authority. Current real role totals are 20/7/11 (Steward/Approver/RetirementSteward); this exact proposed delta would produce 22/7/12,
not an approved target count. Current lifecycle-key set is 18; replacing FG retire with four new keys would yield 21.
Derive and test these sets from definitions, not by adding unrelated grants to match a number.
Coordinate actual manifest emission, exact Auth profile, implemented endpoint/actions and reconciliation atomically as a
validated release sequence; do not “fix” strict reconciliation merely by advertising unavailable endpoints.

Direct-retire transition is source-safe: existing `EntitlementPermissionSyncService` removes only stale
`GrantSource.Module` + `SourceModuleCode = product-item-sku-master` grants; manual and other-source grants survive.
Desired-profile replay must be cardinality-stable, revoke/restore must restore the new exact grants, and old/stale events
must not revive old FG authority. Retained manual retire rights preclude claiming that every retire grant was revoked;
the selected interface still exposes no direct-retire route/profile. An old JWT cannot manufacture a missing endpoint.
No user membership assignment or operational reconciliation is performed. Historical FU23 owner pack is source-only;
its future exact amendment remains a cross-owner dependency, not authorization to create that pack now.

#### Orphan Draft decision — recommendation, not approval

| Alternative | Business/security result | Exact scope |
|---|---|---|
| Creator-only | Least actor reach, but offboarding can strand Draft and block parent retirement | Same proposed command/handler/evidence/test paths; requires demonstrable canonical creator, never guess from display name |
| Exact permission + current scoped steward **recommended** | Avoids orphan lock without an operator module; authorizes the action by permission and current product/LE scope, never role name | Draft only, no pending/conflicting operation, expectedVersion, mandatory bounded reason; same paths, no added general endpoint |

The second option still needs user approval. Preserve original creator/creation-command provenance and record the
actual cancelling actor separately; unavailable legacy creator evidence must not be fabricated. Do not grant another
person's identity/retirement withdrawal: those remain own-requester only. Reject pending/conflicting operations before
repository/audit/workflow mutation. A retry by another actor cannot overwrite the admitted actor/fingerprint.
Tests must cover another creator with exact key/scope, missing key, scope denial, tenant mismatch, role-name-only denial,
mandatory reason, conflicting operation, own versus other-requester withdrawal and immutable replay actor.

#### 21.4.1 Minimum background design — recommendation for approval

| Option | Exact current evidence and coverage | Gap / cost / recommendation |
|---|---|---|
| O1 — unchanged reuse | Existing `AuthProductIdentityWorkflowServiceIdentityProvider.GetAsync(forceRefresh: true)` obtains fresh Auth issuance; `IssueServiceIdentityTokenHandler` checks current identity/credential/exact tenant-audience grant; Platform named validator checks real RS256; existing terminal evidence and FU03 evaluator/fence/G4 are available | Does not itself authorize source mutation. Source FG recovery denies Enforced; no admitted finite scope snapshot or service-actor fence entry. Current token issuance and workflow executor do not check tenant Active. Unchanged reuse cannot satisfy unattended safe apply |
| O2 — narrow local continuation contract **recommended** | Reuse those mechanisms; a new FG-local `FinishedGoodLifecycleExecutionAuthority` validates one durable admitted operation + terminal/cancellation/receipt proof + current own-tenant observation + current local scope/policy/fence. Existing Auth audiences/grants and Platform tenant registry remain SoR | New local contract and small named-service tenant-context read are necessary; no new audience, grant collection or central permit. Exact selected paths below. Lowest proposed operational cost: existing credentials/grants, no duplicate LE-grant administration |
| O3 — new central audience / execution grant / permit | Prior candidate `TRUSTED_FINISHED_GOOD_LIFECYCLE_APPLY`, Platform finite-LE grant and Auth epoch/permit paths are retained as **deferred alternatives**, not erased | Not shown necessary for FG. Would create a new authority SoR, provisioning, epoch-writer coverage and schema cost. Needed only if owners require independently administered background LE grants or coordinated global revocation. Do not implement merely because proposed earlier |

O2 is a new explicit source-side contract, not a claim that an existing transport grant already authorizes FG writes.
It is reachable only from the bounded FG recovery path for an already admitted operation; no public arbitrary-operation
or user-supplied permission/tenant interface. Persist immutable canonical human admission, exact request/fingerprint,
finite admitted LE upper-bound and original workflow binding. Existing source FG operations lack that scope snapshot:
this is a semantic port change, not previously implemented evidence. Preserve maker/checker facts; never persist their JWT.

For workflow application, obtain fresh existing workflow identity, retrieve exact native terminal evidence (or the
separately proposed read-only cancellation evidence), then apply the narrow local policy. The existing provider's forced
refresh captures its rejected token inside its semaphore and acquires afresh; no provider behavior change is selected.
Platform validates the token cryptographically; MDM's structural token parsing alone is not RSA validation.
The service-token remains exact `exp = nbf + 300`, `iat = nbf`; default validation skew is 30 seconds. This transport
lifetime, or its possible skew allowance, is **not** an execution permit or an approved revocation window.

Current tenant Active is a real missing gate: Auth issuance checks client/grant, not tenant registry, and the workflow
executor only establishes TenantScope. Proposed FG own-tenant execution-context **read**, not a permit/grant resolver,
uses three exact **proposed, not yet approved** purposes: `FinishedGoodIdentityTerminalApply` and
`FinishedGoodRetirementTerminalApply` use the existing `TrustedWorkflowConsumerService` named scheme;
`FinishedGoodDraftCancelFinalize` uses the existing `TrustedServiceToken` audit named scheme. Unknown purposes,
mixed-purpose proof payloads and purpose/audience mismatches are rejected; no scheme fallback is allowed.
Tenant comes solely from the validated token and must be Exists + Active through the existing local
`GetTenantStatusQuery`/`GetTenantStatusQueryHandler`/`ITenantRegistryRepository`.
No second X-Internal-Api-Key, arbitrary tenant parameter, new scope grant or source-write assertion. Return only bounded
current tenant status and verified service/purpose/token binding needed by the MDM consumer; unknown/inactive/unavailable
is fail-closed even though an older unrelated client comments on fail-open behavior.
Bind each response to the exact request/attempt nonce, validated tenant, service-sub, token `jti` and purpose, with
explicit observation freshness and expiry validation. The MDM client must reject cached, expired, cross-attempt,
cross-token or mismatched responses; corresponding negative contract tests are required. This response is an
authenticated point-in-time observation, never a source-write permit or atomic revocation guarantee.
This narrow new read contract itself requires explicit cross-owner approval; its exact extra paths are listed after the
original inventory. It is needed for Draft completion, which has no workflow evidence request to piggyback on.

Current source scope = integrity-verified finite admission LE upper-bound intersected with current active local Legal
Entities and current Global Product policy, using existing `ProductLegalEntityScopeEvaluator` unchanged.
No caller-supplied set, GroupWide substitute, human-FU21 impersonation or new Platform LE-grant collection.
Human foreground commands still use FU21. Missing admission bound cannot silently pass Enforced after rollout changes.
Keep 200 LE / 100-period / 1 MB applicable persisted-document bounds, including technical evidence.
The existing write-fence coordinator currently depends on HTTP human actor context: propose a narrow verified service-
identity entry for one FG operation, not a fake HttpContext or global actor replacement. Reacquire local lease and
revalidate rollout/operation/policy at physical CAS; do not retain a tenant writer lease during approval waits.

**DraftCancelFinalize is a distinct local-completion branch, not a WorkCenter terminal decision.** The human has already
durably admitted the irreversible cancellation; the finalizer only confirms its exact receipt and safely completes the
matching state/admission. It needs current own-tenant status, current local policy/activation/fence, immutable cancellation
proof and verified G4 receipt, but no fabricated workflow fields or workflow-purpose grant.
Recommend fresh existing audit-service identity as its separate current executor gate. Current audit provider may
coalesce a concurrently acquired token because its rejected-token snapshot precedes the semaphore; add a narrowly named
fresh-acquisition operation on its existing interface/implementation, preserving every existing G4 caller's semantics.
No shared-cache rewrite, new credential/audience or stale receipt-as-current-authority shortcut.

Actual background actor remains the validated service-sub GUID in the proposed
`service:Diten.MDM:<serviceClientIdentityGuid>` format (54 characters, within ActorId 160); maker/checker remain immutable
provenance. SnapshotReference obeys `^[A-Za-z0-9._:/-]{1,256}$`. Bind operation, proof, current observations and actual
executor into `FinishedGoodLifecycleExecutionAuthorizationEvidence`, preserving first-application fingerprint through
replay/compaction. No FU01 envelope/header expansion. Service identity cannot initiate cancel/retire, approve tasks or
use the local finalizer for another profile.

#### 21.4.2 Revocation behavior — proposed business/security policy

Maker authority belongs to admission time; checker authority belongs to the authenticated decision time; service
identity, tenant and local source conditions belong to the execution attempt. Recommended: later maker/checker departure
does not retroactively invalidate a valid committed business decision. Otherwise ordinary offboarding could strand an
approved record indefinitely. This is a policy for user approval, not a claim that current code implements it.
A specifically revoked/cancelled operation or invalid decision proof still blocks; no maker re-login or retained JWT.

| Situation | Proposed outcome / authority distinction |
|---|---|
| Maker disabled or permission removed after valid admission | Do not impersonate or re-authorize the former maker; valid admitted operation may complete under current service/source checks. Invalid/revoked admission does not pass |
| Checker disabled after valid immutable decision | Preserve decision-time authority/provenance; do not erase the decision solely due to later offboarding |
| Service client/grant revoked before fresh issuance | Auth rejects; defer without source mutation. Restore requires a fresh attempt, not replay of cached execution observations |
| Tenant suspended/deactivated/missing | Named-service context read denies; retained evidence is not authority to ignore suspension |
| LE scope/policy changes | Re-evaluate current policy/active LE against admission upper-bound; empty/missing Enforced result denies. Narrowing may defer; broadening cannot enlarge admission |
| Terminal decision exists but source CAS not applied | Resume same operation with fresh executor/tenant observations and current local fence; no new decision, duplicate audit or version increment |
| Auth/Platform authority observation unavailable | Defer/quarantine with evidence retained; no cached-success fallback or fabricated receipt |
| Revocation after observation and before CAS | Cross-service race remains explicit. Local source lease/operation/policy changes are fenced at CAS; a fresh remote read cannot atomically serialize remote Auth revocation |
| Prior attempt timed out / Mongo result unknown | Read back original operation/outcome before retry; never turn timeout into a new identity or reuse old attempt observations |

O2 has **no central execution permit**. Let L be authority-store visibility lag and W the accepted fresh-observation-to-
physical-CAS interval. The potential revocation exposure is at least L + W, plus any explicitly relied-on clock uncertainty;
L is not measured/bounded by the inspected code. The 300-second JWT lifetime and 30-second validation skew are not W.
No proven numeric global upper bound is claimed. Recommend an owner-approved bounded-observation policy without global
atomic revocation, conditioned on an outer monotonic attempt deadline including semaphore wait, HTTP, retries and CAS,
and an actual database-enforced expiry/fence rather than only a client cancellation token.
An eventual retry starts new observations; it cannot reset the clock on old evidence. Unknown in-flight writes require
read-back. Before choosing a numeric W, measure/test these paths and specify datastore expiry/clock assumptions.
If immediate cross-service revocation is mandatory, O2 is insufficient; O3 still needs a real coordinated revocation
protocol, not merely extra epoch fields or fresh reads.

Test delayed/revoked issuance, stale/concurrent cache acquisition, tenant suspension, observation-to-CAS pauses, local
lease/policy revocation, delayed Mongo acknowledgement, clock changes and retry boundaries. A short timeout used in a
unit test is not proof of a production global revocation bound. No operational measurement or test runs in this turn.

#### 21.4.3 Deferred central alternative and schema evidence

O3's prior new audience, live Auth authorization endpoint/epoch writers, Platform finite-LE execution-grant repository
and HTTP authority client are retained with `deferred-O3` markers below. They are not selected prerequisites for O2.
Current AccessGovernance manifest has five collections, fifteen declared indexes plus five implicit _id indexes = 20;
the approved budget is 5/20. A separately owned Platform execution-grant SoR with implicit _id plus unique tenant/client/
profile would require a proposed 6/22, but no requirement for that additional SoR has been established here.
Keep all existing indexes unchanged. O2 reads existing tenant registry and uses MDM's existing admitted/current scope,
so it requires neither collection nor budget growth. Any future independently administered background scope or central
permit remains a separate owner decision with exact grant/revocation lifecycle and cross-product regression scope.

Historical source also contains generic orphan-operation recovery (including
`services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Lifecycle/OrphanedOperationRecovery/Handlers/CommandHandlers/RecoverOrphanedProductIdentityWorkflowOperationHandler.cs`
at bb9ce94d). It is developed source/reference, not “never implemented”; it is not automatically ported as a new operator
module or evidence that current FG has safe unattended authority. No new general recovery product is selected.

### 21.5 Proposed audit mapping, cancellation and compaction invariants

These are **candidate names/values/central semantics, not allocation or approval**. Aggregate is exact `FinishedGood`
(enum value 5). Verified enum evidence: target has 65 members, maximum 71; final bb9ce94d has 71 members through 71;
d58368d0 has 35 through 35. No duplicate numeric values were found in those enum snapshots. Gaps 32–37 are not free:
FG historical 32–35 and source shared abandonment/supersession 36–37 must not be reused. Candidate 72–76 are absent
from the inspected target/source enum and relevant plan text; this bounded check is not a repository-wide reservation
or a guarantee about concurrent branches. Repeat collision/producer/map checks before exact approval and code-start.

| Event proposal | Ordinal status | Producer moment / meaning | Proposed exact central operation |
|---|---|---|---|
| `FinishedGoodDraftCreated` | Existing 9 | Existing FG draft creation | Preserve existing Create |
| `FinishedGoodIdentitySubmitted` | Historical 32, absent target | Durable submit/Pending binding, not approval | LifecycleTransition, subject to exact producer/map review |
| `FinishedGoodIdentityApproved` | Historical 33, absent target | Verified native approval applied through authorized source CAS | LifecycleTransition |
| `FinishedGoodIdentityRejected` | Historical 34, absent target | Verified native rejection applied through authorized source CAS | LifecycleTransition |
| `FinishedGoodIdentityRetired` | Historical 35, absent target | Only verified WorkCenter retirement approval, never direct-retire | Deactivate |
| `FinishedGoodDraftCancelled` | Candidate 72, UNALLOCATED | Immutable authorized Draft-cancellation decision + local intent; delivery/finalization may still be PendingAudit | LifecycleTransition |
| `FinishedGoodIdentityApprovalWithdrawn` | Candidate 73, UNALLOCATED | Verified native cancellation of own identity approval + source Draft CAS | LifecycleTransition |
| `FinishedGoodRetirementRequested` | Candidate 74, UNALLOCATED | Durable retirement request admitted with exact workflow binding | LifecycleTransition |
| `FinishedGoodRetirementRejected` | Candidate 75, UNALLOCATED | Verified retirement rejection; FG remains approved | LifecycleTransition |
| `FinishedGoodRetirementCancelled` | Candidate 76, UNALLOCATED | Verified own retirement-request withdrawal; FG remains approved | LifecycleTransition |

The exact producers, operation factories, central map and negative tests must agree; do not add mappings merely to
reach a count. Current/source central mapping contains only FG draft creation. No numeric passthrough, unknown ordinal,
case drift or cross-product acceptance. Authority grant/deny/apply evidence is technical auditable execution evidence,
not an invented extra ProductAuditOperation on every retry. Additional business events require their own exact proposal.

**Draft cancellation:** take current tenant/permission/scope and expectedVersion, reject any pending approval or conflicting
active operation, persist immutable cancellation identity/fingerprint/actor/reason and intent with FG still retained.
PendingAudit blocks all new submit/retirement/cancellation attempts except exact replay. Only verified durable G4
receipt plus current finalization authority, matching operation and fenced CAS can mark Finalized. The audit event
describes the irreversible accepted cancellation decision; receipt/finalization is its completion evidence, not a second
identical business event. A missing/unknown/conflicting receipt leaves parent retirement blocked.

Parent admission cleanup references the original `CreationCommandId` and fingerprint, not the new cancellation ID.
Reuse the existing durable-child/confirmed-reservation checks before completing only that admission. Never clear a
parent retirement lease, pull all admissions, burn/reset the confirmed code reservation or unlink another FG.
The narrowed GSKU blocker excludes only this finalized retained cancelled child; an active sibling still blocks.
Do not trust a disposition/Finalized flag alone: missing or mismatched receipt, cancellation operation, fingerprint or
binding must block/quarantine a forged, corrupt or legacy record rather than silently release parent eligibility.
Crash between cancellation decision, G4 delivery/compaction, finalization and matching admission cleanup replays with the
same identities. Original create-key replay must return the same cancelled identity/evidence or explicit terminal conflict,
never allocate a replacement code or present it as newly created Draft.

**Both withdrawals:** current exact action permission + own canonical requester + scope/expectedVersion must be checked
before any mutation. Platform cancellation validates tenant/client/task/object/instance/maker and native CAS but does
not know the FG permission. Require `TerminalAction = Cancel`, `TaskStatus = Cancelled`,
`InstanceStatus = Cancelled` and exact operation/profile binding. Identity withdrawal restores Draft only after proof;
retirement withdrawal preserves Approved and releases only the matching retirement binding. Preflight, 200 without
proof, accepted 202, timeout or unknown result is not source-state authority. Concurrent approval/cancel resolves through
native terminal concurrency; losing/ambiguous outcomes reconcile, never overwrite. Pending identity must first complete
withdrawal before a separate Draft-cancel action is allowed.

**Lost successful cancellation response is a separate mandatory shared dependency.** Current terminal-decision evidence
returns approve/reject only; cancel requires the delegated human token, and cancel-preflight status is not proof.
Propose read-only `POST /api/internal/v1/workflow/trusted-consumer/cancellation-evidence` under the existing exact
workflow service authority. It may retrieve authenticated persisted cancellation evidence but never invoke CancelAsync,
start a workflow or relax the original human-only cancellation requirement.
MDM must durably record an immutable cancellation attempt before the outbound call: original client/requester, instance,
task, object type/ID, maker, both pre-cancel expected versions, reason/comment, idempotency and canonical fingerprint.
The new read validates that exact committed Cancel log, coherent completed trusted start proof, monotonic terminal
history, task/instance Cancelled state, actor/reason/sequence and client/tenant/profile binding. Missing, partial,
ambiguous or conflicting proof fails closed; a fresh preflight or another cancel call cannot manufacture it.
Source finalization then separately requires the new machine execution authority and physical fence. Test native cancel
committed → response lost → MDM restart without maker JWT → read-only proof → one fenced source finalization, with no
duplicate cancel/audit/version. Exact conditional shared paths are in P0/P3; this behavior does not already exist.

**Compaction:** the old `ApplyLocalPendingAsync` counts embedded submit intents and can return
`LOCAL_PENDING_INCONSISTENT` after legitimate compaction, while repository replay accepts a receipt.
Require exactly one matching intent XOR one fully validated durable receipt, matching tenant, source, intent ID,
idempotency, evidence hash, status/version and immutable operation/workflow binding. Existing G4 receipt acknowledgement/
contract validation remains mandatory; mere receipt presence is not proof. Test actual local transition, durable delivery,
compaction, crash before operation checkpoint and restart replay. Missing/duplicate/foreign/drift evidence fails closed;
temporal tests or a fabricated acknowledgement are not end-to-end delivery proof.
Existing FG discovery and temporal/G4 repositories are reused; retain IsDeleted=false for cancelled evidence so discovery
and scope inventory still see the identity. No extra collection scan, direct Mongo transport or HMAC fallback.

### 21.6 UI, stable create retry and sorting proposal

Use current GoldenReferenceSlim / tenant shell and the existing generic WorkCenter UI as visual behavior references
only. One business form field is selected GSKU; technical fields are not counted. No shared layout/theme, WorkCenter,
personalization client, verifier, navigation or unrelated screen change is planned.

Current MVC generates a new Guid for each Create POST; JS sends GskuId and CSRF only, and the existing frontend test
asserts this behavior. Replace that assertion in the proposed UI slice, not treat it as proof of retry safety.
Use a server-generated opaque, Data Protection-protected form-attempt identity bound initially to tenant, canonical actor
and purpose. The form is opened before GSKU selection; the nonce alone does not bind that selection. The proposed contract
requires the first accepted submission to bind the attempt immutably to the selected GSKU/payload, with changed-payload
reuse rejected. **Pre-insert binding is an unresolved proof gate, not established existing behavior:** the current create
handler checks changed GSKU against a persisted FG replay, while pre-insert admission is per GSKU and reservation
reserve/consume receives commandId without GskuId. A crash after admission or reservation consumption but before FG
persistence must be tested with the same attempt and a different GSKU; post-insert mismatch rejection alone is insufficient.
This is not a proven runtime defect or approval to design a binder. Inspect the exact handler reference below and prove
the full durable chain first. If closure requires a handler delta, durable binder/store or additional paths, record the
exact conditional amendment and obtain owner/code-start approval before implementation; the existing UI-only path set
must not be assumed sufficient.
The attempt persists across duplicate clicks, network timeout, 202 and a lost 201 response; same logical
attempt forwards the same backend idempotency key. A genuinely new form intent receives a different key even for the
same GSKU. Do not derive the key solely from GskuId. Reject tamper, cross-actor/tenant reuse and payload drift.
Do not silently rotate an expired/unresolved attempt into a new create: preserve a pending/reconciliation UX until its
outcome can be resolved. Token lifetime and multi-tab behavior need explicit UI/security contract tests; no durable
browser bearer/credential storage.

Current server-side DataTable persists order/Save View but backend fixes CanonicalCode. **Selected: fixed CanonicalCode
ordering with deterministic Id tie-breaker.** Disable unsupported column ordering and sanitize old Save View sort state
to this baseline while preserving filters, visibility and presentation column order. Do not delete personalization
records or add arbitrary server sort fields. Lifecycle/cancellation-disposition filters apply to both Preparation and
Enforced queries before pagination; filter restore/clear must be visible and consistent.
Finalized Cancelled is a distinct FG display/read-model disposition, not Retired or technical deletion. PendingAudit
shows pending completion and exposes no new mutation; retained code, parent, immutable evidence and version remain readable.
Proposed exact read-model fields are `CancellationDisposition` (`None`, `PendingAudit`, `Cancelled`) and derived
`EffectiveLifecycleStatus`; PendingAudit/Cancelled take precedence over the stored shared Draft value for actions,
display and filters. A plain Draft filter must not select these records or expose submit. These names remain proposed
DTO/query contracts, implemented in the already listed models/query/validator/repository paths, not a global enum change.


| User capability | Intended UX without implicit grants |
|---|---|
| Read only | List/detail/filter/Save View; do not initialize the create selector |
| Create only | Selector and create remain valid; no automatic read/list/detail request. Show only the authorized create response/receipt and pending status, not fake detail success |
| Read + create | Existing create plus real list/detail refresh after authorized response; pending is distinguished from confirmed read-back |
| Lifecycle permission missing | No enabled forbidden action; server still enforces every action independently |
| Scope/lifecycle/version changed | Reload authorized read-back, preserve unknown attempt identity, show localized conflict/pending; no success toast based only on accepted 202 |
| Checker / maker offline | Native WorkCenter decision, background authorized source apply, then actual read-only status/version refresh; no maker reconcile POST or standalone FG approve/reject endpoint |

Current seven FG-specific locale files each contain 39 keys by static XML inspection, not a test run.
Reuse existing `CanonicalCode`, `GskuCanonicalCode`, `LifecycleStatus`, `Version`, `ErrorForbidden`,
`ErrorConflict`, `CreateSuccessWithCode`, `CreatePendingWithCode`, `CreatePending`; pending copy must not promise
a list refresh to create-only users.
Candidate new keys and English intent, subject to the chosen contract:

| Proposed key | English meaning |
|---|---|
| `SubmitIdentity`, `SubmitConfirmation` | Submit identity / confirm identity submission |
| `WithdrawIdentityApproval`, `WithdrawConfirmation` | Withdraw own pending identity request / confirm withdrawal |
| `WithdrawRetirementRequest`, `RetirementWithdrawalConfirmation` | Withdraw own undecided retirement request / confirm |
| `CancelDraft`, `CancelDraftConfirmation` | Cancel eligible Draft identity, not delete |
| `DraftCancellationPending`, `LifecycleCancelled` | Audited cancellation pending completion / finalized retained cancellation |
| `RequestRetirement`, `RetirementRequestConfirmation` | Request retirement / confirm retirement request |
| `RetirementReasonLabel`, `RetirementReasonRequired`, `RetirementReasonTooLong` | Reason and validation; use only if owner selects the exact reason contract |
| `LifecyclePending`, `ReadBackPending` | Request accepted but source outcome not confirmed |
| `StateChanged`, `ReconciliationRequired` | Version/state changed or same-attempt continuation required |
| `ReadPermissionRequired` | Read capability is unavailable; no broken automatic list request |
| `ErrorTimeout`, `ErrorServiceUnavailable` | Unknown outcome / service unavailable, without treating retry as a new intent |

Add only keys actually consumed and all seven translations, preferring existing shared keys when semantically identical.
No SharedResource write is currently required. JS syntax, parity and executable MVC/DOM error-state behavior must all
be tested. A regex/source assertion alone does not prove browser behavior.
Historical Section 19 foundation verifier 76 pass / 16 fail is historical, not a new run and not blanket lifecycle
variance. Evaluate every future verifier failure against current prohibited actions and any explicit variance approval;
do not add edit/delete/import/bulk behavior to appease the verifier. The current personalization browser request is
same-origin MVC, which forwards authenticated tenant/bearer server-side: a verifier expecting browser-created tenant
headers is not proof of broken runtime forwarding. Do not change the shared client or verifier to hide that mismatch.

### 21.7 Dependency-ordered proposed exact path inventory

All paths remain future proposals relative to the single target worktree; no runtime write is authorized here.
The original **239 unique candidate paths are preserved once** in the existing lists, reclassified:
A = target exists and will be reused unchanged; B = developed protected-source candidate ported with semantic adaptation;
C = narrow amendment to an existing target file; D = genuinely new proposed code/contract path.
`selected` means included in recommended O2 planning, not user-approved implementation; `deferred-O3` is the retained
larger central alternative, not a selected dependency. A reused path may have had an O3 amendment proposed previously:
that larger delta is deferred, not silently carried into O2.

| Unique file-path accounting | A reuse | B source port | C existing-file amendment | D new proposed file | Total |
|---|---:|---:|---:|---:|---:|
| Original 239, selected O2 | 46 | 23 | 84 | 59 | 212 |
| Original 239, deferred O3 | 0 | 0 | 0 | 27 | 27 |
| Additional exact O2 paths below | 0 | 0 | 2 | 10 | 12 |
| Selected O2 including additions | 46 | 23 | 86 | 69 | 224 |

Thus the proposed target write envelope is **178 paths** (23 source ports + 86 existing amendments + 69 new files),
with 46 unchanged references and 27 deferred new central files. This is a bounded proposal, not proof every optional test
split is indispensable or code-start approval. The 23 B files are physically absent in target but already developed in
the protected source; they are not 23 new designs. The 69 D files are genuinely new proposed paths, not all 239.
Counts include runtime and tests, not only production classes.

A new contract on an existing file remains **C-file**, not a new physical D-file. Examples explicitly selected here:
read-only cancellation evidence on existing workflow interfaces/models/controller/executor/coordinator; fresh audit
acquisition on the existing provider interface/implementation; verified service-actor entry in the scope fence;
FG cancellation/read-model and create-replay changes on existing entity/repository/handler.
Those new behaviors still require exact contract approval even though no new file is created.

#### Slice P-1 — FG-PREINSERT-PROOF v1.0: test-only authority and measured result (2026-09-10)

The explicit FG-PREINSERT-PROOF v1.0 user prompt authorizes only this evidence test, its DB-010 isolation adaptation,
the single FG guard-exception removal and this P-1 evidence record. It does not approve a runtime binder, any other
Section 21 implementation, lifecycle code-start, status promotion or Git mutation. Exact writable paths for this WP:

- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/FinishedGoodDraftFoundationMongoTests.cs`
- `tests/architecture/TenantArchitecture.ArchitectureTests/MongoTestDatabaseGuardTests.cs` — remove only the
  `FinishedGoodDraftFoundationMongoTests.cs` entry from `KnownPerRunDatabase` after eliminating its actual violation.
- `execution/domains/master-data-management/module-packs/MOD-0290-product-item-sku-master.md` — P-1 authority/result only.

Measured target: branch `codex/product-pv-delivery-integration-20260907`, HEAD
`dc6857d03cdafde5628affdcd0e5efc25bae3f7f`; index remained empty. The prior pack changes were preserved.
The unchanged unit counterpart was executed as a regression, not edited. Production handler/repository code and the
existing fixture were read-only; only the interruption decorator is synthetic, never a successful storage result.

**Executed persistence evidence.** Tests use the real create handler and GSKU/code-reservation/FG repositories.
Each of the six new cases independently arranges a fresh tenant and proves the selected interruption was reached
before any FG exists. Changed-parent is the FIRST retry, with no successful same-parent insertion in between:

| Crash point | Same GSKU + key first retry | Changed GSKU + key first retry | Other tenant + same key |
|---|---|---|---|
| A: parent admission persisted, before reservation | Passed: one logical identity/code; completed replay stable | **Failed expected rejection:** actual successful=true, status=201, FG count=1 under the changed GSKU | Passed: foreign parent rejected; own parent succeeds without altering original tenant partial state |
| B: reservation consumed, before FG insert | Passed: consumed identity/code reused for the original GSKU | **Failed expected rejection:** actual successful=true, status=201, FG count=1; original consumed identity/code reused under the changed GSKU | Passed: same isolation checks, including the original consumed reservation |

Exact failing test: `FinishedGoodDraftFoundationMongoTests.Pre_insert_crash_changed_parent_first_retry_must_reject_without_creating_finished_good`,
with `crashPoint: AfterAdmissionBeforeReservation` and `crashPoint: AfterConsumptionBeforeInsert`.
Both before/after snapshots show the original parent's admission surviving while the changed parent receives the FG.
Reservation, consumed identity, binding state and linked local audit intent snapshots are retained in the Mongo TRX;
the failure is not a fixture startup error or a post-insert drift test. The rejection expectation remains red and was
not weakened, skipped or rewritten to accept the defect.

| Validation run | Discovered / executed | Passed / failed / skipped | Evidence |
|---|---:|---:|---|
| MDM Application test project Release build | Build | 0 errors / 6 warnings | Execution-session build output for `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/Diten.MdmService.Application.Tests.csproj` |
| `FinishedGoodDraftFoundationMongoTests` | 17 / 17 | 15 / 2 / 0 | `.testoutput/fg-preinsert-proof-20260910/mongo/fg-mongo.trx` |
| Existing Mongo cases within that same run | 11 / 11 | 11 / 0 / 0 | Same Mongo TRX; subset, not an additional run |
| New pre-insert cases within that same run | 6 / 6 | 4 / 2 / 0 | Same Mongo TRX; subset, not an additional run |
| `FinishedGoodDraftFoundationUnitTests` | 32 / 32 | 32 / 0 / 0 | `.testoutput/fg-preinsert-proof-20260910/unit/fg-unit.trx` |
| `MongoTestDatabaseGuardTests` | 5 / 5 | 3 / 2 / 0 | `.testoutput/fg-preinsert-proof-20260910/guard/mongo-guard.trx` |

Do not add subset/overlapping counts or report the general suite green. The two current guard failures have exactly
the same messages and offender paths as `.testoutput/fu20-baseline-comparison-20260910/current-guards.trx`:

- `MongoTestDatabaseGuardTests.NoTestCreatesItsOwnDatabasePerRun` still names
  `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Audit/AuditOutboxTemporalStorageMigrationMongoTests.cs`,
  `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Audit/PpmAuditRetentionPolicySeedMongoTests.cs`,
  `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Audit/TrustedSourceAuditIntentMongoTests.cs` and
  `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Persistence/DisposableStandaloneMongo.cs`.
- `MongoTestDatabaseGuardTests.PerRunDatabaseExceptionListStaysHonest` still names
  `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/AuditIntentDeliveryMongoTests.cs`,
  `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/LegalEntityMongoRoundTripTests.cs` and
  `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ProductAbbreviationRegisterMongoTests.cs`.

The FG test now uses fixed `ProductLegalEntityScopeMongoCollection.DatabaseName`, no per-run GUID database or
application-Mongo fallback; its sole guard exception was removed. No new FG offender, other exception, guard regex
or rule change was introduced. The remaining seven listed paths are outside this WP and were not fixed here.

**Isolation and evidence limits.** The unchanged `AuditIntentTemporalMongoFixture` supplied test-owned MongoDB 7,
single-node replica-set primary on loopback port 63398, with fixed database `diten_mdm_product_scope_itest` and fresh
TenantIds. Every test's cleanup reported zero owned documents remaining in all six collections, including canonical
code counters; no per-test database drop. Host post-run checks found no owned temporary directory/process remaining;
the pre-existing application mongod PID 4716 remained untouched. No application-Mongo connection was used.
The handler uses the existing **Preparation scope test double**: this proves repository/crash/tenant behavior,
not Enforced authorization, live acceptance or Auth-to-FU01/G4 transport. Local audit snapshots are not central
durable-acceptance proof. All 432 pre-existing `.testoutput` files retained their SHA-256 values; only the three new
TRX artifacts above were added. Artifacts remain outside staging. Diff whitespace, conflict and secret-value review
found no blocking finding; no credential or token value is included in this record.

**Agent Verdict:** defect reproduced; the expected pre-insert parent-drift rejection **fails** in both windows.
This test-only reproduction is not Product PASS or Finished Good lifecycle completion. **Independent Verification
Verdict:** pending the separately assigned strict no-change auditor; this record does not pre-claim its acceptance.

Minimum runtime requirement for a separately approved follow-up: durably bind tenant + normalized create key to the
first GSKU/request fingerprint before the first parent-admission side effect, and reject changed-parent replay without
creating a new identity or mutating another parent. A consume-only or insert-only check cannot close window A, where
no reservation exists yet. The measured chain is `CreateFinishedGoodDraftHandler` -> per-parent `GskuRepository`
admission -> `CodeReservationRepository` replay -> `FinishedGoodRepository` insert; post-insert GSKU checks are too late.
This states the required invariant, not a selected binder design, new collection or authorized runtime path delta.
Keep the red proof and request the smallest exact runtime amendment before implementation; all other architecture
decisions and source-write gates in Section 21 remain unchanged.

#### Slice P-1R — FG-PREINSERT-REMEDIATION-R1 v1.0: narrow approved remediation authority (2026-09-11)

The explicit `FG-PREINSERT-REMEDIATION-R1 v1.0` user prompt supersedes the **test-only writing boundary** of P-1
only for the durable Finished Good create-attempt remediation below. It preserves P-1's two RED observations and
does not turn the red proof into a PASS. This is a bounded MDM backend/persistence amendment, not authorization for
the rest of Phase 1.5: no FG lifecycle code-start, WorkCenter, Auth, Platform, Workflow, Gateway, frontend, service
configuration, operational recovery, data repair/backfill, deployment, Git mutation or status promotion is granted.

The only writable runtime/test/governance paths for P-1R are exactly these nine; a required tenth path is a stop and
requires a new explicit amendment:

1. `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/FinishedGoodCreationAttempt.cs` — new FG-owned
   technical attempt entity only.
2. `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/FinishedGoodCreationAttemptResult.cs` — new
   explicit success/conflict/unavailable result only; null/default is never success.
3. `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/IFinishedGoodRepository.cs` — narrow
   attempt-binding contract; no new repository, DI service or program registration.
4. `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/FinishedGoodRepository.cs` — tenant-first
   attempt persistence and its production-owned index only; retain existing FG and code-reservation indexes unchanged.
5. `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Handlers/CommandHandlers/CreateFinishedGoodDraftHandler.cs`
   — take and verify the binding before the first parent-admission side effect while preserving current validation,
   scope, referenceability, completed-FG replay/tombstone and local-audit paths.
6. `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/FinishedGoodDraftFoundationUnitTests.cs`.
7. `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/FinishedGoodDraftFoundationMongoTests.cs`.
8. `tests/architecture/TenantArchitecture.ArchitectureTests/MongoTestDatabaseGuardTests.cs` — retain the prior FG
   exception removal only; P-1R adds no guard exception, matcher or rule change.
9. `execution/domains/master-data-management/module-packs/MOD-0290-product-item-sku-master.md` — this P-1R
   authority/evidence record in Section 21 only.

**Frozen persistence contract.** `mdm_finished_good_creation_attempts` is an FG-owned, tenant-scoped technical
idempotency collection, not a Finished Good aggregate, a reservation ledger or a shared idempotency facility. It
retains EntityBase technical tenant/identity fields and stores the existing trimmed, upper-cased creation key with
the first `GskuId` and the request fingerprint compatible with `GskuChildCreationAdmission`. Key and fingerprint
bounds remain those of the current create contract. No client payload field is added. A tenant plus normalized key
has a unique, non-reusable binding: archived/soft-deleted attempts do not free that key. The binding is inserted
atomically (or by an equivalent insert-only primitive); it is never updated, adopted, re-bound, TTL-purged or
automatically deleted. Duplicate/unknown write outcomes must be read back and exactly compared before proceeding;
absence of verified binding is fail-closed.

For a new, currently valid and authorized request, the handler obtains that binding before parent admission,
reservation, allocation, audit or FG insertion. Same tenant/key/parent/fingerprint retries continue the same logical
work. A same tenant/key with another GSKU or fingerprint returns the existing `IDEMPOTENCY_KEY_CONFLICT` / 409
convention and starts no new parent admission, reservation, code, audit or FG mutation. The binding is not authority:
each retry still performs current tenant, permission, scope and parent-referenceability checks; invalid or unauthorized
parents create no attempt. Actor policy is unchanged.

**Compatibility is deliberately fail-closed.** A persisted completed FG keeps its existing replay, tombstone and
no-code-reuse behavior. An attempt-less legacy partial admission or consumed reservation may not be silently adopted
or bound to a different parent. It must use the existing tenant-scoped reconciliation/conflict convention with explicit
evidence; P-1R neither completes, deletes, burns/resets nor bulk-cleans legacy state. Cross-version writer safety is
not claimed: no deployment/restart occurs in this slice, and simultaneous old/new writers remain an operational
compatibility decision outside it.

**Required evidence gates.** Retain P-1's two named RED-before cases and make their changed-GSKU FIRST retry green
only by actual rejection. Real handler/repository Mongo tests, with the unchanged test-owned
`AuditIntentTemporalMongoFixture`, must prove all of the following before any local preservation action is considered:

- both interruption windows reject changed-parent first retry and leave the original binding, both parent admissions,
  reservation/code/consumed identity, FG and local-audit links unchanged;
- same-parent retry and concurrent same-key/same-parent replay preserve one identity/code and complete the intended
  admission; a fresh concurrent same-key/different-parent race has exactly one durable binding winner and an
  side-effect-free rejected loser;
- a crash immediately after binding, uncertain binding-write result and cross-tenant same-key each fail closed or
  recover only after verified tenant-scoped read-back; the latter tenant never observes or mutates the first tenant;
- completed legacy replay, tombstone/no-code-reuse, binder-less admission-only and consumed-reservation partial
  cases preserve their existing safe behavior and never create a changed-parent binding;
- the real unique index is established and rejects a divergent persisted binding; invalid/unauthorized parents create
  neither attempt nor downstream state.

Run the MDM test-project Release build, `FinishedGoodDraftFoundationUnitTests`, full
`FinishedGoodDraftFoundationMongoTests`, `GskuChildAdmissionRetirementMongoTests`, relevant scope tests using their
existing safe fixture, and all five `MongoTestDatabaseGuardTests`. Report discovery and passed/failed/skipped per
non-overlapping run; distinguish the known non-FG baseline guard failures from new failures. Preparation doubles
remain repository/crash evidence only, never Enforced authorization, service-token/G4 transport or live acceptance.
The fixed `ProductLegalEntityScopeMongoCollection` database and tenant-owned cleanup, no localhost application-Mongo
fallback, no GUID database, no new shared harness and no skip/fake-success rules remain mandatory. Preserve prior TRX
bytes; new evidence must be separate and unstaged. Require staged-scope review, `git diff --check`, conflict-marker
and secret-value review, plus an independent strict read-only audit. Passing these gates proves this narrow create
invariant only; it does not complete Finished Good lifecycle or live acceptance.

#### Slice P0 — owner decisions and cross-domain catalog / scope / audit closure

Preserve the selected cardinality/sorting/flow directions; resolve exact audit allocation, role supersession,
retirement profile, machine-authority revocation contract, Draft cancellation representation and scope admission proof
before implementation. Related owner packs are references/dependencies, not edits authorized here:
`execution/domains/platform-shared-services/module-packs/MOD-0018-FU21-trusted-multi-legal-entity-scope-resolution.md`;
source-only `execution/domains/platform-shared-services/module-packs/MOD-0018-FU23-product-identity-lifecycle-permission-onboarding.md`;
`execution/domains/platform-shared-services/module-packs/MOD-0021-FU01-trusted-durable-source-audit-intent-ingestion.md`;
`execution/domains/platform-shared-services/module-packs/MOD-0033-FU02-service-identity-token-issuance-foundation.md`;
`execution/domains/master-data-management/module-packs/MOD-0290-FU03-product-legal-entity-scope-assignment.md`.
No ID is minted or owner pack automatically promoted by this list.

- C [selected] `services/Diten.AuthService/src/Diten.AuthService.Application/Common/Services/ProductIdentityLifecycleEntitlementGrantProfile.cs`
- C [selected] `services/Diten.AuthService/src/Diten.AuthService.Domain/Authorization/DefaultRolePermissionTemplate.cs`
- C [selected] `services/Diten.Platform/src/Diten.Platform.API/Security/TrustedLegalEntityScopeCredentialAuthenticator.cs`
- C [selected] `services/Diten.Platform/src/Diten.Platform.Application/Features/Audit/TrustedSourceAuditIntentOperationMap.cs`
- A [selected] `services/Diten.AuthService/src/Diten.AuthService.Application/Common/Services/EntitlementPermissionSyncService.cs`
- C [selected] `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/DefaultRolePermissionTemplateTests.cs`
- C [selected] `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/ProductIdentityLifecycleEntitlementGrantProfileTests.cs`
- C [selected] `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/EntitlementPermissionSyncServiceTests.cs`
- C [selected] `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/ProductIdentityLifecyclePermissionOnboardingMongoTests.cs`
- C [selected] `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/EntitlementSyncConsumerTests.cs`
- C [selected] `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/IntegrationEventInboxRepositoryMongoTests.cs`
- C [selected] `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Authorization/TrustedLegalEntityScopeCredentialAuthenticatorTests.cs`
- C [selected] `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Audit/TrustedSourceAuditIntentContractTests.cs`

P0A below retains the prior central candidate inventory with explicit disposition. O2 reuses current Auth identity/grant/
audience/schema files unchanged (A) and selects only the local authority contracts/evidence/tests plus required narrow
registration. New central Auth/Platform authority/grant/client/options files are deferred-O3. No 6/22 change is selected.

- A [selected] `services/Diten.AuthService/src/Diten.AuthService.Application/Features/ServiceIdentityTokens/ServiceIdentityTokenAudiencePolicy.cs`
- A [selected] `services/Diten.Platform/src/Diten.Platform.API/Security/TrustedServiceTokenValidationExtensions.cs`
- A [selected] `services/Diten.AuthService/src/Diten.AuthService.Domain/Entities/ServiceClientIdentity.cs`
- A [selected] `services/Diten.AuthService/src/Diten.AuthService.Domain/Entities/ServiceClientTenantGrant.cs`
- A [selected] `services/Diten.AuthService/src/Diten.AuthService.Domain/Repositories/IServiceClientIdentityRepository.cs`
- A [selected] `services/Diten.AuthService/src/Diten.AuthService.Domain/Repositories/IServiceClientTenantGrantRepository.cs`
- A [selected] `services/Diten.AuthService/src/Diten.AuthService.Persistence/Repositories/ServiceClientIdentityRepository.cs`
- A [selected] `services/Diten.AuthService/src/Diten.AuthService.Persistence/Repositories/ServiceClientTenantGrantRepository.cs`
- A [selected] `services/Diten.AuthService/src/Diten.AuthService.Api/Program.cs`
- A [selected] `services/Diten.Platform/src/Diten.Platform.Application/DependencyInjection.cs`
- A [selected] `services/Diten.Platform/src/Diten.Platform.Infrastructure/DependencyInjection.cs`
- C [selected] `services/Diten.Platform/src/Diten.Platform.API/Program.cs`
- A [selected] `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Schema/PlatformSchemaManifest.AccessGovernance.cs`
- A [selected] `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Schema/SchemaProfileBudget.cs`
- A [selected] `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Schema/PlatformSchemaManifestTests.cs`
- D [deferred-O3] `services/Diten.AuthService/src/Diten.AuthService.Api/Controllers/Internal/InternalServiceExecutionAuthorizationsController.cs`
- D [deferred-O3] `services/Diten.AuthService/src/Diten.AuthService.Api/Security/ServiceExecutionAuthorizationRequestParser.cs`
- D [deferred-O3] `services/Diten.AuthService/src/Diten.AuthService.Api/Security/ServiceExecutionAuthorizationRequestAuthenticator.cs`
- D [deferred-O3] `services/Diten.AuthService/src/Diten.AuthService.Application/Features/ServiceIdentityTokens/ServiceExecutionAuthorizationModels.cs`
- D [deferred-O3] `services/Diten.AuthService/src/Diten.AuthService.Application/Features/ServiceIdentityTokens/Queries/AuthorizeFinishedGoodExecutionQuery.cs`
- D [deferred-O3] `services/Diten.AuthService/src/Diten.AuthService.Application/Features/ServiceIdentityTokens/Handlers/QueryHandlers/AuthorizeFinishedGoodExecutionHandler.cs`
- D [deferred-O3] `services/Diten.AuthService/src/Diten.AuthService.Application/Features/ServiceIdentityTokens/Validators/AuthorizeFinishedGoodExecutionValidator.cs`
- D [deferred-O3] `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/ServiceIdentityTokens/FinishedGoodServiceExecutionAuthorizationContractTests.cs`
- D [deferred-O3] `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/ServiceIdentityTokens/FinishedGoodServiceExecutionAuthorizationMongoTests.cs`
- D [deferred-O3] `services/Diten.Platform/src/Diten.Platform.API/Controllers/Internal/FinishedGoodExecutionAuthorizationController.cs`
- D [deferred-O3] `services/Diten.Platform/src/Diten.Platform.API/Models/AccessGovernance/FinishedGoodExecutionAuthorizationRequestParser.cs`
- D [deferred-O3] `services/Diten.Platform/src/Diten.Platform.API/Security/FinishedGoodExecutionAuthorizationRequestExecutor.cs`
- D [deferred-O3] `services/Diten.Platform/src/Diten.Platform.Application/Features/AccessGovernance/FinishedGoodExecution/FinishedGoodExecutionAuthorizationModels.cs`
- D [deferred-O3] `services/Diten.Platform/src/Diten.Platform.Application/Features/AccessGovernance/FinishedGoodExecution/IFinishedGoodExecutionAuthorizationProvider.cs`
- D [deferred-O3] `services/Diten.Platform/src/Diten.Platform.Application/Features/AccessGovernance/FinishedGoodExecution/FinishedGoodExecutionAuthorizationProvider.cs`
- D [deferred-O3] `services/Diten.Platform/src/Diten.Platform.Application/Features/AccessGovernance/FinishedGoodExecution/IAuthFinishedGoodExecutionAuthorityClient.cs`
- D [deferred-O3] `services/Diten.Platform/src/Diten.Platform.Domain/Entities/AccessGovernance/FinishedGoodExecutionScopeGrant.cs`
- D [deferred-O3] `services/Diten.Platform/src/Diten.Platform.Domain/Repositories/IFinishedGoodExecutionScopeGrantRepository.cs`
- D [deferred-O3] `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Repositories/FinishedGoodExecutionScopeGrantRepository.cs`
- D [deferred-O3] `services/Diten.Platform/src/Diten.Platform.Infrastructure/Authorization/AuthFinishedGoodExecutionAuthorityClient.cs`
- D [deferred-O3] `services/Diten.Platform/src/Diten.Platform.Infrastructure/Authorization/AuthFinishedGoodExecutionAuthorityClientOptions.cs`
- D [deferred-O3] `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Authorization/FinishedGoodExecutionAuthorizationContractTests.cs`
- D [deferred-O3] `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Authorization/FinishedGoodExecutionAuthorizationMongoTests.cs`
- D [selected] `services/Diten.MdmService/src/Diten.MdmService.Application/Contracts/Workflow/IFinishedGoodLifecycleExecutionAuthority.cs`
- D [selected] `services/Diten.MdmService/src/Diten.MdmService.Application/Contracts/Workflow/FinishedGoodLifecycleExecutionAuthorityModels.cs`
- D [deferred-O3] `services/Diten.MdmService/src/Diten.MdmService.Infrastructure/Authorization/PlatformFinishedGoodLifecycleExecutionAuthorityClient.cs`
- D [deferred-O3] `services/Diten.MdmService/src/Diten.MdmService.Infrastructure/Authorization/AuthFinishedGoodLifecycleExecutionServiceIdentityProvider.cs`
- D [deferred-O3] `services/Diten.MdmService/src/Diten.MdmService.Infrastructure/Authorization/AuthFinishedGoodLifecycleExecutionServiceIdentityProviderOptions.cs`
- D [deferred-O3] `services/Diten.MdmService/src/Diten.MdmService.Infrastructure/Authorization/FinishedGoodLifecycleExecutionAuthorityClientOptions.cs`
- D [selected] `services/Diten.MdmService/src/Diten.MdmService.Domain/ValueObjects/FinishedGoodLifecycleExecutionAuthorizationEvidence.cs`
- D [selected] `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/FinishedGoodLifecycleExecutionAuthorityContractTests.cs`
- D [selected] `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/FinishedGoodLifecycleExecutionAuthorityMongoTests.cs`
- A [selected] `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/ServiceIdentityTokens/ServiceIdentityTokenHandlerTests.cs`
- A [selected] `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/ServiceIdentityTokens/ServiceIdentityTokenSecurityContractTests.cs`
- A [selected] `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/ServiceIdentityTokens/ServiceIdentityTokenMongoTests.cs`

O2 does not add grant epochs or change identity/grant writers. If owners later choose O3, every epoch writer and schema/
revocation contract must be separately enumerated and approved; this deferred list does not establish such coverage.

Additional exact O2 paths beyond the original 239 (no duplicate paths):

- D [selected-addition] `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Workflow/FinishedGoodLifecycleExecutionAuthority.cs`
- D [selected-addition] `services/Diten.Platform/src/Diten.Platform.API/Controllers/Internal/InternalFinishedGoodExecutionContextController.cs`
- D [selected-addition] `services/Diten.Platform/src/Diten.Platform.API/Security/FinishedGoodExecutionContextRequestExecutor.cs`
- D [selected-addition] `services/Diten.Platform/src/Diten.Platform.API/Models/FinishedGood/FinishedGoodExecutionContextModels.cs`
- D [selected-addition] `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Authorization/FinishedGoodExecutionContextContractTests.cs`
- D [selected-addition] `services/Diten.MdmService/src/Diten.MdmService.Infrastructure/Authorization/PlatformFinishedGoodExecutionContextClient.cs`
- D [selected-addition] `services/Diten.MdmService/src/Diten.MdmService.Application/Contracts/Authorization/IFinishedGoodExecutionContextClient.cs`
- D [selected-addition] `services/Diten.MdmService/src/Diten.MdmService.Application/Contracts/Authorization/FinishedGoodExecutionContextModels.cs`
- D [selected-addition] `services/Diten.MdmService/src/Diten.MdmService.Infrastructure/Authorization/FinishedGoodExecutionContextClientOptions.cs`
- D [selected-addition] `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/Authorization/FinishedGoodExecutionContextClientTests.cs`
- C [selected-addition] `services/Diten.MdmService/src/Diten.MdmService.Application/Contracts/Audit/ITrustedSourceAuditServiceIdentityProvider.cs`
- C [selected-addition] `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/Audit/AuthTrustedSourceAuditServiceIdentityProviderTests.cs`

The new local implementation is not an old source port. The new Platform context endpoint is read-only own-tenant
status/service binding, not a grant SoR or permit. Existing Platform API Program and MDM Infrastructure DI rows are C
only for these bounded registrations; existing Auth API/Application/Platform Infrastructure baseline remains unchanged.
The audit provider implementation already occurs in the 239 and is C for the fresh method; the interface/test additions
above do not change existing G4 GetAsync/coalescing semantics.

#### Slice P1 — FG identity domain, storage and workflow admission

Prerequisite: P0 contracts and the conditional fence rule approved. Preserve current create/read and parent admission.
Source files require current serializer, temporal, audit and workflow reconciliation; exclude the old direct-retire command,
handler and validator. DI is a narrow addition, never an old Program.cs or repository overwrite.

- C [selected] `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/FinishedGood.cs`
- C [selected] `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/IFinishedGoodRepository.cs`
- C [selected] `services/Diten.MdmService/src/Diten.MdmService.Domain/Enums/ProductAuditOperation.cs`
- C [selected] `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/FinishedGoodRepository.cs`
- C [selected] `services/Diten.MdmService/src/Diten.MdmService.Persistence/DependencyInjection.cs`
- C [selected] `services/Diten.MdmService/src/Diten.MdmService.Api/Controllers/FinishedGoodsController.cs`
- C [selected] `services/Diten.MdmService/src/Diten.MdmService.Api/ModuleRegistration/ProductItemSkuMasterManifestProvider.cs`
- C [selected] `services/Diten.MdmService/src/Diten.MdmService.Api/Program.cs`
- C [selected] `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/ProductItemSkuMasterModels.cs`
- C [selected] `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Handlers/QueryHandlers/GetFinishedGoodByIdHandler.cs`
- C [selected] `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Handlers/QueryHandlers/GetFinishedGoodsHandler.cs`
- C [selected] `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Queries/GetFinishedGoodsQuery.cs`
- C [selected] `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Validators/GetFinishedGoodsValidator.cs`
- B [selected] `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/FinishedGoodIdentityWorkflowOperation.cs`
- B [selected] `services/Diten.MdmService/src/Diten.MdmService.Domain/Enums/FinishedGoodIdentityWorkflowCheckpoint.cs`
- B [selected] `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/FinishedGoodIdentityWorkflowOperationResults.cs`
- B [selected] `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/IFinishedGoodIdentityWorkflowOperationRepository.cs`
- B [selected] `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/IFinishedGoodIdentityWorkflowTenantPartitionDiscovery.cs`
- B [selected] `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/FinishedGoodIdentityWorkflowOperationRepository.cs`
- B [selected] `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/FinishedGoodIdentityWorkflowTenantPartitionDiscoveryRepository.cs`
- B [selected] `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Lifecycle/FinishedGoodIdentityLifecycleModels.cs`
- B [selected] `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Lifecycle/FinishedGoodIdentityLifecycleAuditIntentFactory.cs`
- B [selected] `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Workflow/FinishedGoodIdentityWorkflowProcessor.cs`
- B [selected] `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Workflow/FinishedGoodIdentityWorkflowStartRequestFactory.cs`
- B [selected] `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Workflow/Commands/StartFinishedGoodIdentityWorkflowCommand.cs`
- B [selected] `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Workflow/Handlers/CommandHandlers/StartFinishedGoodIdentityWorkflowHandler.cs`
- B [selected] `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Workflow/Validators/StartFinishedGoodIdentityWorkflowValidator.cs`
- B [selected] `services/Diten.MdmService/src/Diten.MdmService.Api/Configuration/FinishedGoodIdentityWorkflowOptions.cs`
- B [selected] `services/Diten.MdmService/src/Diten.MdmService.Api/Configuration/FinishedGoodIdentityWorkflowWorkerOptions.cs`
- B [selected] `services/Diten.MdmService/src/Diten.MdmService.Api/Services/ProductItemSkuMaster/FinishedGoodIdentityWorkflowRecoveryCommandLine.cs`
- B [selected] `services/Diten.MdmService/src/Diten.MdmService.Api/Services/ProductItemSkuMaster/FinishedGoodIdentityWorkflowRecoveryRunner.cs`
- B [selected] `services/Diten.MdmService/src/Diten.MdmService.Api/Services/ProductItemSkuMaster/FinishedGoodIdentityWorkflowRecoveryWorker.cs`
- D [selected] `services/Diten.MdmService/src/Diten.MdmService.Domain/ValueObjects/FinishedGoodActiveLifecycleOperationBinding.cs`
- C [selected] `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/FinishedGoodApiContractTests.cs`
- C [selected] `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/FinishedGoodAuthorizationTests.cs`
- C [selected] `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/FinishedGoodDraftFoundationUnitTests.cs`
- C [selected] `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/FinishedGoodDraftFoundationMongoTests.cs`
- C [selected] `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ModuleRegistration/ProductItemSkuMasterManifestProviderTests.cs`
- B [selected] `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/FinishedGoodIdentityLifecycleMongoTests.cs`
- B [selected] `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/FinishedGoodIdentityWorkflowOperationMongoTests.cs`
- B [selected] `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/FinishedGoodIdentityWorkflowProcessorTests.cs`
- B [selected] `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/FinishedGoodIdentityWorkflowRecoveryRunnerTests.cs`
- D [selected] `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/FinishedGoodIdentityLifecycleContractTests.cs`
- D [selected] `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/FinishedGoodAvailableActionsTests.cs`
- D [selected] `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/FinishedGoodLifecycleAuditCompactionMongoTests.cs`
- D [selected] `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/FinishedGoodLifecycleRecoveryMongoTests.cs`

#### Slice P2 — Draft cancellation, both withdrawals, retirement request and machine-authorized completion

Prerequisite: P1, approved exact action/profile/audit contracts and machine execution authority. These are new
FG paths, not an inherited final implementation. Include Draft cancellation from human admission through audit-finalized
parent release, both own withdrawals and background terminal application; no maker-return continuation remains. Existing shared decision/recovery enums and LSKU repository discovery
pattern may be reused without expansion; no second generic WorkCenter bridge or automatic startup runner is planned.
Runner options remain default-disabled and invocation explicit; no configuration file is in the proposal.

- D [selected] `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/FinishedGoodRetirementRequestOperation.cs`
- D [selected] `services/Diten.MdmService/src/Diten.MdmService.Domain/Enums/FinishedGoodRetirementRequestCheckpoint.cs`
- D [selected] `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/IFinishedGoodRetirementRequestOperationRepository.cs`
- D [selected] `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/FinishedGoodRetirementRequestOperationResults.cs`
- D [selected] `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/FinishedGoodRetirementRequestOperationRepository.cs`
- D [selected] `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Lifecycle/FinishedGoodRetirementRequestModels.cs`
- D [selected] `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Lifecycle/FinishedGoodRetirementRequestAuditIntentFactory.cs`
- D [selected] `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Workflow/Commands/WithdrawFinishedGoodIdentityApprovalCommand.cs`
- D [selected] `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Workflow/Handlers/CommandHandlers/WithdrawFinishedGoodIdentityApprovalHandler.cs`
- D [selected] `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Workflow/Validators/WithdrawFinishedGoodIdentityApprovalValidator.cs`
- D [selected] `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Workflow/Commands/StartFinishedGoodRetirementRequestWorkflowCommand.cs`
- D [selected] `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Workflow/Handlers/CommandHandlers/StartFinishedGoodRetirementRequestWorkflowHandler.cs`
- D [selected] `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Workflow/Validators/StartFinishedGoodRetirementRequestWorkflowValidator.cs`
- D [selected] `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Workflow/FinishedGoodRetirementRequestWorkflowProcessor.cs`
- D [selected] `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Workflow/FinishedGoodRetirementRequestWorkflowStartRequestFactory.cs`
- D [selected] `services/Diten.MdmService/src/Diten.MdmService.Api/Configuration/FinishedGoodRetirementRequestWorkflowOptions.cs`
- D [selected] `services/Diten.MdmService/src/Diten.MdmService.Api/Configuration/FinishedGoodRetirementRequestWorkflowWorkerOptions.cs`
- D [selected] `services/Diten.MdmService/src/Diten.MdmService.Api/Services/ProductItemSkuMaster/FinishedGoodRetirementRequestRecoveryCommandLine.cs`
- D [selected] `services/Diten.MdmService/src/Diten.MdmService.Api/Services/ProductItemSkuMaster/FinishedGoodRetirementRequestRecoveryRunner.cs`
- D [selected] `services/Diten.MdmService/src/Diten.MdmService.Api/Services/ProductItemSkuMaster/FinishedGoodRetirementRequestRecoveryWorker.cs`
- C [selected] `services/Diten.MdmService/src/Diten.MdmService.Infrastructure/Workflow/PlatformProductIdentityWorkflowClient.cs`
- C [selected] `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/Workflow/PlatformProductIdentityWorkflowClientTests.cs`
- D [selected] `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/FinishedGoodIdentityApprovalWithdrawalUnitTests.cs`
- D [selected] `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/FinishedGoodRetirementRequestOperationMongoTests.cs`
- D [selected] `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/FinishedGoodRetirementRequestWorkflowProcessorTests.cs`
- D [selected] `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/FinishedGoodRetirementRequestRecoveryContractTests.cs`
- D [selected] `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/FinishedGoodLifecycleScopeContractTests.cs`
- D [selected] `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/FinishedGoodLifecycleEnforcedScopeContractTests.cs`
- D [selected] `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/FinishedGoodLifecycleActivationFenceMongoTests.cs`
- D [selected] `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/FinishedGoodLifecycleTransportContractTests.cs`

- C [selected] `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/GskuRepository.cs`
- C [selected] `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/GskuChildAdmissionRetirementMongoTests.cs`
- D [selected] `services/Diten.MdmService/src/Diten.MdmService.Domain/ValueObjects/FinishedGoodDraftCancellationEvidence.cs`
- D [selected] `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Lifecycle/FinishedGoodDraftCancellationModels.cs`
- D [selected] `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Lifecycle/FinishedGoodDraftCancellationAuditIntentFactory.cs`
- D [selected] `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Workflow/Commands/CancelFinishedGoodDraftCommand.cs`
- D [selected] `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Workflow/Handlers/CommandHandlers/CancelFinishedGoodDraftHandler.cs`
- D [selected] `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Workflow/Validators/CancelFinishedGoodDraftValidator.cs`
- D [selected] `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Workflow/FinishedGoodDraftCancellationProcessor.cs`
- D [selected] `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Workflow/Commands/WithdrawFinishedGoodRetirementRequestCommand.cs`
- D [selected] `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Workflow/Handlers/CommandHandlers/WithdrawFinishedGoodRetirementRequestHandler.cs`
- D [selected] `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Workflow/Validators/WithdrawFinishedGoodRetirementRequestValidator.cs`
- D [selected] `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/FinishedGoodDraftCancellationUnitTests.cs`
- D [selected] `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/FinishedGoodDraftCancellationMongoTests.cs`
- D [selected] `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/FinishedGoodDraftCancellationRecoveryMongoTests.cs`
- D [selected] `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/FinishedGoodRetirementRequestWithdrawalContractTests.cs`
- D [selected] `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/FinishedGoodRetirementRequestWithdrawalMongoTests.cs`

FG entity/repository/API/manifest/read-model and frontend paths are the existing P1/P4 rows, not omitted from the
cancellation slice. Proposed cancellation evidence is embedded in the same FG; existing proposed FG recovery discovery
must include bounded PendingAudit/finalization recovery without a new collection. Pending and finalized cancellation
must be checked by every FG mutation and create replay. Parent blocker changes only for verified finalized cancellation.

Conditional shared admission amendment: no implementation is authorized or claimed complete. First prove whether the
existing marker/repository transaction contract can supply short-lived admission and physical-write fencing; if not,
these exact owner-reviewed paths are required before proceeding. Do not hide the gap behind a local precheck.

- C [selected] `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductLegalEntityScopes/ProductLegalEntityScopeWriteFenceCoordinator.cs`
- C [selected] `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductLegalEntityScopes/ProductLegalEntityScopeMutationIdentity.cs`
- C [selected] `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/IProductLegalEntityScopeRolloutStateRepository.cs`
- C [selected] `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/ProductLegalEntityScopeRolloutStateRepository.cs`
- C [selected] `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ProductLegalEntityScopeWriteAdmissionContractTests.cs`
- C [selected] `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ProductLegalEntityScopeWriteAdmissionMongoTests.cs`

Cancellation/readiness reference paths (reuse, not wholesale amendment):

- A [selected] `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/CodeReservationRepository.cs`
- A [selected] `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/ICodeReservationRepository.cs`
- A [selected] `services/Diten.MdmService/src/Diten.MdmService.Domain/Enums/ProductIdentityLifecycleStatus.cs`
- A [selected] `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/ProductLegalEntityScopeOperationalReadinessRepository.cs`

#### Slice P3 — actual transport, authorization, durability and regression gates

Existing shared runtime references are reused without broader authority; Platform owner changes, if required by an
actual parser/policy mismatch, need a new exact approved delta rather than wildcard acceptance.

- A [selected] `services/Diten.MdmService/src/Diten.MdmService.Domain/ValueObjects/ProductIdentityWorkflowBinding.cs`
- C [selected] `services/Diten.MdmService/src/Diten.MdmService.Application/Contracts/Workflow/ProductIdentityWorkflowTransportModels.cs`
- C [selected] `services/Diten.MdmService/src/Diten.MdmService.Application/Contracts/Workflow/IProductIdentityWorkflowClient.cs`
- A [selected] `services/Diten.MdmService/src/Diten.MdmService.Infrastructure/Workflow/AuthProductIdentityWorkflowServiceIdentityProvider.cs`
- A [selected] `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/AuditIntentTenantPartitionDiscoveryRepository.cs`
- A [selected] `services/Diten.MdmService/src/Diten.MdmService.Application/Contracts/IProductIdentityLifecycleActorContext.cs`
- A [selected] `services/Diten.MdmService/src/Diten.MdmService.Infrastructure/Security/ProductIdentityLifecycleActorContext.cs`
- A [selected] `services/Diten.MdmService/src/Diten.MdmService.Infrastructure/Workflow/HttpContextProductIdentityDelegatedTokenAccessor.cs`
- A [selected] `services/Diten.MdmService/src/Diten.MdmService.Infrastructure/Authorization/PlatformTrustedLegalEntityScopeProviderClient.cs`
- C [selected] `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Handlers/CommandHandlers/CreateFinishedGoodDraftHandler.cs`
- C [selected] `services/Diten.MdmService/src/Diten.MdmService.Infrastructure/Audit/AuthTrustedSourceAuditServiceIdentityProvider.cs`
- A [selected] `services/Diten.MdmService/src/Diten.MdmService.Infrastructure/Audit/PlatformTrustedSourceAuditIntentClient.cs`
- A [selected] `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Audit/AuditIntentDeliveryProcessor.cs`
- A [selected] `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/AuditIntentContract.cs`
- A [selected] `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/AuditIntentDeliveryRepository.cs`
- A [selected] `services/Diten.MdmService/src/Diten.MdmService.Api/Services/Audit/AuditIntentDeliveryWorker.cs`
- C [selected] `services/Diten.MdmService/src/Diten.MdmService.Infrastructure/DependencyInjection.cs`
- A [selected] `services/Diten.Platform/src/Diten.Platform.Application/Features/WorkAggregation/Providers/WorkflowApprovalWorkItemProvider.cs`
- A [selected] `services/Diten.Platform/src/Diten.Platform.Application/Features/WorkAggregation/Providers/WorkflowApprovalWorkItemActionDispatcher.cs`
- C [selected] `services/Diten.Platform/src/Diten.Platform.API/Models/Workflow/TrustedWorkflowConsumerRequestParser.cs`
- C [selected] `services/Diten.Platform/src/Diten.Platform.API/Security/TrustedWorkflowConsumerRequestExecutor.cs`
- A [selected] `services/Diten.Platform/src/Diten.Platform.API/Security/ConfiguredTrustedWorkflowStartAuthorizationPolicy.cs`
- C [selected] `services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/Services/TrustedWorkflowCancellationCoordinator.cs`
- A [selected] `services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/Handlers/QueryHandlers/GetTrustedWorkflowTerminalDecisionEvidenceHandler.cs`

Mandatory proposed read-only cancellation-evidence extension (never service permission to cancel); existing MDM client
and cancellation contract/Mongo/security test rows above/below also gain this exact test scope:

- C [selected] `services/Diten.Platform/src/Diten.Platform.API/Controllers/Internal/InternalTrustedWorkflowConsumerController.cs`
- C [selected] `services/Diten.Platform/src/Diten.Platform.API/Models/Workflow/TrustedWorkflowConsumerRequestModels.cs`
- C [selected] `services/Diten.Platform/src/Diten.Platform.API/Security/ITrustedWorkflowConsumerRequestExecutor.cs`
- C [selected] `services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/Services/ITrustedWorkflowCancellationCoordinator.cs`
- C [selected] `services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/WorkflowModels.cs`
- D [selected] `services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/Queries/GetTrustedWorkflowCancellationEvidenceQuery.cs`
- D [selected] `services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/Handlers/QueryHandlers/GetTrustedWorkflowCancellationEvidenceHandler.cs`
- D [selected] `services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/Validators/GetTrustedWorkflowCancellationEvidenceValidator.cs`
- D [selected] `services/Diten.MdmService/src/Diten.MdmService.Domain/ValueObjects/FinishedGoodWorkflowCancellationAttempt.cs`

Persist the proposed immutable attempt in the already listed FG identity/retirement operation entities and repositories
before remote cancellation; preserve original start evidence separately. No new cancellation collection or parser
interface is implied. Tenant-status authority uses existing local Platform query/registry, not a second shared-key call:

- A [selected] `services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Queries/GetTenantStatusQuery.cs`
- A [selected] `services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Handlers/GetTenantStatusQueryHandler.cs`
- A [selected] `services/Diten.Platform/src/Diten.Platform.Domain/Repositories/ITenantRegistryRepository.cs`
- A [selected] `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Repositories/TenantRegistryRepository.cs`
- C [selected] `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/Authorization/PlatformTrustedLegalEntityScopeProviderClientTests.cs`
- C [selected] `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/Authorization/TrustedLegalEntityScopeProviderContractTests.cs`
- C [selected] `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/Authorization/TrustedLegalEntityScopeDelegatedTokenForwardingTests.cs`
- C [selected] `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Security/TrustedWorkflowConsumerSecurityTests.cs`
- C [selected] `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Security/TrustedWorkflowStartAuthorizationPolicyTests.cs`
- C [selected] `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Workflow/TrustedWorkflowStartAuthorizationTests.cs`
- C [selected] `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Workflow/TrustedWorkflowCancellationContractTests.cs`
- C [selected] `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Workflow/TrustedWorkflowCancellationMongoTests.cs`
- C [selected] `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Workflow/TrustedWorkflowTerminalDecisionEvidenceMongoTests.cs`
- C [selected] `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Authorization/TrustedLegalEntityScopeRequestExecutorTests.cs`
- A [selected] `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/Audit/AuditIntentDeliveryWorkerMongoTests.cs`
- A [selected] `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/Audit/AuditIntentTemporalMigrationMongoTests.cs`
- A [selected] `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ProductLegalEntityScopeMongoCollection.cs`
- A [selected] `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Audit/AuditOutboxTemporalStorageCutoverMongoTests.cs`
- C [selected] `tests/architecture/TenantArchitecture.ArchitectureTests/MongoTestDatabaseGuardTests.cs`

The architecture-guard proposal is only removal of the FG legacy exception after the actual FG test violations are
removed and owner approval is granted; no relaxed guard or unrelated exception cleanup. Existing shared fixtures are
read/reuse references, not proposed helper rewrites. MDM has no tracked module schema manifest to copy from Platform.

The existing create handler listed C is a mandatory narrow cancellation-replay amendment: return the retained
PendingAudit/Cancelled outcome rather than fresh Draft/201 success or new allocation. This does not approve a
pre-insert durable binder; that separate proof/conditional-path gate in 21.6 remains open.

#### Slice P4 — FG-only MVC / UI / locale and Gateway proof

Prerequisite: stable backend contracts, selected fixed sorting and proven retry semantics. Existing views can host actions; no new
shared layout, WorkCenter, navigation or edit/correction partial is required. Keep backend and UI validation/commits
separate when runtime is later approved; do not claim a source-only frontend contains new withdrawal/retirement behavior.

- C [selected] `frontend/Diten.Web/Controllers/FinishedGoodsController.cs`
- C [selected] `frontend/Diten.Web/Models/FinishedGoods/FinishedGoodViewModels.cs`
- C [selected] `frontend/Diten.Web/Views/MasterDataManagement/FinishedGoods/Index.cshtml`
- C [selected] `frontend/Diten.Web/Views/MasterDataManagement/FinishedGoods/_CreateEditOffcanvas.cshtml`
- C [selected] `frontend/Diten.Web/Views/MasterDataManagement/FinishedGoods/_DetailsQuickView.cshtml`
- C [selected] `frontend/Diten.Web/Views/MasterDataManagement/FinishedGoods/_DataTable.cshtml`
- C [selected] `frontend/Diten.Web/Views/MasterDataManagement/FinishedGoods/_Filter.cshtml`
- C [selected] `frontend/Diten.Web/Views/MasterDataManagement/FinishedGoods/_IndexL10n.cshtml`
- C [selected] `frontend/Diten.Web/wwwroot/assets/js/MasterDataManagement/FinishedGoods/index.js`
- C [selected] `frontend/Diten.Web/wwwroot/assets/js/MasterDataManagement/FinishedGoods/index.l10n.js`
- C [selected] `frontend/Diten.Web/Resources/Views/MasterDataManagement/FinishedGoods/FinishedGoodsIndex.en.resx`
- C [selected] `frontend/Diten.Web/Resources/Views/MasterDataManagement/FinishedGoods/FinishedGoodsIndex.tr.resx`
- C [selected] `frontend/Diten.Web/Resources/Views/MasterDataManagement/FinishedGoods/FinishedGoodsIndex.fr.resx`
- C [selected] `frontend/Diten.Web/Resources/Views/MasterDataManagement/FinishedGoods/FinishedGoodsIndex.es.resx`
- C [selected] `frontend/Diten.Web/Resources/Views/MasterDataManagement/FinishedGoods/FinishedGoodsIndex.zh.resx`
- C [selected] `frontend/Diten.Web/Resources/Views/MasterDataManagement/FinishedGoods/FinishedGoodsIndex.ar.resx`
- C [selected] `frontend/Diten.Web/Resources/Views/MasterDataManagement/FinishedGoods/FinishedGoodsIndex.ru.resx`
- C [selected] `frontend/Diten.Web/tests/finished-good-draft-foundation.test.js`
- C [selected] `gateway/Diten.ApiGateway.Tests/OcelotConfigurationTests.cs`
- D [selected] `frontend/Diten.Web.Tests/Controllers/FinishedGoodsControllerTests.cs`
- D [selected] `frontend/Diten.Web/tests/finished-good-lifecycle.test.js`

### 21.8 Test acceptance matrix and evidence discipline

The following tests are **planned**, not executed in this task. Build success alone never closes a row.

| Gate | Required positive evidence | Required fail-closed / race evidence |
|---|---|---|
| Current create/read | Approved GSKU + approved revision, immutable code reservation, real allocation replay, existing read/selector behavior | Cross-tenant 404, inaccessible parent, changed parent state, denied create/read without fallback |
| Catalog and roles | Actual emitted MDM manifest reconciles through real Auth profile; exact proposed FG role delta after approval | Missing/extra/case-drift definitions; no Admin/Viewer mutation or automatic membership |
| Supersession | Module-source retire removed, request-retirement restored once, replay cardinality stable | Manual/other-source retained, revoked/new-token denied; pre-revocation JWT behavior explicitly bounded |
| Scope client/provider | Real MDM serialization → real FU21 parser/authenticator/executor/provider and exact pairs | Wrong resource/permission/case, wrong tenant/client, empty/suspended scope; no workflow/repository/audit mutation on denial |
| Submit | Operation admission, workflow start receipt, Pending CAS, one local audit intent, immutable start binding | Stale version, duplicate/drifting operation, concurrent submit, maker/service actor confusion, unknown start outcome |
| Checker read-back | Native approve/reject evidence → correct FG status/version and audit once | Same canonical maker checker, conflicting subject, mismatched object/task/instance/template/tenant, forged/nonterminal evidence |
| Both withdrawals | Same canonical requester, stored operation kind, exact corresponding action/scope, native cancellation proof | Wrong profile/requester, concurrent approval/cancel, expired lease/stale owner, preflight/202/timeout cannot reset; identity→Draft, retirement request→Approved |
| Retirement request | Approved-source admission → native decision → background-authorized rejection/retirement/read-back | No direct-retire path, scope loss, duplicate/conflicting request, terminal drift; own withdrawal competes safely with approval |
| Background authority | Human offline; fresh existing Auth issuance → named-service own-tenant context + real workflow evidence → local operation-bound continuation | Revoked/disabled identity/grant, wrong purpose/audience/tenant, unavailable observation, widened admission scope, activation/stale physical CAS; no maker return or token-as-permit assumption |
| Draft cancellation | Draft-only authorized immutable cancellation, PendingAudit then verified receipt/fenced Finalized, original code retained | Pending approval requires prior confirmed withdrawal; pending/foreign receipt or forged Finalized flag blocks parent, sibling still blocks, crash/replay and original create-key cannot recreate, only matching admission released |
| G4 / compaction | Real durable acceptance, verified receipt, actual compaction, crash-before-checkpoint and same-operation replay | Missing/duplicate/foreign/drift receipt, payload conflict, ambiguous network; no fake acknowledgement or second audit intent |
| Temporal / storage | Current serializer and temporal dual-write/read compatibility, repository indexes, CAS/replay | No global serializer change, whole-document replacement, automatic migration or bypass |
| Browser retry | Same opaque attempt across timeout/202/lost-201/repeated click, distinct new intent and multi-tab behavior | Tamper, changed GSKU, changed actor/tenant, expired unresolved identity; crash after admission/reservation consumption before FG insertion followed by same-attempt changed-GSKU retry must reject; no automatic duplicate create |
| UI action/read-back | Exact permissions + server lifecycle/version; actual response controls success; create-only and read-only UX | Forbidden action not merely hidden, stale state/409/timeout, no unsupported edit/delete/import |
| Filtering / Save View | Fixed CanonicalCode/Id, disabled unsupported ordering, lifecycle/cancellation filtering and restore/clear | Old saved sort sanitized without deleting preferences; both Preparation/Enforced filters before pagination, no arbitrary sort endpoint |
| Gateway / L10n | Existing root/nested GET/POST carry exact FG paths; seven locale consumed-key parity + JS syntax | No PUT/other route expansion; every verifier failure reviewed individually, no blanket variance |

Real workflow transport proof must feed the actual serialized MDM request into Platform's actual parser, named
service-token validation, exact authorization policy, request executor and native start/cancellation coordinator.
Wrong profile, audience, tenant, client/object/template binding and mixed-profile payload must fail.
Machine execution tests must accept the three distinct proof schemas and reject workflow fields on DraftCancelFinalize,
cancellation-receipt substitution for native terminal evidence, mixed profiles, over-limit/duplicate Legal Entity sets,
oversized technical evidence, malformed service-sub GUID, mixed actor and unsafe SnapshotReference. Assert exact
300-second service token validation separately from the not-yet-quantified observation-to-CAS deadline; validate real Auth issuance and named RSA processing, not just a fabricated ClaimsPrincipal.
Require local tenant Exists + Active; missing/deleted, Provisioning, Suspended, Deactivated or unavailable status denies.
Both withdrawals need real lost-response cancellation-evidence retrieval tests, including wrong key/hash/client/profile/
requester, approval-first outcome, incomplete log/task/instance, ambiguous matching logs and forbidden service-only Cancel.
Exercise actual Auth issuer/grant repository → named Platform JWT validation → local tenant query → real MDM context client. A fake HttpMessageHandler that returns 200 or a mocked success receipt proves client formatting/error handling only.
Likewise a mocked permissive scope provider does not prove FU21 interoperability or current mutation authorization.

Mongo plan uses existing test-owned mechanisms, **not** the application localhost:27017 fallback:

- Auth onboarding reuses the existing `ProductIdentityLifecyclePermissionOnboardingMongoTests.cs` mechanism:
  explicit `MONGO_TEST_URI` without fallback, fixed `diten_auth_permission_onboarding_itest` database, serialized
  `Auth permission onboarding Mongo` collection and fresh tenantA/tenantB with owned-document cleanup. Supply only
  the isolated test-owned fixture URI; no new harness, Platform schema copy or application database connection.
- Reuse `AuditIntentTemporalMongoFixture`, defined in
  `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/Audit/AuditIntentTemporalMigrationMongoTests.cs`,
  without changing that shared fixture. It owns the available mongod process, ephemeral loopback port and disposable data
  directory and initializes replica-set/primary topology. Use the fixed database contract from
  `ProductLegalEntityScopeMongoCollection`, fresh test TenantId and tenant-owned document cleanup/serialization.
- Platform native transaction tests reuse `AuditOutboxTemporalReplicaSetFixture` in
  `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Audit/AuditOutboxTemporalStorageCutoverMongoTests.cs`
  and actual WorkflowWorkCenter schema. Do not use Platform's hardcoded-27017 integration harness or copy its schema
  profile into MDM. The known Windows `DisposableMongoReplicaSet` helper debt is not solved by this plan.
- Adapt existing `FinishedGoodDraftFoundationMongoTests.cs` and the two source-candidate FG Mongo files before reuse:
  current foundation tests create GUID databases, drop the database and fall back to application port, with a legacy
  architecture exception. Preserve production index key order/options/uniqueness; no missing-index workaround.
  No new shared harness, arbitrary database cleanup, app fallback, fixture-start skip or fake/in-memory replacement.
- Run transaction-required replay/concurrency/atomicity/fence tests on the actual isolated replica set; test
  transaction-unavailable denial separately. Test data/process ownership is explicit; no real tenant/config/credential
  writes. Future test authorization is required before starting these fixtures.
- Existing FG foundation tests call handlers with permissive preparation doubles and their receipt fixtures are not
  real G4/FU01 acceptance. They remain useful unit/persistence regressions, not Enforced authorization or live delivery proof.

Historical artifact inspection found exact FG classes in
`.testoutput/gsku-amendment-preflight-20260908/mdm-full.trx`: 66 passed, 0 failed, 0 skipped
(17 API, 6 authorization, 11 Mongo, 32 unit). A substring name match would include four unrelated tests and incorrectly
report 70. Later `.testoutput/lsku-backend-regressions-20260909.trx` contains 8 passed FG tests
(1 API, 5 Mongo, 2 unit), overlapping the first run; do not add these counts.
No target FG lifecycle TRX was found; there is no executed evidence for the newly proposed Draft cancellation or machine authority. Source CAS/lease tests and mocked processor crash tests do not establish the new
processor + G4 compaction crash sequence. Preserve the existing .testoutput evidence outside future commits.
The known baseline failures remain open:

- `UserLookupValidationContractTests.ResponseJsonDoesNotLeakTenantOrProfileAuthorizationOrStatusDetails`
- `UserLookupValidationContractTests.ResponseDtoContainsOnlyUserIdAndReferenceable`
- `MongoTestDatabaseGuardTests.NoTestCreatesItsOwnDatabasePerRun`
- `MongoTestDatabaseGuardTests.PerRunDatabaseExceptionListStaysHonest`

The unresolved concurrency-flakiness case is
`ProductItemSkuMasterMongoTests.Concurrent_first_gsku_commands_allocate_unique_parent_ordinals_and_soft_delete_never_reuses`.
These are prior comparison findings, not fresh executions or newly attributed FG regressions; this document does not
turn the general suite green or close flakiness because a later run passed.

Future validation must report test discovery, executed test bodies, passed/failed/skipped and topology separately by
run, disclose overlapping filters and distinguish prior artifacts from fresh runs. Planned gates: affected MDM/Auth/
Platform/frontend/Gateway Release builds; focused tests above; actual emitted-manifest reconciliation; current
Global Product/GSKU/LSKU/ABB, scope, human JWT/default scheme and audit regressions; both Mongo architecture guards;
seven locales, JS syntax and the real packaged-Python Golden verifier. No test is run merely to produce a plan.
Before any later commit: exact stage inventory, complete staged diff, secret-value review, conflict and whitespace
checks; never stage .testoutput, config, secrets or unrelated governance changes. Hook bypass is not permitted.

### 21.9 Separate operational / live acceptance gates

Code and isolated tests will not create operational readiness. Later explicit authorization is required for exact
tenant service-client/audience grants, human responsibility assignment, current catalog reconciliation, template/
object binding, trusted scope configuration, selected recovery policy and default-disabled worker invocation.
No credential values belong in this pack. Provisioning is not automatically granted by runtime code-start.

A separately approved Local Development browser exercise must prove actual same-origin create/retry, scope-filtered
selection, assigned WorkCenter maker/checker decisions, withdrawal race outcome, retirement decision, source read-back
and audit receipt under intended roles. No current pack statement proves that live path. Production/Staging,
operational migration/cutover and service restart remain prohibited here; live audit acceptance and temporal activation
must have their own safety gates. Navigation remains unchanged.

### 21.10 Final owner decisions before the first runtime prompt

The six architectural directions remain selected. The remaining decisions are narrowed, not hidden:

1. Approve O2's FG-local continuation contract plus named-service own-tenant read and separate read-only cancellation-
   evidence recovery. Do not approve the deferred new audience/central grant/6/22 option merely by approving FG.
2. Approve admission-time maker and decision-time checker authority after later offboarding, subject to current service,
   tenant, local scope/fence and explicitly revoked-operation denial.
3. Choose bounded observation rather than immediate global revocation, or require a separately designed coordinated
   protocol. No numeric maximum is yet proven; approve an operational W only after visibility/clock/physical-CAS tests.
4. Approve or reject scoped permission-based stewardship cancellation of another creator's Draft with mandatory reason.
   Both pending-process withdrawals remain own-requester only; no general operator module.
5. Approve/amend exact four new action keys, role delta (derived 20/7/11 → 22/7/12), five FU21 pairs (current 32 → proposed
   37) and safe module-owned retire supersession. These counts follow sets, never justify adding extra grants.
6. Approve/amend candidate audit names/semantics and unallocated 72–76 only after fresh collision/producer/map checks;
   preserve historical 9 and 32–35 and shared 36–37.
7. Approve exact cancellation/read-model and retirement reason/profile contracts, finite admitted scope capture, service
   actor/audit provenance and physical write-fence adaptation. Legacy operations without required proof fail closed.
8. Authorize only the first narrow pre-insert replay evidence slice P-1 before any binder redesign. A test result may
   reveal a smaller concrete amendment; static analysis has not executed that defect.
9. Runtime source/test edits, builds, fixture execution, commits and provisioning still need their own explicit grant.
   Current task writes only this section. Existing multiplicity stays; business rationale remains an open question,
   not permission to migrate or invent a discriminator.

No “all dependencies closed” or runtime-ready claim follows from this document. Deferred O3 schema/epoch work is not
part of the recommended first runtime prompt.

### 21.11 Review roles and present-task boundary

The repository definitions used are `.antigravity/agents/module-pack-author.md` with
`.antigravity/workflows/prepare-module-pack.md` for the sole pack writer;
`.antigravity/agents/orchestrator.md` for coordination; `.antigravity/agents/business-analyst.md` for domain/business
evidence; `.antigravity/agents/backend-architect.md`, `.antigravity/agents/security-agent.md`,
`.antigravity/agents/frontend-ui-ux.md` and `.antigravity/agents/testing-agent.md` for independent read-only
specialist reviews. Agent definitions were discovered from the repo, not invented.
Two specialist review workers were reused for backend/frontend and security/testing roles under the available
concurrency limit; they are not five independent model workers. The pack author is separate from both reviewers.

First-round findings incorporated: business evidence does not prove multiple-FG necessity; backend exposes absent
withdraw/retirement-request, strict mapping gaps, compaction replay defect and admission/recovery authority gap;
security exposes manifest/profile mismatch, source-safe revoke, JWT staleness and evidence-versus-authority separation;
frontend exposes per-POST create identity, unsupported sorting and create-only/read-only UX; testing exposes legacy
Mongo ownership, mocked evidence limits and overlapping historic counts.
The first proposal's independent second review was completed and its documentary corrections were retained.
This narrow decision review compares unchanged reuse, local extension and the deferred central alternative without
restarting the plan. Business/backend and security/testing read-only findings informed O2, the separate Draft finalizer,
scoped-steward orphan proposal, observed-revocation limits, path accounting and the first pre-insert proof test.
Independent second review of this narrow revision: security/testing read the full section and found no remaining
blocking documentary issue after the exact-purpose/scheme and attempt/token-bound response clarifications above.
Backend/frontend read the full section and identified the P-1 false-positive risk: changed-GSKU must be the first retry
in an independently arranged pre-insert crash case, not follow a successful same-parent insertion; this was corrected.
Its remaining path-count review was interrupted by usage limits and is not represented as completed. The orchestrator
independently verified 251 unique paths, retention of the original 239, and zero target path-classification mismatches.
These are documentary/static review results only, not runtime readiness, new approval or executed test evidence.

### 21.12 P0A — Finished Good lifecycle scope/audit foundation — approved bounded code-start (2026-09-11)

This user-approved P0A slice authorizes **only** the five exact human FU21 scope pairs and the strict FinishedGood
audit enum/map foundation recorded below. It is not a status promotion, full Finished Good lifecycle code-start,
or evidence that any lifecycle producer, handler, worker, manifest, UI, Gateway route, Auth grant/profile, or live
acceptance exists.

#### Owner decisions retained verbatim as implementation constraints

- A current, authorized and product/legal-entity-scoped steward may cancel another creator's Draft with a mandatory
  reason; physical deletion and code reuse remain forbidden.
- Identity and retirement-request withdrawal remains limited to the operation's own canonical requester; a terminal
  decision cannot be overwritten.
- Retirement is requested through WorkCenter; direct-retire UI/API authority is not introduced.
- A valid WorkCenter terminal decision may be applied in the source service without waiting for the maker to return,
  but only under a later narrow, explicit and audited background-mutation/recovery authority. Tenant, scope,
  revocation and fence checks are not bypassed.
- Later maker/checker separation does not itself void an already valid decision. Current service, tenant, scope,
  fence and explicit operation-cancellation checks remain mandatory. No global/instant atomic revocation guarantee
  or numeric implementation window is approved or evidenced.
- The first later UI slice uses fixed canonical-code ordering; it must not promise alternate sort behavior that UI or
  Save View cannot execute.
- Existing FG multiplicity under one GSKU is unchanged; its business rationale remains an open decision, not a
  verified design or a data-migration authority.
- No new central audience, grant collection or 6/22 schema budget is approved; the existing 5 collections / 20
  logical-index budget remains unchanged.

#### P0A exact runtime/test authorization and dependency boundary

The only P0A runtime/test paths are the two FU21 authenticator paths, the two FU01 audit-map paths and the MDM
`ProductAuditOperation` enum plus its Finished Good foundation unit test. The independent implementation agents
must retain strict case-sensitive matching, tenant/delegated-human/client/audience binding and fail-closed unknown
handling. The exact FU01 table records the target addition of nine mappings: historical FinishedGood ordinals
32–35 and newly allocated ordinals 72–76. The pre-existing `FinishedGoodDraftCreated` ordinal 9 `Create` mapping
remains separate, yielding ten FinishedGood mappings in total.

| Contract | Exact P0A delta | Explicitly not authorized by P0A |
|---|---|---|
| FU21 human scope | Five FinishedGood action pairs; acceptance is not a user grant | wildcard/prefix/read-create substitution, direct-retire or background-service pair |
| FU01 audit map | Ordinals 72–76 map only as stated in the FU01 amendment; existing 9 and historical 32–35 stay immutable | event producer, transport, delivery worker, new central audit schema or ordinal reuse of 36–37 |
| Lifecycle authority | Future source application remains gated by a narrow background-authority design and verified evidence | arbitrary service mutation, maker-token reuse, operation/config provisioning or startup activation |

#### P0A evidence checkpoint — historical execution evidence, not rerun in this checkpoint

The retained TRX evidence records Platform focused tests as **137 passed / 0 failed / 0 skipped**, Finished Good
unit tests as **35 / 0 / 0**, and non-Mongo regressions as **62 / 0 / 0**. These are historical P0A runs, not new
results from this documentation-only checkpoint. Real Mongo, the general suite and live acceptance were not run.

The first Platform focused run was **135 passed / 1 failed** out of 136 because the newly written full-37-union
assertion incorrectly rejected the pre-existing `mdm.finished-goods.read` pair. Its acceptance is part of the
preserved 32-pair contract. The final assertion instead verifies that this existing read pair remains accepted,
the five specified P0A pairs are exact, and direct-retire, wildcard and case-drift inputs are rejected. The
intermediate test-source diff is not retained as evidence here; no additional explanation or source-level proof is
claimed.

Required later evidence is pair acceptance and wrong module/key/case/direct-retire/wildcard rejection; strict map
acceptance and wrong aggregate/operation/case/numeric rejection; enum collision proof preserving 9, 32–35 and
leaving 36–37 unused; then scoped build/regression evidence. Historical and future test runs must be reported
separately. P0A readiness is limited to this foundation and does not make the broader §21 lifecycle ready-for-dev.

### 21.13 P1A — Finished Good identity-workflow operation storage — approved bounded code-start (2026-09-11)

This user-approved P1A amendment authorizes only the tenant-scoped storage, immutable admission-snapshot, CAS/lease,
replay and bounded recovery-discovery foundation for a future Finished Good identity workflow. It is a storage-only
runtime code-start: it does not approve all P1/P2, start a workflow, apply a terminal decision, mutate a
`FinishedGood` lifecycle state, activate a worker, or make Finished Good lifecycle ready-for-dev, complete, or live
accepted. P0A and P-1R evidence remains unchanged.

#### P1A exact runtime/test allow-list

No directory or wildcard authority is implied. A twelfth path is a stop condition and needs a new explicit amendment.
The eleven exact writable paths are:

1. `execution/domains/master-data-management/module-packs/MOD-0290-product-item-sku-master.md` — only this §21.13
   P1A authority and later evidence record.
2. `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/FinishedGoodIdentityWorkflowOperation.cs`
3. `services/Diten.MdmService/src/Diten.MdmService.Domain/Enums/FinishedGoodIdentityWorkflowCheckpoint.cs`
4. `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/FinishedGoodIdentityWorkflowOperationResults.cs`
5. `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/IFinishedGoodIdentityWorkflowOperationRepository.cs`
6. `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/IFinishedGoodIdentityWorkflowTenantPartitionDiscovery.cs`
7. `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/FinishedGoodIdentityWorkflowOperationRepository.cs`
8. `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/FinishedGoodIdentityWorkflowTenantPartitionDiscoveryRepository.cs`
9. `services/Diten.MdmService/src/Diten.MdmService.Domain/ValueObjects/FinishedGoodLifecycleAdmissionScopeSnapshot.cs`
10. `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/FinishedGoodIdentityLifecycleContractTests.cs`
11. `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/FinishedGoodIdentityWorkflowStorageMongoTests.cs`

The pre-existing `FinishedGoodCreationAttempt` R1 collection and its `FinishedGood`/`IFinishedGoodRepository`/
`FinishedGoodRepository` paths are expressly excluded. P1A does not modify Auth, Platform, audit enum/map, API,
manifest, DI, Program, workflow client/processor/handler/worker, scope coordinator/rollout repository, frontend,
Gateway, configuration, migration, guard exceptions, a shared test harness, or any business data.

#### P1A frozen storage contract

- `mdm_finished_good_identity_workflow_operations` is a tenant-owned technical operation collection. Its tenant is
  taken only from trusted server context. It is distinct from the FG creation-attempt collection and is neither a
  workflow approval nor authority to write a source Finished Good.
- The first accepted operation atomically race-binds its operation/start-idempotency identity, Finished Good, GSKU,
  Product Definition Revision, maker, expected Finished Good version, normalized request/fingerprint and immutable
  admission snapshot. Exact same logical facts replay; every changed fact conflicts. Tombstone and terminal records
  never release an identity or key for reuse.
- `FinishedGoodLifecycleAdmissionScopeSnapshot` is typed, versioned and immutable, never an unbounded JSON bag. It
  binds the tenant, source-product chain and first human-admission facts; it contains at most 200 distinct, non-empty
  Legal Entity IDs in canonical order plus the required policy/rollout references and integrity fingerprint. It stores
  neither JWT nor credential. A caller-supplied list is not verified human admission evidence.
- Missing, malformed or legacy snapshots do not manufacture `GroupWide`, wildcard or `Preparation` fallback under
  Enforced scope. P1A stores no scope capture/validation handler, and repository test input is not evidence of a
  verified human admission.
- Ambiguous insert/update outcomes are success only after exact tenant-scoped persisted read-back proves the immutable
  facts. Cancellation and programming exceptions propagate. No `ReplaceOne`, upsert, rebind, silent adoption or
  whole-document overwrite is allowed.
- Claim/advance is limited by tenant, operation, fingerprint, eligible checkpoint, lease owner, generation and expiry.
  A stale owner/generation, expired lease, terminal reopen or evidence drift is illegal. The physical Mongo update must
  check expiry at mutation time using the documented repository/server-time assumption; a caller-provided past
  `NowUtcTicks` or a cancellation token alone is insufficient proof of a valid lease. A lease is never retained while
  awaiting a workflow decision, and this local mechanism is not a global Auth revocation guarantee.
- Every insert, reserve, claim and advance measures the complete BSON document including technical fields against the
  absolute 1 MiB limit. A reserve-only check or a fixed remaining-byte assumption is insufficient.
- Temporal fields used for sort/range remain the current scalar representation; P1A adds no global
  `DateTimeOffset` serializer change or migration. Due-operation and tenant-partition discovery use bounded,
  deterministic cursors, not an in-memory whole-collection scan. Both discovery surfaces must share exactly the same
  eligibility semantics and never disclose another tenant's operation contents.
- `Completed`, manual-reconciliation, `AbandonedBeforeWorkflowStart` and `Superseded` are excluded from automatic
  discovery. Legacy `AwaitingMakerReplay` is not an active path in the new design; values are not renumbered or
  silently made actionable.

#### P1A acceptance and evidence gates

Implementation must use the existing test-owned Mongo replica-set fixture, fixed test database and tenant-owned
cleanup; it must not use application MongoDB at `localhost:27017`, a per-run database, a new harness, Platform schema
copy, or a Mongo guard exception. Before a later preservation action, run the MDM Release build and non-overlapping
focused contract/storage tests, reporting discovery/executed/passed/failed/skipped and topology per run without
claiming old PASS results as fresh evidence.

Required real-Mongo evidence is: unique index and same-key races; different Finished Good/GSKU/maker/scope-fingerprint
drift rejection; cross-tenant read/write/replay isolation; tombstone/terminal no-reuse; stale owner/generation,
takeover and expired physical write rejection (including a past caller timestamp); verified ambiguous-write read-back
with immutable evidence; scalar due-time ordering/range/bounded cursor; exclusion of every terminal/manual/
abandoned/superseded operation from both discovery surfaces; finite-snapshot max/duplicate/empty-ID/fingerprint
checks; complete-document BSON limits on reserve and update/claim paths; and tenant-owned cleanup. Existing FG create
R1, GSKU child-admission and Product Legal Entity Scope regressions must remain covered. Known baseline Mongo guard
failures are reported as baseline, never converted into a P1A PASS.

The repository-level checkpoint does not prove WorkCenter transport, terminal evidence, background source mutation,
activation-fence CAS, current tenant status, cancellation evidence, cross-service revocation observation, manifest or
human authorization. Those contracts, real admission capture and every handler/worker/API surface remain later,
separately approved slices.

### 21.14 P1A remediation R1 — Control Tower rework contract (2026-09-11)

Control Tower did not accept the initial P1A storage evidence as closure. The earlier `7/7` storage run predates the
five remediation scenarios below and is historical evidence only; it is not remediation proof. The complete P1A
working set remains uncommitted at the start of this remediation. This amendment authorizes rework only inside the
same eleven exact paths listed in §21.13; that list is unchanged, no directory or wildcard is authorized, and any
twelfth path is a fail-closed stop requiring a new explicit amendment. The pack frontmatter/status, R1/P0A records
and the broader lifecycle code-start boundary remain unchanged.

The test writer must preserve a RED-before artifact for each defect before the responsible runtime writer applies a
fix, then execute the same focused behavior as GREEN-after evidence. A test that never reaches the defective
predicate, weakens the expected result, delays only before calling the method, or substitutes a mock/in-memory store
does not satisfy this gate. The table below records required observations, not test results; every evidence field is
`PENDING` until a real command/TRX and inspected runtime diff exist.

| Control Tower defect | Required RED-before observation | Required GREEN-after contract | Guarantee boundary |
|---|---|---|---|
| Physical Mongo-evaluated lease expiry | Begin `AdvanceAsync` with a valid lease, pause after the operation has started but before the Mongo update is evaluated, allow the lease to expire, then show that the current update can still mutate checkpoint/version/business facts. A delay wholly before the call is invalid evidence. | The physical update predicate evaluates lease expiry against Mongo server time at update evaluation, while retaining tenant, operation, owner, generation, checkpoint and version fencing. The expired write is rejected and leaves checkpoint/version/business facts unchanged. Claim must use the same scalar-time precision and must never return success with a newly created already-expired lease. | Server-time means the Mongo command/update-filter evaluation boundary only. It is not a claim about commit/ack time, clock-global linearizability or atomic Auth revocation. No production test hook may be added. |
| Future retry claim eligibility | Persist otherwise claimable operations with `NextAttemptAtUtcTicksV1` in the future and show that the current claim filter can acquire one despite both discovery surfaces treating it as not due. | Tenant discovery, operation discovery and claim share one null/due/future rule: null or due may proceed; future must be rejected even when the lease is absent/expired; once due it may be claimed. Rejection changes no lease, generation or version. | This is local retry eligibility only; it does not activate a worker or promise scheduling latency. |
| `GroupWide` product policy versus finite human admission | Show that current snapshot validation rejects `Enforced + GroupWide` when a valid finite admitted Legal Entity list is present. Preserve a separate RED case proving empty/missing admission is unsafe. | `ScopeMode.GroupWide` remains the observed product-policy mode while `LegalEntityIds` remains the verified finite human-admission upper bound. `Enforced + GroupWide +` a canonical list of at most 200 distinct non-empty IDs is valid; `Enforced +` empty/missing is fail-closed. Scoped mode and tenant/product/actor bindings remain unchanged. | No wildcard, policy-derived expansion, `Preparation`/legacy inference or authorization is created. Later intersection with current policy may only narrow the admitted set. Capture/evaluator/background authority remains out of scope. |
| Deterministic snapshot content integrity | Mutate each bound fact while retaining the old syntactically valid 64-hex fingerprint, and show the current format-only check accepts at least one stale fingerprint; separately show any caller-owned list can be mutated after construction if that changes stored meaning. | A versioned, unambiguous canonical encoding deterministically derives the fingerprint from tenant, source identities, admission actor/command, policy/rollout references, observation facts and canonically ordered finite Legal Entity IDs. Construction takes a defensive copy; persisted read-back/replay/claim recomputes and rejects stale, malformed or legacy snapshots. | Content hashing detects drift; it is not an authorization decision, digital signature or cryptographic source-identity attestation. Field boundaries must not rely on ambiguous concatenation. |
| Actual/full BSON size ceiling | Demonstrate that the current small-document test does not exercise rejection at the 1 MiB boundary, then prove whether a test-owned oversized/unknown-field document can bypass reserve/claim/advance accounting or be silently lost. | Each relevant write preserves unknown BSON fields and proves the complete post-mutation persisted BSON is `<= 1 MiB` under the same atomic/CAS boundary as the write. If valid field limits make the boundary unreachable, calculate and document the maximum serialized valid input and test that maximum instead of weakening production limits. | No whole-document `ReplaceOne`, schema/migration or global serializer change. A typed reserialization that drops unknown fields, a fixed remaining-byte guess, or a pre-read size with an unfenced race is not proof. |

#### P1A remediation R1 evidence gates

The R1 run preserved six exact RED tests across the five Control Tower defects: `6` discovered, `6` executed,
`0` passed, `6` failed and `0` skipped. The original RED shell command text was not retained after execution-context
compaction, so it is deliberately not reconstructed here. The exact test identities, outcomes and immutable TRX
artifacts are the retained RED evidence:

| Defect / exact test | RED result and observed failure | RED TRX / topology |
|---|---|---|
| Physical expiry — `Advance_started_with_valid_lease_cannot_mutate_after_mongo_evaluates_expired_lease` | `0/1/0` passed/failed/skipped: the expired advance was accepted and changed checkpoint `1 -> 2` and version `1 -> 2`. | `.testoutput/fg-lifecycle-p1a-remediation-r1-red-20260911-01/lease-expiry-final/lease-expiry-red-final.trx`; test-owned single-node replica set, `127.0.0.1:55185`, fixed `diten_mdm_product_scope_itest`. |
| Future retry — `Retry_eligibility_null_due_and_future_is_identical_across_discovery_and_claim` | `0/1/0`: a future-due row was claimed and changed. | `.testoutput/fg-lifecycle-p1a-remediation-r1-red-20260911-01/retry-final/retry-red-final.trx`; test-owned single-node replica set, `127.0.0.1:55231`, fixed database. |
| GroupWide finite admission — `Enforced_groupwide_accepts_finite_human_admission_but_rejects_empty_admission` | `0/1/0`: construction rejected the finite `Enforced + GroupWide` admission that the approved contract permits. | `.testoutput/fg-lifecycle-p1a-remediation-r1-red-20260911-01/groupwide/groupwide-red.trx`; domain contract test, no Mongo topology. |
| Snapshot content integrity — `Admission_snapshot_fingerprint_binds_every_fact_and_exposed_scope_cannot_be_tampered` | `0/1/0`: caller-input defensive copying passed (`caller-copy=True`), but the snapshot-exposed collection was mutable (`exposed-copy=False`), and thirteen changed bound facts retained an accepted stale fingerprint; malformed and legacy values were rejected. | `.testoutput/fg-lifecycle-p1a-remediation-r1-red-20260911-01/fingerprint-02/fingerprint-red-02.trx`; domain contract test, no Mongo topology. |
| Persisted snapshot integrity — `Persisted_stale_malformed_and_legacy_admission_snapshots_are_rejected_without_mutation` | `0/1/0`: for each stale, malformed and legacy persisted snapshot, reads and claims were accepted (`readRejected=False`, `claimRejected=False`); replay was already conflict-rejected (`replayRejected=True`). | `.testoutput/fg-lifecycle-p1a-remediation-r1-red-20260911-01/persisted-fingerprint-final/persisted-fingerprint-red-final.trx`; test-owned single-node replica set, `127.0.0.1:55289`, fixed database. |
| Full BSON — `Full_bson_ceiling_is_atomic_for_claim_and_advance_and_preserves_unknown_fields` | `0/1/0`: claim/advance produced `1,048,685`/`1,048,671` byte documents, changed the row and lost the unknown field. The separately measured maximum valid reservation was `6,686` bytes. | `.testoutput/fg-lifecycle-p1a-remediation-r1-red-20260911-01/bson-final/bson-red-final.trx`; test-owned single-node replica set, `127.0.0.1:55330`, fixed database. |

The retained GREEN command provenance is below. `$wt` was the exact integration worktree, `$proj` was
`services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/Diten.MdmService.Application.Tests.csproj`,
and `$out` was the named evidence directory. `$set.Filter` was the explicit six-test OR filter for the six identities
above, `FullyQualifiedName~FinishedGoodIdentityLifecycleContractTests`,
`FullyQualifiedName~FinishedGoodIdentityWorkflowStorageMongoTests`, the respective
`FinishedGoodDraftFoundationUnitTests` / `FinishedGoodDraftFoundationMongoTests` class filters, the exact union of
`FirstGskuIdentityLifecycleUnitTests` plus the four Product Legal Entity Scope classes, or
`FullyQualifiedName~MongoTestDatabaseGuardTests`, as applicable:

```powershell
dotnet build (Join-Path $wt 'services\Diten.MdmService\Diten.MdmService.sln') -c Release --no-restore *> (Join-Path $out 'build.log')
dotnet test $proj -c Release --no-build --list-tests --filter $set.Filter *> (Join-Path $out 'discovery.log')
dotnet test $proj -c Release --no-build --filter $set.Filter --logger "trx;LogFileName=$($set.Name).trx" --results-directory $out *> (Join-Path $out 'run.log')
```

The first storage repeat used `--no-restore`; repeats two through five used `--no-build`. All were run after the
recorded Release build. Results below are separate, overlapping runs and must not be added into one aggregate PASS:

| Gate | Discovery / execution result | Evidence |
|---|---|---|
| Exact six RED predicates, final GREEN | `6` discovered, `6` executed, `6/0/0` passed/failed/skipped | `.testoutput/fg-lifecycle-p1a-remediation-r1-green-20260911-04/exact-six/exact-six-green-r4.trx` |
| MDM Release build | Build succeeded with `0` errors and `3` existing warnings | `.testoutput/fg-lifecycle-p1a-remediation-r1-green-20260911-05/mdm-release-build/build.log` |
| P1A contract | `6` discovered, `6` executed, `6/0/0` | `.testoutput/fg-lifecycle-p1a-remediation-r1-green-20260911-05/p1a-contract/p1a-contract.trx` |
| Full P1A storage repeat 1 | `11` discovered, `11` executed, `11/0/0`; `127.0.0.1:50753` | `.testoutput/fg-lifecycle-p1a-remediation-r1-green-20260911-05/storage-run-01/storage-run-01.trx` |
| Full P1A storage repeat 2 | `11` discovered, `11` executed, `11/0/0`; `127.0.0.1:50826` | `.testoutput/fg-lifecycle-p1a-remediation-r1-green-20260911-05/storage-run-02/storage-run-02.trx` |
| Full P1A storage repeat 3 | `11` discovered, `11` executed, `11/0/0`; `127.0.0.1:50902` | `.testoutput/fg-lifecycle-p1a-remediation-r1-green-20260911-05/storage-run-03/storage-run-03.trx` |
| Full P1A storage repeat 4 | `11` discovered, `11` executed, `11/0/0`; `127.0.0.1:50973` | `.testoutput/fg-lifecycle-p1a-remediation-r1-green-20260911-05/storage-run-04/storage-run-04.trx` |
| Full P1A storage repeat 5 | `11` discovered, `11` executed, `11/0/0`; `127.0.0.1:51044` | `.testoutput/fg-lifecycle-p1a-remediation-r1-green-20260911-05/storage-run-05/storage-run-05.trx` |
| FG create R1 unit regression | `35` discovered, `35` executed, `35/0/0` | `.testoutput/fg-lifecycle-p1a-remediation-r1-green-20260911-05/fg-r1-unit/fg-r1-unit.trx` |
| FG create R1 real-Mongo regression | `25` discovered, `25` executed, `25/0/0` | `.testoutput/fg-lifecycle-p1a-remediation-r1-green-20260911-05/fg-r1-mongo/fg-r1-mongo.trx` |
| GSKU child-admission plus Product Legal Entity Scope regressions | `31` discovered, `31` executed, `31/0/0` | `.testoutput/fg-lifecycle-p1a-remediation-r1-green-20260911-05/gsku-scope/gsku-scope.trx` |
| Mongo architecture guards | `5` discovered, `5` executed, `3/2/0`; `PerRunDatabaseExceptionListStaysHonest` and `NoTestCreatesItsOwnDatabasePerRun` remain the same two known baseline failure categories, so the general guard suite is not green | `.testoutput/fg-lifecycle-p1a-remediation-r1-green-20260911-05/architecture-mongo-guards/architecture-mongo-guards.trx` |

The final real-Mongo storage repetitions used independent test-owned single-node replica sets on dynamic loopback
ports, each reporting `primary=True`, the fixed `diten_mdm_product_scope_itest` database and tenant-owned cleanup;
none used the application service at `localhost:27017`. The exact-six final confirmation used the same topology on
`127.0.0.1:51250`.

Intermediate evidence remains material and is not superseded by the final GREEN runs. The first GREEN exact-six run
was `5/1/0`; its physical-expiry test still observed an accepted expired mutation. The testing handoff attributes
that iteration to using command-start `$$NOW`, but its TRX does not retain an intermediate source diff, so the
attribution is not presented as independently reconstructed proof. R2 was `4/2/0`: the physical-expiry test surfaced
the exact Mongo `WriteConflict` code `112`, while the BSON test reported `claimRejected=False` and
`advanceRejected=False` with both rows unchanged; this did not satisfy the approved GREEN contract, which requires
rejection. R4 reached exact-six `6/0/0`, but three distinct historical uninstrumented full-storage runs each reached
only `10/1/0`: `.testoutput/fg-lifecycle-p1a-remediation-r1-green-20260911-03/p1a-storage/p1a-storage-green.trx`
at `16:17` (SHA-256 `30bffeaf4d20d64accdcd5de0043fef91d1a07ed2f7127fb63b97a7e60bec256`, execution
`5e1a8109-a8ce-4622-9058-3d71d6ba9e6c`),
`.testoutput/fg-lifecycle-p1a-remediation-r1-green-20260911-03/p1a-storage-rerun/p1a-storage-green-rerun.trx`
at `16:20` (SHA-256 `2ed3354fb5eef1cbbf482831e6fa55948bad332f74c17afb44347db19c859bbd`, execution
`267c7456-43f8-4f21-8330-db1d374c6d40`), and
`.testoutput/fg-lifecycle-p1a-remediation-r1-green-20260911-04/p1a-storage/p1a-storage-green-r4.trx`
at `16:26` (SHA-256 `1b674ea44f604c0eae1ab771b2051c0ed8805b964f6aa00ff48d078278953ef0`, execution
`eb74baaf-a4c3-4fe0-b710-75824c8bd4b0`). In each,
`Lease_claim_is_single_writer_and_stale_or_expired_claim_cannot_physically_advance` observed an all-null concurrent
claim result. The first run may have used the invalid one-millisecond fixture, but neither the TRXs nor a retained
intermediate source diff establishes that source-state provenance, so the possibility is not treated as proof or used
to omit the run. Ten subsequent isolated executions of that test passed, and one instrumented full-storage run passed
`11/0/0`; neither diagnostic result erases the unexplained historical concurrency flakiness. The five final
uninstrumented `11/0/0` runs above are the retained repeatability evidence.

The inspected final runtime design uses transaction-local Mongo `$currentDate` evaluation and aborts an expired
mutation before commit; performs bounded retry for transient transaction failure including the explicitly observed
`WriteConflict` code `112`; and uses atomic `$mergeObjects`/`$bsonSize` projections so the full BSON document and
unknown fields remain inside the CAS boundary. This is bounded storage evidence only. It is not evidence of a global
clock guarantee, instantaneous Auth revocation, authorization, actual human admission capture, workflow start/
transport, terminal decision application, background authority, source lifecycle mutation, API/manifest/DI/Program
integration, UI/Gateway behavior, general-suite health, operational readiness, live acceptance or Finished Good
lifecycle completion.

**Agent Verdict:** `CONDITIONAL PASS` for the bounded P1A storage contract. The exact remediation predicates and five
final uninstrumented storage repetitions are GREEN, but the unexplained historical concurrency flakiness and the two
known baseline architecture-guard failures remain visible. This agent verdict is not Control Tower acceptance. The
earlier `7/7` remains historical pre-remediation evidence and is not R1 proof.

Execution must continue to use only the existing test-owned replica-set fixture, fixed database and tenant-owned
cleanup. Application MongoDB at `localhost:27017`, per-run databases, a new harness, a guard exception, broad
write-concern swallowing or evidence-free ambiguous success remain prohibited. Discovery/executed/passed/failed/
skipped counts and topology must be recorded per non-overlapping run. The two known architecture-guard baseline
failures remain visible and cannot be represented as a general-suite or unconditional P1A PASS. Real workflow,
handler, worker, API, manifest, DI/Program and live acceptance were not run or proven by this remediation evidence.

P1A remediation can establish only the bounded storage contract. It cannot establish actual human admission capture,
workflow start/transport, terminal decision application, background authority, source lifecycle mutation, API/
manifest/DI/Program integration, UI/Gateway behavior, operational readiness, live acceptance or Finished Good
lifecycle completion.

#### FG-P1A-CLAIM-DIAG-01 — bounded all-null claim diagnosis (2026-09-11)

This diagnostic work package was limited to the existing P1A storage test and this evidence record. Its exact
writable paths were
`services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/FinishedGoodIdentityWorkflowStorageMongoTests.cs`
and this §21.14 subsection; runtime repositories, contracts, fixtures, DI and configuration remained read-only.
Evidence was written only below `.testoutput/fg-p1a-claim-diag-01/run-20260911-01/`. The previously retained 566
evidence files were preserved; the diagnostic run added 80 files. No staging, commit, push, application Mongo access,
operational provisioning or service activation was part of this work package.

The three historical all-null failures retained in §21.14 do not carry the exact test-source hash, test/runtime DLL
hashes or lease input needed to reconstruct their executing state. Their root cause therefore remains **UNKNOWN**.
In particular, the current two-second lease cannot be projected backwards onto those runs, and the unproven
one-millisecond-fixture possibility is not promoted to an explanation. Later passing runs, including this diagnostic
work, do not close or erase that historical flakiness.

The unchanged pre-instrumentation test was freshly built and executed three times with a two-second lease. Each run
discovered and executed one test and reported `1/0/0` passed/failed/skipped on independent test-owned single-node
replica sets using the fixed `diten_mdm_product_scope_itest` database: `127.0.0.1:64070`, `127.0.0.1:64123` and
`127.0.0.1:64174`. The pre-instrumentation Release build succeeded with `0` errors and `8` warnings. The baseline
source SHA-256 was
`746362c1fb10860009bf77028cf30230b808680b6ed5a61e94bee28ef7801a25`; the test DLL SHA-256 was
`e2b346c7b9f07996a0f4cb7239aeca32f0c6a73f3d093601e3fd0ea2610d8b06`. The P1A runtime DLL SHA-256 remained
`f14e5360fb9c2dcd16cbc67b5a0787451313d30ca6a1010ff53d14e855e98396` throughout every diagnostic build and
experiment. The current two-second scenario is consequently **NOT REPRODUCED**, not proven fixed.

Test-only command-event and persisted-state diagnostics then measured the following bounded hypotheses. Diagnostic
identifiers are synthetic or redacted; full Mongo command bodies, credentials and tokens were not recorded.

| Hypothesis | Observation | Result | Evidence |
|---|---|---|---|
| Current two-second six-contender claim can reproduce all-null | One instrumented run discovered/executed one test and reported `1/0/0` on `127.0.0.1:59447`. One contender committed; five losing transactions surfaced Mongo `WriteConflict` code `112` with `TransientTransactionError` and were not converted into simultaneous owners. | **NOT REPRODUCED**. Contention and bounded retry are observed in the current code, but do not establish the cause of the historical runs. | `experiment-01-instrumented-six-contender/` |
| Long lease can create two active owners in the same generation | The first run reported `0/1/0`, but the failure was a test-only `BsonInt32.AsInt64` `InvalidCastException`, not a storage assertion or runtime failure. After correcting only that diagnostic assertion, the rerun reported `1/0/0` on `127.0.0.1:59662`: exactly one generation-1 owner and one committed claim state. | Safety evidence: no double active owner was observed. The initial failure is retained and is not classified as a runtime defect. | `experiment-02-single-writer-long-lease/`; `experiment-02b-single-writer-long-lease/` |
| Expired/blocked mutation can poison ownership or prevent recovery | The controlled expiry run reported `1/0/0` on `127.0.0.1:59709`. The expired stale owner could not advance or change checkpoint/version/business facts; after contention cleared, a fresh owner took generation `+1`, advanced, and the stale owner remained rejected. | Fail-closed safety and liveness are demonstrated for the controlled scenario: fresh takeover/advance succeeds and stale-owner mutation is rejected. This is not a reconstruction of the historical all-null event. | `experiment-03-expiry-liveness/` |
| Test-only diagnostics disturb the wider P1A storage contract | The full storage class discovered/executed 13 tests and reported `13/0/0` on `127.0.0.1:53054`. | No diagnostic-run storage regression was observed. | `experiment-04-full-storage/` |
| Test-only diagnostics disturb the lifecycle contract | The lifecycle contract run discovered/executed six tests and reported `6/0/0`; no Mongo topology applies. | No diagnostic-run contract regression was observed. | `experiment-05-lifecycle-contract-regression/` |

The final diagnostic test-source SHA-256 was
`300894ec831b16aa3473b018c87e4df690853d3d619bbd2a14cfbe486e278ab2`; its test DLL SHA-256 was
`a1ff1f459dd8cb9291142ac86455cc6adab613644f2a16530a848ed9d0473dbc`. The first instrumented Release build
failed with three test compile errors and no executed tests; those test-only compilation errors were corrected before
any diagnostic claim was accepted. The fresh final Release build succeeded with `0` errors and `3` existing warnings.

**Diagnostic verdict:** historical root cause `UNKNOWN`; current two-second reproduction `NOT REPRODUCED`; current
single-writer safety passed; controlled expiry recovery preserved liveness and rejected stale-owner advance. No
runtime correction is recommended or proven by this bounded evidence. P1A remains a `CONDITIONAL PASS` storage
checkpoint with the three historical all-null failures and the two known architecture-guard baseline failures still
open. This record grants no P1A acceptance, flakiness closure, lifecycle completion, live acceptance or merge-ready
status. A later closure attempt needs retained source/DLL/lease provenance for a reproduced all-null event, or another
directly observed cause under the same bounded test-owned topology.

##### Diagnostic evidence inventory baseline

The previously reported aggregate digests
`f80bae48895888dffe4ce7e1ab17a1328048ea05082459d0c1a4f7b16517f0e0` and
`b7f4052ee9a8a08a6863c00c3ed0cf5ae6c7e1cdbbab9633404a70338384e124` differ. The reason for that discrepancy is
**UNKNOWN** because no preserved historical manifest or calculation command was found. The historical statement that
the evidence was preserved is therefore insufficient hash-comparison evidence; the discrepancy is not, by itself,
proof of data loss.

Future comparisons use this canonical inventory method: each file path is relative to the worktree and uses `/` as
the separator; normalized relative-path strings are sorted with PowerShell `[StringComparer]::Ordinal`; each line is encoded as
`relativePath<TAB>lowercase SHA256<LF>` using UTF-8 without a BOM; each per-file SHA-256 is calculated over the raw file
bytes; and the aggregate SHA-256 is calculated over the resulting manifest bytes. With that method, the pre-existing
evidence set contains 566 files and has aggregate SHA-256
`4c87c5c5aaea9b71f99da3fedeb0677aa77fa60a4c6c3cee0e00284bfc1139ee`; the new diagnostic set contains 80 files and
has aggregate SHA-256 `750877b7b2b35be489013869ed9159f0f73a6460f0040300cedffb2589007124`; the combined set contains
646 files and has aggregate SHA-256 `239af8810160c1f77a9297db66e74c9e06e85c5e88eafac1007464afbe3cd97e`.

This inventory baseline does not alter the diagnostic conclusions: the historical root cause remains `UNKNOWN`, the
current two-second scenario remains `NOT REPRODUCED`, and P1A remains `CONDITIONAL PASS`. It grants no acceptance,
status promotion, lifecycle completion, live acceptance or merge-ready claim.

### 21.15 Phase 1.5 — Finished Good human-admission boundary — draft proposal (2026-09-11)

This amendment remains a **planning-only draft** for every slice except the completed exact Slice B boundary and the
bounded A0 evidence recorded in §21.15.4. User approvals grant runtime/test code-start only for Slice B's seven exact
paths and A0's exact FU03 allow-lists; they do not change this pack's frontmatter or status, authorize A1/A2/Slice C,
promote any lifecycle stage, or claim that P1A is accepted. P0A's exact FU21 pairs and audit mappings are consumed
unchanged and must not be re-added. P1A remains a
conditional storage/integrity/CAS/recovery foundation; it is not human authorization, coherent scope capture, a
production caller, or workflow activation.

The proposed admission boundary is:

`canonical tenant human → exact mdm.finished-goods.submit → real FG/GSKU/revision/product chain → trusted and local
finite Legal Entity scope → coherent immutable snapshot → existing operation reservation`.

The first slice stops at a durable `Prepared` reservation. It does not start a workflow, apply a terminal decision,
create a background authority, mutate the source lifecycle, produce a lifecycle audit intent, or advertise UI/
Gateway behavior.

#### 21.15.1 Re-measured code truth and authority gaps

| Concern | Current code truth | Consequence for this amendment |
|---|---|---|
| Tenant | `ITenantContext` exposes the middleware-resolved tenant at `services/Diten.MdmService/src/Diten.MdmService.Application/Common/ITenantContext.cs:3-7`. Current MDM middleware accepts `jwtTenant ?? headerTenant`, including header-only input, at `services/Diten.MdmService/src/Diten.MdmService.Infrastructure/Middleware/TenantResolutionMiddleware.cs:27-46`. FU21's trusted human boundary instead requires exactly one authenticated JWT `tenant_id` at `services/Diten.Platform/src/Diten.Platform.API/Security/TrustedLegalEntityScopeJwtContext.cs:18-41`. | Header/body tenant alone is not admission authority. The new MDM admission boundary must require exactly one authenticated JWT `tenant_id`, exact equality with `ITenantContext.TenantId`, and exact equality with any canonical gateway header; absent, duplicate, malformed or mismatched facts deny before parent/provider/reserve. |
| Canonical human | `ProductIdentityLifecycleActorContext` rejects unauthenticated, duplicate or conflicting `sub`/name-identifier facts at `services/Diten.MdmService/src/Diten.MdmService.Infrastructure/Security/ProductIdentityLifecycleActorContext.cs:16-39`, but admits `tenant_user`, `platform_admin` and `partner_admin` at `services/Diten.MdmService/src/Diten.MdmService.Infrastructure/Security/ProductIdentityLifecycleActorContext.cs:65-66`. | FU21's current end-to-end human contract admits only exact `tenant_user` at `services/Diten.Platform/src/Diten.Platform.API/Security/TrustedLegalEntityScopeJwtContext.cs:25-41`; the other types are not automatic FG authority. Service actors are denied. |
| Exact local permission | The same context parses permission claims but compares values case-insensitively at `services/Diten.MdmService/src/Diten.MdmService.Infrastructure/Security/ProductIdentityLifecycleActorContext.cs:41-56`. FU21 accepts ordinal module/permission pairs at `services/Diten.Platform/src/Diten.Platform.API/Security/TrustedLegalEntityScopeCredentialAuthenticator.cs:33-50`; the FG submit pair is recorded in `execution/domains/platform-shared-services/module-packs/MOD-0018-FU21-trusted-multi-legal-entity-scope-resolution.md:602-623`. | Admission needs a separate exact check for literal `mdm.finished-goods.submit`; read/create, prefix, wildcard and case drift are not substitutes. The client cannot supply the permission name. |
| Actor consistency | `ProductLegalEntityScopeCandidateFacade` uses `IProductIdentityActorContext` at `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductLegalEntityScopes/ProductLegalEntityScopeCandidateFacade.cs:15-31`; that context takes the first name-identifier or `sub` without lifecycle actor-type/cardinality/equality checks at `services/Diten.MdmService/src/Diten.MdmService.Infrastructure/Security/ProductIdentityActorContext.cs:16-25`. | Resolve the lifecycle human once and require exact equality with the candidate/provider subject. A facade success for another subject is rejected. |
| Trusted candidates | Provider result carries tenant, subject, module, permission, `EvaluatedAtUtc` and Legal Entity IDs at `services/Diten.MdmService/src/Diten.MdmService.Application/Contracts/Authorization/ITrustedLegalEntityScopeProvider.cs:13-39`. The client validates echoed context, UTC freshness window and bounded canonical IDs at `services/Diten.MdmService/src/Diten.MdmService.Infrastructure/Authorization/PlatformTrustedLegalEntityScopeProviderClient.cs:358-415`. | These are provider observations, not client authority. Credential/JWT material is never persisted. Current provider window is `[-5m,+1m]`; a narrower number is a new owner decision. |
| Local candidates | The facade validates provider facts and intersects once with same-tenant active/non-deleted Legal Entities at `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductLegalEntityScopes/ProductLegalEntityScopeCandidateFacade.cs:88-150`; the bounded referenceable read is documented at `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/ILegalEntityRepository.cs:11-19`. | Foreign, deleted or non-referenceable IDs are removed; an empty verified result is denial, never wildcard or GroupWide fallback. |
| Policy evaluation | `ProductLegalEntityScopeEvaluator` allows existing tenant access in Preparation at `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductLegalEntityScopes/ProductLegalEntityScopeEvaluator.cs:37-46`; Enforced computes intersections at `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductLegalEntityScopes/ProductLegalEntityScopeEvaluator.cs:58-109`. | Preparation is a compatibility decision, not complete immutable admission evidence. The approved first slice is Enforced-only. |
| Existing consumer guard | `ProductLegalEntityScopeConsumerGuard` is inside `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Handlers/QueryHandlers/GetGlobalProductsHandler.cs:82-274`. Missing rollout synthesizes Preparation at `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Handlers/QueryHandlers/GetGlobalProductsHandler.cs:114-122`; the result does not preserve matched policy-period evidence. | It cannot be the source of a complete admission snapshot. Existing query behavior remains unchanged. |
| Parent chain | FG→GSKU is `FinishedGood.GskuId` at `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/FinishedGood.cs:5-12`; GSKU→revision is `Gsku.ProductDefinitionRevisionId` at `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/Gsku.cs:6-20`; revision→Global Product is `ProductDefinitionRevision.GlobalProductId` at `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/ProductDefinitionRevision.cs:6-16`. Tenant/non-deleted reads are at `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/FinishedGoodRepository.cs:113-115`, `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/GskuRepository.cs:34-49`, `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/ProductDefinitionRevisionRepository.cs:27-29` and `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/GlobalProductRepository.cs:32-34`. | All four records are re-read from the current tenant. Client-supplied parent IDs cannot establish the chain. The FG must be Draft at the expected version. |
| Snapshot/storage | Snapshot v1 is `services/Diten.MdmService/src/Diten.MdmService.Domain/ValueObjects/FinishedGoodLifecycleAdmissionScopeSnapshot.cs:8-185`. P1A reserves and compares it at `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/FinishedGoodIdentityWorkflowOperationRepository.cs:25-118,834-895`. | Hash equality proves integrity, not authorization or coherent capture. V1 lacks Global Product, exact permission/module, expected version, distinct observation times and effective period identity. |
| Production wiring | Persistence registration moves from `IFinishedGoodRepository` directly to LSKU at `services/Diten.MdmService/src/Diten.MdmService.Persistence/DependencyInjection.cs:52-55`; no P1A operation registration exists. API/manifest expose only read/create at `services/Diten.MdmService/src/Diten.MdmService.Api/Controllers/FinishedGoodsController.cs:20-44` and `services/Diten.MdmService/src/Diten.MdmService.Api/ModuleRegistration/ProductItemSkuMasterManifestProvider.cs:76-89`. | There is no production human-admission caller. API/manifest are a later substep and are advertised only after the internal boundary exists. |

#### 21.15.2 Complete admission snapshot field/source contract

The table covers every current v1 field and every additional fact required for coherent admission. “Persist” means
inside `FinishedGoodIdentityWorkflowOperation.AdmissionScopeSnapshot`; outer operation facts stay independently
equality-checked. A new version must not reinterpret a v1 row silently.

| Field | State | Authoritative source / exact evidence | Binding, freshness, failure and replay rule |
|---|---|---|---|
| `SnapshotVersion` | v1 present | Server factory/constant at `services/Diten.MdmService/src/Diten.MdmService.Domain/ValueObjects/FinishedGoodLifecycleAdmissionScopeSnapshot.cs:34,37-75`. | Server assigns at first reserve. Unknown/old shape fails closed pending explicit compatibility. Replay preserves it. |
| `TenantId` | v1 present | Resolved `ITenantContext` at `services/Diten.MdmService/src/Diten.MdmService.Application/Common/ITenantContext.cs:3-7`; operation repo binds tenant at `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/FinishedGoodIdentityWorkflowOperationRepository.cs:25-35,52`. | Admission additionally requires exactly one authenticated JWT tenant at `services/Diten.Platform/src/Diten.Platform.API/Security/TrustedLegalEntityScopeJwtContext.cs:25-41`, equal to resolved context and any gateway header. Header/body-only, duplicate, malformed or mismatch denies. Persist once; replay rechecks current tenant. |
| `FinishedGoodId`, `FinishedGoodVersion`, `FinishedGoodLifecycleStatus` | ID v1 present; version/status proposed v2 | Tenant/non-deleted read at `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/FinishedGoodRepository.cs:113-115`; identity/version at `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/EntityBase.cs:5-11`; status at `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/FinishedGood.cs:11`. | Read under the ordered capture; require exact Draft and the requested expected version. Foreign/deleted/missing/status/version drift denies. Retry with another FG conflicts. |
| `GskuId`, `GskuVersion`, `GskuLifecycleStatus` | ID v1 present; version/status proposed v2 | Persisted FG link at `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/FinishedGood.cs:7`; tenant/referenceable GSKU read at `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/GskuRepository.cs:34-49`; inherited version at `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/EntityBase.cs:11`; status at `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/Gsku.cs:16`. | Live link and approved semantic parent state are strict; foreign/deleted/changed/non-referenceable parent denies and replay cannot substitute another GSKU. Capture Version as history/coherence evidence; whether unrelated Version drift alone denies is D3, with semantic-only behavior recommended. |
| `ProductDefinitionRevisionId`, `ProductDefinitionRevisionVersion`, `ProductDefinitionRevisionLifecycleStatus` | ID v1 present; version/status proposed v2 | Persisted GSKU link at `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/Gsku.cs:8`; tenant revision read at `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/ProductDefinitionRevisionRepository.cs:27-29`; inherited version at `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/EntityBase.cs:11`; status at `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/ProductDefinitionRevision.cs:10`. | Live link and approved semantic state are strict; missing/foreign/link/status contraction denies. Persist Version in immutable history; Version-only denial awaits D3, with semantic-only behavior recommended. |
| `GlobalProductId`, `GlobalProductVersion`, `GlobalProductLifecycleStatus` | **v1 missing; proposed v2** | Persisted revision link at `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/ProductDefinitionRevision.cs:8`; tenant product read at `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/GlobalProductRepository.cs:32-34`; inherited version at `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/EntityBase.cs:11`; status at `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/GlobalProduct.cs:10`; policy binding at `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/ProductLegalEntityScopePolicy.cs:11`. | Foreign/deleted/link/status contraction denies; replay cannot follow a changed link. Persist Version as history/coherence evidence. D3 decides whether unrelated Version drift alone denies; semantic-only behavior is recommended. |
| `AdmissionCommandId` | v1 present | Operation ID contract at `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/FinishedGoodIdentityWorkflowOperationResults.cs:7-17`; snapshot currently accepts caller value at `services/Diten.MdmService/src/Diten.MdmService.Domain/ValueObjects/FinishedGoodLifecycleAdmissionScopeSnapshot.cs:37-51`. | Require non-empty server-validated ID and snapshot command = outer operation ID. Identical facts replay; actor, payload, parent identity/link or target FG expected-version drift conflicts. Parent-only generic Version behavior follows D3. |
| `StartIdempotencyKey` binding | outer only; proposed v2 hash fact | Outer immutable fact at `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/FinishedGoodIdentityWorkflowOperation.cs:10-18`; lookup contract at `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/IFinishedGoodIdentityWorkflowOperationRepository.cs:11-17`. | Normalize under P1A rule and bind into operation/snapshot fingerprints. Same key with different facts conflicts. |
| `AdmissionActorSubjectId` | v1 present | V1 factory currently accepts a caller value at `services/Diten.MdmService/src/Diten.MdmService.Domain/ValueObjects/FinishedGoodLifecycleAdmissionScopeSnapshot.cs:37-63`; no production human-admission caller exists. The planned FG-specific context is Slice B. | First slice exact `tenant_user`; candidate/provider subject must match. Duplicate/conflicting/service/platform/partner denies before scope/repo. Persist GUID only; recheck current authority on replay. |
| `ExpectedFinishedGoodVersion` | outer only; proposed v2 | Outer operation at `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/FinishedGoodIdentityWorkflowOperation.cs:15`; reservation contract at `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/FinishedGoodIdentityWorkflowOperationResults.cs:13`; live version at `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/EntityBase.cs:11`. | Compare during capture. Stale/negative/mismatch conflicts with zero reserve. Persist/fingerprint; replay preserves first value. |
| `ModuleCode` | **v1 missing; proposed v2** | Server constant `product-item-sku-master` at `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductLegalEntityScopes/ProductLegalEntityScopeCandidateFacade.cs:11-13` and FU21 pack `execution/domains/platform-shared-services/module-packs/MOD-0018-FU21-trusted-multi-legal-entity-scope-resolution.md:602-623`. | No client input. Provider echo must be ordinal-equal. Persist/fingerprint; mismatch/case drift denies. |
| `PermissionKey` | **v1 missing; proposed v2** | Server constant `mdm.finished-goods.submit` at `execution/domains/platform-shared-services/module-packs/MOD-0018-FU21-trusted-multi-legal-entity-scope-resolution.md:602-623` and `services/Diten.Platform/src/Diten.Platform.API/Security/TrustedLegalEntityScopeCredentialAuthenticator.cs:33-50`. | Exact local claim then exact provider echo. Read/create/wildcard/prefix/case drift deny. Recheck current permission on replay without rewriting history. |
| `ProviderEvaluatedAtUtcTicksV1` | v1 conflated; proposed distinct v2 | Trusted result at `services/Diten.MdmService/src/Diten.MdmService.Application/Contracts/Authorization/ITrustedLegalEntityScopeProvider.cs:13-39`; client validation at `services/Diten.MdmService/src/Diten.MdmService.Infrastructure/Authorization/PlatformTrustedLegalEntityScopeProviderClient.cs:358-415`. | Must satisfy current provider UTC window in the same attempt. Missing/future/stale/malformed denies. Persist first observation; retry cannot replace it. |
| Local scope evaluation time | not persisted separately in v1; no separate v2 field | Local narrowing occurs at `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductLegalEntityScopes/ProductLegalEntityScopeCandidateFacade.cs:110-124`. | The locally referenceable IDs are re-read against the single server-evaluated admission instant below. A second process-clock field would duplicate the same local validity decision and could imply false ordering, so it is not added. Empty intersection denies. |
| `ScopePolicyId` | v1 present | Tenant/product lookup at `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/ProductLegalEntityScopePolicyRepository.cs:55-65`; ID at `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/EntityBase.cs:5`. | First slice requires real current policy; synthetic/empty forbidden. Capture coherently; changed/deleted/foreign policy conflicts or denies. |
| `ScopePolicyVersion` | v1 present | Policy `EntityBase.Version` at `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/EntityBase.cs:11`. | Capture exact non-negative version. Read→reserve drift gives one coherent later set or zero-write conflict. Replay preserves. |
| `ScopePeriodId` | **v1 missing; proposed v2** | Effective period selection at `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/ProductLegalEntityScopePolicy.cs:53-65`; identity at `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/ProductLegalEntityScopePolicy.cs:280-307`. | Require exactly one current real period. Empty/synthetic ID forbidden. Persist/fingerprint; missing/overlap/change denies or conflicts. |
| `ScopeEffectiveFromUtcTicksV1` | **v1 missing; proposed v2** | `ProductLegalEntityScopePeriod.EffectiveFromUtc` at `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/ProductLegalEntityScopePolicy.cs:280-307`. | Persist/fingerprint the bound selected at policy-evaluation time. Non-UTC/future/mismatch denies; replay preserves the original bound. |
| `ScopeEffectiveToUtcTicksV1` | **v1 missing; proposed nullable v2** | `ProductLegalEntityScopePeriod.EffectiveToUtc` at `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/ProductLegalEntityScopePolicy.cs:280-307`. | Persist/fingerprint null or exact exclusive end. Invalid/expired/mismatch denies; replay preserves the original bound. |
| `ScopeMode` | v1 present | Effective period mode at `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/ProductLegalEntityScopePolicy.cs:53-65,280-286`. | First slice accepts Enforced Scoped or GroupWide; never infer mode from empty set. Persist/replay unchanged. |
| Policy evaluation time | not persisted separately in v1; no separate v2 field | Effective-period bounds are at `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/ProductLegalEntityScopePolicy.cs:280-307`. | Period selection uses the same `AdmissionValidityEvaluatedAtUtcTicksV1` instant below. A second process-clock field would duplicate the local decision. Provider observation time remains separate because it is remote evidence. |
| `RolloutStateId` | v1 present | Tenant rollout read at `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/ProductLegalEntityScopeRolloutStateRepository.cs:56-59`; entity ID at `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/EntityBase.cs:5`. | First slice requires real non-deleted rollout. Missing denies; no sentinel/synthetic ID. Persist/replay unchanged. |
| `RolloutVersion` | v1 present | `EntityBase.Version` at `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/EntityBase.cs:11`; rollout validation at `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/ProductLegalEntityScopeRolloutState.cs:49-59`. | Capture with policy/parents. Drift gives a coherent later set or zero-write conflict. Persist/replay unchanged. |
| `RolloutMode` | v1 present | `ProductLegalEntityScopeRolloutState.Mode` at `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/ProductLegalEntityScopeRolloutState.cs:5-8`. | Approved first slice requires Enforced. Missing, Preparation or Suspended creates no snapshot/reservation. |
| `LegalEntityIds` | v1 present | Provider/local narrowing at `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductLegalEntityScopes/ProductLegalEntityScopeCandidateFacade.cs:88-150`; effective policy at `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductLegalEntityScopes/ProductLegalEntityScopeEvaluator.cs:58-109`. | Sorted distinct finite `exact-submit provider ∩ tenant active/referenceable local ∩ effective policy`. Scoped intersects period IDs; GroupWide keeps finite provider/local. Empty denies; 200 accepted, 201/duplicate/empty rejected without truncation. Later expansion never enlarges first set. |
| `AdmissionValidityEvaluatedAtUtcTicksV1` | **v1 missing; proposed v2** | No reservation server-time capture exists today: current `ReserveAsync` uses process `UtcNowTicks()` at `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/FinishedGoodIdentityWorkflowOperationRepository.cs:38-88`; existing Mongo `$$NOW` expression at `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/FinishedGoodIdentityWorkflowOperationRepository.cs:979-989` supports other mutations, not reserve. | The first transaction-bound rollout write establishes ordering but does **not** supply the validity time. After all time-independent facts are read, a final lease-qualified rollout update evaluates/returns `$$NOW`; the selected period is then checked against that final instant before hashing/insertion. Mongo defines [`$$NOW`](https://www.mongodb.com/docs/manual/reference/aggregation-variables/) as the current datetime held constant through that aggregation pipeline; it is **not** commit/ack time and does not prove commit order. It becomes a valid admission instant only if that transaction commits while every invalidating writer remains ordered by the same lease. Ambiguous outcome uses exact persisted read-back; timestamp is never regenerated. |
| `AdmissionObservedAtUtcTicksV1` | v1 present but ambiguous | Caller-supplied single time at `services/Diten.MdmService/src/Diten.MdmService.Domain/ValueObjects/FinishedGoodLifecycleAdmissionScopeSnapshot.cs:19,37-75`; only `>0` validation at `services/Diten.MdmService/src/Diten.MdmService.Domain/ValueObjects/FinishedGoodLifecycleAdmissionScopeSnapshot.cs:118-145`. | V2 cannot let one value impersonate provider observation and the local server-evaluated validity instant. No snapshot field is called commit/ack time. V1 compatibility stays fail-closed pending separate reconciliation/migration. |
| `IntegrityFingerprint` | v1 present | Canonical SHA-256 at `services/Diten.MdmService/src/Diten.MdmService.Domain/ValueObjects/FinishedGoodLifecycleAdmissionScopeSnapshot.cs:161-185`; repo equality/read validation at `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/FinishedGoodIdentityWorkflowOperationRepository.cs:834-895`. | Create an uncommitted operation shell; obtain and read back the server-evaluated admission instant under the lease-bound transaction; re-read/final-check every fact and period bound; then compute the canonical fingerprint client-side from that complete immutable set, finalize the shell and commit. Invalid/mismatch denies or quarantines. It is integrity, not signature/authorization. Ambiguous outcome finds the first row by exact tenant/operation/idempotency identity and validates its stored hash; it never regenerates time or hash. |

The complete stored operation, including all BSON fields, remains at most 1,048,576 bytes and the snapshot at most
200 Legal Entity IDs. The two limits are independent; meeting the ID cap does not prove the BSON cap.

#### 21.15.3 Approved planning decisions and unresolved owner choices

The following five directions are approved for this plan. They grant no runtime code-start beyond the exact bounded
Slice B authorization in §21.15.4:

1. **Human actor:** the first admission slice accepts exact `tenant_user` only. `platform_admin`/`partner_admin` need a
   separate Platform/FU21 owner decision and authenticator/test amendment. Service actors remain denied.
2. **Preparation/no-policy:** admission is fail-closed Enforced-only. Missing rollout, Preparation, Suspended, missing
   policy or no current period creates no snapshot/reservation. Supporting Preparation later needs an approved
   discriminated evidence shape; fake IDs/versions, wildcard and implicit legacy→Enforced promotion are forbidden.
3. **Provider freshness:** retain the current `[-5m,+1m]` client contract. This bounds an observed remote result; it
   is not instantaneous revocation or an atomic cross-service authorization guarantee. No narrower numeric contract
   is invented here.
4. **Exposure:** the first slice remains internal. No public submit API or manifest entry is in this slice, and
   `Prepared` does not mean workflow started or approval submitted.
5. **V1 compatibility:** v1 rows are neither silently upgraded nor rewritten. Missing admission facts deny
   continuation until a separately approved reconciliation/migration. Actual v1 data presence remains an operational
   inventory question because this documentation-only turn did not connect to MongoDB.

Replay ordering is explicit and separates history from current authority:

1. Resolve the current authenticated JWT tenant and canonical human; bind both to `ITenantContext` and the requested
   operation/key. A tenant, actor, operation, payload, expected-version or parent mismatch with an existing row is a
   conflict before any new snapshot is constructed.
2. Lookup the existing operation by operation ID/idempotency key. Its first snapshot, provider time, policy/rollout
   versions, admitted set and hash are historical immutable facts; do not call the v1 `ReserveAsync` equality path
   with a freshly captured replacement snapshot. Current exact replay comparison at
   `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/FinishedGoodIdentityWorkflowOperationRepository.cs:862-895`
   remains storage evidence, but the new admission contract must separate historical lookup from current authority.
3. Recheck current exact `mdm.finished-goods.submit` and current trusted/local/policy scope. Revoked permission,
   missing/expired policy, Suspended/Preparation mode, empty set, or a contraction that no longer admits every LE
   required by the operation denies continuation without changing the stored row. A changed provider timestamp alone
   is not payload drift and is never copied into history.
4. Current permission and effective scope must still contain **every** Legal Entity in the historical admitted set.
   If so, return the original Prepared reservation; any additional newly allowed IDs are ignored. If the current set
   drops any historical admitted ID, deny without mutation. Policy/rollout version, time or provider-observation drift
   alone is not a conflict; only the current semantic containment result controls allow/deny, and history never grows.
5. Only when no existing operation exists may the coordinator construct one new v2 snapshot and attempt the atomic
   local reserve. Duplicate/ambiguous write reads back and compares that first persisted operation; it does not recapture.

Local read→reserve coherence needs ordering plus a transaction; reads and an unrelated operation insert are not
enough. MongoDB documents that transaction reads can be stale and that an actual write/lock on a shared document is
needed to create write-conflict ordering ([Transactions - Production Considerations](https://www.mongodb.com/docs/manual/core/transactions-production-consideration/)).
The approved planning mechanism therefore consumes the existing tenant rollout-state writer lease; it does not add a
new general lock collection:

1. Resolve exact tenant human, permission and provider facts without mutation. Existing operation lookup happens
   before recapture and preserves the replay rules above.
2. Acquire the tenant writer lease by the existing rollout-document CAS at
   `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/ProductLegalEntityScopeRolloutStateRepository.cs:190-237`.
   Admission starts no transaction and creates no operation when the rollout document is absent, not Enforced, fenced
   or already leased.
3. Start the MDM transaction with snapshot read concern and majority write concern, then perform a real
   lease-token/generation-qualified binding write on that same rollout document. It binds the admission operation to
   the lease and establishes the common-document conflict/serialization point, but does not claim a validity time.
   Snapshot reads alone do not establish this ordering.
4. In that transaction re-read, in order, rollout → policy and its period set → Global Product → revision → GSKU → FG
   → sorted finite Legal Entity documents. Require the exact lease token/generation, Enforced mode, no activation
   fence, the approved parent semantic predicates, and the target FG expected version; capture parent Versions without
   silently deciding D3.
5. After those time-independent reads, perform a second lease-qualified rollout update/read that evaluates and returns
   `AdmissionValidityEvaluatedAtUtcTicksV1` with Mongo `$$NOW`. Select and validate exactly one period using
   `EffectiveFrom <= final admission instant < EffectiveTo` (or null end). This final evaluation catches a period that
   expires after the initial lock but before reservation; `$$NOW` is not commit or acknowledgment time.
6. Build the complete immutable facts, compute the canonical hash only after final server time/read-back, insert/finalize the
   Prepared operation, and pre-commit revalidate the same rollout tenant/lease token/generation. Commit while the lease
   remains held. Release only after a verified successful commit or verified abort; an abort leaves no reservation,
   while an unknown result keeps the lease/fence fail-closed until exact persisted read-back determines the outcome.
   Never recapture time or recompute a candidate hash.

This handles distinct races only after every invalidating writer participates in the same physical ordering. The
coordinator is orchestration, not authorization: it accepts a parseable actor and derives a mutation identity at
`services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductLegalEntityScopes/ProductLegalEntityScopeWriteFenceCoordinator.cs:33-85`,
while its release policy at lines 117-128 is not durable business-outcome proof. The MediatR behavior at
`services/Diten.MdmService/src/Diten.MdmService.Application/Behaviors/ProductLegalEntityScopeWriteFenceBehavior.cs:17-36`
only reaches marker requests. A lease handle, maker GUID, service-token used to read evidence, or boolean success from
this layer is never source-aggregate mutation authority.

**Foreground, background and recovery authority contract.** Every accepted value is a server-created discriminated
record; callers cannot submit `isTrusted`, an actor ID, permission name or generic service bypass.

| Caller class | Caller → verified proof | Tenant binding | Exact operation → aggregate → mutation | Current status / required boundary |
|---|---|---|---|---|
| Foreground FG admission | Proposed internal admission handler → completed FG-specific context at `services/Diten.MdmService/src/Diten.MdmService.Infrastructure/Security/FinishedGoodHumanAdmissionContext.cs:26-48,92-98` → current FU21/provider result | One authenticated `tenant_user` JWT tenant = `ITenantContext` = optional canonical header; exact subject and finite LE set | `mdm.finished-goods.submit` → Finished Good plus persisted parent chain → create one immutable `Prepared` reservation | Human proof exists only for this foreground boundary. It is never used by a worker, and no JWT/human is synthesized. Repository acceptance still needs an atomic guarded session. |
| Foreground scope/parent writer | Exact authenticated command and owner-approved permission → a verified writer-authority adapter | Exact resolved tenant, command, purpose, operation, aggregate and mutation fingerprint | The command's existing business operation → its source aggregate → only its existing CAS mutation | Current coordinator only parses `IProductIdentityActorContext.ActorId` at `ProductLegalEntityScopeWriteFenceCoordinator.cs:49`; it does not close permission, grant or direct repository bypass. The adapter is missing. |
| Background workflow continuation | `services/Diten.MdmService/src/Diten.MdmService.Api/Services/ProductItemSkuMaster/ProductIdentityWorkflowRecoveryWorker.cs` and `ProductIdentityWorkflowRecoveryRunner.cs:22-153` → claimed immutable P1A operation → verified terminal evidence → current named service identity and exact local execution grant | Tenant from immutable operation must equal worker scope, current service proof and evidence tenant | Exact checkpoint action → exact Global Product/GSKU/revision/FG aggregate → terminal source CAS plus immutable outcome/evidence checkpoint | Existing workflow token provider can authenticate the Platform evidence read, and processors verify terminal evidence before mutation (for example `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Workflow/GlobalProductIdentityWorkflowProcessor.cs:733-797`); neither proves current local mutation authority. There is no implemented MDM execution-authority provider. Maker remains provenance, not executor. |
| Crash recovery / optional takeover | Explicit recovery runner → typed persisted operation/lease phase, proof reference+hash and exact read-back → current named executor authority | Exact tenant plus prior command, old token/generation, operation, aggregate, mutation fingerprint and current executor | One of `inspect`, `reconcile`, `resume` or `qualified release`; never an untyped continuation | Missing. Recovery authority must be server-created and action-specific. It cannot turn evidence-read permission or an expired lease into new business authority. |

The existing Auth-issued named workflow identity and Platform validation may be reused as authentication/proof
building blocks; they do not create a second credential/audience/grant source and must not be substituted for the
missing MDM mutation-authority decision. Human/delegated actor and service identity remain different discriminants.
Remote grant revocation cannot join the Mongo transaction, so continuation revalidates the current named identity and
exact action before acquiring the tenant lease; the immutable operation retains the proof reference/hash that was
actually used.

**Admission-invalidating writer classification.** The list is deliberately field/state based, not every lifecycle
method or every changed document.

| Classification | Field/state and exact writer evidence | Required treatment |
|---|---|---|
| Already ordered, regression required | Scope policy periods/mode/LE IDs/effective bounds through the three marker commands at `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductLegalEntityScopes/Commands/CreateProductLegalEntityScopePolicyCommand.cs:6-10`, `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductLegalEntityScopes/Commands/ReplaceProductLegalEntityScopePolicyCommand.cs:6-10`, `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductLegalEntityScopes/Commands/EndProductLegalEntityScopePolicyCommand.cs:6-10` and behavior at `services/Diten.MdmService/src/Diten.MdmService.Application/Behaviors/ProductLegalEntityScopeWriteFenceBehavior.cs:17-36`; rollout mode/fence at `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/ProductLegalEntityScopeRolloutStateRepository.cs:281-315,343-405` and `services/Diten.MdmService/src/Diten.MdmService.Api/Services/ProductLegalEntityScopes/ProductLegalEntityScopeOperationalRunner.cs:210-296`. | Preserve the existing singleton rollout document and prove these writes share the physical transaction fence; do not create another lock collection. |
| Mandatory MOD-0220 amendment | Legal Entity contraction through `OperationalStatus`/`IsDeleted`: Suspend/Archive/Delete at `services/Diten.MdmService/src/Diten.MdmService.Application/Features/LegalEntity/Handlers/CommandHandlers/SuspendLegalEntityHandler.cs:17-34`, `services/Diten.MdmService/src/Diten.MdmService.Application/Features/LegalEntity/Handlers/CommandHandlers/ArchiveLegalEntityHandler.cs:17-34` and `services/Diten.MdmService/src/Diten.MdmService.Application/Features/LegalEntity/Handlers/CommandHandlers/DeleteLegalEntityHandler.cs:16-21`. The nominal update path is also unsafe today: `services/Diten.MdmService/src/Diten.MdmService.Application/Features/LegalEntity/Handlers/CommandHandlers/UpdateLegalEntityHandler.cs:18,46-49` sends the read entity to `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/RepositoryBase.cs:40-50`, whose whole-document `ReplaceOneAsync` has no expected-version CAS and can restore stale lifecycle/delete state. | Enrol the three contraction writers and temporarily fence Update. Add an owner-local `ILegalEntityRepository`/`LegalEntityRepository` editable-field-only CAS that preserves stored `OperationalStatus`/`IsDeleted`; do not change global `RepositoryBase`. `ActivateLegalEntityHandler.cs:17-38` is an expansion regression, not a mandatory admission invalidator. After hardening, genuine name-only Update remains outside the lease. |
| Mandatory MOD-0290 amendment | Global Product accepted-parent contraction through lifecycle/delete at `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/GlobalProductRepository.cs:601-603,815`; revision `GlobalProductId`/delete or accepted-state contraction, concretely retirement at `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/ProductDefinitionRevisionRepository.cs:176-218`; GSKU `ProductDefinitionRevisionId`/delete or accepted-state contraction and `RetirementOperationId` child-admission fence at `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/GskuRepository.cs:511-545,576-606`; FG Draft/`GskuId`/`IsDeleted` and the target operation's expected FG version once a mutation path exists. | Exact foreground/background callers obtain verified authority; repository methods require one transaction-bound guard handle and enforce the common rollout write with the business CAS and outcome checkpoint. |
| Pending the parent-Version decision | Generic parent `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/EntityBase.cs:11` Version currently also changes for Global Product name/correction at `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/GlobalProductRepository.cs:383-386,716-722`, GSKU pack/UoM correction at `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/GskuRepository.cs:795-798`, and GSKU `ActiveLifecycleOperation` at `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/GskuRepository.cs:629-658`. | Recommended: bind stable semantic admission predicates and keep generic Versions as immutable historical/coherence observations, so presentation/correction drift does not revoke admission. If the owner instead chooses strict current Version equality, every actual Version-changing CAS must participate without gaining a new business permission. Excluding those writers while requiring strict Version equality is forbidden. |
| Out of scope for admission | Legal Entity activation after stale-update hardening; Global Product names; GSKU pack/UoM and unrelated correction fields; read-only queries/UI; workflow evidence reads; G4 delivery attempts/receipts/temporal compaction. | No writer lease, new permission or repository wrapper under the recommended semantic-predicate decision. FU03 H1b audit delivery remains independently fenced; evidence read does not authorize mutation. |

**Atomic enforcement boundary.** Persistence owns the Mongo session. In one transaction it performs a
token/generation/tenant/operation/aggregate/mutation-qualified write on the singleton rollout row, the exact business
CAS, immutable audit/outcome checkpoint, and a final qualified rollout guard write. A coordinator preflight or a
separate token lookup is insufficient. Wrong tenant, purpose, operation, aggregate, mutation fingerprint, token or
generation rejects inside the transaction. Post-lease parent/policy/Legal Entity reads use the same fresh session;
they cannot reuse the candidate facade's process memoization/echo path at
`ProductLegalEntityScopeCandidateFacade.cs:70-85,92-124`. Domain/Application layers receive typed proof/guard values,
not raw Mongo sessions.

The P1A operation lease remains separate from the FU03 tenant writer lease. Claim acquisition and Mongo-server-time
read-back are at `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/FinishedGoodIdentityWorkflowOperationRepository.cs:305-442`;
mutation/advance expiry validation is separately at lines 527-586. Its claim is durable workflow ownership, not
tenant-wide write authority. Ordering for an existing continuation is: operation claim → remote evidence outside the
tenant transaction → current executor authorization → FU03 tenant lease → bounded local transaction that
revalidates both leases and exact proof → business CAS/outcome/read-back commit → qualified tenant release →
operation advance/release. New FG admission acquires the tenant lease before inserting `Prepared`. Never reverse the
lease order, hold the tenant lease while waiting for human/remote/G4 work, or release unconditionally in `finally`.

Current lease truth is narrower than the previous plan stated. Generation already persists and increments from
`current.WriterLeaseGeneration + 1` at `ProductLegalEntityScopeRolloutStateRepository.cs:213-229`; release at lines
262-278 clears only `ActiveWriterLease`, so release/reacquire does not reset generation. Acquisition at lines 202-210
returns an exact same command/actor/kind/fingerprint lease as acquired even when its expiry has passed; a different
command remains blocked. Baseline bind at lines 240-259 and release at lines 262-278 qualify token/generation but do
not test expiry. Therefore current behavior is neither safe bounded takeover nor blanket expired-owner rejection.
Expiry is also currently process-clock-authored: `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductLegalEntityScopes/ProductLegalEntityScopeWriteFenceCoordinator.cs:59-66`
computes acquired/expiry timestamps with `TimeProvider`, and
`services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/ProductLegalEntityScopeRolloutStateRepository.cs:190-229`
persists those caller values without `$$NOW`/`$currentDate`. A future server-time comparison against that value would
not be clock-skew-safe. Expiry never terminates an in-flight Mongo transaction.

**Crash/recovery decision table.** Takeover is recovery-only and is not approved for implementation by this plan.

| Persisted situation | Who may act / exact permitted action | Preserved invariant and result |
|---|---|---|
| Lease acquired; no baseline | Current exact owner may replay acquisition. Recovery may bind only when durable acquisition identity and verified read-back prove the intended operation and no business write; otherwise inspect and retain. | Absence of baseline is not proof that no work occurred. Manual reconciliation remains the default. |
| Baseline bound; business mutation not begun | Current owner, or a separately verified recovery authority, may issue a qualified abort/release after exact no-mutation proof. | No new key, snapshot, operation or aggregate substitution. |
| Transaction abort is verified | Exact owner/recovery authority may qualified-release the same token/generation after the abort outcome is durable. | Aborted business/audit/outcome writes remain absent. |
| Commit response is lost | Recovery reads the immutable operation/business outcome and proof checkpoint by the original tenant/key/fingerprint. Matching committed facts permit exact replay/read-back; absent or contradictory facts retain the lease and require manual reconciliation. | Never infer success, recapture time, regenerate a hash or reuse the key for another aggregate. |
| Commit succeeded; release failed | After exact persisted outcome proof, recovery may release only the matching tenant/token/generation/operation/mutation. It does not redo the business mutation. | Commit is returned from immutable read-back; old owner cannot release a later generation. |
| Old owner is suspended and expiry passes | Default is fail-closed/manual. If separately approved, acquisition must first author acquired/expiry time on the Mongo server and return exact persisted read-back; only then may a recovery-only server-time CAS match old token, generation, expiry, command, payload fingerprint and phase, increment the existing generation in that same CAS, and preserve predecessor identity. | Current client-authored expiry is insufficient under clock skew. Time passage alone does not decide outcome or authorize new business work. No lease duration is invented here. |
| New generation wins; old owner resumes | Both old and new transactions must write the same rollout row. Old token/generation fails bind, business CAS, outcome commit and release; transaction conflict/serialization determines the winner. | No ABA and no stale-owner commit after takeover. Expiry alone never cancels an already running transaction. |
| Outcome remains ambiguous | Verified recovery may inspect only; mutation/resume/release stays denied. | Fail closed and escalate to manual reconciliation with immutable evidence. |
| Legacy/malformed lease or missing token/generation/proof | No automatic action. | Retain/manual; no TTL, blind unlock, direct Mongo repair, audit bypass or success inference. |

Manual reconciliation is the only currently safe liveness posture. A bounded takeover becomes eligible only after every
invalidating writer is physically fenced and an owner-approved persisted recovery phase/outcome shape exists. It is a
CAS transfer of recovery authority, not deletion/unlock. The smallest solution reuses the singleton rollout row and
durable generation, adds a module-owned verified writer-authority discriminant and one persistence-owned guarded-write
session; it rejects generic `isTrusted`, HttpContext synthesis, coordinator-only checks, a new lock collection, TTL,
blind release, direct Mongo repair and a second credential/audience source.

Only these decisions are newly required; the five decisions at the start of §21.15.3 are not reopened:

1. **Execution-authority ownership:** approve the FU03-owned discriminated writer-authority provider and exact
   foreground/background/recovery action model. Recommendation: reuse the existing Auth-issued named workflow identity
   only as an authentication input and add no credential/audience/collection; any exact local mutation grant and its
   Auth owner amendment must be approved explicitly before background code-start.
2. **Liveness posture:** retain manual fail-closed recovery initially, or later approve recovery-only bounded takeover
   together with its persisted phase/predecessor/outcome proof. Recommendation: keep takeover disabled through A0/A1/A2;
   enable it only after every invalidating repository seam is physically fenced and acquisition/takeover use one
   Mongo-server-authored time/read-back contract rather than the current client timestamp. No numeric expiry is decided.
3. **Parent-version semantics:** either bind owner-approved stable semantic admission predicates and treat generic
   Global Product/revision/GSKU Versions as historical/coherence observations, or keep strict current Version equality
   and enrol every real Version-changing CAS. Recommendation: use stable semantic predicates so name/correction-only
   changes do not expand this security fence; preserve captured Versions in immutable history and keep exact
   idempotency/fingerprint checks for the operation itself.

#### 21.15.4 Smallest dependency-ordered implementation slices

**Slice A — owner-split amendments.** The order is mandatory and the allow-lists are owner-split. A0 received exact
runtime/test code-start authority through `FU03-A0-FOREGROUND-GUARDED-REPLACE-01` on 2026-09-12. A1, A2 and Slice C
remain planning-only and blocked. A0 freezes the exact paths and signatures below; it does not promote this pack or
authorize any adjacent slice.

**A0 — MOD-0290-FU03 verified writer entry and physical transaction fence, takeover disabled — bounded code-start
approved 2026-09-12.** It proves one existing real scope-policy writer end to end before any Legal Entity, parent or
FG admission writer is enrolled. D1 is frozen as foreground-human Replace only; D2 keeps takeover off and every
ambiguous ownership/outcome fail-closed; D3 selects semantic parent predicates for a later admission slice while
preserving exact Finished Good expected-version. A0 changes no parent/lifecycle behavior and grants no correction
exemption.

Exact A0 FU03 runtime allow-list:

1. `services/Diten.MdmService/src/Diten.MdmService.Application/Contracts/Authorization/IProductLegalEntityScopeWriterAuthorityProvider.cs` — A0 foreground-human Replace verifier only; background/recovery variants are not in A0.
2. `services/Diten.MdmService/src/Diten.MdmService.Infrastructure/Security/ProductLegalEntityScopeWriterAuthorityProvider.cs` — foreground adapter over the authenticated principal and `ITenantContext`; exact server-owned permission only, no generic HttpContext/service bypass.
3. `services/Diten.MdmService/src/Diten.MdmService.Domain/ValueObjects/ProductLegalEntityScopeVerifiedWriterAuthority.cs` — immutable foreground tenant/purpose/operation/aggregate/mutation/proof facts; no service/recovery constructor in A0.
4. `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/IProductLegalEntityScopeGuardedWriteSession.cs` — bounded repository contract, not a raw Mongo/session leak.
5. `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/ProductLegalEntityScopeGuardedWriteSession.cs` — same-session rollout write + business CAS + outcome boundary.
6. `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/ProductLegalEntityScopeWriterLease.cs`
7. `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/IProductLegalEntityScopeRolloutStateRepository.cs`
8. `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/ProductLegalEntityScopeRolloutStateRepository.cs`
9. `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductLegalEntityScopes/ProductLegalEntityScopeMutationIdentity.cs`
10. `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductLegalEntityScopes/ProductLegalEntityScopeWriteFenceCoordinator.cs`
11. `services/Diten.MdmService/src/Diten.MdmService.Application/Behaviors/ProductLegalEntityScopeWriteFenceBehavior.cs`
12. `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/IProductLegalEntityScopePolicyRepository.cs`
13. `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/ProductLegalEntityScopePolicyRepository.cs`
14. `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductLegalEntityScopes/Handlers/CommandHandlers/ReplaceProductLegalEntityScopePolicyHandler.cs` — the sole A0 business writer, exact `mdm.product-legal-entity-scopes.replace` purpose.
15. `services/Diten.MdmService/src/Diten.MdmService.Application/DependencyInjection.cs`
16. `services/Diten.MdmService/src/Diten.MdmService.Infrastructure/DependencyInjection.cs`
17. `services/Diten.MdmService/src/Diten.MdmService.Persistence/DependencyInjection.cs`

`services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/ProductLegalEntityScopeRolloutState.cs` is explicitly
excluded from A0. No recovery phase, predecessor or outcome field is added. Existing durable generation and persisted
lease shape are reused without renumbering, TTL, legacy conversion or takeover.

Exact A0 signature contract (names and parameter order are frozen for this named step):

```csharp
Task<ProductLegalEntityScopeVerifiedWriterAuthority?> ResolveForegroundReplaceAsync(
    Guid aggregateId,
    ProductLegalEntityScopeMutationIdentity mutation,
    CancellationToken cancellationToken = default);

Task<ProductLegalEntityScopePolicyWriteResult> ReplaceAsync(
    ProductLegalEntityScopeVerifiedWriterAuthority authority,
    ProductLegalEntityScopeWriterLease lease,
    ProductLegalEntityScopePolicy requestedPolicy,
    int expectedVersion,
    CancellationToken cancellationToken = default);
```

The verified value is server-created and immutable. It carries exact `TenantId`, canonical human `SubjectId`,
`CommandId`, aggregate type `ProductLegalEntityScopePolicy`, the tenant-read policy's technical `Id` as `AggregateId`, purpose `foreground-human-replace`,
operation `ProductLegalEntityScopePolicyReplaced`, permission `mdm.product-legal-entity-scopes.replace`, mutation kind,
payload fingerprint and a server-derived proof fingerprint over those facts. Callers cannot provide or rewrite the
permission, actor, tenant, purpose, operation or proof fingerprint. A nullable resolution is denial, never legacy or
trusted fallback.

The handler sequence is frozen as: preserve the current read-only Preparation precheck and tenant-filtered policy read
needed to obtain the technical aggregate ID, without returning policy facts → resolve exact authority before any replay
response → perform the exact replay comparison, then existing candidate/provider and local Legal Entity validation outside the Mongo transaction →
acquire/bind the exact existing writer lease → invoke the guarded session. The guarded session owns the raw Mongo
session and, in one transaction, revalidates tenant, active token/generation, command, actor, mutation kind,
fingerprint and `Preparation`; performs a real qualified write on the existing rollout document; applies the policy
expected-version CAS plus the existing immutable audit intent/outcome; and reads back the exact committed policy.
Remote/provider work is never awaited inside the transaction. The existing policy repository's unguarded update entry
must reject a Replace-shaped transition, while existing Create and End behavior remains unchanged.

Commit/read-back and lease release are separate. Exact committed read-back may resume the same command without
repeating the business mutation. Absence, contradiction or an ambiguous read retains the lease and returns the
existing reconciliation-required behavior; it does not infer abort/success. Release is attempted only after a
verified committed result or verified zero-mutation abort and is qualified by the same token/generation. A release
failure cannot cause business mutation replay, and no unconditional `finally` release is allowed.

`services/Diten.MdmService/src/Diten.MdmService.Api/Services/ProductLegalEntityScopes/ProductLegalEntityScopeOperationalRunner.cs`
is reference/regression-only unless code review proves it directly performs a newly guarded mutation. The existing
`services/Diten.MdmService/src/Diten.MdmService.Api/Controllers/ProductLegalEntityScopesController.cs:89-108`
attribute remains an outer HTTP check, not the proof producer; A0's
dedicated provider must independently enforce the exact server-owned permission, canonical human/tenant binding and
mutation purpose. A0 closes only this foreground Replace writer. It does not authorize background/recovery mutation or
takeover; it preserves current same-identity replay and different-identity denial while closing direct repository
bypass for one policy write.

Exact A0 FU03 test allow-list:

1. `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ProductLegalEntityScopeWriterAuthorityTests.cs` — new foreground wrong tenant/purpose/operation/aggregate/mutation/proof and human-impersonation rejection; service/evidence/recovery inputs are negative cases only.
2. `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ProductLegalEntityScopeWriteAdmissionContractTests.cs`
3. `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ProductLegalEntityScopeWriteAdmissionMongoTests.cs`
4. `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ProductLegalEntityScopeActivationMongoTests.cs`
5. `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ProductLegalEntityScopeAuthorizationTests.cs`
6. `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ProductLegalEntityScopeCommandTests.cs`
7. `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/DependencyInjectionSmokeTests.cs`

**A0 bounded evidence record — 2026-09-12.** These are distinct historical runs from
`.testoutput/fu03-a0-foreground-guarded-replace-01-20260912/`; overlapping runs are not added together. The initial
guarded Mongo run was `9/3/0` (passed/failed/skipped). The implementation review diagnosed all three failures as the
raw BSON policy predicate using `Id` while the persisted identifier is `_id`; the failure symptoms and final `_id`
source agree with that diagnosis, but no immutable pre-correction source snapshot/diff was retained for independent
proof of the cause. The failures were not classified as a Mongo topology/environment gate. The corrected final guarded
Mongo run was `16/0/0`. It includes a real committed mutation whose
response is lost followed by exact persisted read-back, exact replay without a duplicate business/audit mutation, and
both missing-outcome and drifted-outcome ambiguous read-backs retaining the lease and failing closed.

The separate focused unit/contract/security plus existing Create/End run was `24/0/0`; activation Mongo was `3/0/0`;
Finished Good human-context regression was `17/0/0`; and DI regression was `4/0/0`. The final discovery log contains
`1316` total lines: four header lines and `1312` listed tests; `1316` is not a test count. The corrective MDM API Release
build completed with `0` errors and `5` existing warnings; the test-project
Release build completed with `0` errors and `3` existing warnings. The contract evidence source-scans production code
and fixes the sole issuer call site at
`services/Diten.MdmService/src/Diten.MdmService.Infrastructure/Security/ProductLegalEntityScopeWriterAuthorityProvider.cs`.
The private constructor and internal factory prevent public construction, but
`InternalsVisibleTo("Diten.MdmService.Infrastructure")` still gives the whole friend assembly access; the sole-source
test therefore remains required defense in depth rather than compiler-enforced per-class issuer isolation.

This evidence closes only A0's foreground-human guarded Replace boundary. No general suite or live acceptance was
run. Takeover, background/recovery mutation authority, A1/A2 enrolment and Finished Good admission remain unproved and
unauthorized by A0.

**A0 expired-writer-lease remediation evidence — 2026-09-13.** This is a separate measured run set retained under
`.testoutput/fu03-a0-expired-lease-remediation-20260913/`; it neither replaces nor aggregates with the preceding A0
RED/correction/final records, and overlapping runs are not summed. The pre-remediation exact-six real-Mongo run was
`4/2/0` (passed/failed/skipped). Both an initially expired exact-identity lease and a lease valid at entry but expired
before the physical Mongo update incorrectly succeeded and mutated persisted policy version `0 -> 1`, scope-period
count `1 -> 2` and audit-intent count `1 -> 2`. The completed committed exact replay and all three expired identity-
drift denials remained passing.

The first GREEN exact-six was `6/0/0`. Its first full guarded-Mongo candidate was `18/3/0`: three test-owned
historical `FixedClock`/lease setup timestamps were correctly treated as expired by the server-time predicate.
Expectations were unchanged; only those inputs were made current-time-relative, after which the full guarded-Mongo
run was `21/0/0`. Those results proved the repository-level fence, but were not final: independent audit found that
an initial or physical lease-qualification miss returned verified-zero and the handler/coordinator could therefore
release the expired lease.

The narrow end-to-end correction adds `LeaseRetentionRequired` to the guarded write result. It is set only when the
transaction's initial rollout read or physical rollout-document update misses exact lease qualification, and the
coordinator refuses release when it is set. Exact committed recovery/read-only replay remains successful and
releasable after expiry without a new mutation; an ordinary verified-zero result remains releasable. The shared
persisted-lease predicate remains enforced at baseline bind and through `QualifiedRolloutFilter` at both transaction
read and physical rollout update. Its existing BSON Array `DateTimeOffset` handling is hardened with nested `$cond`
guards requiring an array of size `2`, numeric UTC ticks and zero offset before strictly comparing expiry ticks with
Mongo server `$$NOW` converted to .NET epoch ticks; malformed or missing values evaluate false without an expression
exception.

Final current-binary records are exact-six `6/0/0`, malformed/retention contract `3/0/0`, full guarded Mongo
`23/0/0`, focused unit/contract/security plus existing Create/End `25/0/0`, activation Mongo `3/0/0`, Finished Good
human-context `17/0/0` and DI `4/0/0`; discovery was `1324`. These runs overlap and are reported separately, never
summed. Evidence inventory digests preserve the prior outside set at `707` files / `bbdb02...` and prior expired
RED/first-GREEN set at `25` files / `5eb2d2...`; the new retention-GREEN set is `11` files / `259a665...`, and the
complete expired-remediation set is `36` files / `0c096b...`. The MDM API Release build completed with `0` warnings
and `0` errors; the test-project Release build completed with `3` existing warnings and `0` errors. No schema or
time-representation change, migration, lease-duration change, takeover, extension, unlock or background/recovery
authority was introduced. No general suite or live acceptance was run. This evidence remains bounded to A0
foreground Replace and grants no A1/A2, Finished Good, background recovery, operational activation or Production
authority.

**A1 — MOD-0220 Legal Entity invalidators and stale-update hardening.** Exact proposed runtime allow-list:

1. `services/Diten.MdmService/src/Diten.MdmService.Application/Features/LegalEntity/Handlers/CommandHandlers/SuspendLegalEntityHandler.cs`
2. `services/Diten.MdmService/src/Diten.MdmService.Application/Features/LegalEntity/Handlers/CommandHandlers/ArchiveLegalEntityHandler.cs`
3. `services/Diten.MdmService/src/Diten.MdmService.Application/Features/LegalEntity/Handlers/CommandHandlers/DeleteLegalEntityHandler.cs`
4. `services/Diten.MdmService/src/Diten.MdmService.Application/Features/LegalEntity/Handlers/CommandHandlers/UpdateLegalEntityHandler.cs`
5. `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/ILegalEntityRepository.cs`
6. `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/LegalEntityRepository.cs`

Exact proposed A1 test allow-list:

1. `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/LegalEntityCommandTests.cs`
2. `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/LegalEntityScopeWriteFenceMongoTests.cs` — new stale active/suspended/archive/delete and old-owner fencing races; Activate is regression-only.
3. `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ProductLegalEntityScopeWriteAdmissionContractTests.cs` — exact owner inventory only.

Do not modify `RepositoryBase.cs`. The Legal Entity owner must first make Update an editable-field-only expected-version
CAS that cannot write back a stale lifecycle/delete value, then enrol only the actual referenceability writers.

**A2 — MOD-0290 exact parent/FG invalidators.** Repository contract/implementation allow-list:

1. `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/IGlobalProductRepository.cs`
2. `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/GlobalProductRepository.cs`
3. `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/IGskuRepository.cs`
4. `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/GskuRepository.cs`
5. `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/IProductDefinitionRevisionRepository.cs`
6. `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/ProductDefinitionRevisionRepository.cs`
7. `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/IFinishedGoodRepository.cs`
8. `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/FinishedGoodRepository.cs`

Exact caller allow-list under the recommended semantic-predicate decision; each path still needs a per-method proof of
the exact contraction it writes:

1. `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Lifecycle/Handlers/CommandHandlers/RetireGlobalProductIdentityHandler.cs`
2. `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Workflow/GlobalProductRetirementRequestWorkflowProcessor.cs`
3. `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Lifecycle/FirstGskuIdentityRetirementProcessor.cs`
4. `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Lifecycle/Handlers/CommandHandlers/RetireGskuIdentityPairHandler.cs`
5. `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Workflow/GskuRetirementRequestWorkflowProcessor.cs`
6. `services/Diten.MdmService/src/Diten.MdmService.Api/Services/ProductItemSkuMaster/GlobalProductRetirementRequestRecoveryRunner.cs`
7. `services/Diten.MdmService/src/Diten.MdmService.Api/Services/ProductItemSkuMaster/GskuRetirementRequestRecoveryRunner.cs`
8. `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Lifecycle/FirstGskuIdentityRetirementRecoveryRunner.cs`

Draft/name/correction, pack/UoM, submit/approve/reject and unrelated lifecycle callers are not auto-enrolled. They enter
this allow-list only if the owner deliberately selects strict generic Version semantics or code review proves that a
specific method writes one of the semantic predicates above.

A2 background and recovery paths remain blocked by D1. Their exact local authority provider/runtime/Auth amendment
paths cannot be declared final from current code because no such producer exists. Before A2 code-start, the FU03/Auth
owners must amend this pack with the exact existing-or-new provider, current-grant verifier, DI and tests; the result
must reuse the existing named workflow identity as an input and must not invent a second credential, audience or grant
collection. The runner paths above may only consume that discriminant; they cannot mint it. The proposed
`services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ProductLegalEntityScopeWriterRecoveryMongoTests.cs`
and its positive background/recovery cases belong to that D1-following amendment, not A0.

Exact proposed A2 test allow-list:

1. `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ProductLegalEntityScopeWriteAdmissionContractTests.cs`
2. `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ProductLegalEntityScopeWriteAdmissionMongoTests.cs`
3. `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/GlobalProductRetirementRequestWorkflowProcessorTests.cs`
4. `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/GlobalProductRetirementRequestRecoveryWorkerMongoTests.cs`
5. `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/FirstGskuIdentityRetirementUnitTests.cs`
6. `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/GskuRetirementRequestUnitTests.cs`
7. `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/GskuRetirementRequestMongoTests.cs`
8. `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/GskuChildAdmissionRetirementMongoTests.cs`
9. `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/FinishedGoodIdentityWorkflowStorageMongoTests.cs`
10. `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/DependencyInjectionSmokeTests.cs`

Each foreground/background caller supplies a verified discriminant; each repository physically enforces the same
rollout token/generation in the business transaction. A handler/processor call alone is not enrolment, and background
processors do not pass through request-scoped MediatR behavior. After A0, A1 and A2 pass, only then may Slice C add the
internal Enforced FG v2 admission reserve. No public endpoint, workflow activation or all-writer-complete claim follows.

**Slice B — FG-specific actor boundary; shared behavior unchanged — bounded code-start approved 2026-09-11.**

Existing shared `IProductIdentityLifecycleActorContext` and `ProductIdentityLifecycleActorContext` remain unchanged so
Global Product/GSKU/LSKU/ABB retain current actor and ordinal-comparison behavior. Exact new paths:

23. `services/Diten.MdmService/src/Diten.MdmService.Application/Contracts/IFinishedGoodHumanAdmissionContext.cs`
24. `services/Diten.MdmService/src/Diten.MdmService.Infrastructure/Security/FinishedGoodHumanAdmissionContext.cs`
25. `services/Diten.MdmService/src/Diten.MdmService.Infrastructure/DependencyInjection.cs`

This is the only executable sub-slice authorized by this §21.15 amendment. The exact minimal Application contract is
frozen before implementation as follows; it introduces no `HttpContext`, `ClaimsPrincipal`, token, header, permission
parameter, public DTO or additional model:

```csharp
namespace Diten.MdmService.Application.Contracts;

public interface IFinishedGoodHumanAdmissionContext
{
    bool TryResolveSubmitter(out Guid tenantId, out Guid subjectId);
}
```

`TryResolveSubmitter` returns `true` only after the complete authenticated human, tenant and exact server-owned submit
permission boundary succeeds. Every failure returns `false` with **both** `tenantId` and `subjectId` equal to
`Guid.Empty`; callers must never observe or use a partial identity. The Infrastructure implementation is scoped and
may consume only the already validated request principal, `IHttpContextAccessor` and `ITenantContext`. It must enforce:

1. an authenticated principal and exactly one ordinal `actor_type=tenant_user`; missing, duplicate,
   `service`, `platform_admin`, `partner_admin`, case drift and every other value fail closed;
2. at most one ordinal `sub` and at most one ordinal `ClaimTypes.NameIdentifier`, with at least one present; each
   present value is a non-empty canonical `D`-format GUID and both, when present, identify the same subject;
3. exactly one ordinal `tenant_id`, a non-empty canonical `D`-format GUID equal to resolved
   `ITenantContext.TenantId`; unresolved/empty context fails closed;
4. no `X-Tenant-Id` header is required, but when present it has exactly one non-empty canonical `D`-format value,
   contains no comma-list, and equals both the JWT tenant and `ITenantContext.TenantId`; header-only authority is
   forbidden;
5. the server-owned ordinal value `mdm.finished-goods.submit` only. Existing `permission` and `permissions` claim-type
   transport forms remain consumable, including their existing comma/space/semicolon tokenization, but each candidate
   value is compared with `StringComparison.Ordinal`; missing, read/create, wildcard, prefix and case-drift values do
   not substitute. No caller supplies a permission or module name; and
6. no provider/repository call, writer lease, mutation, lifecycle/workflow action or audit production. Principal,
   header, bearer, token and credential values are neither logged nor persisted.

Exact Slice B runtime write allow-list:

1. `services/Diten.MdmService/src/Diten.MdmService.Application/Contracts/IFinishedGoodHumanAdmissionContext.cs` — new,
   exact interface above only.
2. `services/Diten.MdmService/src/Diten.MdmService.Infrastructure/Security/FinishedGoodHumanAdmissionContext.cs` — new,
   exact fail-closed implementation only.
3. `services/Diten.MdmService/src/Diten.MdmService.Infrastructure/DependencyInjection.cs` — only the scoped context
   registration and its required using.

Exact Slice B writable test allow-list:

1. `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/FinishedGoodHumanAdmissionContextTests.cs` — new;
   all positive claim/header transport forms and every actor/subject/tenant/permission rejection above, including
   both outputs remaining empty on every rejection.
2. `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ProductIdentityLifecycleActorContextTests.cs` —
   preservation assertions only; the shared implementation remains byte-for-byte unchanged.
3. `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/DependencyInjectionSmokeTests.cs` — only scoped
   resolution of the new interface plus preservation of existing registrations.

Slice B explicitly excludes provider/FU21 transport, parent or Legal Entity reads, scope evaluation, writer lease,
snapshot/reservation, repository/persistence, lifecycle handler/API/manifest, background authority, Auth/Platform/
Legal Entity changes, Gateway/frontend and operational configuration. A successful context means only that local
human/tenant/exact-permission resolution passed; it is not admission, workflow authority or current-grant revocation
proof. Slice A/C remain blocked from code-start.

**Slice C — snapshot plus ordered atomic local reservation; internal only.**

Changed existing runtime paths:

26. `services/Diten.MdmService/src/Diten.MdmService.Domain/ValueObjects/FinishedGoodLifecycleAdmissionScopeSnapshot.cs`
27. `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/FinishedGoodIdentityWorkflowOperation.cs`
28. `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/FinishedGoodIdentityWorkflowOperationResults.cs`
29. `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/IFinishedGoodIdentityWorkflowOperationRepository.cs`
30. `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/FinishedGoodIdentityWorkflowOperationRepository.cs`
31. `services/Diten.MdmService/src/Diten.MdmService.Persistence/DependencyInjection.cs`

New runtime paths:

32. `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Lifecycle/FinishedGoodIdentityAdmissionModels.cs`
33. `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Lifecycle/Commands/ReserveFinishedGoodIdentityAdmissionCommand.cs`
34. `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Lifecycle/Handlers/CommandHandlers/ReserveFinishedGoodIdentityAdmissionHandler.cs`
35. `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Lifecycle/Validators/ReserveFinishedGoodIdentityAdmissionValidator.cs`

No public controller/manifest path is allowed in the first slice. No Auth/Platform runtime delta is proposed for
tenant-user-only admission. Consumed unchanged: `ITenantContext`, trusted provider/client, candidate facade, evaluator,
P0A FU21 pair, and P1A collection/index/claim/recovery/BSON behavior. The broad query consumer guard is not admission
evidence. Mongo `$$NOW` supplies the admission-evaluation instant only; it is not an authoritative commit time.

Future Slice A/C planning test inventory follows. It is not current code-start authority. For the approved Slice B
work package, only the three exact writable test paths stated in the Slice B block above are writable:

1. `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/FinishedGoodHumanAdmissionContextTests.cs` — new
2. `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ProductLegalEntityScopeCandidateFacadeTests.cs`
3. `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/FinishedGoodIdentityLifecycleContractTests.cs`
4. `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/FinishedGoodIdentityWorkflowStorageMongoTests.cs`
5. `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/FinishedGoodIdentityWorkflowAdmissionHandlerTests.cs` — new
6. `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/FinishedGoodIdentityWorkflowAdmissionMongoTests.cs` — new
7. `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ProductLegalEntityScopeWriteAdmissionContractTests.cs`
8. `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ProductLegalEntityScopeWriteAdmissionMongoTests.cs`
9. `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ProductLegalEntityScopeActivationMongoTests.cs`
10. `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/LegalEntityCommandTests.cs`
11. `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/LegalEntityScopeWriteFenceMongoTests.cs` — new MOD-0220 amendment test
12. `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/FinishedGoodAuthorizationTests.cs`
13. `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/DependencyInjectionSmokeTests.cs`
14. `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ProductIdentityLifecycleActorContextTests.cs` — preservation assertions only
15. `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ProductLegalEntityScopeMongoTests.cs` — only if the affected tests cannot consume `AuditIntentTemporalMongoFixture.ReplicaConnectionString` directly; the sole allowed change is removing its application-`localhost:27017` fallback at lines 463-475 in favor of the supplied test-owned replica connection.

Run unchanged as dependency regressions, not writable scope:

- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/Authorization/PlatformTrustedLegalEntityScopeProviderClientTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/Authorization/TrustedLegalEntityScopeProviderContractTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/Authorization/TrustedLegalEntityScopeDelegatedTokenForwardingTests.cs`
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Authorization/TrustedLegalEntityScopeRequestExecutorTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/FinishedGoodApiContractTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ModuleRegistration/ProductItemSkuMasterManifestProviderTests.cs`

Order: exact human/permission → provider and actor echo → tenant parent chain → local LE narrowing → rollout/policy/
period → acquire/validate common lease → initial ordering write → bounded reads → final server admission instant and
period check → v2 snapshot/hash → reserve transaction commit → exact outcome read-back → separate token/generation
lease release → repo DI. Public API/manifest are deliberately absent. No later step manufactures skipped evidence.

#### 21.15.5 Measurable acceptance matrix

| Case | Required result / side-effect boundary | Minimum evidence |
|---|---|---|
| Enforced Scoped | Canonical finite intersection persisted once in Prepared reservation. | Unit + existing-fixture real Mongo. |
| Enforced GroupWide | Mode remains GroupWide; snapshot stores finite provider/local set; empty denies. | Unit + provider contract + real Mongo. |
| Service/platform/partner actor | Approved first slice rejects before provider/parent/reserve. | FG-specific actor/security unit + HTTP-shape; real transport separate. |
| Duplicate/conflicting subject or actor/provider drift | Fail closed; provider/repository/reserve counts zero. | Strict unit mocks + actor tests. |
| Tenant mismatch | Non-disclosing denial; no cross-tenant read/write/reserve. | Unit + real-Mongo isolation. |
| Wrong permission | Only exact submit succeeds; missing/read/create/wildcard/prefix/case variants deny before scope. | Actor unit + unchanged FU21 regression. |
| Provider failure | Echo/time/timeout/body/list violations and confirmed empty deny; no partial fallback. | Provider client/contract; mocks are not real transport. |
| Parent failure | Foreign/deleted/non-referenceable/changed chain, non-Draft FG or stale version deny with zero reserve. | Unit + real Mongo. |
| Local LE failure | Invalid IDs narrow; empty denies; no truncation/wildcard. | Candidate tests + real Mongo. |
| Missing/legacy evidence | No rollout, Preparation, Suspended, no policy/current period produces no snapshot/reserve. | Unit + real-Mongo counts. |
| Preparation/legacy→Enforced race | Admission reads before the transition barrier; both operations contend on the rollout document. Result is one coherent Enforced set or zero-write denial; no permissive legacy snapshot. | Barrier-controlled real Mongo with exact rollout/lease state assertions. |
| Policy/rollout race | Admission pauses after first read and before reserve while the other operation writes a different business document but must acquire the same tenant lease. Exactly one ordering wins; stale admission never persists. | Transaction/barrier real Mongo; an operation-document insert race or mock precheck is insufficient. |
| Parent lifecycle race | Global Product/revision/GSKU/FG semantic invalidator crosses the same post-read/pre-reserve barrier. Until exact writer enrolment exists this case must stay red/block code-start; after amendment it yields a matching link/status/fence set or zero-write conflict. Parent Version-only drift follows D3 and is not silently treated as an invalidator. | Per-writer barrier real Mongo plus exact semantic-state and captured-version read-back. |
| Legal Entity race | Suspend/Archive/Delete, or the current stale whole-document Update, crosses the post-local-read/pre-reserve barrier on a different Legal Entity document. Until MOD-0220 hardening/enrolment exists this case stays red; afterward a now-unreferenceable ID cannot enter the snapshot. Activate is an expansion regression only. | `LegalEntityScopeWriteFenceMongoTests` plus admission Mongo test; assert both different documents and common lease ordering. |
| Effective-period expiry race | Admission instant is before/at/after the exclusive end under barriers; the final bound check rejects an expired period and creates no operation. | Test-owned server-time real Mongo; no process-clock-only mock. |
| Lease expiry/stale owner | Current exact same command/actor/kind/fingerprint acquisition replay is preserved even after expiry; a different command remains denied, and no takeover exists. If separately approved after full physical fencing, acquisition first changes to Mongo-server-authored acquired/expiry time with exact read-back; recovery-only takeover then uses one server-time CAS over the old token/generation/expiry/identity/phase, increments the durable generation in that CAS, and makes the old owner fail bind/business-CAS/outcome-commit/release. Ambiguous ownership remains fail-closed. | Current exact-replay/different-identity regressions; conditional skewed-client-clock and takeover-vs-stale-owner barrier real Mongo plus contract tests for every guarded repository. |
| Exact replay | Reauthorize now; return original snapshot/times/hash with unchanged row count/version. | Unit + real Mongo. |
| Replay drift/revocation | Drift conflicts; revocation denies continuation without rewriting history. | Unit + provider contract + real Mongo. |
| Rejection effects | Zero reserve, workflow start, lifecycle mutation and lifecycle audit intent. | Strict mocks + real-Mongo counts. |
| Immutability | Defensive copy, ordering, hash, stale/legacy/fact-drift rejection, unknown BSON preservation. | Domain + P1A Mongo regression. |
| V1 compatibility | V1 remains readable as historical storage where supported but continuation is denied without manufacturing v2 facts; row/hash/version are not rewritten. Operational v1 count remains unknown until separately authorized inventory. | Domain + P1A Mongo regression; no live inventory claim. |
| Limits | 200 accepted if full BSON ≤1 MiB; 201/duplicate/empty/>1 MiB fails atomically, no truncation. | Domain + real-Mongo BSON tests. |
| Internal exposure | No controller or manifest mutation; Prepared is observable only through the internal contract and is not workflow submission. | API/manifest regression proves no new endpoint/action. |

Exact candidate-test traceability (names are proposed and must be used or replaced by equally explicit names in the
same exact files before code-start):

| Acceptance rows | Exact test file and proposed test method(s) |
|---|---|
| Verified writer authority | `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ProductLegalEntityScopeWriterAuthorityTests.cs` — `ResolveAsync_WrongTenantPurposeOperationAggregateOrMutation_Denies`, `ResolveAsync_HumanOrMakerImpersonation_Denies`, `ResolveAsync_EvidenceReadProof_DoesNotAuthorizeMutation`, `ResolveAsync_MissingForgedOrRevokedServiceProof_Denies`; foreground, background and recovery discriminants are tested separately. |
| Atomic repository enforcement | `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ProductLegalEntityScopeWriteAdmissionContractTests.cs` — `GuardedRepositories_RequireExactVerifiedAuthorityAndSession`; `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ProductLegalEntityScopeWriteAdmissionMongoTests.cs` — `GuardedWrite_DirectRepositoryBypass_Denies`, `GuardedWrite_RolloutAndBusinessCasShareTransaction`, `GuardedWrite_WrongTokenGenerationOrMutation_RollsBackAllEffects`. |
| Crash/recovery outcomes | `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ProductLegalEntityScopeWriterRecoveryMongoTests.cs` — `Recovery_LeaseWithoutBaseline_RetainsUnlessNoWorkProven`, `Recovery_BoundBeforeWriteOrVerifiedAbort_QualifiedReleaseOnly`, `Recovery_CommitResponseLost_ReadsExactOutcomeOrRetains`, `Recovery_CommitSucceededReleaseFailed_ReleasesWithoutRedo`, `Recovery_UnknownOrLegacyState_RequiresManualReconciliation`. Takeover-specific methods remain conditional on D2 approval. |
| P1A/FU03 separation | `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/FinishedGoodIdentityWorkflowStorageMongoTests.cs` — `OperationLease_DoesNotAuthorizeSourceMutationOrReplaceTenantWriterLease`; current claim/replay tests remain regressions. |
| Semantic-field versus generic Version | Parent repository tests plus `ProductLegalEntityScopeWriteAdmissionMongoTests.cs` — `Admission_NameOrCorrectionOnlyVersionDrift_FollowsApprovedPredicateDecision`, `Admission_RetirementOrReferenceabilityContraction_IsAlwaysFenced`; the test expectation is blocked until D3 is approved. |
| Enforced Scoped / GroupWide / local LE | `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/FinishedGoodIdentityWorkflowAdmissionHandlerTests.cs` — `ReserveAsync_EnforcedScoped_PersistsExactFiniteIntersection`, `ReserveAsync_EnforcedGroupWide_PersistsFiniteCandidates`, `ReserveAsync_EmptyIntersection_DeniesWithoutEffects`; `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/FinishedGoodIdentityWorkflowAdmissionMongoTests.cs` — `ReserveAsync_EnforcedScope_PersistsOnePreparedOperation`. |
| Actor/subject/tenant failures | `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/FinishedGoodHumanAdmissionContextTests.cs` — `Resolve_DuplicateOrConflictingTenantSubject_Denies`, `Resolve_HeaderOnlyTenant_DeniesForAdmission`, `Resolve_ServicePlatformOrPartnerActor_DeniesFinishedGoodAdmission`; handler tests — `ReserveAsync_ActorOrProviderSubjectMismatch_DeniesBeforeEffects`; Mongo tests — `ReserveAsync_CrossTenantParent_DoesNotReadOrWrite`. |
| Exact permission / shared actor regression | FG actor tests — `HasExactPermission_SubmitOnly_IsOrdinal`; handler tests — `ReserveAsync_MissingSubstituteWildcardOrCaseDriftPermission_DeniesBeforeScope`; unchanged `ProductIdentityLifecycleActorContextTests` proves Global Product/GSKU/LSKU/ABB behavior is unchanged; unchanged Platform executor tests remain the FU21 component regression. |
| Provider failure/freshness | `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/Authorization/PlatformTrustedLegalEntityScopeProviderClientTests.cs` — existing echo/time/body boundary tests; `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ProductLegalEntityScopeCandidateFacadeTests.cs` — existing canonical/local narrowing tests; handler tests — `ReserveAsync_ProviderFailureOrEmpty_DeniesBeforeReserve`. |
| Parent/status/version | Handler tests — `ReserveAsync_ForeignDeletedOrBrokenParent_Denies`, `ReserveAsync_NonDraftOrStaleFinishedGoodVersion_ConflictsWithoutEffects`; Mongo tests — `ReserveAsync_ParentSemanticChainAndExpectedFinishedGoodVersion_AreTenantAtomic`; parent-only generic Version drift follows the explicit D3 test row. |
| Missing/legacy evidence | Handler tests — `ReserveAsync_MissingPreparationSuspendedOrNoPolicy_DeniesWithoutSnapshot`; Mongo tests — `ReserveAsync_LegacyToEnforcedRace_NeverPersistsLegacyAdmission`. |
| Read→reserve races | Admission Mongo tests — `ReserveAsync_DifferentOperationDocuments_CommonRolloutWriteSerializes`, `ReserveAsync_ParentForegroundAndBackgroundInvalidatorAfterReadBeforeReserve_CommitsMatchingSemanticSetOrNothing`, `ReserveAsync_EffectivePeriodExpiresAtBarrier_CreatesNoOperation`; scope Mongo tests — `AdmissionFence_ConcurrentPolicyCreateReplaceEnd_AreOrderedInBothDirections`, `AdmissionFence_ConcurrentRolloutTransition_IsOrderedInBothDirections`; Legal Entity fence tests — `AdmissionFence_ConcurrentSuspendArchiveDeleteAndStaleUpdate_AreOrdered`. Each test writes distinct business documents and asserts the common rollout lease/token/generation, not merely the operation unique index. |
| Lease expiry/stale owner | Scope write-admission contract/Mongo tests — required now: `WriterLease_ExpiredExactIdentity_ReplaysWithoutGenerationReset`, `WriterLease_DifferentIdentity_ExpiredOrUnexpired_DeniesWithoutMutation`, `WriterLease_ReleaseThenReacquire_IncrementsDurableGeneration`; conditional on a later owner-approved liveness amendment: `WriterLease_AcquireAndExpiry_UseServerTimeDespiteSkewedClientClock`, `WriterLease_ExpiredTakeover_UsesServerTimeExactRecoveryCas`, `WriterLease_StaleOwnerCannotBindMutateCommitOrRelease`, `WriterLease_AmbiguousOutcome_RetainsForManualReconciliation`; repeat against each parent/Legal Entity guarded repository seam. |
| Replay/revocation/drift | Handler tests — `ReserveAsync_ExistingOperation_ReauthorizesWithoutRecapture`, `ReserveAsync_CurrentRevocation_DeniesWithoutHistoryRewrite`, `ReserveAsync_ActorPayloadParentIdentityOrTargetVersionDrift_Conflicts`; Mongo tests — `ReserveAsync_Replay_PreservesAdmissionInstantSnapshotHashAndRowVersion`, `ReserveAsync_AmbiguousCommit_ReadsBackFirstSnapshotWithoutRegeneratingTimeOrHash`. Parent-only generic Version drift follows D3. |
| Zero side effects | Handler tests — `ReserveAsync_EachRejectedBoundary_HasZeroReserveWorkflowLifecycleAndAuditCalls`; Mongo tests — `ReserveAsync_Rejection_LeavesOperationAndAuditCollectionsEmpty`. |
| Immutability/limits/BSON/V1 | `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/FinishedGoodIdentityLifecycleContractTests.cs` — `AdmissionSnapshotV2_IsCanonicalImmutableAndRejectsInvalidBounds`; `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/FinishedGoodIdentityWorkflowStorageMongoTests.cs` — `ReserveAsync_V2PreservesUnknownFieldsAndOneMiBBound`, `ReserveAsync_LegalEntityBoundary200Accepted201RejectedAtomically`, `ReadV1_DoesNotUpgradeAndContinuationFailsClosed`. |
| Writer inventory / DI / internal-only API/manifest | `ProductLegalEntityScopeWriteAdmissionContractTests.cs` expands the current frozen foreground inventory to every admitted command and background/recovery entry point that reaches guarded repositories; `DependencyInjectionSmokeTests.cs` — `FinishedGoodAdmissionRepository_IsRegistered`; existing API/manifest tests run unchanged and prove no submit endpoint/action was added. |

The MDM→Platform real-transport acceptance row remains **PENDING and blocks transport acceptance** for this slice.
The three unchanged MDM provider/delegated-token tests and Platform executor test prove components/HTTP shape only;
their totals must not be presented as live or real transport evidence.

Real-Mongo tests use the existing `AuditIntentTemporalMongoFixture`, serialized
`ProductLegalEntityScopeMongoCollection`, fixed `diten_mdm_product_scope_itest`, fresh TenantId and tenant-owned
cleanup. No GUID database, new harness, application `localhost:27017` fallback, Platform schema copy, guard exception,
service start or operational data. Report discovery and passed/failed/skipped per non-overlapping run. Fake HTTP
handlers prove shapes only; real MDM→Platform transport is a separate Development live-smoke gate.

#### 21.15.6 Exclusions, non-claims and review record

Except for the completed exact bounded Slice B context/DI/tests and the separately approved/evidenced A0 foreground
Replace boundary, excluded: A1/A2/Slice C, workflow start/transport, terminal apply, background authority, draft
cancellation, correction/retirement, audit production/delivery changes,
UI/L10n/Gateway/navigation, provisioning, config/data, migration/cutover, service start, live WorkCenter/browser
acceptance and Production/Staging. P1A historical flakiness remains `UNKNOWN`; no worker activation or lifecycle
completion is authorized.

Historical prior §21.15 planning review chain (completed before
`FG-WRITER-AUTHORITY-RECOVERY-PLAN-01`; its PASS does not verify the current amendment):

- `module-pack-author`: sole writer of §21.15.
- `orchestrator`: read-only scope/dependency coordination — `CONDITIONAL PASS`, planning-only.
- combined `data-agent` + `backend-architect`: transaction/fence/time/fingerprint and current writer-path review —
  `CONDITIONAL PASS`, planning-only.
- `security-agent`: actor/tenant/scope/replay/race — `CONDITIONAL PASS`, planning-only.
- `testing-agent`: measurable matrix/harness discipline — `CONDITIONAL PASS`, planning-only.
- independent `read-only-auditor`: first verdict `CONDITIONAL` identified final-period timing, false marker-enrolment
  wording and release-order defects; after the author corrected only §21.15, the second narrow verdict was `PASS —
  planning-only` with no open finding.

`FG-WRITER-AUTHORITY-RECOVERY-PLAN-01` review chain (2026-09-12):

- `module-pack-author`: sole writer of the current §21.15 authority/recovery amendment.
- `orchestrator`: read-only sequential coordination — `CONDITIONAL PASS`, planning-only.
- `backend-architect`: invalidating writer/caller/repository and current generation review — `CONDITIONAL PASS`,
  planning-only.
- `security-agent`: foreground/background/recovery proof, impersonation and expiry semantics — `CONDITIONAL PASS`,
  planning-only.
- `data-agent`: same-session rollout/business CAS, server-time and crash recovery review — `CONDITIONAL PASS`,
  planning-only.
- `testing-agent`: authority, bypass, race, crash and existing-fixture acceptance matrix — `CONDITIONAL PASS`,
  planning-only; no test was run.
- independent `read-only-auditor`: first verdict `CONDITIONAL` identified the missing exact foreground authority
  producer, unsafe server-time takeover claim over client-authored expiry, inaccurate P1A claim line evidence and an
  ambiguous historical review record. The author corrected only §21.15; the second narrow verdict was `PASS —
  planning-only` with no open finding.

The `FG-WRITER-AUTHORITY-RECOVERY-PLAN-01` authoring turn itself changed no runtime/test code and performed no
build/test, Mongo/service operation or Git stage/commit/push. A later, separately approved
`FU03-A0-FOREGROUND-GUARDED-REPLACE-01` turn produced the bounded A0 evidence recorded above; it did not authorize
A1/A2/Slice C. Previous §21 sections and P0A/P1A evidence remain unchanged; `.testoutput/**` remains untracked evidence
outside this amendment.
