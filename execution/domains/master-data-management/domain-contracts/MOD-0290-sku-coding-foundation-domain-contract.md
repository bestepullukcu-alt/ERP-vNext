---
module_id: MOD-0290
name: SKU & Coding Foundation Domain Contract
document_type: Domain Contract
status: draft
owner_domain: master-data-management
owners: product-data-owner / enterprise-architect
parent_dcp: execution/portfolio/delivery-capability-packs/DCP-004-mod-0290-sku-coding-foundation-readiness.md
canonical_blueprint: docs/System Capability & Implementation Blueprint - master 8.1.xlsx
code_authority: none
module_pack_authority: none
---

# MOD-0290 SKU & Coding Foundation — Domain Contract

> **Draft-only guard:** This document is the first-phase Product/SKU model detail for DCP-004. It is not a Module
> Pack, does not authorize runtime implementation, and does not promote DCP-004 or any Module Pack status.
>
> **Authority chain:** DCP-004 is the formal approval artifact for locked architecture, scope, ownership, sequencing
> and gates. This Domain Contract is its controlled supporting design record and detailed field elaboration. It is not
> an independent governance authority, does not replace DCP approval and cannot weaken a DCP decision.

> **Superseding controlled-reference storage decision (2026-08-07):** For the exact first-GSKU families
> `pack-applicability` and `uom`, MOD-0048 supplies one global, code-owned, deployment-versioned catalog shared by all
> tenants. These families are not tenant-owned BRD records and require no reference tenant, assignment or publish
> operation. Existing `ReferenceCatalogSelection` fields remain: they persist deterministic provider version identity,
> positive version number, resolution mode and resolution time so historical GSKU facts remain explainable. Tenant-
> owned reference families continue to use their separately approved provider lifecycle.

## 1. Purpose and authority

This contract defines the first-phase Product/SKU identity boundary, aggregates, cardinalities, fields and business
invariants that a later MOD-0290 Module Pack must respect.

For MOD-0290 business, architecture, domain, field-model and Module Pack decisions, the sole Blueprint authority is
`docs/System Capability & Implementation Blueprint - master 8.1.xlsx`:

- `Blueprint_Data!A291:AG291` defines MOD-0290 Product / Item / SKU Master;
- `SoR_Map!A256:E256` assigns product master records, item master records, SKUs and UoM mappings to MOD-0290;
- `Dependencies!A1281:D1285` lists the direct Blueprint dependencies MOD-0003, MOD-0040, MOD-0021, MOD-0252 and
  MOD-0253.

Master 7 is only a legacy verifier/tool-compatibility input. It does not determine this contract and is not proof of
Master 8.1 alignment. Repository-wide AGENTS.md/DCP-002/registry/verifier wording cleanup remains visible governance
work but does not block DCP-004 approval or MOD-0290 Module Pack draft authoring. MOD-0040 reconciliation remains a
separate ready-for-dev/code-start gate under DCP-004.

## 2. Scope

### 2.1 In scope

- tenant-scoped Product/SKU system of record;
- Global Product, Product Definition Revision, GSKU, LSKU, Finished Good, MarketTradeName, LegacyAlias and
  CodeReservation;
- immutable, system-generated and non-reusable canonical codes for Global Product, GSKU, LSKU and Finished Good;
- controlled Product/SKU descriptors and scalar strength presentation;
- explicit GSKU pack applicability and scalar pack presentation;
- Product/SKU identity lifecycle, maker-checker approval and child-first retirement;
- effective-dated MarketTradeName replacement;
- manual legacy onboarding through LegacyAlias;
- tenant isolation, technical soft-delete and optimistic concurrency invariants.

### 2.2 Non-goals

The first phase does not own or persist Composition, active-substance formulation, MA, Registered Presentation,
artwork/label/leaflet lifecycle, packaging hierarchy, BOM, manufacturing version, quality specification,
batch/release, GTIN lifecycle, direct LSKU–Finished Good linkage, bulk migration, ERP/PLM feeds or runtime external
contract publication.

## 3. Canonical glossary and ownership

| Term | Contract meaning | SoR |
|---|---|---|
| Global Product | Stable tenant product identity above revision and SKU levels | MOD-0290 |
| Product Definition Revision | Explicitly referenced Product Definition revision; no automatic current revision | MOD-0290 |
| GSKU | Global SKU identity for one Product Definition Revision | MOD-0290 |
| LSKU | Market-context SKU identity under one GSKU | MOD-0290 |
| Finished Good | Finished-Good identity bound to exactly one GSKU | MOD-0290 |
| MarketTradeName | LSKU-owned market/language/effective-period name record | MOD-0290 |
| LegacyAlias | Raw legacy identifier retained for lookup and traceability; never canonical output | MOD-0290 |
| CodeReservation | Durable reserve/consume/cancel/expire ledger for canonical-code allocation | MOD-0290 |
| Reference-data hosting/publish | Governed value-set hosting and publication | Reconciled Reference Data owner per DCP G2/G3 |
| Product/SKU semantics and UoM mapping | Applicability, validation and Product/SKU meaning of controlled values | MOD-0290 |
| Legal Entity lifecycle/referenceability | Current Legal Entity master and referenceability | MOD-0220; MOD-0290 only stores an approved LSKU reference |
| Workflow run/task history | Approval orchestration records | MOD-0023; MOD-0290 remains identity-state SoR |
| Central audit retention/query | Central audit-event service | MOD-0021; MOD-0290 retains local mutation consistency responsibility |

