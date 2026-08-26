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