## 4. Aggregate, relationship and cardinality contract

| Parent / owner | Child / reference | Cardinality | First-phase invariant |
|---|---|---:|---|
| Global Product | Product Definition Revision | 1 → 0..* | Revision approval requires an Identity Approved parent |
| Product Definition Revision | GSKU | 1 → 0..* | Every GSKU has exactly one explicit revision parent |
| GSKU | LSKU | 1 → 0..* | Every LSKU has exactly one GSKU parent |
| GSKU | Finished Good | 1 → 0..* | Every Finished Good references exactly one GSKU |
| LSKU | MarketTradeName | 1 → 0..* | Names are separated by market, approved language and effective period |
| Canonical identity | LegacyAlias | 1 → 0..* | Alias target must be same-tenant and must not replace canonical code |
| CodeReservation | Canonical identity | 1 reservation → 0..1 consumed identity; 1 code-bearing identity → exactly 1 consumed reservation | A reservation may be consumed by at most one matching same-tenant identity; every code-bearing identity requires exactly one matching consumed-reservation proof |
| LSKU | Finished Good | No direct relationship | Future relationship may exist only through Market Supply Assignment (BL-018) |
| Product Definition/GSKU | Composition | No first-phase relationship | Composition ID/FK/placeholder is prohibited (BL-015) |

## 5. Shared identity invariants

The Product identity lifecycle applies to Global Product, Product Definition Revision, GSKU, LSKU, Finished Good and
MarketTradeName. LegacyAlias uses its separate `ACTIVE → RETIRED` lifecycle, and CodeReservation uses the separate
state machine in §7.

| Shared field | Type | Decision | Contract |
|---|---|---|---|
| `Id` | UUID, exactly 1 | System-derived | Immutable technical identity |
| `TenantId` | UUID, exactly 1 | System-derived | Set from trusted server-side tenant context; never accepted from client payload |
| `LifecycleStatus` | Controlled enum, exactly 1 | System-derived | Changed only by lifecycle commands; values are defined in §14 |
| `Version` / concurrency token | Integer or opaque token, exactly 1 | System-derived | Every mutation uses expected-version matching; last-write-wins is prohibited |
| `CreatedAt`, `UpdatedAt` | UTC timestamp | System-derived | Technical evidence; not user-editable |
| `IsDeleted`, `DeletedAt` | Boolean + UTC timestamp | System-derived | Technical soft-delete; never substitutes for business retirement |

Identity records and aliases are tenant-scoped. Cross-tenant lookup or mutation fails closed without revealing whether
the target exists. All unique indexes and duplicate checks include `TenantId`. Soft deletion never frees a canonical
code or permits code reuse.

`TenantId` exists only in trusted server-side, read, audit or internal representations. Create/edit/command DTOs and
API write contracts do not contain it. A client payload that supplies `TenantId` is rejected fail-closed; it is never
accepted or used as tenant context.

## 6. Canonical-code policy

### 6.1 Code-bearing entities and namespace

`CanonicalCode` is required for Global Product, GSKU, LSKU and Finished Good. These four entity types share one
tenant-wide canonical-code namespace.

The code is:

- generated only by MOD-0290;
- allocated only through the common CodeReservation ledger and assigned by consuming a matching reservation;
- immutable after reservation consumption/assignment;
- low-semantic: immutable entity-type prefix plus opaque system sequence;
- unique across all four code-bearing entity types within the tenant;
- non-reusable after reservation, consumption, cancellation, expiry, retirement or technical soft-delete.

Direct `CanonicalCode` assignment, manual override and every reservation-bypass path are prohibited. `EntityType`
may select the allocator/prefix, but it never narrows the shared tenant-wide uniqueness scope. The common ledger owns
uniqueness on `TenantId + ReservedCode` independently of entity type; no cross-collection Mongo unique-index
capability is assumed.

The code must not contain product, pack, site, manufacturer, country, market, MA, lifecycle or changeable
organization segments. MOD-0040 technical correlation/interface identity remains distinct from this business-code
namespace.

### 6.1.1 Internal identity versus future SOP-controlled identity

The shared `Id` in §5 is the immutable technical/internal aggregate identity. `CanonicalCode` is a separate,
low-semantic, system-generated MOD-0290 business identifier allocated by `CodeReservation`. Neither field is assumed
to be a future SOP-controlled material, FPF, FPP or artwork code, and neither carries that code's revision.

GMG-SCM-SOP-0001 examples such as `FPF-...-V1`, `FPP-...-V1` and `LF-...-V1` require a later owner-approved contract
under DCP-005. That contract must decide the relationship among permanent internal UID, internal `CanonicalCode`,
controlled base code, controlled revision and `LegacyAlias`. This domain contract adds no runtime field, placeholder
foreign key or Composition relationship for that future decision.

`RevisionIdentifier` remains only the immutable parent-scoped ordinal of a MOD-0290 Product Definition Revision. It
is not an SOP FPF/FPP/artwork revision. FPF is not automatically Product Definition Revision or GSKU, and FPP is not
automatically LSKU or Finished Good. Their mappings and cardinalities remain open DCP-005 decisions. The existing
prohibition on a direct LSKU-Finished Good relationship remains unchanged; any future linkage must use an approved
Market Supply Assignment contract.

### 6.2 Reservation policy

Reservation transitions are `RESERVED → CONSUMED`, `RESERVED → CANCELLED` or `RESERVED → EXPIRED`. Terminal codes
remain permanently unavailable. Reserve, consume, cancel and expire operations require stable command idempotency
and durable G4 audit evidence.

Every successfully created Global Product, GSKU, LSKU and Finished Good proves exactly one matching same-tenant
reservation in `CONSUMED` state. Allocation, consume and identity creation use one stable idempotent command flow;
an identity may not commit until the matching consume is durably established. Direct assignment and bypass remain
prohibited.

The reverse cardinality is intentionally asymmetric: a CodeReservation has zero or one consumed identity. Because
cross-collection transaction topology is not proven, an ambiguous identity write after durable consume remains
`PendingIdentityWrite` and requires reconciliation; it is never automatically burned merely because an identity lookup
returns no record. That reservation is never rolled back into the available pool, and its code is never reused. A
terminal burned-without-identity outcome is permitted only for a deterministically proven pre-insert failure or after a
separately owner-approved persistent recovery fence or transaction boundary makes a late identity insert impossible.
Failure reason, critical audit evidence, retry/reconciliation outcome and recovery disposition must remain durable. The
technical Mongo representation, index definitions and G4 consistency mechanism remain Module Pack decisions, but they
must implement these business invariants without weakening them.

## 7. Entity field catalog

Decision labels are normative: `Required`, `Conditional-open`, `Controlled-reference-required`, `System-derived`,
`Deferred` and `Prohibited`. A `Conditional-open` field is not a final requirement until its named owner approves the
open decision in §17.

### 7.1 Global Product

| Field | Type/cardinality | Decision | Mutability and validation |
|---|---|---|---|
| `GlobalProductId` | UUID, 1 | System-derived | Immutable |
| `TenantId` | UUID, 1 | System-derived | Server-side only |
| `CanonicalCode` | String, 1 | Required / System-derived | From exactly one matching consumed reservation; immutable; tenant-wide shared namespace; no-reuse |
| `StewardLabel` | Unicode text, 0..1 | Conditional-open | Draft-only edit; must not be presented as MarketTradeName |
| `LifecycleStatus` | Enum, 1 | System-derived | §14 lifecycle |
| `Version`, timestamps, soft-delete fields | Technical | System-derived | §5 and §16 |
| Market, MA, site, manufacturer or regulatory identity | — | Prohibited | Not persisted on Global Product in phase one |

### 7.2 Product Definition Revision

| Field | Type/cardinality | Decision | Mutability and validation |
|---|---|---|---|
| `ProductDefinitionRevisionId` | UUID, 1 | System-derived | Immutable; consumers use this explicit ID |
| `TenantId` | UUID, 1 | System-derived | Server-side only |
| `GlobalProductId` | UUID, 1 | Required | Same tenant; immutable after create; retired parent is not referenceable |
| `RevisionIdentifier` | String, 1 | Required / System-derived | Immutable ordinal label within its Global Product, initially formatted `REV-001`, `REV-002`, ...; does not imply current/effective status |
| `CreationCommandId` | UUID/string command identity, 1 | System-derived | Immutable stable idempotency identity shared with the first GSKU creation; never client-authored as a business field; used only for combined-write reconciliation |
| `ProductTypeCode` | Controlled code, 0..1 or 1 | Conditional-open | Controlled reference if approved; requiredness/applicability is not an implementation requirement until the Product Data and Reference Data owners approve it |
| `DosageFormCode` | Controlled code, applicability-dependent | Controlled-reference-required | ProductType-compatible published value |
| `RouteOfAdministrationCodes` | Controlled code collection, 0..* | Controlled-reference-required | Cardinality and ProductType applicability remain open |
| `StrengthRepresentationType` | Controlled code, applicability-dependent | Controlled-reference-required | Approval supports only scalar SIMPLE_STRENGTH or SIMPLE_CONCENTRATION direction |
| `StrengthValue` | Decimal, 0..1 | Conditional-open | Required and positive when scalar strength applies |
| `StrengthUomCode` | Controlled code, 0..1 | Controlled-reference-required | Required with `StrengthValue`; UoM meaning/mapping remains MOD-0290-owned |
| `LifecycleStatus` and technical fields | Technical | System-derived | §5, §14 and §16 |
| `EffectiveFrom`, `EffectiveTo` | Date/time | Deferred | BL-016 |
| `IsCurrent` | Boolean | Prohibited | Not persisted in phase one; consumers use explicit RevisionId |
| `CompositionId`, Composition FK/placeholder | UUID or equivalent | Prohibited | BL-015; no competing Composition SoR |

Scalar strength is a presentation descriptor only. It stores no ingredient, active-moiety, formula or per-ingredient
quantity. Complex/multi-active records may remain Draft but cannot enter identity approval.

### 7.3 GSKU

| Field | Type/cardinality | Decision | Mutability and validation |
|---|---|---|---|
| `GskuId` | UUID, 1 | System-derived | Immutable |
| `TenantId` | UUID, 1 | System-derived | Server-side only |
| `ProductDefinitionRevisionId` | UUID, 1 | Required | Same tenant; immutable after create; parent approval required at GSKU approval |
| `CanonicalCode` | String, 1 | Required / System-derived | From exactly one matching consumed reservation; shared tenant namespace; immutable and non-reusable |
| `PackApplicabilityCode` | Controlled code, 1 | Required / Controlled-reference-required | User-approved first contract: SetCode `pack-applicability`; initial catalog permits only `SCALAR_QUANTITY_APPLIES`; explicit published value is mandatory and null is invalid |
| `PackQuantity` | Positive decimal, 1 | Required | Required for every first-phase GSKU because the initial applicability catalog permits only scalar quantity |
| `PackUomCode` | Controlled code, 1 | Required / Controlled-reference-required | Required with `PackQuantity`; user-approved UoM SetCode is `uom` |
| `PackApplicabilitySelection` | Embedded `ReferenceCatalogSelection`, 1 | Required / System-derived evidence | Holds provider-resolved SetCode, ValueCode, catalog version identity/number, resolution mode and resolution time; draft may refresh `LATEST`, submit/approval freezes `PINNED` |
| `PackUomSelection` | Embedded `ReferenceCatalogSelection`, 1 | Required / System-derived evidence | Same contract for selected UoM; client cannot supply provider version identity, number, mode or timestamp |
| `CreationCommandId` | UUID/string command identity, 1 | System-derived | Same immutable idempotency identity as the created Revision; replay resumes the same pair and never creates a second Revision or GSKU |
| `LifecycleStatus` and technical fields | Technical | System-derived | §5, §14 and §16 |
| `PackagingLevelCode` / packaging hierarchy | Code/relationship | Deferred | BL-017 |
| `GTIN` lifecycle fields | — | Deferred | BL-022 |
| `CompositionId` or placeholder | UUID | Prohibited | BL-015 |

### 7.4 LSKU

| Field | Type/cardinality | Decision | Mutability and validation |
|---|---|---|---|
| `LskuId` | UUID, 1 | System-derived | Immutable |
| `TenantId` | UUID, 1 | System-derived | Server-side only |
| `GskuId` | UUID, 1 | Required | Same tenant; immutable after create; parent approval required at LSKU approval |
| `CanonicalCode` | String, 1 | Required / System-derived | From exactly one matching consumed reservation; shared tenant namespace; immutable and non-reusable |
| `MarketCode` | Controlled code, 1 | Controlled-reference-required | First LSKU phase uses the universal MOD-0048-FU01 `market` catalog with exact ISO 3166-1 alpha-2 `^[A-Z]{2}$` country codes; no request normalization or country-external region code |
| `LegalEntityId` | UUID, 0..1 or 1 | Conditional-open | Applicability/nullability unresolved; no universal requirement is assumed |
| `LifecycleStatus` and technical fields | Technical | System-derived | §5, §14 and §16 |
| `FinishedGoodId` | UUID | Prohibited | No direct phase-one LSKU–Finished Good relationship; BL-018 |
| MA / Registered Presentation fields | — | Prohibited | BL-019 |

If `LegalEntityId` is applicable, MOD-0290 stores only the reference. It does not copy Legal Entity master data.
Current referenceability, failure behavior and validation points remain governed by DCP-004 G6.

### 7.5 Finished Good

| Field | Type/cardinality | Decision | Mutability and validation |
|---|---|---|---|
| `FinishedGoodId` | UUID, 1 | System-derived | Immutable |
| `TenantId` | UUID, 1 | System-derived | Server-side only |
| `GskuId` | UUID, exactly 1 | Required | Same tenant; immutable after create; parent approval required at FG approval |
| `CanonicalCode` | String, 1 | Required / System-derived | From exactly one matching consumed reservation; shared tenant namespace; immutable and non-reusable |
| `StewardLabel` | Unicode text, 0..1 | Conditional-open | Draft-only; cannot represent market, MA, manufacturing or quality readiness |
| `LifecycleStatus` and technical fields | Technical | System-derived | §5, §14 and §16 |
| `LskuId` | UUID | Prohibited | BL-018 |
| Composition, BOM, manufacturing, quality, batch/release fields | — | Prohibited | BL-015 and BL-021 |
| GTIN lifecycle fields | — | Deferred | BL-022 |

### 7.6 MarketTradeName

| Field | Type/cardinality | Decision | Mutability and validation |
|---|---|---|---|
| `MarketTradeNameId` | UUID, 1 | System-derived | Immutable historical-row identity |
| `TenantId` | UUID, 1 | System-derived | Server-side only |
| `LskuId` | UUID, 1 | Required | Same tenant; immutable after create; LSKU parent required |
| `MarketCode` | Controlled code, 1 | Controlled-reference-required | Uses the same universal MOD-0048-FU01 `market` catalog and exact ISO 3166-1 alpha-2 country code as the owning LSKU |
| `LanguageCode` | Controlled code, 1 | Controlled-reference-required | Must be an approved language for the market |
| `Name` | Unicode text, 1 | Required | Draft-editable; approved value is never overwritten |
| `ProposedEffectiveFrom` | Date/time, 1 | Required | Draft proposal only; not part of approved timeline |
| `EffectiveFrom`, `EffectiveTo` | Date/time, 0..1 → approved period | System-derived | Set/closed only by controlled replacement approval |
| `ReplacesMarketTradeNameId` | UUID, 0..1 | Conditional-open | Explicit lineage persistence remains an owner decision |
| `IsCurrent` | Query result | System-derived | Calculated from approved timeline; not persisted |
| `LifecycleStatus` and technical fields | Technical | System-derived | §5, §14 and §16 |
| `IsUsed` | Boolean | Prohibited | Official downstream-use contract is BL-024 |

### 7.7 LegacyAlias

| Field | Type/cardinality | Decision | Mutability and validation |
|---|---|---|---|
| `LegacyAliasId` | UUID, 1 | System-derived | Immutable |
| `TenantId` | UUID, 1 | System-derived | Server-side only |
| `TargetEntityType` | Controlled internal type, 1 | Required | Must be an allowed MOD-0290 target type |
| `TargetEntityId` | UUID, 1 | Required | Same tenant; existing target identity |
| `RawAliasValue` | Unicode string, 1 | Required | Preserved exactly; never overwritten by normalization |
| `SourceSystemCode` | Controlled code, 0..1 or 1 | Conditional-open | Requirement and catalog remain open |
| `AliasTypeCode` | Controlled code, 0..1 | Conditional-open | Target-compatible type contract remains open |
| `NormalizedLookupKey` | String, 1 | System-derived | Case/space/punctuation lookup key; raw value remains authoritative evidence |
| `AliasStatus` | Enum, 1 | System-derived | Separate `ACTIVE → RETIRED` lifecycle; not part of Product identity approval states |
| Technical timestamps and soft-delete fields | Technical | System-derived | §5, §11 and §14; soft-delete never substitutes for authorized alias retirement |
| `CanonicalCode` | — | Prohibited | Legacy alias is never canonical output |
| Import batch/staging/migration fields | — | Deferred | BL-023 |

### 7.8 CodeReservation

| Field | Type/cardinality | Decision | Mutability and validation |
|---|---|---|---|
| `CodeReservationId` | UUID, 1 | System-derived | Immutable |
| `TenantId` | UUID, 1 | System-derived | Server-side only |
| `EntityType` | Controlled internal type, 1 | Required | One of Global Product, GSKU, LSKU or Finished Good; allocator/prefix selector only, never a uniqueness partition |
| `ReservedCode` | String, 1 | Required / System-derived | Common ledger is unique by `TenantId + ReservedCode` independent of entity type; immutable and permanently non-reusable |
| `ReservationState` | Enum, 1 | System-derived | `RESERVED`, `CONSUMED`, `CANCELLED` or `EXPIRED`; terminal `CONSUMED` may be identity-bound or burned-without-identity |
| `CommandId` / idempotency key | UUID/string, 1 | Required / System-derived | Stable per operation; duplicate request cannot allocate another code |
| `ReservedAt`, `ExpiresAt` | UTC timestamp | System-derived | Expiry duration/policy remains open |
| `ReservedByActorId` | Trusted actor reference, 1 | System-derived | Derived from authenticated actor, not request-body identity |
| `ConsumedEntityId` | UUID, 0..1 | System-derived | Matching same-tenant identity when creation succeeds; remains null while an ambiguous write is reconciliation-pending; terminal burn without identity requires the separately approved safe mechanism |
| `ConsumedAt` | UTC timestamp, 0..1 | System-derived | Required when the reservation reaches `CONSUMED`, including an owner-approved terminal burned outcome |
| `CancelledAt`, `CancellationReason` | Timestamp + reason, 0..1 | Conditional-open | Cancel authorization/reason policy remains open |
| `Version`, technical timestamps, soft-delete fields | Technical | System-derived | Soft-delete never frees the code |

## 8. Product Definition and controlled-reference contract

The first-phase controlled-reference families are ProductType, DosageForm, RouteOfAdministration,
StrengthRepresentationType, UoM and PackApplicability. For the GSKU entry slice, the user approved enterprise-global
business semantics, SetCodes `pack-applicability` and `uom`, initial PackApplicability ValueCode
`SCALAR_QUANTITY_APPLIES`, and initial UoM ValueCodes `C62`, `GRM`, `KGM`, `MLT` and `LTR`. These two exact families
are MOD-0048 code-owned universal catalog version `GSKU-UNIVERSAL-V1`; tenants cannot alter them and no reference-
tenant, assignment or publish lifecycle applies. Deterministic provider version evidence, authenticated access,
strict contract validation and consumer failure behavior remain mandatory. The values are not duplicated as an MDM
fallback or accepted from the browser.

MOD-0048 owns the universal catalog contract and deployment version. MOD-0290 owns Product/SKU applicability,
cross-field validation, UoM mapping and business semantics. Provider unavailability or missing required version fails
closed at submit/approval; it does not silently accept free text or stale/unapproved values.

## 9. Pack applicability

- `PackApplicabilityCode` is a locked first-phase required controlled reference. It is explicit and cannot be null.
- The initial approved catalog contains only `SCALAR_QUANTITY_APPLIES`; therefore every first-phase GSKU requires a
  positive `PackQuantity` and a compatible `PackUomCode`.
- Quantity-free, kit, hierarchy or other non-scalar presentation cases are not represented by a placeholder value;
  they remain deferred to the approved BL-017 packaging-hierarchy re-entry condition.
- When pack presentation does not apply, the explicit non-applicable value is retained; silent null is invalid.
- Packaging level and multi-level packaging hierarchy remain BL-017.

### 9.1 GSKU reference-selection evidence and first-create recovery

- Each first-phase GSKU persists two embedded `ReferenceCatalogSelection` values: one for `pack-applicability` and one
  for `uom`. Each carries `SetCode`, `ValueCode`, `CatalogVersionId`, `CatalogVersionNumber`, `ResolutionMode` and
  `ResolvedAtUtc`.
- On a Draft, provider resolution may set or refresh the selection with `ResolutionMode = LATEST`. At submit/approval,
  the provider returns the final resolved catalog version; the same selection becomes `PINNED` and is immutable.
- Provider-derived version identity, version number, mode and timestamp are never accepted from a client payload.
- `CreateFirstGskuDraft` uses one stable `CreationCommandId` across the Revision and GSKU. A partial or ambiguous write
  is reconciliation-pending; replay finds and completes the same pair rather than producing an independent empty
  Revision, a second ordinal or a second GSKU.

## 10. MarketTradeName timeline and replacement

Approved MarketTradeName periods use the half-open interval `[EffectiveFrom, EffectiveTo)`.

1. A replacement begins as a Draft proposal with `Name` and `ProposedEffectiveFrom`.
2. The proposal is not an active member of the approved timeline and does not close the existing approved row.
3. On approval, one controlled consistency boundary closes the old row at the new `EffectiveFrom`, adds the new
   approved row, and validates no overlap and no forbidden gap for the same LSKU + market + language.
4. Rejection or cancellation leaves the existing approved row unchanged.
5. Each approved market language has a separate record.
6. Former rows remain searchable and auditable.

Nitop → Nitopin within one market/language is a MarketTradeName replacement. It does not create a new Global Product
or GSKU. An approved `Name` is never overwritten.

## 11. LegacyAlias contract

- Manual legacy onboarding creates the applicable canonical identities through normal first-phase rules.
- LegacyAlias starts `ACTIVE` when an authorized steward attaches it to an existing same-tenant target and may move
  only `ACTIVE → RETIRED` through an authorized steward action.
- Attach and retire are critical audited operations. LegacyAlias does not enter `DRAFT`,
  `PENDING_IDENTITY_APPROVAL` or `IDENTITY_APPROVED`, and it does not inherit Product parent/child approval rules.
- The raw legacy value is stored unchanged as `RawAliasValue`.
- Normalization is separate and used only for lookup/duplicate analysis.
- A LegacyAlias cannot be consumed or exported as the canonical code.
- Exact and normalized collisions must be distinguishable to the steward.
- Bulk profiling, staging, mapping, import, rollback and migration success are not claimed without a real legacy
  export and an approved migration pack; they remain BL-023.

## 12. Lifecycle, approval and retirement invariants

Identity lifecycle:

`DRAFT → PENDING_IDENTITY_APPROVAL → IDENTITY_APPROVED → RETIRED`

- Product Data Steward submits; a distinct Product Identity Approver approves or rejects.
- Self-approval is prohibited.
- Reject returns the record to Draft with reason and audit evidence.
- Product Definition Revision approval requires an Identity Approved Global Product.
- GSKU approval requires an Identity Approved Product Definition Revision.
- LSKU and Finished Good approval require an Identity Approved GSKU.
- A retired parent is not referenceable for new child creation or new approval.
- Approved children block parent retirement.
- Automatic cascade retirement is prohibited; children retire first through controlled actions.
- Draft children require controlled cancellation before parent retirement.
- `IDENTITY_APPROVED` guarantees identity, code, duplicate and basic master-data integrity only. It does not assert
  regulatory, market, manufacturing, quality or commercial readiness.

This Product identity lifecycle excludes LegacyAlias and CodeReservation. LegacyAlias uses `ACTIVE → RETIRED` under
authorized steward control with critical audit evidence; CodeReservation uses the state machine in §6.2/§7.8.

## 13. Duplicate and lookup behavior

- Canonical-code uniqueness is owned by the common CodeReservation ledger as `TenantId + ReservedCode`, independent
  of entity type. Every code-bearing entity's `CanonicalCode` must match its single consumed reservation; no
  collection-local index or combination of separate collection indexes is treated as cross-entity uniqueness proof.
- Duplicate checks do not treat MarketTradeName differences as new Global Product/GSKU evidence.
- Alias lookup compares raw exact value and a separately derived normalized key.
- Approved controlled-reference values are matched by code, not localized display text.
- Canonical-code lookup and alias lookup return the canonical target identity while preserving which alias matched.
- Cross-tenant matches are never disclosed.

Descriptor-based Product/GSKU duplicate matching rules remain Module Pack acceptance criteria and must not invent
Composition, regulatory or manufacturing identity semantics.

## 14. Tenant isolation, soft-delete and concurrency

### Tenant isolation

- `TenantId` is assigned only from the authenticated, trusted server-side tenant context on every create.
- Create/edit/command DTOs and API write contracts do not contain `TenantId`.
- If a client payload supplies `TenantId`, fail-closed validation rejects the request before command handling.
- `TenantId` may appear only in secure read, audit or internal representations and is never client write input.
- Parent and child references must resolve inside the same tenant.
- Cross-tenant access fails closed with non-leaking not-found behavior.
- Canonical-code, alias and reservation uniqueness/indexes are tenant-scoped.

### Soft delete and retention

- `IsDeleted`/`DeletedAt` is technical soft-delete; `RETIRED` is the business lifecycle state.
- Approved/historical MarketTradeName rows, aliases and reservation/no-reuse evidence must remain auditable.
- LegacyAlias technical soft-delete never substitutes for authorized `ACTIVE → RETIRED` transition; attach/retire
  critical-audit evidence and historical lookup traceability remain retained.
- Soft deletion cannot free a canonical code, reservation or alias uniqueness decision for silent reuse.
- Pending G4 audit intent remains deliverable after aggregate soft delete.

### Concurrency

- Every mutable aggregate uses expected-version conditional updates.
- Stale edit, submit, approval, rejection, replacement, retirement and reservation transition fail without changing
  state.
- Last-write-wins is prohibited for every mutable aggregate, including LegacyAlias attach/retire and CodeReservation
  allocation/consume/burn evidence. Technical proof closes under DCP-004 G4 and Module Pack acceptance criteria.
- Generic aggregate optimistic concurrency is first-phase integrity. Product Definition temporal/current/parallel
  revision behavior remains separately deferred to BL-016.

## 15. Explicit exclusions and backlog mapping

| Excluded capability or field group | Backlog | First-phase boundary |
|---|---|---|
| Composition/active substance/complex strength SoR | BL-015 | No Composition ID/FK/placeholder; scalar descriptor only |
| Revision effective dating/current/parallel behavior | BL-016 | Explicit RevisionId; no `IsCurrent` |
| Packaging hierarchy | BL-017 | Scalar pack presentation only |
| Market Supply Assignment / LSKU–FG relation | BL-018 | No direct FK |
| MA / Registered Presentation | BL-019 | No regulatory fields |
| Artwork, label and leaflet lifecycle | BL-020 | No artefact/content lifecycle |
| BOM, manufacturing version, quality, batch and release | BL-021 | Not copied into Product/SKU identity |
| GTIN lifecycle | BL-022 | No simple unmanaged GTIN field |
| Bulk legacy migration | BL-023 | Manual onboarding + LegacyAlias only |
| MarketTradeName official downstream-use contract | BL-024 | No synthetic `IsUsed` |
| ERP/PLM ingestion, distribution and feeds | BL-025 | DCP G7 scoped deferral |
| Runtime external data-contract publication | BL-026 | DCP G7 scoped deferral |

BL-027 is not a Product/SKU field-model capability. Its provider-owned legacy PSS-012 governance risk remains
explicitly preserved in DCP-004 and cannot be interpreted as phase-one Product/SKU scope or as G2/G3 closure.

## 16. DCP gate references

This contract does not redesign DCP-004 technical mechanisms:

- **G4:** critical mutation, local durable audit intent, common-ledger reservation allocation/consumption consistency,
  burned-reservation evidence and optimistic-concurrency proof remain governed by DCP-004 §10/§15; this contract
  requires the business invariants but does not select Candidate B or C or assume transaction-capable topology.
- **G5:** MOD-0290 retains identity-state SoR; workflow actor, S2S, idempotency, callback and maker-checker proof remain
  governed by DCP-004.
- **G6:** LegalEntityId topology, applicability, validation points, failure classes and historical-reference behavior
  remain the LSKU-only gate in DCP-004.
- **G7:** MOD-0003, MOD-0252 ERP and MOD-0253 PLM remain under the approved scoped deferral. Manual legacy onboarding
  is independent; external feed/publication readiness is not claimed.

## 17. Open decisions

No recommendation in this table is an approved field/model decision.

`PackApplicabilityCode` is not an open decision: DCP-004 locks it as a required controlled reference with explicit
non-null applicability, owned by Product Data and consumed from the reconciled Reference Data owner. In contrast,
`ProductTypeCode` remains conditional until the Product Data and Reference Data owners approve its applicability and
requiredness below.

| Open decision | Recommendation | Reason | Owner / closure stage |
|---|---|---|---|
| LSKU `LegalEntityId` applicability/nullability | Define an explicit ProductType/use-case applicability matrix; do not use silent nullable acceptance | No evidence makes it mandatory for every LSKU | Product Data owner + MOD-0220 owner / G6 before affected slice ready-for-dev |
| Exact six-family contracts | Approve family-specific SetCode, scope, catalog, attributes, version/pin/as-of, retirement and fail-closed consumption | Runtime candidate existence is not a published Product/SKU contract | Product Data owner + Reference Data owner / G2-G3 |
| `ProductTypeCode` applicability and requiredness | Approve whether the controlled reference is applicable/required for each first-phase product class before treating it as an implementation requirement | ProductType may drive other field rules, but no owner-approved applicability contract currently makes it universally mandatory | Product Data owner + Reference Data owner / field contract before ready-for-dev |
| Route cardinality | Permit a controlled list, with ProductType attributes defining minimum/maximum cardinality | International products may require multiple routes; universal single-value rule is unproven | Product Data owner + Reference Data owner / field contract before ready-for-dev |
| Revision identifier format | **User-approved:** use a system-generated stable ordinal/label within Global Product, initially `REV-001`, `REV-002`, ...; do not encode effective/current semantics | Revision identity is needed while temporal behavior is deferred | Product Data owner / Module Pack contract |
| `StewardLabel` need on Global Product/Finished Good | Add only if approved create/search/list use cases cannot operate on code and descriptors; prohibit marketing/regulatory meaning | Avoids both unusable lists and a competing MarketTradeName field | Product Data owner + UX owner / final field count |
| First-phase `MarketCode` ownership | Closed 2026-08-07: MOD-0048-FU01 universal `market` catalog; ISO 3166-1 alpha-2 country codes, exact uppercase two-letter tokens, no request normalization; country-external commercial/regulatory regions deferred | Stable shared country-market identity without treating tenant country settings or PSS-011 as business SoR | Product Data owner + Reference Data owner / closed for first LSKU phase |
| Approved-language ownership | Select the authoritative language source and market-language validation contract | MarketTradeName language semantics are not settled by the LSKU country-market decision | Product Data owner + Reference Data/Localization owner / before MarketTradeName ready-for-dev |
| Reservation expiry/cancel policy | Approve duration, actor permission, cancellation reason and terminal retry rules; keep all terminal codes non-reusable | No-reuse does not define operational authorization or timing | Product Data owner + Security/Operations / code policy |
| Alias uniqueness | Approve tenant + source + alias type + normalized-key scope after source evidence; keep raw and normalized collisions separate | Same raw code may be valid in different legacy sources | Product Data owner + Migration steward / before LegacyAlias ready-for-dev |
| MarketTradeName timeline granularity | Use UTC instants with market-local display unless business evidence requires date-only semantics | `[from,to)` comparisons require one exact granularity/timezone rule | Product Data owner + Localization owner / timeline AC |
| `ReplacesMarketTradeNameId` persistence | Persist explicit lineage only if audit/search requirements cannot derive it reliably from the approved timeline | Avoid redundant state while retaining explainable replacement history | Product Data owner + Audit owner / MarketTradeName AC |

## 18. Module Pack acceptance-criterion checklist

The later MOD-0290 Module Pack must convert this checklist into testable acceptance criteria and test expectations:

- [ ] DCP-004 is approved before Module Pack draft authoring; repository-wide Master 7 cleanup is not treated as a
      draft-authoring prerequisite.
- [ ] After DCP approval, canonical MOD-0290 registry entry/identity reconciliation closes before the official Module
      Pack draft artifact is authored; this gate is separate from Master 7 cleanup.
- [ ] The Module Pack remains draft/planning-only until separately approved or ready-for-dev.
- [ ] Master 8.1 evidence ranges and MOD-0290 ownership are cited directly.
- [ ] MOD-0040 reconciliation or approved scoped waiver closes before ready-for-dev/code-start.
- [ ] All eight core entities and their owned relationships are in scope; no excluded aggregate leaks in.
- [ ] `TenantId` is server-assigned; write DTOs/contracts exclude it, supplied values are rejected fail-closed, and
      every repository query/index/reference is tenant-scoped.
- [ ] Global Product, GSKU, LSKU and Finished Good share one tenant-wide canonical-code namespace enforced by the
      entity-type-independent common CodeReservation ledger; collection-local indexes are not cross-entity proof.
- [ ] Canonical codes are system-generated, immutable, low-semantic and non-reusable across all terminal paths.
- [ ] Every code-bearing identity has exactly one matching consumed reservation; identity creation and consume are
      controlled by one stable idempotent command flow, direct assignment/manual override/bypass are rejected, and
      each reservation has at most one identity.
- [ ] Ambiguous identity-write failure after durable consume remains reconciliation-pending and is never automatically
      burned from an absence lookup; the code is never reused. A terminal burned reservation without identity requires
      a deterministically proven pre-insert failure or an owner-approved persistent fence/transaction mechanism.
- [ ] CodeReservation reserve/consume/cancel/expire operations are idempotent and preserve no-reuse evidence.
- [ ] Finished Good has exactly one GSKU; GSKU has zero-to-many Finished Goods; no direct LSKU–FG FK exists.
- [ ] Product Definition uses explicit RevisionId; effective dating and `IsCurrent` are absent.
- [ ] Composition ID/FK/placeholder is rejected from Product Definition and GSKU.
- [ ] Six controlled-reference family contracts and fail-closed provider consumption satisfy G2/G3.
- [ ] Scalar strength and complex/multi-active approval-block rules are testable.
- [ ] Pack applicability explicitly controls PackQuantity/PackUom requiredness.
- [ ] Parent approval, retired-parent, maker-checker, reject-to-Draft and child-first retirement invariants are tested.
- [ ] MarketTradeName replacement preserves the old approved row until the new proposal is approved, then performs
      one overlap/gap-validated timeline transition.
- [ ] LegacyAlias preserves raw value, separates normalization, never replaces canonical code and uses only the
      authorized-steward, critical-audited `ACTIVE → RETIRED` lifecycle.
- [ ] Soft-delete, retirement and no-reuse retention rules are distinct and tested.
- [ ] Expected-version concurrency covers edits and every lifecycle/reservation/timeline transition.
- [ ] G4, G5, G6 and G7 requirements are referenced as DCP gates rather than reimplemented as assumptions.
- [ ] BL-015–BL-026 exclusions are asserted by request/validator/schema tests as applicable.
- [ ] Final approved create/edit field count determines UI GoldenReference; this contract does not select Slim/Compact.

## 19. Authoring and code-start boundaries

- DCP-004 approval authorizes scope, ownership, sequence and gate closure paths; it does not prove technical readiness.
- After DCP-004 approval, canonical MOD-0290 registry identity reconciliation must close before the official Module
  Pack Draft artifact is authored. This is separate from repository-wide Master 7 cleanup, which may remain open.
- Module Pack approval/ready-for-dev requires MOD-0040 closure and all technical gates applicable to its delivery
  slice, including the affected open field contracts.
- Production code requires DCP-004 approved/ready-for-execution, the relevant Module Pack approved/ready-for-dev,
  and delivery-step technical evidence.
- This Draft Domain Contract alone authorizes none of those transitions.

## 20. References

- `docs/System Capability & Implementation Blueprint - master 8.1.xlsx`
  - `Blueprint_Data!A291:AG291`
  - `Dependencies!A1281:D1285`
  - `SoR_Map!A256:E256`
- `execution/portfolio/delivery-capability-packs/DCP-004-mod-0290-sku-coding-foundation-readiness.md`
- `docs/product-backlog.md` — BL-015 through BL-027; BL-027 remains a DCP-level provider governance risk, not a
  Product/SKU field-model capability
- `execution/domains/master-data-management/domain-config.md`
- `execution/domains/master-data-management/README.md`
- `AGENTS.md`
