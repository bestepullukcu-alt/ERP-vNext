---
id: MOD-0048-FU01
name: Enterprise Business Reference Data Provider Hardening
domain: platform-shared-services
service: Diten.Platform
shell: none
golden_reference: none
entity_base: BaseEntity
status: ready-for-dev
owner: reference-data-owner / platform-shared-services
branch: feature/pss/mod-0048-fu01-reference-data-provider
started: 2026-08-02
target: 2026-08-02
form_field_count: 0
---

# MOD-0048-FU01 — Enterprise Business Reference Data Provider Hardening

> **Named-step code-start guard:** This pack is `ready-for-dev` only for the explicitly authorized
> **`BRD Provider Internal Foundation`** delivery step under canonical parent
> **MOD-0048 — Reference Data Management**. That authorization is limited to the exact Section 5 allow-list and does
> not authorize catalog seed or publish behavior, resolve/assignment endpoints, auth/security work, readiness endpoint
> registration, consumer integration, gateway/frontend work, runtime-environment enablement or any production-readiness
> claim. Every FU01 runtime path outside the named-step allow-list remains fail-closed and requires a later named step,
> its applicable owner gates and separate explicit user authorization. The frontmatter branch is planned only; no
> branch has been created or switched by this revision.

> **Draft next named-step guard:** **`BRD Verified GSKU Catalog Publication`** is recorded below as an
> authoring-only draft. It does not inherit code-start authority from the pack's `ready-for-dev` frontmatter or from
> the completed foundation work. Its Section 5 draft allow-list becomes executable only after every named-step gate
> in Section 18 is closed by the stated owner and the user gives separate explicit code-start authorization. Until
> then, the current authorization remains **`BRD Provider Internal Foundation`** only.

> **Verified GSKU Resolver named-step guard:** **`Verified GSKU Resolver`** is implementation-ready and separately
> code-start authorized by the user for the exact Platform and MDM allow-list in Section 5. Trusted consumer-tenant
> binding is closed by forwarding only the already validated inbound user Bearer JWT and requiring Platform to
> independently validate it before reading its `tenant_id` claim. The resolver additionally requires its
> audience-specific MDM credential; the credential proves only `DITENMDMSERVICE` plus
> `VERIFIED_GSKU_RESOLVE`, never a tenant. Tenant authorization is decided per request by the validated JWT tenant and
> that tenant's ACTIVE/non-deleted assignment; neither factor substitutes for the other. The user-approved
> multi-tenant credential correction remains inside the same exact named-step code-start authority. This authorization does not inherit or
> broaden FU16 and does not authorize GSKU
> entity/create-handler/validator/persistence work, submit/approval `PINNED` behavior, production activation, gateway,
> frontend, assignment administration, AuthService token issuance, PSS-011 expansion or any generic/legacy resolver,
> loader or publisher readiness claim.

> **Draft operational-readiness named-step guard:** **`Verified GSKU Catalog Operational Readiness & Bounded UoM
> Enumeration`** is an implementation-ready design consisting of **A. `Verified GSKU Catalog Local Operational
> Foundation`** and **B. `Bounded Verified UoM Enumeration`**. This documentation revision preserves the existing
> `ready-for-dev` frontmatter but grants no code-start, operational-run or production-enable authority. Neither substep
> may be implemented or invoked until the user separately authorizes its exact Section 5 allow-list. No reference
> tenant, catalog load/publish, assignment, credential, secret, environment/configuration value or runtime state is
> created or changed by this revision. Local Development success is explicitly not `Live`/Production readiness.

> **Superseding universal-catalog decision (2026-08-07):** The user approved a hybrid reference-data model for the
> two locked GSKU sets only. `pack-applicability` and `uom` are now Platform/MOD-0048-owned, code-owned,
> deployment-versioned universal lookups. They are identical for every authenticated tenant and are not tenant-
> stewarded BRD records. Therefore these two SetCodes require no internal reference tenant, tenant assignment,
> catalog seed/load/publish operation, governance-mode eligibility or Mongo publication evidence. All earlier
> statements in this pack that make those mechanisms mandatory for these exact two SetCodes are superseded and
> retained only as historical design/evidence for the generic BRD provider. Credential validation, independently
> validated tenant JWT, timeout/failure envelopes and the rule that clients cannot add or override catalog values
> remain mandatory. Tenant-owned or steward-managed reference families continue to use the generic BRD lifecycle.

> **Planning-only market named-step guard (2026-08-07):** **`Verified Market Catalog for LSKU Draft Identity
> Foundation`** is an additive provider-owned named step for `SetCode: market` under this existing follow-up. It
> creates no MOD/FU/DCP and grants neither code-start nor catalog provisioning/activation authority. The source and
> grammar decision is closed; the user must still separately authorize the exact Section 5 code allow-list. A later
> `MARKET-ARTIFACT-01` is closed for artifact authoring; separate operational approval is required to load/publish the approved immutable
> artifact and make it resolvable. This authoring revision changes no runtime, test, configuration, credential,
> catalog data, branch or Git state and does not authorize any MDM, Auth, Gateway or frontend path.

> **Named-step implementation evidence (2026-08-07):** the user's explicit `@orchestrator` blocker-remediation
> request closed the separate code-start gate for only the Section 5 verified-market runtime/test allow-list. The
> implementation and its unit/contract/real-Mongo/regression evidence are complete; `MARKET-ARTIFACT-01` is closed
> for artifact authoring, while operational provisioning remains open and was not performed.

## 1. Module Summary

This follow-up hardens the existing PSS-012 Business Reference Data runtime as the provider implementation path
for enterprise business reference-data contracts governed by MOD-0048. PSS-012 remains a deprecated legacy
runtime/provider alias; it does not replace MOD-0048 as the canonical module.

The first named consumers are the MOD-0290 GSKU `PackApplicability` and `UoM` contracts. For these two locked
families the provider exposes a code-owned universal catalog with deterministic version identities; it does not
create tenant-owned BRD rows. MOD-0290 remains the source of truth for Product/SKU meaning, UoM mapping,
compatibility and GSKU field validation. Generic, steward-managed business reference families retain the durable
version/publish/assignment model elsewhere in this pack.

The pack closes a single shared provider-B boundary. It does not create separate packs per reference-data family.

Authority evidence:

- Master 8.1 `Blueprint_Data!A49:AG49`: `MOD-0048 — Reference Data Management`.
- Master 8.1 `SoR_Map!A212:D212`: reference code sets are owned by MOD-0048.
- Master 8.1 `Dependencies!A108:D109`: MOD-0048 depends on MOD-0001 and MOD-0023.
- DCP-004: provider behavior is B; MOD-0290 adapter and semantic validation are C.
- Registry: PSS-012 is a deprecated runtime/provider implementation alias of MOD-0048.

## 2. Ownership and Boundaries

### In scope

- Current authorized delivery step: **`BRD Provider Internal Foundation`**, limited to provider options plus durable
  tenant-assignment and publish-operation entity/repository/index foundations, their internal DI registration and the
  exact unit/real-Mongo evidence listed in Sections 5 and 17.
- Production-safe governed business reference-data provider behavior in `Diten.Platform`.
- Set, catalog version, value, attribute-definition, publish, historical resolution and usage-registration contracts.
- Enterprise-global semantic catalog invariants with separately governed tenant access/assignment.
- The approved G2 entry catalogs:
  - SetCode `pack-applicability`; initial ValueCode `SCALAR_QUANTITY_APPLIES`.
  - SetCode `uom`; initial ValueCodes `C62`, `GRM`, `KGM`, `MLT`, `LTR`.
- Draft-latest and submit/approval-pinned resolution, value retirement/replacement/no-reuse, stable consumer failures
  and production-safe authorization/governance boundaries; `as-of` is deferred.
- Named step `Verified GSKU Resolver`: an authenticated, MDM-service-only, tenant-bound Platform resolver plus an MDM
  typed client/adapter for exactly `pack-applicability = SCALAR_QUANTITY_APPLIES` and
  `uom = C62|GRM|KGM|MLT|LTR`. It performs ACTIVE/non-deleted assignment verification before catalog reads and accepts
  only the durable verified-publication path; generic PSS-012 resolution and generic/legacy load or publish results
  are explicitly non-evidence.
- Idempotent durable publish recovery/reconciliation proof without a transaction assumption.
- Provider-side integration and authorization test contracts needed by MOD-0290.
- Draft named step `Verified GSKU Catalog Operational Readiness & Bounded UoM Enumeration`:
  - A default-disabled, Development-only, one-shot operational path that invokes only
    `LoadVerifiedGskuCatalogFromFileAsync -> PublishVerifiedAsync -> durable operation/checkpoints -> verified
    completion read-back -> supported assignment application operation` for the exact locked artifact.
  - A provider-owned internal enumeration route at
    `POST /api/internal/v1/reference-data/verified-gsku/enumerate-uom` returning only the five locked selectable UoMs
    and their display/sort/precision data after assignment-before-read and verified-publication proof.

### Out of scope

- PSS-011 `api/lookups`, `LookupsController`, Platform system lookup entities, DTOs or behavior.
- MOD-0290 entities, GSKU validation, UoM compatibility logic or MDM consumer implementation.
- Quantity-free, kit or packaging-hierarchy presentations; these remain BL-017.
- ProductType, DosageForm, RouteOfAdministration and StrengthRepresentationType production catalogs unless a
  later owner-approved scope revision adds their exact contracts.
- Composition, MA, market, site, GTIN, artwork, manufacturing or regulatory semantics.
- Bulk quarantine, migration or reapproval of legacy provider data; BL-027 remains separate.
- All runtime implementation outside the exact `BRD Provider Internal Foundation` allow-list, including production
  catalog seed/publish behavior, new API exposure and production enablement.
- Frontend, Razor, navigation, gateway or public route work.
- For `Verified GSKU Resolver`: GSKU entity/create-handler/validator/persistence changes, submit/approval `PINNED`
  selection, assignment management, usage registration, generic resolver hardening, readiness-health registration,
  gateway routing, production configuration/enablement, `as-of`, scheduled effective periods and any set/value beyond
  the locked two sets and six values.
- For `Verified GSKU Catalog Operational Readiness & Bounded UoM Enumeration`: public administration API/UI, a new
  public or Gateway route, generic BRD publish/controller use, legacy catalog-worker changes, direct Mongo writes,
  runtime use of the test-only positive eligibility seam, `Live` eligibility without real workflow/evidence proof,
  Production/Staging execution, committed tenant/credential/secret values, MDM typed-client/create-options/UI work,
  cache/hardcoded fallback and any set/value outside the locked artifact.

### Ownership decision

- Canonical capability and reference code-set lifecycle owner: MOD-0048.
- Provider implementation owner: platform-shared-services / `Diten.Platform`.
- Legacy runtime evidence: PSS-012, as a non-executable deprecated alias.
- Product/SKU semantic owner and consumer: MOD-0290 / `Diten.MdmService`.

## 3. Owned Objects

This pack owns the provider contracts around the existing Business Reference Data object family; it does not
pre-approve a replacement data model.

| Provider object/contract | Ownership in this follow-up |
|---|---|
| `BusinessReferenceDataSet` | Reference-tenant-owned canonical set identity, SetCode, enterprise-global semantic declaration, lifecycle and published-version pointer |
| `BusinessReferenceDataVersion` | Reference-tenant-owned immutable published catalog version, effective-window metadata and historical pinned resolution; scheduled/`as-of` selection is deferred |
| `BusinessReferenceDataValue` | Stable ValueCode, display metadata, stored effective-date metadata, retirement and replacement evidence; scheduled selection is deferred |
| `BusinessReferenceDataAttributeDefinition` | Owner-approved typed attribute schema and requiredness |
| `BusinessReferenceDataProviderOptions` | Server-side provider configuration; supplies the required canonical `ReferenceTenantId` with no default and no client input path |
| `BusinessReferenceDataTenantAssignment` / `business_reference_data_tenant_assignments` | Durable reference-tenant-owned access grant for one consumer tenant and SetCode; soft-delete/concurrency guarded and incapable of semantic mutation |
| `BusinessReferenceDataUsageRegistration` | Consumer dependency, scope, latest/pinned mode and criticality registration for the first delivery; it is observability/impact metadata, not access authorization |
| Assignment repository contract | Create, resolve, revoke/reactivate and soft-delete assignments with reference-tenant filter, expected version and non-leaking consumer access checks |
| Publish/governance service | Validate, approve and publish through a production-safe path; Disabled/Mock is insufficient |
| Consumer query service | Draft-latest and submit/approval-pinned lookup with stable failures and historical pinned resolution; `as-of` is deferred |
| Tenant access/assignment contract | Server-side assignment determines which consumer tenant may read the reference-tenant canonical catalog; it never grants semantic mutation rights |
| `BusinessReferenceDataPublishOperation` / `business_reference_data_publish_operations` | Separate durable reference-tenant-owned publish state machine record; owns operation identity, idempotency, lifecycle state, checkpoint, expected pointer context, retry/error evidence and completion verification |

Current named-step eligibility is narrower than pack ownership: only `BusinessReferenceDataProviderOptions`,
`BusinessReferenceDataTenantAssignment`, the assignment repository foundation and
`BusinessReferenceDataPublishOperation` persistence foundation may be implemented. Set/version/value behavior,
publish/governance/consumer services, usage behavior and every endpoint remain unchanged.

No GSKU, Product Definition, CodeReservation or other MOD-0290 aggregate is owned here.

`BusinessReferenceDataTenantAssignment` and `BusinessReferenceDataUsageRegistration` are deliberately separate.
An assignment answers whether a tenant may resolve a canonical set. A usage registration records how an authorized
consumer depends on that set. Creating a usage registration never creates access, and deleting one never revokes an
assignment.

## 4. Entity Fields

The following is the minimum provider contract view. Existing persistence names may be retained only when they
satisfy these semantics. Any incompatible model change must be explicitly reviewed before implementation.

### Set and catalog version

| Field/slot | Type | Required | Contract |
|---|---|---:|---|
| `SetCode` | string | Yes | Immutable stable key; initial approved keys are `pack-applicability` and `uom` |
| `Name` | localized/display string | Yes | Steward-facing name; not a consumer semantic key |
| `SemanticScope` | controlled contract | Yes | Fixed to enterprise-global business semantics for these two families; tenant-local semantic catalogs are prohibited |
| `CanonicalCatalogTenantId` | server-resolved GUID | Yes | Identifies the configured reference tenant that physically owns the canonical catalog; absent from client write DTOs |
| `TenantAccessAssignment` | server-owned provider contract | Yes for consumer access | Binds a consumer tenant to the canonical catalog; cannot change SetCode, ValueCode, meaning, attributes or version content |
| `Status` | controlled lifecycle | Yes | Draft/approved/published/retired behavior must be fail-closed |
| `PublishedVersionId` | identifier | Published set | Must point to the authoritative published catalog or a recoverable pending promotion state |
| `CatalogVersionNumber` | positive integer | Yes | Business catalog version; immutable after publish |
| `EffectiveFrom` / `EffectiveTo` | UTC instant | Conditional | Stored as a valid half-open interval `[from,to)`; first delivery does not select a version by schedule or `as-of` |
| `ScopeKey` | string/identifier | Existing-model compatibility | If retained, it identifies only the canonical reference-tenant scope or server-owned assignment; it never selects tenant-local semantic content and is never client controlled |
| `ConcurrencyToken` | opaque token | Mutation | Expected-version/conditional-write protection; last-write-wins is forbidden |

### Provider options

| Field/slot | Type | Required | Contract |
|---|---|---:|---|
| `ReferenceTenantId` | GUID | Yes | No default. Bound from trusted server-side `BusinessReferenceData:Provider` configuration; never read from a client DTO, header, route or request body |

`BusinessReferenceDataProviderOptions` is configuration, not a Mongo entity. Its owner is the MOD-0048 provider.
Missing, empty or invalid `ReferenceTenantId` leaves the Platform host running, fails provider readiness and makes
every provider-dependent resolve/assignment endpoint return exact `503 REFERENCE_PROVIDER_CONFIGURATION_INVALID`;
it never falls back to
`BusinessReferenceDataCatalogLoadOptions.TenantId`, ambient tenant or a hardcoded GUID.

### Publish operation

`BusinessReferenceDataPublishOperation` is a separate tenant-aware entity whose concrete runtime base is
`TenantScopedEntity : BaseEntity`. Frontmatter uses the module-pack-standard value `entity_base: BaseEntity`; the
existing and planned BRD entities retain the concrete `TenantScopedEntity` specialization so `TenantId` is physically
enforced under the configured reference tenant.

| Field/slot | Type | Required | Contract |
|---|---|---:|---|
| inherited `TenantId` | GUID | Yes | Equals server-resolved `ReferenceTenantId`; client input prohibited |
| `PublishOperationId` | GUID | Yes | Immutable provider-generated operation identity |
| `BusinessReferenceDataSetId` | GUID | Yes | Target set under the same reference tenant |
| `BusinessReferenceDataVersionId` | GUID | Yes | Target version under the same set/reference tenant |
| `IdempotencyKey` | string | Yes | Trimmed immutable request key; unique per reference tenant among non-deleted operations |
| `OperationState` | enum | Yes | Exactly `PENDING`, `RUNNING`, `RECOVERY_REQUIRED`, `COMPLETED`, `FAILED_TERMINAL` |
| `PublishCheckpoint` | enum | Yes | Exactly `INITIALIZED`, `TARGET_VERSION_WRITTEN`, `PRIOR_VERSIONS_DEPRECATED`, `REQUIRED_WRITES_VERIFIED`, `POINTER_PROMOTED`, `COMPLETION_VERIFIED` |
| `ExpectedPublishedVersionId` | nullable GUID | Yes | Parent pointer value captured before mutation; stale mismatch blocks/reconciles rather than overwrites |
| `ExpectedSetVersion` / `ExpectedTargetVersionToken` | concurrency tokens | Yes | Conditional-write/fencing context for parent and target version |
| `RetryCount` / `LastAttemptAt` | non-negative integer / UTC instant | Yes | Durable retry evidence; replay resumes the same operation |
| `LastErrorCode` / `LastErrorAt` | stable code / UTC instant | Conditional | Sanitized recovery or terminal-failure evidence |
| `CompletedAt` | UTC instant | Completed | Set only after pointer and terminal publication state are re-read and verified |
| inherited audit/soft-delete fields | base fields | Yes | `CreatedAt`, `UpdatedAt`, `IsDeleted`, concurrency and trusted actor evidence |

`OperationState` describes whether the operation may run, must recover or is terminal. `PublishCheckpoint` describes
the last durably completed write/verification boundary; the two are never aliases. Allowed transitions are:

- `PENDING/INITIALIZED -> RUNNING/*` for the first attempt.
- Checkpoints advance without skip or reversal:
  `INITIALIZED -> TARGET_VERSION_WRITTEN -> PRIOR_VERSIONS_DEPRECATED -> REQUIRED_WRITES_VERIFIED -> POINTER_PROMOTED -> COMPLETION_VERIFIED`.
- Retryable interruption: `RUNNING/* -> RECOVERY_REQUIRED/<last durable checkpoint> -> RUNNING/<same checkpoint>`.
- Before pointer promotion, an irrecoverable validation/stale-context conflict may become `FAILED_TERMINAL`.
- After `POINTER_PROMOTED`, the operation cannot become `FAILED_TERMINAL`; it remains `RECOVERY_REQUIRED` until
  reconciliation reaches `COMPLETED/COMPLETION_VERIFIED`.
- `COMPLETED` is immutable. Same idempotency key replays the same result; a different target under the same key is a
  conflict.

The target version write, prior-version transitions and all required durable writes are re-read and verified before
`PublishedVersionId` promotion. Consumer resolution recognizes a publication only when the pointer, target version
and `COMPLETED/COMPLETION_VERIFIED` operation agree; partial operations never expose a false published claim.

### Verified GSKU resolver credential

| Field/slot | Type | Required | Contract |
|---|---|---:|---|
| `Identifier` | string | Yes | Resolver-specific non-secret identifier; FU16/shared credentials are rejected |
| `ActiveSecret` / eligible previous secret | sensitive string | Yes | Timing-safe comparison, rotation overlap and revocation rules; never logged or returned |
| `ConsumerService` | controlled string | Yes | Must derive exactly `DITENMDMSERVICE` server-side |
| `AllowedAudience` | controlled string | Yes | Must derive exactly `VERIFIED_GSKU_RESOLVE` server-side |
| `ConsumerTenantId` or any tenant constraint | none | Forbidden | Not present in credential options, authentication result, headers or DTOs; tenant comes only from the independently validated JWT |

### Tenant assignment

| Field/slot | Type | Required | Contract |
|---|---|---:|---|
| inherited `TenantId` | GUID | Yes | Must equal server-resolved `BusinessReferenceDataProviderOptions.ReferenceTenantId`; client input prohibited |
| `BusinessReferenceDataTenantAssignmentId` | GUID | Yes | Immutable provider-generated assignment identity |
| `ConsumerTenantId` | GUID | Yes | Tenant receiving resolve/read access; cannot equal empty GUID and cannot be client-substituted during lookup |
| `SetCode` | string | Yes | Canonical set granted to the consumer tenant; assignment cannot create or alter the set |
| `AssignmentStatus` | controlled lifecycle | Yes | `ACTIVE` or `REVOKED`; only `ACTIVE` and non-deleted assignments authorize access |
| `RevokedAt` / `RevokedBy` | UTC instant / trusted actor | Revoked | Durable revocation evidence; reactivation requires an owner-approved transition on the same record |
| inherited `CreatedAt/By`, `UpdatedAt/By` | audit metadata | Yes | Trusted server actor and timestamps; never client authored |
| inherited `IsDeleted` | boolean | Yes | Soft delete immediately removes access but never releases catalog codes or semantic content |
| inherited `Version` | positive integer | Yes | Expected-version conditional update/revoke; stale mutation fails with conflict |

The active-record uniqueness invariant is a partial unique index on
`TenantId (ReferenceTenantId) + ConsumerTenantId + SetCode` where `IsDeleted = false`. A revoked non-deleted record is
updated/reactivated through expected-version rules rather than bypassed with a duplicate assignment.

### Values and attributes

| Field/slot | Type | Required | Contract |
|---|---|---:|---|
| `ValueCode` | string | Yes | Stable, immutable and never reassigned to a different meaning |
| `DisplayName` | localized/display string | Yes | Human-readable; does not replace ValueCode |
| `Symbol` | string attribute | Optional/open metadata | A future display-symbol decision does not alter the locked code, dimension or precision contract |
| `DimensionCode` | controlled attribute | UoM required | Required typed attribute; initial values are locked below |
| `MaximumDecimalPrecision` | non-negative integer attribute | UoM required | Required typed attribute; provider rejects quantity precision above the locked maximum |
| `IsDeprecated` | boolean | Yes | Deprecated values are historical and unavailable for new selection by default |
| `ReplacementValueCode` | ValueCode reference | Conditional | Must resolve within the same set and approved replacement chain |
| `EffectiveFrom` / `EffectiveTo` | UTC instant | Conditional | Stored interval integrity is validated; scheduled/`as-of` value selection is outside the first delivery |
| `Attributes` | typed map | Conditional | Every key must exist in the published attribute definition and pass required/type/enum rules |

### Locked initial catalogs

| SetCode | Initial ValueCodes | Locked provider rule |
|---|---|---|
| `pack-applicability` | `SCALAR_QUANTITY_APPLIES` | Only initial first-phase value; tenant-local semantic alternatives are forbidden |
| `uom` | `C62`, `GRM`, `KGM`, `MLT`, `LTR` | Stable codes; dimension and maximum decimal precision are locked below |

| UoM ValueCode | DimensionCode | MaximumDecimalPrecision |
|---|---|---:|
| `C62` | `COUNT` | 0 |
| `GRM` | `MASS` | 3 |
| `KGM` | `MASS` | 3 |
| `MLT` | `VOLUME` | 3 |
| `LTR` | `VOLUME` | 3 |

Provider storage of attributes does not transfer Product/SKU interpretation to MOD-0048. MOD-0290 decides whether a
published UoM is compatible with a Product/SKU field.

### Draft named-step operational options and enumeration shape — `Verified GSKU Catalog Operational Readiness & Bounded UoM Enumeration`

The Development pilot uses a separate options section named
`BusinessReferenceData:VerifiedGskuOperationalProvisioning`. It is not an extension or alias of
`BusinessReferenceData:CatalogLoad` and does not change general publication eligibility.

| Option | Required when enabled | Contract |
|---|---:|---|
| `Enabled` | Yes | Defaults `false`; `true` is still rejected unless `IHostEnvironment.IsDevelopment()` is true |
| `CatalogPath` | Yes | Trusted server-side path to the exact existing `mod-0290-gsku-reference.json`; no directory scan |
| `ExpectedCatalogVersion` | Yes | Exact locked value `1.0.0` |
| `ExpectedCatalogFingerprint` | Yes | Exact current artifact SHA-256 `e95ef856e87cfaf726b8e4c939e56499791933e69b90bc7fbb6718a949422a5d`; mismatch fails before mutation |
| `ReferenceTenantId` | Via provider options | Must resolve as one non-empty GUID from `BusinessReferenceData:Provider`; never duplicated or overridden here |
| `ConsumerTenantId` | Yes | Explicit non-empty pilot tenant; cannot equal `ReferenceTenantId` |
| `ActorId` | Yes | Non-empty trusted server-side operational actor; no request/body/ambient fallback |
| `IdempotencyNamespace` | Yes | Stable owner-approved namespace used to derive the same per-set load/publish/assignment facts on replay |

No committed `appsettings*.json` value is authorized. Configuration is supplied only through a separately authorized,
secret-safe local mechanism; tenant IDs are configuration, credentials remain in the existing resolver-specific
options, and no secret is generated, displayed or copied.

`POST /api/internal/v1/reference-data/verified-gsku/enumerate-uom` has no request-body fields and returns exactly:

```text
Uoms[]:
  Code
  DisplayText
  SortOrder
  MaximumDecimalPrecision
```

The deterministic order is `C62`, `GRM`, `KGM`, `MLT`, `LTR`, with precision `0,3,3,3,3`. The response excludes
reference/consumer tenant identity, assignment identity/evidence, credential data, catalog version identity/number,
resolution mode/time, publish-operation identity and all secret/configuration values.

### Draft named-step catalog artifact shape — `BRD Verified GSKU Catalog Publication`

The planned artifact path is
`services/Diten.Platform/src/Diten.Platform.API/Seed/business-reference-data/mod-0290-gsku-reference.json`. This is a
trusted file input to the existing PSS-012 catalog loader, not a second seed system. The file contains exactly two
sets and six values. The exact document shape is:

```json
{
  "catalog_version": "1.0.0",
  "module": "BusinessReferenceData",
  "note": "MOD-0290 initial GSKU reference catalog",
  "sets": [
    {
      "set_code": "pack-applicability",
      "set_name": "Pack Applicability",
      "scope_type": "global",
      "status": "Active",
      "description": "Applicability rules for product and SKU pack quantities.",
      "attribute_definitions": [],
      "values": [
        {
          "value_code": "SCALAR_QUANTITY_APPLIES",
          "display_name": "Scalar Quantity Applies",
          "description": "A positive scalar pack quantity applies to the SKU presentation.",
          "is_active": true,
          "sort_order": 10,
          "attributes": {}
        }
      ]
    },
    {
      "set_code": "uom",
      "set_name": "Unit of Measure",
      "scope_type": "global",
      "status": "Active",
      "description": "Units of measure permitted for the initial GSKU pack quantity.",
      "attribute_definitions": [
        { "attribute_code": "DimensionCode", "display_name": "Dimension Code", "data_type": "string", "is_required": true },
        { "attribute_code": "MaximumDecimalPrecision", "display_name": "Maximum Decimal Precision", "data_type": "integer", "is_required": true }
      ],
      "values": [
        { "value_code": "C62", "display_name": "One", "is_active": true, "sort_order": 10, "attributes": { "DimensionCode": "COUNT", "MaximumDecimalPrecision": "0" } },
        { "value_code": "GRM", "display_name": "Gram", "is_active": true, "sort_order": 20, "attributes": { "DimensionCode": "MASS", "MaximumDecimalPrecision": "3" } },
        { "value_code": "KGM", "display_name": "Kilogram", "is_active": true, "sort_order": 30, "attributes": { "DimensionCode": "MASS", "MaximumDecimalPrecision": "3" } },
        { "value_code": "MLT", "display_name": "Millilitre", "is_active": true, "sort_order": 40, "attributes": { "DimensionCode": "VOLUME", "MaximumDecimalPrecision": "3" } },
        { "value_code": "LTR", "display_name": "Litre", "is_active": true, "sort_order": 50, "attributes": { "DimensionCode": "VOLUME", "MaximumDecimalPrecision": "3" } }
      ]
    }
  ]
}
```

`Attributes` remains the existing `Dictionary<string,string>` persistence contract, so precision values are canonical
decimal strings in JSON and Mongo. The loader must parse them invariant-culture as non-negative integers and reject
non-canonical or out-of-contract values before any publication attempt. It must persist both
`attribute_definitions` and per-value `attributes`; unknown, missing, duplicate or incorrectly typed attributes are a
blocking catalog conflict. The artifact identity, audit note and English display text above are the user-approved
initial catalog metadata; they are immutable for this first catalog version.

### Named-step contract shape — `Verified GSKU Resolver`

The internal route is `POST /api/internal/v1/reference-data/verified-gsku/resolve`. The request is tenant-free,
contains exactly one `LATEST` item for each field being created and contains at most one item per locked set:

```json
{
  "selections": [
    {
      "set_code": "pack-applicability",
      "value_code": "SCALAR_QUANTITY_APPLIES",
      "resolution_mode": "LATEST"
    },
    {
      "set_code": "uom",
      "value_code": "KGM",
      "resolution_mode": "LATEST"
    }
  ]
}
```

This first GSKU draft named step accepts only `LATEST`; request fields for catalog version, server resolution mode or
resolution timestamp are forbidden. Submit/approval `PINNED` selection is a later MOD-0290 step. The successful
`Response<T>.Data` contains only the minimum provider evidence:

```json
{
  "selections": [
    {
      "set_code": "uom",
      "value_code": "KGM",
      "catalog_version_id": "11111111-1111-1111-1111-111111111111",
      "catalog_version_number": 1,
      "resolution_mode": "LATEST",
      "resolved_at_utc": "2026-08-03T00:00:00Z",
      "is_retired": false,
      "selectable_for_new": true
    }
  ]
}
```

The envelope also carries the standard status/success/errors/reason/correlation fields. Each selection returns exactly
`SetCode`, `ValueCode`, `CatalogVersionId`, `CatalogVersionNumber`, `ResolutionMode`, `ResolvedAtUtc`, `IsRetired` and
`SelectableForNew`; display text, attributes, `ReferenceTenantId`, `ConsumerTenantId`, assignment identity,
idempotency key and publish-operation identity are not returned. MDM client DTOs cannot supply or override SetCode,
catalog version, resolution mode or timestamp; the adapter constructs the two server-owned SetCodes and accepts only
provider-derived evidence.

### Planning named-step contract — `Verified Market Catalog for LSKU Draft Identity Foundation`

#### Ownership and universal/shared decision

- Canonical `SetCode` is exactly `market`; clients cannot send another set name.
- Master 8.1 `Blueprint_Data!A49:AG49` and `SoR_Map!A212:E212` assign reference code sets and their lifecycle to
  MOD-0048. Master 8.1 `Blueprint_Data!A291:AG291` and `SoR_Map!A256:E256` assign Product/Item/SKU identities and
  UoM mappings to MOD-0290; they do not assign market-code lifecycle to MOD-0290.
- The `market` values are one universal/shared provider catalog. Every correctly authenticated MDM tenant sees the
  same active codes from the same published version. `BusinessReferenceDataTenantAssignment` is not an access
  predicate for this set and no per-tenant catalog copy, override or MDM enum/free-text fallback is allowed.
- Whether a tenant or Legal Entity operates in a market is a separate future business-assignment capability. It does
  not add, remove or filter `market` catalog values and is outside this provider step and the first LSKU foundation.
- MOD-0290 is a consumer only: it supplies one exact selected code to the internal provider resolver and persists
  server-returned technical selection evidence. It never owns market values, publication or active/retired state.

#### Authoritative source and exact value-code contract

The user closed `MARKET-SOURCE-01` on 2026-08-07 for the first LSKU phase. A market is exactly a country market and
the authoritative code authority is the ISO 3166 Maintenance Agency's current officially assigned ISO 3166-1
alpha-2 set. Country-external commercial or regulatory groupings such as `EU`, `MENA`, export regions or custom sales
territories are outside this named step and require a later owner-approved catalog family/contract; they are never
inserted as user-assigned alpha-2 values.

| Contract item | Locked decision |
|---|---|
| Canonical ValueCode | Exact ISO 3166-1 alpha-2 token from the locked official source snapshot |
| Grammar and length | `^[A-Z]{2}$`; exactly two ASCII uppercase letters |
| Normalization | None. Request-time trim, uppercase, case-fold, alias and fuzzy conversion are prohibited |
| Change authority | ISO 3166 Maintenance Agency; provider owner reviews a new official snapshot before publishing a new immutable catalog version |
| Country display text | Source-snapshot country short name stored as provider display metadata; it is not LSKU identity and may later be localized without changing ValueCode |
| Non-country regions | Deferred; no `EU`, `MENA`, commercial-zone or regulatory-region placeholder in the first phase |

The downloadable/current source snapshot, complete initial active rows, declared row count, deterministic sort order,
usage/license basis, immutable provider version identity and artifact SHA-256 are materialized under
`MARKET-ARTIFACT-01` before operational provisioning. They are data/provisioning evidence rather than an open runtime
design decision and do not authorize direct Mongo writes or startup loading.

Runtime behavior around the approved grammar is locked:

- catalog publication accepts only tokens present in the locked official source snapshot and passing `^[A-Z]{2}$`;
- exact-code resolve uses `StringComparison.Ordinal` against the stored canonical token;
- request-time trimming, uppercasing, case-folding, aliasing, punctuation removal and fuzzy/country fallback are
  prohibited; any non-canonical variant is a missing code;
- duplicates after the approved normalization are a publication-blocking conflict, never last-write-wins.

#### Lifecycle, version and immutable evidence

- `ACTIVE -> RETIRED` is terminal for a published ValueCode. Retired codes are absent from active enumeration and
  cannot be selected for a new LSKU.
- A published ValueCode is never reused for another meaning, including after retirement, soft delete, replacement,
  failed provisioning or later catalog versions. Replacement, if approved, uses a different code and explicit
  same-set lineage.
- Every change creates a new immutable catalog version. Existing published versions and durable publish-operation
  evidence remain readable internally; no in-place edit of a published version is allowed.
- Latest resolve succeeds only when the set pointer, immutable target version and non-deleted durable operation agree
  on `COMPLETED/COMPLETION_VERIFIED`. Generic `Published` status, legacy PSS-012 query results, Disabled/Mock,
  a test-only positive seam or a direct Mongo document is not verified market evidence.
- The provider's internal resolve response carries exactly the server-derived
  `SetCode + ValueCode + CatalogVersionId + CatalogVersionNumber + ResolutionMode + ResolvedAtUtc` required by the
  MOD-0290 persistence seam. Catalog version/reference-tenant/credential/assignment/publish-operation evidence is
  never accepted from a client and is never returned by browser-facing or LSKU business DTOs.

#### Internal resolver and bounded enumeration

Both surfaces reuse the existing verified resolver credential and independently validated tenant-user JWT sequence;
they add no credential, tenant header, public route or browser surface.

| Surface | Request | Success contract | Fail-closed contract |
|---|---|---|---|
| Exact resolve: `POST /api/internal/v1/reference-data/verified-market/resolve` | Exact JSON object `{"market_code":"<canonical>"}`; no SetCode/version/mode/tenant/evidence or unknown fields | One internal technical selection; server fixes `SetCode=market`, `ResolutionMode=LATEST` | Missing/retired/non-selectable code: `404 REFERENCE_MARKET_NOT_FOUND`; missing/unpublished/inconsistent catalog or invalid provider config: `503 REFERENCE_PROVIDER_UNAVAILABLE`; two-second budget expiry: `504 REFERENCE_PROVIDER_TIMEOUT` |
| Active enumeration: `POST /api/internal/v1/reference-data/verified-market/enumerate-active` | Bodyless; no query, paging, filter, tenant, version or evidence input | Exact complete active set of the current verified version, deterministic `sort_order` then ordinal `code`; item fields are only `code`, `display_text`, `sort_order` | Empty/duplicate/over-owner-approved-bound/unpublished/inconsistent catalog: `503 REFERENCE_PROVIDER_UNAVAILABLE`; two-second budget expiry: `504 REFERENCE_PROVIDER_TIMEOUT`; never partial success or cached/hardcoded fallback |

Authentication failures retain the existing `401 REFERENCE_UNAUTHENTICATED` /
`403 REFERENCE_FORBIDDEN` provider envelopes. Contract-shape violations remain `409 REFERENCE_CONTRACT_MISMATCH`.
The tenant JWT authenticates and binds the calling tenant context but does not filter the universal catalog and never
creates a provider assignment. No catalog/version/reference-tenant/credential/assignment field appears in enumeration.

## 5. Repo Scope

### Exact named-step allow-list — `BRD Provider Internal Foundation`

Only the following exact paths are eligible under the current `ready-for-dev` status and user code-start
authorization. Existing combined PSS-012 files are extended in place; these paths do not authorize a parallel provider,
second catalog model or behavior outside the foundation contracts:

- `services/Diten.Platform/src/Diten.Platform.Domain/Entities/ReferenceDataEntitiesv2.cs`, limited to
  `BusinessReferenceDataTenantAssignment`, `BusinessReferenceDataPublishOperation` and their foundation enums.
- `services/Diten.Platform/src/Diten.Platform.Domain/Repositories/IBusinessReferenceDataStewardshipRepository.cs`,
  limited to internal assignment and publish-operation persistence contracts.
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Repositories/BusinessReferenceDataStewardshipRepository.cs`,
  limited to those assignment and publish-operation repository contracts and their tenant/soft-delete/concurrency filters.
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Settings/BusinessReferenceDataProviderOptions.cs`
  (planned exact file), limited to the internal no-default `ReferenceTenantId` option contract.
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/DependencyInjection.cs`, limited to binding the provider
  options without `ValidateOnStart` and registering the existing stewardship repository dependencies required by this
  foundation. No auth challenge, health check, hosted service or endpoint registration is allowed.
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Configurations/MongoDbIndexConfigurations.cs`,
  limited to the new `business_reference_data_tenant_assignments` and
  `business_reference_data_publish_operations` collection index blocks.
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/BusinessReferenceData/BusinessReferenceDataProviderOptionsTests.cs`
  (planned exact file).
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/BusinessReferenceData/BusinessReferenceDataTenantAssignmentMongoTests.cs`
  (planned exact file).
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/BusinessReferenceData/BusinessReferenceDataPublishOperationMongoTests.cs`
  (planned exact file).
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/BusinessReferenceData/BusinessReferenceDataPublishStateMachineTests.cs`
  (planned exact file; pure foundation invariants only).

No wildcard directory is authorized. A required change outside this list stops implementation and requires a pack
revision plus a separately authorized named step. The existing test project already compiles files by SDK glob and
references Domain/Infrastructure, so its `.csproj` is not in this allow-list.

### Draft exact named-step allow-list — `BRD Verified GSKU Catalog Publication` (not code-start authorized)

If and only if the Section 18 gates close and the user separately authorizes this named step, its exhaustive path
allow-list is:

- `services/Diten.Platform/src/Diten.Platform.Domain/Entities/ReferenceDataEntitiesv2.cs`, limited to reuse-preserving
  publish-operation/state-machine helpers strictly required to connect the existing PSS-012 publish path; no new
  set/version/value aggregate or collection.
- `services/Diten.Platform/src/Diten.Platform.Domain/Repositories/IBusinessReferenceDataStewardshipRepository.cs`,
  limited to operation claim/checkpoint/recovery and verified-publication repository contracts.
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Repositories/BusinessReferenceDataStewardshipRepository.cs`,
  limited to those contracts and their existing reference-tenant, soft-delete and concurrency fences.
- `services/Diten.Platform/src/Diten.Platform.Application/Features/BusinessReferenceData/Services/BusinessReferenceDataCatalogLoaderService.cs`,
  limited to the Section 4 catalog shape, attribute-definition/value-attribute transport, reference-tenant enforcement,
  idempotent load and invocation of the verified publish path.
- `services/Diten.Platform/src/Diten.Platform.Application/Features/BusinessReferenceData/Services/IBusinessReferenceDataPublishService.cs`,
  limited to the internal durable-operation request/result contract; no endpoint-facing contract.
- `services/Diten.Platform/src/Diten.Platform.Application/Features/BusinessReferenceData/Services/BusinessReferenceDataPublishService.cs`,
  limited to replacing best-effort publication with FU01 operation claim/checkpoint/fencing/recovery and verified
  completion for this existing service.
- `services/Diten.Platform/src/Diten.Platform.Application/Features/BusinessReferenceData/Services/BusinessReferenceDataValidationService.cs`,
  limited to blocking validation of the Section 4 attribute definitions, required attributes, `DimensionCode` values
  and canonical non-negative integer precision.
- `services/Diten.Platform/src/Diten.Platform.Application/Features/BusinessReferenceData/Services/IBusinessReferenceDataGovernanceAdapters.cs`,
  limited to the internal publication-eligibility decision contract; no endpoint or transport contract.
- `services/Diten.Platform/src/Diten.Platform.Application/Features/BusinessReferenceData/Services/BusinessReferenceDataGovernanceAdapters.cs`,
  limited to exposing the existing governance mode as a fail-closed publication eligibility decision; no Live adapter,
  workflow transport or evidence integration.
- `services/Diten.Platform/src/Diten.Platform.Application/DependencyInjection.cs`, limited to internal registration of
  the above existing-service extensions; no worker, HTTP client, endpoint, health check or production activation.
- `services/Diten.Platform/src/Diten.Platform.API/Seed/business-reference-data/mod-0290-gsku-reference.json`, limited
  to the exact Section 4 two-set/six-value artifact after all placeholder decisions close.
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/BusinessReferenceData/BusinessReferenceDataGskuCatalogLoadMongoTests.cs`
  (planned exact file).
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/BusinessReferenceData/BusinessReferenceDataVerifiedPublishMongoTests.cs`
  (planned exact file).
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/BusinessReferenceData/BusinessReferenceDataGovernanceModeTests.cs`
  (planned exact file).

No wildcard directory, configuration file, worker or existing seed file is authorized. A needed path outside this
list blocks implementation and requires a pack revision plus explicit user authorization.

### Exact named-step allow-list — `Verified GSKU Resolver` (code-start authorized)

The user has closed the Section 18 tenant-binding decision and separately authorized this named step. Implementation
is exhaustive to the following exact files.

**Platform verified resolver:**

- `services/Diten.Platform/src/Diten.Platform.Application/Features/BusinessReferenceData/Models/BusinessReferenceDataVerifiedResolveModels.cs`
- `services/Diten.Platform/src/Diten.Platform.Application/Features/BusinessReferenceData/Queries/ResolveVerifiedGskuReferenceDataQuery.cs`
- `services/Diten.Platform/src/Diten.Platform.Application/Features/BusinessReferenceData/Handlers/QueryHandlers/ResolveVerifiedGskuReferenceDataHandler.cs`
- `services/Diten.Platform/src/Diten.Platform.Application/Features/BusinessReferenceData/Validators/ResolveVerifiedGskuReferenceDataValidator.cs`
- `services/Diten.Platform/src/Diten.Platform.Application/Features/BusinessReferenceData/BusinessReferenceDataExceptionBehavior.cs`
  (only the Section 13 mapping for this query; generic PSS-012 behavior is unchanged).
- `services/Diten.Platform/src/Diten.Platform.Application/DependencyInjection.cs`
  (only the narrow query/handler/validator registrations required by this step).
- `services/Diten.Platform/src/Diten.Platform.Domain/Repositories/IBusinessReferenceDataStewardshipRepository.cs`
  (read-only ACTIVE/non-deleted assignment and verified target-publication proof contracts only).
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Repositories/BusinessReferenceDataStewardshipRepository.cs`
  (those read contracts only; no mutation, schema or index change).
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/DependencyInjection.cs`
  (resolver read dependencies only; no committed values, worker, health or activation).
- `services/Diten.Platform/src/Diten.Platform.API/Models/BusinessReferenceData/BusinessReferenceDataVerifiedResolveRequests.cs`
- `services/Diten.Platform/src/Diten.Platform.API/Controllers/Internal/InternalBusinessReferenceDataController.cs`
  (one action at `POST /api/internal/v1/reference-data/verified-gsku/resolve`; no tenant input surface).
- `services/Diten.Platform/src/Diten.Platform.API/Configuration/VerifiedGskuResolverCredentialOptions.cs`
  (resolver-only identifier, active/previous secret, overlap, revocation, consumer service and allowed audience; no
  tenant field and no committed value).
- `services/Diten.Platform/src/Diten.Platform.API/Security/VerifiedGskuResolverCredentialAuthenticator.cs`
  (timing-safe validation and server-side derivation of only `DITENMDMSERVICE` and
  `VERIFIED_GSKU_RESOLVE`; its result carries no tenant constraint).
- `services/Diten.Platform/src/Diten.Platform.API/Security/VerifiedGskuResolverJwtTenantContext.cs`
  (controller-only extraction of non-empty `tenant_id` and exact `tenant_user` actor from an independently validated
  JWT principal and rejection of `X-Tenant-Id`; it accepts no credential tenant argument).
- `services/Diten.Platform/src/Diten.Platform.API/Program.cs`
  (bind/register only the resolver-specific credential/authenticator; no AuthService issuance or generic auth change).
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/BusinessReferenceData/BusinessReferenceDataVerifiedResolveMongoTests.cs`
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/BusinessReferenceData/BusinessReferenceDataVerifiedResolveContractTests.cs`
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/BusinessReferenceData/BusinessReferenceDataVerifiedResolverAuthorizationTests.cs`
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/BusinessReferenceData/VerifiedGskuResolverJwtTenantContextTests.cs`
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/BusinessReferenceData/BusinessReferenceDataTenantContextTests.cs`
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/DependencyInjectionSmokeTests.cs`
  (resolver registrations and absence of fallback/worker/health activation only).

**User-approved multi-tenant credential correction â€” exact runtime delta:**

- `services/Diten.Platform/src/Diten.Platform.API/Configuration/VerifiedGskuResolverCredentialOptions.cs`, only to
  remove `ConsumerTenantId` from the resolver credential option shape.
- `services/Diten.Platform/src/Diten.Platform.API/Security/VerifiedGskuResolverCredentialAuthenticator.cs`, only to
  remove tenant validation/output while retaining resolver-specific service/audience, rotation, revocation and
  timing-safe secret validation.
- `services/Diten.Platform/src/Diten.Platform.API/Security/VerifiedGskuResolverJwtTenantContext.cs`, only to remove the
  credential-tenant parameter/equality check and return the single non-empty tenant from the independently validated
  exact `tenant_user` JWT.
- `services/Diten.Platform/src/Diten.Platform.API/Controllers/Internal/InternalBusinessReferenceDataController.cs`,
  only to consume the JWT-derived tenant without reading a tenant from credential authentication.
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/BusinessReferenceData/BusinessReferenceDataVerifiedResolverAuthorizationTests.cs`
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/BusinessReferenceData/VerifiedGskuResolverJwtTenantContextTests.cs`
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/BusinessReferenceData/BusinessReferenceDataVerifiedResolveMongoTests.cs`
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/BusinessReferenceData/BusinessReferenceDataTenantContextTests.cs`

No MDM client, request DTO, application query/handler, repository, assignment schema, generic middleware or Program
change is required by this correction. The existing MDM client continues to send the same resolver credential and
delegated Bearer per request without any tenant field. Any required path outside this exact delta stops the correction
and requires a pack revision.

**MDM typed client and resolver adapter:**

- `services/Diten.MdmService/src/Diten.MdmService.Application/Contracts/ReferenceData/IVerifiedGskuReferenceResolver.cs`
  (typed application abstraction and minimum result only).
- `services/Diten.MdmService/src/Diten.MdmService.Infrastructure/ReferenceData/VerifiedGskuResolverOptions.cs`
  (Platform base address, bounded timeout and resolver-only credential binding; no defaults or tenant fields).
- `services/Diten.MdmService/src/Diten.MdmService.Infrastructure/ReferenceData/PlatformVerifiedGskuResolverClient.cs`
  (typed HTTP client/adapter, resolver credential plus delegated current-request Bearer forwarding, `REFERENCE_*`
  mapping, no retry or semantic fallback, deterministic request-scope restore and raw-token redaction).
- `services/Diten.MdmService/src/Diten.MdmService.Infrastructure/DependencyInjection.cs`
  (typed client/options registration only).
- `services/Diten.MdmService/src/Diten.MdmService.Api/Program.cs`
  (passes trusted configuration to the Infrastructure registration only; no endpoint or GSKU handler wiring).
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ReferenceData/PlatformVerifiedGskuResolverClientTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ReferenceData/VerifiedGskuDelegatedTokenForwardingTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ReferenceData/VerifiedGskuReferenceResolverContractTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ReferenceData/VerifiedGskuResolverDependencyInjectionTests.cs`

No directory wildcard or adjacent file is authorized. A needed middleware, permission-onboarding, configuration,
gateway, AuthService or GSKU aggregate path outside this list blocks implementation and requires a pack revision plus
separate explicit authorization.

### Authorized superseding named step — `Universal GSKU Reference Lookup`

User authorization on 2026-08-07 replaces the former operational-provisioning requirement for the exact SetCodes
`pack-applicability` and `uom`. The runtime delta is limited to:

- `services/Diten.Platform/src/Diten.Platform.Application/Features/BusinessReferenceData/Services/VerifiedGskuUniversalCatalog.cs`
- `services/Diten.Platform/src/Diten.Platform.Application/Features/BusinessReferenceData/Handlers/QueryHandlers/ResolveVerifiedGskuReferenceDataHandler.cs`
- `services/Diten.Platform/src/Diten.Platform.Application/Features/BusinessReferenceData/Handlers/QueryHandlers/EnumerateVerifiedGskuUomsHandler.cs`
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/BusinessReferenceData/BusinessReferenceDataVerifiedResolveContractTests.cs`
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/BusinessReferenceData/BusinessReferenceDataVerifiedResolveMongoTests.cs`
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/BusinessReferenceData/BusinessReferenceDataVerifiedUomEnumerationContractTests.cs`
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/BusinessReferenceData/BusinessReferenceDataVerifiedUomEnumerationMongoTests.cs`

The existing internal controller, credential authenticator, JWT tenant-context validation, DTO envelope and MDM
typed client remain unchanged. The catalog is exactly version `GSKU-UNIVERSAL-V1`: Pack Applicability contains only
`SCALAR_QUANTITY_APPLIES`; UoM contains only `C62`, `GRM`, `KGM`, `MLT`, `LTR` with precision `0,3,3,3,3`.
Tenants cannot add, edit, retire or override these values. A catalog change requires a new deployment version,
deterministic version identity, pack review and regression tests. No reference-tenant, assignment, loader, publisher,
operational runner, Mongo repository/index or configuration file is in this allow-list.

### Planning exact named-step allow-list — `Verified Market Catalog for LSKU Draft Identity Foundation` (no code-start)

The following is the minimum exhaustive runtime/test allow-list after the user gives separate code-start
authorization. **Planned** files do not exist yet. Existing files may change only for the stated
append-only market behavior; UoM and Pack Applicability contracts are regression-protected.

**Provider runtime**

- `services/Diten.Platform/src/Diten.Platform.API/Controllers/Internal/InternalVerifiedMarketReferenceDataController.cs`
  (**planned**): exact two internal actions. It must delegate credential authentication, delegated-JWT validation,
  tenant scope, two-second budget and deterministic restoration to the shared executor below; copying that sequence
  into the controller is prohibited.
- `services/Diten.Platform/src/Diten.Platform.API/Security/IVerifiedReferenceDataRequestExecutor.cs` and
  `services/Diten.Platform/src/Diten.Platform.API/Security/VerifiedReferenceDataRequestExecutor.cs` (**planned**):
  one internal execution boundary for credential -> independently validated JWT -> tenant scope -> linked budget ->
  restoration. It adds no tenant selection, credential type or fallback.
- `services/Diten.Platform/src/Diten.Platform.API/Controllers/Internal/InternalBusinessReferenceDataController.cs`:
  refactor only to delegate its existing verified-GSKU resolve/UoM paths to the same executor with byte-for-byte
  compatible routes, request shapes and failure envelopes; no business behavior or permission expansion.
- `services/Diten.Platform/src/Diten.Platform.API/Models/BusinessReferenceData/BusinessReferenceDataVerifiedMarketRequests.cs`
  (**planned**): exact-code request with unknown-field rejection; no tenant, SetCode, version, mode or evidence input.
- `services/Diten.Platform/src/Diten.Platform.Application/Features/BusinessReferenceData/Models/BusinessReferenceDataVerifiedMarketModels.cs`
  (**planned**): internal technical selection and three-field enumeration models.
- `services/Diten.Platform/src/Diten.Platform.Application/Features/BusinessReferenceData/Queries/ResolveVerifiedMarketReferenceDataQuery.cs`
  and
  `services/Diten.Platform/src/Diten.Platform.Application/Features/BusinessReferenceData/Queries/EnumerateVerifiedMarketsQuery.cs`
  (**planned**).
- `services/Diten.Platform/src/Diten.Platform.Application/Features/BusinessReferenceData/Handlers/QueryHandlers/ResolveVerifiedMarketReferenceDataHandler.cs`
  and
  `services/Diten.Platform/src/Diten.Platform.Application/Features/BusinessReferenceData/Handlers/QueryHandlers/EnumerateVerifiedMarketsHandler.cs`
  (**planned**): verified pointer/immutable-target/operation reads only; no load, publish, repair or assignment.
- `services/Diten.Platform/src/Diten.Platform.Application/Features/BusinessReferenceData/Validators/ResolveVerifiedMarketReferenceDataValidator.cs`
  and
  `services/Diten.Platform/src/Diten.Platform.Application/Features/BusinessReferenceData/Validators/EnumerateVerifiedMarketsValidator.cs`
  (**planned**): exact shape/canonical-code and bodyless-query contracts.
- `services/Diten.Platform/src/Diten.Platform.Application/Features/BusinessReferenceData/Services/BusinessReferenceDataCatalogLoaderService.cs`:
  additive `LoadVerifiedMarketCatalogFromFileAsync` validation path for the owner-approved immutable artifact only.
- `services/Diten.Platform/src/Diten.Platform.Application/Features/BusinessReferenceData/Services/IBusinessReferenceDataPublishService.cs`
  and
  `services/Diten.Platform/src/Diten.Platform.Application/Features/BusinessReferenceData/Services/BusinessReferenceDataPublishService.cs`:
  additive verified-market publication/replay contract using the existing durable state/checkpoint machine; no
  test-only positive eligibility and no change to verified GSKU behavior.
- `services/Diten.Platform/src/Diten.Platform.Domain/Repositories/IBusinessReferenceDataStewardshipRepository.cs`
  and
  `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Repositories/BusinessReferenceDataStewardshipRepository.cs`:
  exact `market` latest/active/historical proof reads and publication read-back only; no consumer assignment check.
- `services/Diten.Platform/src/Diten.Platform.Application/Features/BusinessReferenceData/BusinessReferenceDataExceptionBehavior.cs`:
  only the locked market `404/503/504` mapping.
- `services/Diten.Platform/src/Diten.Platform.API/Seed/business-reference-data/mod-0290-market-reference.json`
  (**materialized; artifact-authoring closed under `MARKET-ARTIFACT-01`**): owner-approved immutable
  values/version/source metadata; never a
  fallback list and never loaded by the legacy worker.

**Provider tests**

- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/BusinessReferenceData/BusinessReferenceDataVerifiedMarketResolveContractTests.cs`
  (**planned**).
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/BusinessReferenceData/BusinessReferenceDataVerifiedMarketEnumerationContractTests.cs`
  (**planned**).
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/BusinessReferenceData/BusinessReferenceDataVerifiedMarketAuthorizationTests.cs`
  (**planned**).
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/BusinessReferenceData/VerifiedReferenceDataRequestExecutorTests.cs`
  (**planned**): shared credential/JWT/tenant/budget ordering, cancellation and scope-restoration proof plus existing
  verified-GSKU regression.
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/BusinessReferenceData/BusinessReferenceDataVerifiedMarketCatalogLoadMongoTests.cs`
  (**planned**).
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/BusinessReferenceData/BusinessReferenceDataVerifiedMarketPublishMongoTests.cs`
  (**planned**).
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/BusinessReferenceData/BusinessReferenceDataVerifiedMarketResolveMongoTests.cs`
  (**planned**).
- Existing regression-only files:
  `BusinessReferenceDataVerifiedResolveContractTests.cs`,
  `BusinessReferenceDataVerifiedResolveMongoTests.cs`,
  `BusinessReferenceDataVerifiedUomEnumerationContractTests.cs`,
  `BusinessReferenceDataVerifiedUomEnumerationMongoTests.cs`,
  `BusinessReferenceDataVerifiedPublishMongoTests.cs`,
  `BusinessReferenceDataPublishOperationMongoTests.cs` and
  `BusinessReferenceDataPublishStateMachineTests.cs` under
  `services/Diten.Platform/tests/Diten.Platform.Application.Tests/BusinessReferenceData/`.

Existing reuse references that remain unchanged/protected are
`VerifiedGskuUniversalCatalog.cs` (deployment-versioned universal lookup pattern),
`VerifiedGskuResolverCredentialAuthenticator.cs`, `VerifiedGskuResolverJwtTenantContext.cs` and the current
verified GSKU/UoM DTO/query/handler/validator files. Generic
`BusinessReferenceDataConsumerQueryService.cs`, PSS-011 lookups and legacy PSS-012 `Published` results are not
verified market evidence.

No other file is eligible. In particular no MDM, AuthService, Gateway, frontend, appsettings/configuration,
`ReferenceDataEntitiesv2.cs`, Mongo index definition, public BRD controller, hosted worker, direct Mongo provisioner,
test-only production seam, registry, DCP, Domain Contract, backlog or `.antigravity/**` path is allow-listed. A
schema/index/security/configuration requirement discovered during implementation blocks the step and requires pack
revision plus separate approval.

### Draft exact named-step allow-list — `Verified GSKU Catalog Operational Readiness & Bounded UoM Enumeration` (no code-start)

The historical A/B plan below is superseded for the two universal GSKU SetCodes. It remains reference material only
for a future steward-managed family that explicitly opts into the generic BRD lifecycle; it is not an outstanding
GSKU gate. The existing paths below were verified in the repository. Paths marked **planned** are the minimum new files; they do
not exist yet and this documentation revision does not create them. The combined named step is exhaustive to this list
only after separate user code-start authorization.

**A — Verified GSKU Catalog Local Operational Foundation**

- `services/Diten.Platform/src/Diten.Platform.API/Configuration/VerifiedGskuOperationalProvisioningOptions.cs`
  (**planned**): default-disabled Development-only options and exact artifact/version/fingerprint, consumer tenant,
  trusted actor and idempotency namespace contract. No secret or credential field.
- `services/Diten.Platform/src/Diten.Platform.Application/Features/BusinessReferenceData/Services/IBusinessReferenceDataVerifiedGskuOperationalEligibility.cs`
  (**planned**): Application-owned narrow decision/opaque-authorization contract consumed by the verified loader and
  publisher; the authorization has no string/boolean representation, configuration binder or serialized form and is
  accepted only from the registered eligibility service.
- `services/Diten.Platform/src/Diten.Platform.API/Services/BusinessReferenceData/DevelopmentBusinessReferenceDataVerifiedGskuOperationalEligibility.cs`
  (**planned**): host implementation requiring Development + explicit enablement + exact locked artifact/fingerprint
  + valid provider/consumer/actor facts. It is not `IBusinessReferenceDataPublicationEligibility` and cannot make
  generic/Live publication eligible.
- `services/Diten.Platform/src/Diten.Platform.API/Services/BusinessReferenceData/VerifiedGskuOperationalProvisioningRunner.cs`
  (**planned**): at most one reconciliation turn per process start; calls the supported application contracts only.
  It never calls `LoadFromFileAsync`, never writes repositories/Mongo directly and never changes
  `BusinessReferenceDataCatalogLoadWorker.cs`.
- `services/Diten.Platform/src/Diten.Platform.Application/Features/BusinessReferenceData/Commands/EnsureVerifiedGskuTenantAssignmentsCommand.cs`
  (**planned**),
  `services/Diten.Platform/src/Diten.Platform.Application/Features/BusinessReferenceData/Handlers/CommandHandlers/EnsureVerifiedGskuTenantAssignmentsHandler.cs`
  (**planned**) and
  `services/Diten.Platform/src/Diten.Platform.Application/Features/BusinessReferenceData/Validators/EnsureVerifiedGskuTenantAssignmentsValidator.cs`
  (**planned**): supported internal application operation for exactly two assignments (`pack-applicability`, `uom`).
- `services/Diten.Platform/src/Diten.Platform.Application/Features/BusinessReferenceData/Services/BusinessReferenceDataCatalogLoaderService.cs`:
  an operational overload of existing `LoadVerifiedGskuCatalogFromFileAsync` that requires and propagates the opaque
  authorization; invocation/replay only, with legacy load behavior unchanged.
- `services/Diten.Platform/src/Diten.Platform.Application/Features/BusinessReferenceData/Services/IBusinessReferenceDataPublishService.cs`
  and `services/Diten.Platform/src/Diten.Platform.Application/Features/BusinessReferenceData/Services/BusinessReferenceDataPublishService.cs`:
  a verified-GSKU operational overload of `PublishVerifiedAsync` that validates the opaque authorization against the
  exact artifact/fingerprint and durable operation; durable replay/reconciliation and completion read-back only. The
  existing generic/runtime overload still evaluates `IBusinessReferenceDataPublicationEligibility` and remains
  negative in Disabled/Mock/FailClosed/unproven Live.
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Settings/BusinessReferenceDataProviderOptions.cs`:
  stable request-time classification API that does not force invalid options through `IOptions.Value` construction.
- `services/Diten.Platform/src/Diten.Platform.API/Observability/BusinessReferenceDataProviderReadinessHealthCheck.cs`
  (**planned**): non-throwing `ready`-only provider configuration/operational-state probe. It never participates in
  liveness and never repairs, loads, publishes or assigns.
- `services/Diten.Platform/src/Diten.Platform.Domain/Repositories/IBusinessReferenceDataStewardshipRepository.cs` and
  `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Repositories/BusinessReferenceDataStewardshipRepository.cs`:
  idempotent assignment application/reconciliation and exact verified-completion read proof only.
- `services/Diten.Platform/src/Diten.Platform.Application/DependencyInjection.cs`,
  `services/Diten.Platform/src/Diten.Platform.Infrastructure/DependencyInjection.cs` and
  `services/Diten.Platform/src/Diten.Platform.API/Program.cs`: register only the separate eligibility/options,
  application operation and default-disabled hosted runner. `Program.cs` is necessary because hosted-service
  registration is an API-host concern. No `appsettings*.json` file is allow-listed.
- Existing artifact
  `services/Diten.Platform/src/Diten.Platform.API/Seed/business-reference-data/mod-0290-gsku-reference.json`: read-only
  runtime input under the locked version/fingerprint; this step does not edit it.
- Existing tests extended only where the current contract belongs:
  `services/Diten.Platform/tests/Diten.Platform.Application.Tests/BusinessReferenceData/BusinessReferenceDataProviderOptionsTests.cs`,
  `services/Diten.Platform/tests/Diten.Platform.Application.Tests/BusinessReferenceData/BusinessReferenceDataGovernanceModeTests.cs`,
  `services/Diten.Platform/tests/Diten.Platform.Application.Tests/BusinessReferenceData/BusinessReferenceDataGskuCatalogLoadMongoTests.cs`,
  `services/Diten.Platform/tests/Diten.Platform.Application.Tests/BusinessReferenceData/BusinessReferenceDataVerifiedPublishMongoTests.cs`,
  `services/Diten.Platform/tests/Diten.Platform.Application.Tests/BusinessReferenceData/BusinessReferenceDataTenantAssignmentMongoTests.cs`,
  `services/Diten.Platform/tests/Diten.Platform.Application.Tests/BusinessReferenceData/BusinessReferenceDataPublishOperationMongoTests.cs`,
  `services/Diten.Platform/tests/Diten.Platform.Application.Tests/BusinessReferenceData/BusinessReferenceDataPublishStateMachineTests.cs` and
  `services/Diten.Platform/tests/Diten.Platform.Application.Tests/DependencyInjectionSmokeTests.cs`.
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/BusinessReferenceData/VerifiedGskuOperationalEligibilityTests.cs`,
  `services/Diten.Platform/tests/Diten.Platform.Application.Tests/BusinessReferenceData/VerifiedGskuOperationalProvisioningRunnerTests.cs`,
  `services/Diten.Platform/tests/Diten.Platform.Application.Tests/BusinessReferenceData/EnsureVerifiedGskuTenantAssignmentsTests.cs`
  and `services/Diten.Platform/tests/Diten.Platform.Application.Tests/BusinessReferenceData/BusinessReferenceDataProviderReadinessTests.cs`
  (**planned**).

**B — Bounded Verified UoM Enumeration**

- `services/Diten.Platform/src/Diten.Platform.Application/Features/BusinessReferenceData/Models/BusinessReferenceDataVerifiedUomModels.cs`
  (**planned**): exact four-field response item.
- `services/Diten.Platform/src/Diten.Platform.Application/Features/BusinessReferenceData/Queries/EnumerateVerifiedGskuUomsQuery.cs`,
  `services/Diten.Platform/src/Diten.Platform.Application/Features/BusinessReferenceData/Handlers/QueryHandlers/EnumerateVerifiedGskuUomsHandler.cs`
  and `services/Diten.Platform/src/Diten.Platform.Application/Features/BusinessReferenceData/Validators/EnumerateVerifiedGskuUomsValidator.cs`
  (**planned**):
  tenant-free bounded query, assignment-before-read and verified-publication proof.
- `services/Diten.Platform/src/Diten.Platform.Application/Features/BusinessReferenceData/BusinessReferenceDataExceptionBehavior.cs`:
  only the exact enumeration `404/409/503/504` mapping.
- `services/Diten.Platform/src/Diten.Platform.Domain/Repositories/IBusinessReferenceDataStewardshipRepository.cs` and
  `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Repositories/BusinessReferenceDataStewardshipRepository.cs`:
  bounded ACTIVE/selectable UoM plus pointer/immutable-target/verified-operation read contract; no mutation.
- `services/Diten.Platform/src/Diten.Platform.API/Controllers/Internal/InternalBusinessReferenceDataController.cs`:
  one additive bodyless `POST enumerate-uom` action reusing the existing resolver credential and JWT tenant-context
  sequence; any body/query input is rejected without adding a request DTO.
- `services/Diten.Platform/src/Diten.Platform.Application/DependencyInjection.cs`: enumeration query/handler/validator
  registration only. No additional credential, public controller, Gateway or MDM registration.
- Existing auth/context regressions:
  `services/Diten.Platform/tests/Diten.Platform.Application.Tests/BusinessReferenceData/BusinessReferenceDataVerifiedResolverAuthorizationTests.cs`,
  `services/Diten.Platform/tests/Diten.Platform.Application.Tests/BusinessReferenceData/VerifiedGskuResolverJwtTenantContextTests.cs`
  and `services/Diten.Platform/tests/Diten.Platform.Application.Tests/BusinessReferenceData/BusinessReferenceDataTenantContextTests.cs`.
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/BusinessReferenceData/BusinessReferenceDataVerifiedUomEnumerationContractTests.cs`,
  `services/Diten.Platform/tests/Diten.Platform.Application.Tests/BusinessReferenceData/BusinessReferenceDataVerifiedUomEnumerationMongoTests.cs`
  and `services/Diten.Platform/tests/Diten.Platform.Application.Tests/BusinessReferenceData/BusinessReferenceDataVerifiedUomEnumerationAuthorizationTests.cs`
  (**planned**).

No change to `BusinessReferenceDataCatalogLoadWorker.cs`, committed appsettings, `ReferenceDataEntitiesv2.cs`, Mongo
index definitions, public BRD controllers, MDM, frontend, Gateway or AuthService is required by this named step. The
existing assignment and publish-operation indexes already support the selected application and read predicates; a
new index or entity/schema change blocks implementation and requires pack revision plus separate approval.

## 6. Protected Paths

- `.antigravity/**`
- `AGENTS.md`
- `docs/System Capability & Implementation Blueprint - master 8.1.xlsx`
- `docs/product-backlog.md`
- `execution/domains/master-data-management/**`
- `services/Diten.MdmService/**`, except only the exact code-start-authorized `Verified GSKU Resolver` MDM files in
  Section 5
- `services/Diten.AuthService/**`
- `services/Diten.DevEnablementService/**`
- `services/Diten.Platform/src/Diten.Platform.API/Controllers/LookupsController.cs`
- `services/Diten.Platform/src/Diten.Platform.API/Controllers/TenantReferenceDataController.cs`; it remains PSS-011
  stopgap evidence and is not widened into the MOD-0048 provider surface
- `services/Diten.Platform/src/Diten.Platform.Application/Features/ReferenceData/SelfRegistration/ReferenceDataManifestProvider.cs`
  and its frontend-route-only manifest contract; backend-only assignment permission is not added here
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/ReferenceData/ReferenceDataManifestProviderTests.cs`;
  its existing frontend-route/enforced-permission zero-drift contract remains unchanged
- `services/Diten.Platform/src/Diten.Platform.Application/Features/Lookups/**`
- `services/Diten.Platform/src/Diten.Platform.Application/Features/BusinessReferenceData/Services/BusinessReferenceDataCatalogLoaderService.cs`
- `services/Diten.Platform/src/Diten.Platform.Application/Features/BusinessReferenceData/Services/BusinessReferenceDataPublishService.cs`
- `services/Diten.Platform/src/Diten.Platform.Application/Features/BusinessReferenceData/Services/BusinessReferenceDataConsumerQueryService.cs`
- `services/Diten.Platform/src/Diten.Platform.Application/Features/BusinessReferenceData/Services/BusinessReferenceDataGovernanceAdapters.cs`
- `services/Diten.Platform/src/Diten.Platform.Application/Features/BusinessReferenceData/BusinessReferenceDataExceptionBehavior.cs`
- `services/Diten.Platform/src/Diten.Platform.Application/DependencyInjection.cs`
- `services/Diten.Platform/src/Diten.Platform.API/Program.cs`
- `services/Diten.Platform/src/Diten.Platform.API/Controllers/BusinessReferenceDataController.cs`
- `services/Diten.Platform/src/Diten.Platform.API/Security/HasPermissionAttribute.cs`
- `services/Diten.Platform/src/Diten.Platform.API/Security/BusinessReferenceDataEndpointAttribute.cs` (not created)
- `services/Diten.Platform/src/Diten.Platform.API/Observability/BusinessReferenceDataProviderReadinessHealthCheck.cs`
  (not created)
- `services/Diten.Platform/src/Diten.Platform.API/appsettings.json`
- `services/Diten.Platform/src/Diten.Platform.API/appsettings.Development.json`
- every committed `appsettings*.json` under Platform or MDM
- PSS-011 `/api/lookups` response shapes, routes, caching and system lookup behavior
- `gateway/Diten.ApiGateway/**/ocelot.json`
- `frontend/**`
- Production catalog files/seeds until a distinct seed/publish delivery step is approved
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Settings/BusinessReferenceDataCatalogLoadOptions.cs`;
  its `TenantId` remains seed/catalog-load configuration and is not provider identity or access authority
- Every controller, public/internal route, auth/JWT metadata or middleware path, hosted worker, publish/seed service,
  consumer exposure and runtime environment/production enablement path until separately authorized
- Every non-Business-Reference-Data index block in
  `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Configurations/MongoDbIndexConfigurations.cs`

For draft step `BRD Verified GSKU Catalog Publication`, the current protected list remains in force except for the
exact files explicitly enumerated in that step's draft Section 5 allow-list after code-start authorization. The
following remain protected without exception for that step:

- `services/Diten.Platform/src/Diten.Platform.API/Program.cs`, all `appsettings*.json`, and
  `services/Diten.Platform/src/Diten.Platform.API/Services/BusinessReferenceData/BusinessReferenceDataCatalogLoadWorker.cs`
- every controller, API model, endpoint metadata, auth/JWT/permission, readiness/liveness and HTTP/S2S path
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Settings/BusinessReferenceDataCatalogLoadOptions.cs`;
  reference-tenant ownership must come from provider options and the planned artifact is not activated through
  configuration in this step
- `BusinessReferenceDataConsumerQueryService.cs`, all consumer resolve contracts, assignment enforcement, historical
  pinned/latest resolution and usage registration behavior
- existing seed artifacts `document-management-qms.json` and `legal-entity-reference.json`
- Mongo index configuration; the foundation's existing BRD operation indexes are reused and this step creates no
  collection or index
- gateway, frontend, MDM, MOD-0290 runtime/adapter/tests and every governance document other than this pack

For `Verified GSKU Resolver`, the base protected list remains in force except for its exact code-start-authorized
Section 5 paths. The following remain protected without exception:

- `services/Diten.Platform.Common/src/Diten.Platform.Common/Tenancy/TenantResolutionMiddleware.cs` and all other
  shared tenant middleware: the new internal action must not broaden the legacy `/api/internal` bypass or make
  `X-Tenant-Id` authoritative.
- `services/Diten.MdmService/src/Diten.MdmService.Infrastructure/Middleware/TenantResolutionMiddleware.cs`: inbound MDM
  behavior is evidence only and remains unchanged; delegated-token forwarding occurs in the exact typed client path.
- `services/Diten.Platform/src/Diten.Platform.API/Program.cs`, all `appsettings*.json`, health-check/readiness files,
  hosted workers and catalog-load configuration/activation.
- Existing `BusinessReferenceDataController.cs` stewardship/generic consumer actions, all PSS-011/PSS-012 generic
  resolver and lookup controllers, generic query request/handler/validator files and legacy response contracts.
- Catalog loader, publish service, governance adapters, verified catalog artifact and all seed files; this step reads
  already verified publication evidence and cannot publish, repair or seed it.
- Tenant-assignment mutation/admin endpoints, usage-registration mutation, Mongo index configuration and entity
  schemas; this step consumes the existing assignment and operation foundations read-only.
- `services/Diten.AuthService/**`, including token/client-credential issuance, claim minting and refresh behavior.
- FU16 module-registration credential code and tests, including
  `ModuleRegistrationCredentialOptions.cs`, `ModuleRegistrationCredentialAuthenticator.cs`,
  `InternalModuleRegistrationController.cs`, `PlatformRegistrationOptions.cs`, `ModuleRegistrationHostedService.cs`
  and their focused tests. Resolver credentials are audience-specific and separate; FU16 credentials are never
  automatically reused, renamed or generalized.
- All Global Product and Product Abbreviation Register/ABB business code, plus all GSKU entity, command handler,
  validator and persistence files. This step provides only the MDM typed client/adapter seam for a later MOD-0290 step.
- `gateway/**`, `frontend/**`, all MDM files outside the exact Section 5 list, and all governance documents other than
  this pack.

For `Verified GSKU Catalog Operational Readiness & Bounded UoM Enumeration`, the base protected list remains in force
except only for its exact draft Section 5 allow-list after separate code-start authorization. The following remain
protected without exception: `BusinessReferenceDataCatalogLoadWorker.cs`; every committed `appsettings*.json`;
`ReferenceDataEntitiesv2.cs`; `MongoDbIndexConfigurations.cs`; public/generic BRD controllers; all MDM, AuthService,
Gateway and frontend files; all tenant middleware; every public administration API/UI; and all runtime environment,
tenant, credential, secret, load/publish, assignment or smoke mutation until a later exact operational authorization.

## 7. Dependencies

- Canonical parent `MOD-0048 — Reference Data Management`.
- Master 8.1 dependency `MOD-0001 — System-of-Record & Ownership Registry`.
- Master 8.1 dependency `MOD-0023 — Workflow Designer` for production publication governance.
- MOD-0021 audit facilities for durable publication evidence where the approved design consumes them.
- Existing PSS-012 Business Reference Data runtime as reuse/gap evidence.
- Auth permission owner for least-privilege service and steward permissions.
- Tenant/security owner for trusted tenant binding and cross-tenant non-disclosure.
- MOD-0290 contract owner for consumer semantics and compatibility test vectors.

Draft step `BRD Verified GSKU Catalog Publication` additionally depends on the implemented FU01 provider options and
durable publish-operation foundation, a reachable real MongoDB for evidence, and owner closure of the Section 18
catalog/governance/recovery decisions. MOD-0023 and MOD-0031 remain unavailable runtime dependencies: this step must
not simulate them with Disabled/Mock or implement a Live adapter. No MOD-0290 code dependency is introduced.

`Verified GSKU Resolver` additionally depends on the verified two-family catalog and durable publication evidence,
ACTIVE/non-deleted assignments for the consumer tenant and both SetCodes, resolver-audience-specific per-service
credential provisioning, MDM's already validated inbound user JWT, and Platform's independent JWT validation using
the existing issuer/audience/signing/lifetime configuration. FU16 module-registration credentials are reference
evidence for secret handling only and are not a resolver dependency or fallback. No AuthService token issuance change
or new service-token system is required.

`Verified GSKU Catalog Operational Readiness & Bounded UoM Enumeration` additionally depends on the existing locked
artifact, verified loader/publisher state machine, provider options, assignment/publish-operation indexes, verified
resolver credential/JWT tenant-context implementation and MOD-0290's frozen four-field UoM consumer contract. It adds
no runtime dependency on MDM and does not treat MOD-0023/MOD-0031 absence as permission to make `Live` eligible.

This follow-up is delivery-derived for MOD-0290; it is not a direct MOD-0290 Blueprint edge and does not alter Master
8.1 dependencies.

## 8. Runtime Constraints

- Current code-start authority covers two independently bounded Section 5 allow-lists: the earlier
  `BRD Provider Internal Foundation` and the newly user-authorized `Verified GSKU Resolver`. The resolver authority is
  limited to its narrow Platform endpoint/auth-context/read path and MDM typed client; it does not load/seed/publish,
  administer assignments, change health behavior or implement GSKU aggregate/create flow.
- The new operational-readiness/enumeration named step is design-only. It is not a third authorized allow-list; its
  paths become eligible only after separate user code-start, and an operational run requires another exact approval.
- The foundation must extend the existing PSS-012 `ReferenceDataEntitiesv2.cs`, stewardship repository and Mongo index
  registration. A parallel provider implementation, second reference-data catalog aggregate/collection family or new
  service is forbidden.
- Foundation DI is internal only: provider option binding plus repository availability. It must not register a hosted
  worker, publish/resolve service, controller, readiness health check, JWT/auth behavior or production configuration.
- Real-Mongo success for provider options/assignment/publish-operation foundation proves only persistence contracts;
  it is not runtime readiness, consumer exposure, production enablement or a published-catalog claim.
- The remaining bullets apply to their named scopes. Anything outside the two authorized exact allow-lists remains
  fail-closed under the current status.
- Enterprise-global business semantics are stored physically as one canonical catalog owned by a configured reference
  tenant. Tenant-local semantic catalog creation, copy-on-write or override is forbidden.
- The existing PSS-012 `TenantScopedEntity` persistence model is retained only as the physical reference-tenant
  boundary. This is not proof of true cross-tenant global storage and must not be represented as such.
- The canonical catalog `TenantId` comes from trusted server-side typed configuration. Catalog create/import/publish
  DTOs cannot supply or override it; supplied tenant identity is rejected rather than ignored.
- Provider identity comes only from `BusinessReferenceDataProviderOptions.ReferenceTenantId`. It has no default and
  never falls back to `BusinessReferenceDataCatalogLoadOptions.TenantId`, whose purpose remains seed/catalog load.
- Missing, empty or invalid `ReferenceTenantId` does not terminate the Platform host. Provider readiness is unhealthy,
  and every provider-dependent resolve/assignment endpoint fails closed with exact
  `503 REFERENCE_PROVIDER_CONFIGURATION_INVALID`; no catalog-load, ambient-tenant or hardcoded fallback is permitted.
- Provider options binding/validation must not use a startup-terminating `ValidateOnStart` path. Validation produces
  readiness and provider-surface failure state while the host continues serving non-provider surfaces.
- `BusinessReferenceDataProviderReadinessHealthCheck` is registered in the real `Program.cs`
  `AddDitenObservability` health-check callback with tag `ready` only. Invalid BRD provider configuration therefore
  affects provider readiness but never the `live` predicate or Platform host liveness. Existing readiness path,
  filter and sanitized response writer are preserved.
- Consumer access is granted only by a server-owned tenant assignment to the canonical reference-tenant catalog.
  Assignment changes access only and cannot mutate or fork semantic content.
- A usage registration cannot satisfy assignment lookup and cannot authorize access. Assignment revoke or soft delete
  takes effect immediately even when an active usage registration remains.
- Every consumer resolve follows one trusted-context algorithm: Platform independently validates the delegated JWT,
  accepts exactly one non-empty `tenant_id` for an exact `tenant_user`, establishes `ITenantContext` from that claim,
  and the handler captures that value as `ConsumerTenantId` before opening any temporary scope. The resolver
  credential never supplies, constrains or selects this tenant. Resolve `ReferenceTenantId` only from
  `BusinessReferenceDataProviderOptions`; enter the reference-tenant `TenantScope`; verify the exact assignment
  predicate `ReferenceTenantId + captured ConsumerTenantId + SetCode + ACTIVE + !IsDeleted`; only then read the
  reference-tenant catalog. A request DTO, route, body or raw tenant header is never an authority for either tenant.
- The temporary reference-tenant scope is exception/cancellation safe. Its disposal/finally path deterministically
  restores the captured consumer scope before control returns to subsequent middleware or application code.
- No reference-tenant catalog read may occur before the assignment predicate succeeds. The current PSS-011
  allow-list stopgap reads `BusinessReferenceDataCatalogLoadOptions.TenantId` and opens reference scope without this
  durable assignment check; allow-list expansion cannot satisfy or substitute for this algorithm.
- Missing assignment, tenant mismatch or inaccessible catalog fails closed with the same non-leaking 404 behavior.
- Published catalogs and values are immutable. Changes create a new catalog version.
- ValueCode and retired code meanings are never reused across catalog versions.
- Disabled or Mock governance cannot publish or satisfy production consumption for these catalogs.
- Publish consistency is implemented only through the separate durable `BusinessReferenceDataPublishOperation` state
  machine in Section 4; no Mongo transaction or atomic multi-write assumption is accepted. State and checkpoint
  transitions are conditional/idempotent, retries resume the same operation, and stale expected pointer/version
  context cannot overwrite a newer publication. The parent `PublishedVersionId` pointer is promoted only after
  `REQUIRED_WRITES_VERIFIED`; consumer-visible success requires `COMPLETED/COMPLETION_VERIFIED`. No pending, failed or
  partially applied operation may expose a published claim.
- For the first two-family delivery, draft selection may resolve `latest`; MOD-0290 submit/approval pins the exact
  catalog version and later resolution uses that pin. `as-of` selection and scheduled effective-period behavior are
  explicitly deferred. A retired value is rejected for new selection, while a historical pinned record continues to
  resolve; replacement is optional, must remain within the same set, and `ValueCode` is never reused.
- Provider outage, timeout, stale version, contract mismatch and unauthorized access never fall back to hardcoded or
  free-text values.
- Exact BRD auth envelopes are endpoint-scoped. Only actions carrying explicit
  `BusinessReferenceDataEndpointAttribute` metadata receive `401 REFERENCE_UNAUTHENTICATED` from JWT challenge or
  `403 REFERENCE_FORBIDDEN` from the permission-result path. Both paths test endpoint metadata before selecting the
  BRD envelope. Metadata-absent Platform endpoints retain their existing challenge/permission response contract;
  unconditional host-wide `REFERENCE_*` auth mapping is forbidden. This is a deferred generic BRD endpoint rule and
  does not describe `Verified GSKU Resolver`, whose exact credential/audience contract is in Section 14.
- `Platform.BusinessReferenceData.Assignment.Manage` exists only on real assignment-administration actions marked
  with `[HasPermission]`. Existing `HasPermissionReflector` discovers that backend action for permission registry
  onboarding; no frontend route, page or `ReferenceDataManifestProvider` action is invented.
- This `ready-for-dev` status records code-start only for the exact Section 5 allow-list. It does not make any later
  endpoint, seed/publish, consumer, security, readiness, gateway, UI or production path eligible.
- No browser or frontend calls the Platform service port directly.

For draft step `BRD Verified GSKU Catalog Publication`, loading is an internal, explicitly invoked application action
under the configured reference tenant. The seed file's presence must not activate the existing hosted worker or any
environment. Disabled, Mock and FailClosed governance modes are all ineligible to publish or to produce a provider-
ready claim; only an owner-approved production-safe mode may enter durable publication. A successful return requires
the same operation's immutable fingerprint, all durable checkpoints, re-read target Published/Immutable state,
`PublishedVersionId` equality and `COMPLETED/COMPLETION_VERIFIED`. Any stale pointer, set `RowVersion`, target
`ConcurrencyToken`, crash or mismatched replay fails closed or remains `RECOVERY_REQUIRED`; it never returns success
or exposes a false published claim.

For `Verified GSKU Resolver`, the only eligible application operation is a typed batch of one or two `LATEST`
selections, with each SetCode restricted to `pack-applicability` or `uom`, at most one selection per set, and ValueCode
restricted respectively to `SCALAR_QUANTITY_APPLIES` or `C62|GRM|KGM|MLT|LTR`. It contains no tenant, scope key,
version, server-derived mode/timestamp, `as_of`, effective date, `include_deprecated`, publication operation or
provider-readiness assertion. Submit/approval `PINNED` behavior is not implemented by this step.

The required execution order is indivisible and fail-closed:

1. MDM's existing JwtBearer handler has already validated the inbound user Bearer JWT. The typed resolver client reads
   the raw Authorization value only from the current authenticated request, forwards it only on the one resolver
   `HttpRequestMessage`, and never copies it to default headers, DTOs, logs, responses, persistence or audit metadata.
2. Platform independently validates the forwarded JWT through its existing JwtBearer handler, including signature,
   issuer, audience, lifetime and signing-key rotation. The `/api/internal` middleware bypass is not tenant evidence.
3. The resolver controller rejects any `X-Tenant-Id` and any tenant-bearing input, requires exact
   `actor_type = tenant_user`, parses one non-empty `tenant_id` claim from the validated Platform principal and rejects
   missing/malformed/multiple tenant claims plus `platform_admin`, `partner_admin` and every other actor type.
4. Platform validates the resolver-only credential identifier/secret with timing-safe active/previous comparison,
   expiry/overlap and revocation checks. The server mapping must derive exactly consumer service `DITENMDMSERVICE`
   and audience `VERIFIED_GSKU_RESOLVE`; the credential options/result contain no tenant field or constraint.
5. Only after both independent factors pass does the controller open the outer consumer `TenantScope` using solely
   the Platform-validated JWT `tenant_id` and dispatch the resolver query. MDM sends neither tenant nor reference
   tenant in header, route, query or body. The same MDM resolver credential is valid across tenants; authorization to
   either locked set is still decided separately by that JWT tenant's assignment in step 7.
6. The query handler validates
   `BusinessReferenceDataProviderOptions.ReferenceTenantId` without fallback and opens a nested reference-tenant
   `TenantScope`. The generic PSS-012 consumer service is not called and cannot provide verified resolver evidence.
7. Under reference scope and before set/version/value reads, each requested SetCode must have the exact ACTIVE,
   non-deleted assignment `ReferenceTenantId + captured ConsumerTenantId + SetCode`. Failure is the same non-leaking
   404 and no catalog read occurs.
8. `LATEST` reads only the set's current `PublishedVersionId`. Set/version/reference-tenant identity must agree and
   the version must be immutable. Generic ordering, scope-key, `as-of` or merely `Status=Published` is not proof.
9. The live pointer must agree with a matching non-deleted publish operation in
   `COMPLETED/COMPLETION_VERIFIED`. Disabled, Mock, FailClosed, generic loader/publisher output, generic resolver output
   and legacy publication status are rejected as false proof.
10. The exact requested value is selected. Retired/deprecated or otherwise non-selectable values are rejected for new
   `LATEST` selection; no replacement, free-text, hardcoded or cached semantic fallback is used.
11. The response returns only server-derived SetCode, ValueCode, catalog version ID/number, `LATEST`, trusted
   `ResolvedAtUtc`, `IsRetired` and `SelectableForNew`. It never returns display/attribute, assignment,
   reference-tenant or publish-operation data.
12. Nested reference scope and outer consumer scope use `using`/`finally` semantics. Success, mapped failure,
   exception, timeout and cancellation restore Platform reference -> consumer -> prior/unresolved context. MDM
   disposes the per-call request/token header and restores its request/client context on every exit.

13. Platform uses a fixed two-second server budget with no retry. MDM uses a shorter caller cancellation budget or
    propagates cancellation; timeout/5xx never triggers fallback. The MDM client builds per-call requests without
    mutating shared/default headers and restores/disposes request scope on success, mapped failure and cancellation.

The existing generic `GetPublishedValuesAsync`, `GetValuesAsync`, `GetHierarchyAsync`, generic PSS-012 resolver,
generic loader and legacy publisher remain unchanged and are never MOD-0290 provider-ready or verified-publication
proof.

### Draft runtime contract — `Verified GSKU Catalog Operational Readiness & Bounded UoM Enumeration`

#### A. Verified GSKU Catalog Local Operational Foundation

The one-shot runner is eligible only when every condition is true before the first mutation: the host is
`IHostEnvironment.IsDevelopment()`, `Enabled=true`, the exact artifact path/version/SHA-256 fingerprint matches, the
configured `ReferenceTenantId` and explicit `ConsumerTenantId` are non-empty and different, the trusted server actor
is non-empty, and the required sets are exactly `pack-applicability` and `uom`. Environment name alone is never
authority. The same options in Staging or Production are rejected before loader, publisher or assignment dispatch.

Disabled, Mock and FailClosed remain ineligible. Existing `Live` remains ineligible without real workflow/evidence
adapter proof. The runner receives a separate narrow eligibility decision solely for the locked Development pilot;
it does not replace, wrap or return a positive result from runtime `IBusinessReferenceDataPublicationEligibility`,
does not register the test-only positive seam and does not change generic PSS-012/production behavior.

The registered narrow eligibility service issues an opaque, non-bindable, non-serializable, operation-scoped authorization only after that
preflight. The runner passes it into the verified loader; the loader propagates it to the verified publisher, which
revalidates its exact artifact/fingerprint/tenant/actor/idempotency facts. No string/boolean/config flag can be supplied
directly to `PublishVerifiedAsync` to bypass eligibility, and all existing callers without that authorization continue
through the always-fail-closed runtime eligibility decision (or the existing test-only seam in tests).

One process start performs at most one reconciliation turn in this exact order:

1. Validate all operational facts and locked artifact identity/fingerprint without mutation.
2. Invoke `IBusinessReferenceDataCatalogLoaderService.LoadVerifiedGskuCatalogFromFileAsync` with the same stable
   artifact and idempotency namespace.
3. Reuse `IBusinessReferenceDataPublishService.PublishVerifiedAsync` and its existing
   `BusinessReferenceDataPublishOperation` checkpoint state machine; never invoke the generic/legacy publisher.
4. Re-read each pointer, immutable target version and matching non-deleted operation. Both sets must agree with
   `COMPLETED/COMPLETION_VERIFIED`; partial publication is failure/recovery, never success.
5. Dispatch the supported assignment application command, which reconciles exactly one ACTIVE non-deleted assignment
   for each locked set to the explicit consumer tenant.
6. Re-read both assignments and return/log only sanitized outcome codes and correlation data.

Crash/restart repeats the same facts and resumes the same publish operations. It creates neither a second catalog
version nor duplicate operation/assignment. Assignment create is idempotent only when tenant, set, ACTIVE state and
owner-approved payload facts match. Existing ACTIVE payload drift is `409 REFERENCE_ASSIGNMENT_CONFLICT`; REVOKED,
inactive or soft-deleted history is not silently replaced. Reactivation requires a separate owner-approved operation
with exact expected version and is not performed by this runner. A consumer tenant equal to the reference tenant is
rejected before mutation.

Missing, empty, malformed or unbindable provider tenant configuration must be observable at startup/readiness as an
invalid provider dependency but must not escape as an uncontrolled `OptionsValidationException` while constructing a
request-scoped repository/handler. Provider-dependent request paths deterministically return
`503 REFERENCE_PROVIDER_CONFIGURATION_INVALID`; other generic BRD paths are not made unnecessarily unstartable.
Hardcoded GUID, ambient tenant and `BusinessReferenceData:CatalogLoad:TenantId` fallback are forbidden.

Startup binding records a sanitized invalid-state diagnostic and leaves the host/liveness up; it does not use
`ValidateOnStart` for provider options. The `ready`-only health check reports unhealthy until provider configuration is
valid and, when the Development pilot is enabled, both locked publications and both assignments pass read-back. It
does not run the provisioner or mutate data. Request-time handlers use the same non-throwing classification source and
map invalid configuration to the stable 503 envelope. This separates host construction, readiness and request failure
without making unrelated BRD controllers or liveness unavailable.

#### B. Bounded Verified UoM Enumeration

The additive bodyless action is `POST /api/internal/v1/reference-data/verified-gsku/enumerate-uom`. It reuses the
existing verified-resolver service credential only to prove `DITENMDMSERVICE + VERIFIED_GSKU_RESOLVE`; tenant never
comes from that credential. Platform independently validates the forwarded Bearer JWT, accepts exact `tenant_user`
and one valid `tenant_id`, rejects `X-Tenant-Id`, establishes consumer scope, and restores it deterministically after
success, mapped failure, exception, cancellation or timeout.

Under nested reference scope the handler checks ACTIVE/non-deleted assignments for `pack-applicability` and `uom`
before any catalog read. A cross-tenant decoy assignment cannot authorize. It then proves the exact locked artifact,
set and values through the live pointer, immutable published target and matching non-deleted
`COMPLETED/COMPLETION_VERIFIED` operation. Pointer/status, generic PSS-012 publication or any non-locked artifact is
insufficient.

Only ACTIVE/selectable `C62`, `GRM`, `KGM`, `MLT`, `LTR` are returned, in stored `SortOrder` with a deterministic code
tie-breaker. Retired/missing/extra/duplicate/out-of-contract values do not produce a partial list or fallback. The
exact successful item shape is `Code`, `DisplayText`, `SortOrder`, `MaximumDecimalPrecision`; precision must be
`C62=0` and all four mass/volume values `=3`. No cache or hardcoded response list may substitute for verified reads.
The same two-second Platform budget and no-retry rule as resolve applies.

### Planning runtime constraints — `Verified Market Catalog for LSKU Draft Identity Foundation`

- The universal/shared decision is semantic access, not physical tenant ownership: current BRD persistence may use
  the server-resolved reference tenant internally, but consumer tenant assignment is never evaluated for `market`
  and no reference-tenant identifier crosses the provider transport boundary.
- The new controller must delegate credential authentication, independent delegated-JWT validation, caller tenant
  scope establishment, linked two-second budget and deterministic restoration to the shared verified-reference
  executor. It may perform only strict market request-shape validation and application dispatch around that boundary;
  copying the sequence or accepting `X-Tenant-Id` is prohibited.
- Resolver/enumeration handlers enter the provider's reference scope only after caller authentication, use read-only
  repository contracts, and never load, publish, repair, provision or create assignments.
- The catalog is not usable until the immutable artifact is loaded and published by a separately authorized
  operational action through the verified loader/publisher and durable operation state machine. Before that, both
  surfaces return `503 REFERENCE_PROVIDER_UNAVAILABLE`.
- No application startup, test fixture, hosted worker, appsettings value or direct repository/Mongo call may activate
  the catalog. A test may arrange Mongo state through production application contracts only; it may not inject a
  production-positive eligibility seam.
- Existing `pack-applicability` and `uom` stay code-owned `GSKU-UNIVERSAL-V1` lookups with their current values,
  identities and response shapes. Market work cannot route them through Mongo, assignment or publication paths.

## 9. Layout & Shell Contract

- `shell: none`.
- This is a backend/provider contract; no Razor layout, page, navigation item or DataTable is created.
- `golden_reference: none` and `form_field_count: 0` are deliberate.
- Any future stewardship UI is a separately scoped and approved artifact with its own Slim/Compact decision.

## 10. Backend File Convention

The existing `Features/BusinessReferenceData` feature boundary is retained. Any authorized implementation follows:

```text
services/Diten.Platform/src/Diten.Platform.Application/Features/BusinessReferenceData/
├── Commands/
├── Queries/
├── Handlers/
│   ├── CommandHandlers/
│   └── QueryHandlers/
├── Validators/
├── Models/
└── Services/
```

- One public command/query/handler/validator per file.
- Commands and queries end with `Command`/`Query`; handlers use `{Verb}{Subject}Handler` without
  `CommandHandler`/`QueryHandler` suffix.
- Controllers remain thin and return the repository-standard `Response<T>` envelope.
- Domain entities do not depend on API concerns.
- Existing public contracts are not silently broken; breaking changes require an explicit compatibility decision.

## 11. Frontend File Contract

- No frontend files are created or changed.
- No lookup dropdown, management screen, DataTable, JavaScript client, localization resource or proxy controller is
  part of the current named step.
- PSS-011 Platform system lookup consumers remain unchanged.
- A future consumer UI must receive separate scope, route, shell, localization and Golden Reference approval.

## 12. Validation Rules

| Contract element | Required | Validation | Persistence/pre-check |
|---|---:|---|---|
| Parent identity | Yes | Must remain `MOD-0048` | Registry parent collision check |
| SetCode | Yes | Exact lowercase stable code for the initial two families; surrounding whitespace or case variants are rejected; no semantic alias collision | Unique in approved semantic scope |
| `pack-applicability` values | Yes | Initial catalog exactly `SCALAR_QUANTITY_APPLIES` | Reject additional initial values without scope revision |
| `uom` values | Yes | Initial catalog exactly `C62`, `GRM`, `KGM`, `MLT`, `LTR` | Case-stable uniqueness and no-reuse across versions |
| Enterprise semantic scope | Yes | Exactly one reference-tenant canonical catalog; tenant-local semantic copies/overrides forbidden | Canonical-source and reference-tenant ownership proof |
| Canonical catalog tenant | Yes | Trusted typed configuration only; never client DTO/header/route/body input | Host stays up; provider readiness unhealthy and provider-dependent surface returns exact `503 REFERENCE_PROVIDER_CONFIGURATION_INVALID`; no fallback tenant |
| Provider readiness registration | Later step only | Actual `Program.cs` `AddDitenObservability` callback; `ready` tag only | Protected in the current named step; no health behavior is enabled |
| Provider option separation | Yes | `BusinessReferenceDataCatalogLoadOptions.TenantId` is seed-only and cannot resolve provider identity/access | No fallback between option types |
| Delegated user JWT | Named step | MDM forwards only the already validated current-request Bearer token; Platform independently validates signature/issuer/audience/lifetime/key before claim use | Missing/invalid token is 401; raw token never enters DTO/log/response/persistence/audit |
| Trusted consumer context | Named step | Platform resolver controller requires exact `tenant_user`, one non-empty GUID `tenant_id` and no `X-Tenant-Id` or tenant input; credential authentication carries no tenant | Missing/malformed/multiple claim or platform/partner actor is 403 before dispatch/read; a valid JWT tenant proceeds only to its own assignment check |
| Tenant assignment | Yes for access | Exact predicate `ReferenceTenantId + captured ConsumerTenantId + SetCode + ACTIVE + !IsDeleted`; semantic mutation forbidden | Check before any catalog read; partial unique index on reference tenant + consumer tenant + SetCode |
| Assignment mutation | Yes | Trusted actor, expected `Version`, ACTIVE/REVOKED lifecycle and soft delete | Stale update/revoke fails 409; revoked/deleted assignment grants no access |
| Usage versus assignment | Yes | Usage metadata never grants access and cannot replace an assignment | Resolve path checks assignment independently of usage registration |
| Catalog version | Yes | Positive; immutable after publish | Set + version uniqueness and expected concurrency |
| Effective interval metadata | Conditional | If stored, `[from,to)` and start before end; scheduled effective-period selection is outside the first delivery | Persistence validation only; no first-delivery scheduled selection claim |
| UoM value attributes | Yes | `C62=COUNT/0`; `GRM,KGM=MASS/3`; `MLT,LTR=VOLUME/3` | Required/type/enum/range validation before submit/publish |
| Replacement | Conditional | Same set, existing value, no cycles, no meaning reuse | Replacement graph validation |
| Publish request | Later step only | Approved actor, non-self approval, idempotency, expected state/version | No publish behavior is implemented by this foundation |
| Publish operation identity | Yes | Non-empty immutable operation/set/version identities; all share configured reference tenant | Non-deleted unique `TenantId (ReferenceTenantId) + IdempotencyKey`; target mismatch on replay is 409 |
| Operation state | Yes | Only Section 4 lifecycle transitions; `COMPLETED` immutable; post-pointer failure remains recoverable | Conditional state transition with expected operation version |
| Publish checkpoint | Yes | Monotonic Section 4 checkpoint order; never used as operation lifecycle state | Conditional checkpoint advance; no skip to pointer/completion |
| Pointer context | Yes | Captured expected pointer and concurrency tokens must still match | Stale operation fails/reconciles without overwrite |
| Usage registration | Consumer use | Consumer + set + scope + first-delivery latest/pinned mode uniqueness | Index must include the approved scope dimensions |
| Full consumer lifecycle | Deferred | Draft `latest` and submit/approval pinned behavior remain the broader provider contract | This named step implements only draft `LATEST`; `as-of` remains rejected |
| Verified GSKU credential | Named step | Resolver-only active/eligible credential; server mapping derives `DITENMDMSERVICE` + `VERIFIED_GSKU_RESOLVE`; FU16/shared key/wrong audience forbidden | 401/403 before MediatR and repository access |
| Consumer tenant context | Named step | One non-empty tenant exclusively from Platform-validated JWT `tenant_id`; no credential/header/body/route/query tenant | Per-request tenant scope followed by ACTIVE/non-deleted assignment; two-tenant decoy isolation before catalog read |
| Verified GSKU request | Named step | One or two unique selections; only the two locked SetCodes and six locked ValueCodes; `LATEST` only; no tenant/version/server-mode/timestamp/scope/as-of/effective/include-deprecated input | Tenant-free API model and validator; contract mismatch is 409 without repository access |
| Latest version proof | Named step | Set pointer, target immutable version and matching non-deleted `COMPLETED/COMPLETION_VERIFIED` operation agree | Version status or generic/legacy result alone is insufficient |
| Historical pinned proof | Deferred | Submit/approval `PINNED` is not accepted or resolved by this named step | Later MOD-0290 step must define exact historical behavior; no fallback is introduced here |
| Retired value | Named step | `LATEST` new selection rejects when `IsRetired=true` or `SelectableForNew=false` | Never silently upgrades to replacement or free text |
| Resolve timeout | Named step | Fixed two-second Platform budget, no retry; caller cancellation remains distinct | Timeout maps to 504 once, no retry/fallback or scope leak |
| MDM client evidence fields | Named step | SetCode, version ID/number, mode and timestamp are provider-derived and absent from caller DTOs | Reject unknown/forbidden fields; never trust client evidence |
| Local operational enablement | Draft named step | `Development` AND explicit `Enabled=true`; all other environments/modes reject before mutation | Separate eligibility contract; generic/runtime publication eligibility unchanged |
| Locked artifact | Draft named step | Exact path, `1.0.0`, approved SHA-256 fingerprint, exactly two sets/six values | Validate before load/publish; directory scan and legacy loader forbidden |
| Operational tenant/actor | Draft named step | Non-empty configured reference tenant, explicit non-empty different consumer tenant, non-empty trusted actor | No ambient/header/body/catalog-load/hardcoded fallback; failure before mutation |
| Assignment reconciliation | Draft named step | Exactly `pack-applicability` and `uom`; matching ACTIVE record is replay, drift/conflict is 409 | No duplicate; revoked/deleted/inactive is not auto-reactivated/replaced |
| UoM enumeration request | Draft named step | Bodyless; no query/header tenant or additional fields | Contract rejection before assignment/catalog reads |
| UoM enumeration result | Draft named step | Exact five codes, deterministic order, exact four fields and precision `0/3/3/3/3`; ACTIVE/selectable only | Assignment first; locked fingerprint + pointer + immutable target + verified operation proof |
| BRD auth metadata | Later endpoint step only | Explicit `BusinessReferenceDataEndpointAttribute`; absent metadata cannot select BRD auth codes | Protected in the current named step |
| Assignment permission onboarding | Later endpoint step only | Exact `[HasPermission("Platform.BusinessReferenceData.Assignment.Manage")]`; no implied permission and no frontend manifest action | Protected until a real assignment action is separately authorized |
| Runtime code-start gate | Yes | `ready-for-dev` + exact Foundation and `Verified GSKU Resolver` allow-lists + their recorded explicit user authorizations | Every non-allow-listed FU01 runtime path remains blocked |

## 13. Failure Path to Verify

For the current foundation step, repository/option tests must fail closed without creating runtime behavior:

- duplicate active assignment violates the partial unique index; soft-deleted history does not authorize access;
- stale assignment expected version cannot update, revoke or reactivate another writer's state;
- cross-reference-tenant or cross-consumer-tenant reads and mutations return no record and leak no identifier;
- same publish idempotency key with a different set/version target is a replay conflict;
- state/checkpoint writes are conditional and monotonic, survive re-read, and cannot persist a completed/published
  foundation claim unless pointer/target/operation verification prerequisites agree;
- missing/empty provider tenant remains invalid and cannot fall back to catalog-load options.

These are internal persistence/contract outcomes. The HTTP wire contract below is locked for later endpoint and
publish steps; no controller, auth envelope or endpoint behavior is authorized by the current foundation:

The following wire contract is locked for this pack. Every failure returns
`Response<T>.Fail(errorCode, statusCode, reasonCode: errorCode, correlationId)` with `Data = null`,
`IsSuccessful = false`, `StatusCode` equal to the HTTP status, `Errors = [errorCode]`, `reason_code = errorCode` and
the trusted request `correlation_id`. No cross-tenant identifier, catalog payload or internal exception text is
included.

| Failure code | HTTP | Exact behavior |
|---|---:|---|
| `REFERENCE_SET_NOT_FOUND` | 404 | Set absent; no alternate tenant lookup |
| `REFERENCE_SET_NOT_ACCESSIBLE` | 404 | Missing/wrong tenant assignment; non-leaking |
| `REFERENCE_VALUE_NOT_FOUND` | 404 | Value absent; no free-text acceptance |
| `REFERENCE_VERSION_PIN_CONFLICT` | 409 | Required pinned version cannot be resolved; submit/approval never falls back to latest |
| `REFERENCE_VALUE_RETIRED` | 409 | New selection rejected; historical resolution remains available |
| `REFERENCE_SCHEMA_CONFLICT` | 409 | Required attribute key/type/enum/range differs from the published schema |
| `REFERENCE_CONTRACT_MISMATCH` | 409 | Set/version/access contract conflicts with the consumer registration |
| `REFERENCE_PUBLISH_CONFLICT` | 409 | Stale version or concurrent publish; no pointer overwrite |
| `REFERENCE_PUBLISH_OPERATION_STALE` | 409 | Expected pointer/version or operation fencing context is stale; no write/pointer overwrite |
| `REFERENCE_PUBLISH_RECOVERY_REQUIRED` | 409 | Operation is `RECOVERY_REQUIRED`; replay resumes its last durable checkpoint and publication is not exposed as complete |
| `REFERENCE_TENANT_OVERRIDE_FORBIDDEN` | 409 | Client attempts to supply/override canonical reference tenant identity |
| `REFERENCE_ASSIGNMENT_CONFLICT` | 409 | Duplicate or stale assignment create/revoke/update; no last-write-wins |
| `REFERENCE_RESOLUTION_CONTRACT_INVALID` | 409 | Unsupported set/value/mode, duplicate set, missing/forbidden pin fields, `as-of` or scheduled-effective input; no catalog read |
| `REFERENCE_PUBLICATION_NOT_VERIFIED` | 503 | Pointer/target/durable-operation proof is absent or inconsistent; generic legacy status/result is never accepted |
| `REFERENCE_GOVERNANCE_NOT_PRODUCTION_SAFE` | 503 | Disabled, Mock or FailClosed mode cannot publish or satisfy production consumption |
| `REFERENCE_PROVIDER_CONFIGURATION_INVALID` | 503 | `ReferenceTenantId` is missing, empty or invalid; no catalog-load/ambient/hardcoded fallback |
| `REFERENCE_PROVIDER_UNAVAILABLE` | 503 | Provider unavailable; no hardcoded or unbounded-cache fallback |
| `REFERENCE_PROVIDER_TIMEOUT` | 504 | Provider timeout; safe retry classification and no duplicate mutation |
| `REFERENCE_UNAUTHENTICATED` | 401 | Delegated user JWT or resolver credential is missing/invalid/expired/revoked; reject before dispatch/read |
| `REFERENCE_FORBIDDEN` | 403 | JWT actor/tenant claim is invalid, `X-Tenant-Id` is present or credential service/audience is wrong; no assignment/catalog read |

For `Verified GSKU Catalog Operational Readiness & Bounded UoM Enumeration`, the following refinements are exact:

- operational preflight failure (non-Development, disabled, artifact/version/fingerprint mismatch, invalid/same tenant
  identities or missing trusted actor) fails before mutation with a sanitized stable operational reason; it never
  falls through to generic publication or the test seam;
- duplicate/drifted/inactive/revoked/deleted assignment facts return `409 REFERENCE_ASSIGNMENT_CONFLICT`; automatic
  reactivation or replacement is forbidden;
- missing either enumeration assignment is non-leaking `404 REFERENCE_SET_NOT_ACCESSIBLE` before catalog read;
- missing locked set is `404 REFERENCE_SET_NOT_FOUND`; missing locked value is `404 REFERENCE_VALUE_NOT_FOUND` only
  after both assignments succeed;
- extra/duplicate/out-of-contract locked-set content, precision/display/sort mismatch or retired/non-selectable values
  return `409 REFERENCE_CONTRACT_MISMATCH` with no partial list;
- invalid provider configuration or absent/incomplete/mismatched publication proof returns respectively
  `503 REFERENCE_PROVIDER_CONFIGURATION_INVALID` or `503 REFERENCE_PUBLICATION_NOT_VERIFIED`;
- Disabled/Mock/FailClosed and unproven Live return `503 REFERENCE_GOVERNANCE_NOT_PRODUCTION_SAFE`; timeout is exact
  `504 REFERENCE_PROVIDER_TIMEOUT`; no fallback is used.

Additional required failure tests: duplicate SetCode/ValueCode, attribute required/type/enum failure, replacement cycle,
invalid stored effective interval, wrong tenant, stale pin, unsupported/deferred `as-of` mode, duplicate usage
registration and publish retry after crash at every persistence boundary. Failure handling must also prove that JWT
challenge/permission results are middleware/authorization failures, while configuration, assignment, catalog,
publish and availability failures are provider business failures returned by the provider surface.

Metadata-absent Platform endpoints preserve their existing JWT challenge and permission-denial responses. Regression
tests must prove that adding BRD metadata handling does not emit `REFERENCE_UNAUTHENTICATED` or
`REFERENCE_FORBIDDEN` outside explicit BRD endpoints.

For `Verified GSKU Resolver`, exact ownership of the requested status classes is:

- `401 REFERENCE_UNAUTHENTICATED`: missing/invalid/expired delegated JWT; or missing/unknown credential identifier,
  missing/wrong secret, expired previous secret or revoked resolver credential. No dispatch/read occurs.
- `403 REFERENCE_FORBIDDEN`: JWT lacks exactly one non-empty `tenant_id`, actor is not `tenant_user`,
  `X-Tenant-Id` is present, or credential mapping is not exactly `DITENMDMSERVICE` +
  `VERIFIED_GSKU_RESOLVE`. FU16 and shared keys do not qualify. No credential/JWT tenant-mismatch branch exists because
  the credential carries no tenant.
- non-leaking `404 REFERENCE_SET_NOT_ACCESSIBLE`: missing/revoked/deleted/wrong-consumer assignment. Authorized callers
  may receive `REFERENCE_SET_NOT_FOUND` or `REFERENCE_VALUE_NOT_FOUND` only after assignment succeeds; none includes
  tenant, assignment, reference-tenant or operation identifiers.
- `409 REFERENCE_VALUE_RETIRED` or `REFERENCE_RESOLUTION_CONTRACT_INVALID`: retired/non-selectable new selection,
  unsupported set/value/mode, duplicate set, or supplied tenant/version/mode/timestamp evidence; never fallback to
  replacement, free text or hardcoded data.
- `503 REFERENCE_PROVIDER_CONFIGURATION_INVALID`, `REFERENCE_PUBLICATION_NOT_VERIFIED` or
  `REFERENCE_PROVIDER_UNAVAILABLE`: invalid provider identity, absent/inconsistent verified publication or provider
  dependency unavailable respectively. Generic legacy publication never upgrades any of them to success.
- `504 REFERENCE_PROVIDER_TIMEOUT`: the fixed two-second Platform budget elapsed. It is not used for caller-
  initiated cancellation, does not retry internally and restores scopes before returning.

All use `Response<T>.Fail(code, status, reasonCode: code, correlationId: trustedCorrelation)`. Correlation comes from
the existing server correlation context, never from a request DTO; internal exception text and credentials are never
returned.

Publish failure handling persists the operation state/checkpoint before returning. Retry uses the same operation and
idempotency key; conflicting target reuse fails 409. A crash at each checkpoint resumes without duplicating writes.
Stale expected pointer/version context cannot promote. `POINTER_PROMOTED` failures remain `RECOVERY_REQUIRED` until
pointer, target, prior-version transitions and completion are re-read and reconciled; no partial state is returned as
published.

For every provider business failure, consumer context is captured before temporary scope. Invalid provider options
return the exact 503 without catalog access. A failed exact assignment predicate returns the non-leaking 404 without
catalog access. Success, exception, cancellation and every early failure dispose the temporary reference scope and
restore the captured consumer scope before the response leaves the provider path. PSS-011 allow-list behavior is not
a fallback failure path.

## 14. Authorization Convention

- Controller policy: authenticated Platform service/steward surface; no anonymous access.
- Permission boundaries are fixed for this follow-up:
  - `Platform.BusinessReferenceData.Consumer.Read`
  - `Platform.BusinessReferenceData.Usage.Register`
  - `Platform.BusinessReferenceData.Version.Submit`
  - `Platform.BusinessReferenceData.Version.Approve`
  - `Platform.BusinessReferenceData.Version.Publish`
  - `Platform.BusinessReferenceData.Assignment.Manage` (new, distinct assignment-admin key).
- `Platform.BusinessReferenceData.Assignment.Manage` is placed only on a real backend assignment-administration
  controller action through `[HasPermission]`. Existing `HasPermissionReflector` discovers the action key and the
  existing controller-reflection worker performs registry onboarding. The frontend-route-only
  `ReferenceDataManifestProvider` and its zero-drift tests remain unchanged; no synthetic page/action is added.
- The assignment action and endpoint do not yet exist. They are explicit implementation deliverables after all
  later endpoint-step gates close; the current named step neither creates the action nor registers the permission.
- Before any later security, S2S, assignment-administration or consumer-exposure named step, the Security and Reference
  Data owners must approve:
  - canonical human steward and approver identity;
  - submitter/approver separation with no self-approval bypass;
  - service identity versus delegated human identity;
  - MOD-0290 least-privilege read/resolve/usage permissions;
  - tenant binding and credential ownership/rotation;
  - break-glass behavior, audit and expiry, if allowed.
- Request-body actor or tenant values are never trusted as the authenticated decision identity.
- Before temporary reference scope begins, the consumer tenant is captured from trusted server-side `ITenantContext`;
  reference tenant comes only from `BusinessReferenceDataProviderOptions`. Scope exit restores the consumer context
  deterministically, and assignment is checked before catalog access.
- A consumer credential cannot select the reference tenant, create an assignment or elevate itself to a
  steward/publisher. Assignment admin is not combined with consumer, steward or publisher roles; neither Consumer.Read
  nor Usage.Register nor version permissions imply `Assignment.Manage`.
- Explicit provider-dependent actions carry `BusinessReferenceDataEndpointAttribute` endpoint metadata.
  Unauthenticated requests on those endpoints are owned by the JWT challenge configured in the existing
  `Infrastructure/DependencyInjection.cs` registration and return exact `401 REFERENCE_UNAUTHENTICATED`
  `Response<T>`. Authenticated permission denial on those endpoints is owned by `HasPermissionAttribute` and returns
  exact `403 REFERENCE_FORBIDDEN` `Response<T>`. Both paths read endpoint metadata before selecting the BRD contract.
- When BRD metadata is absent, JWT challenge and `HasPermissionAttribute` retain the existing Platform response
  behavior. Provider business failures never masquerade as middleware challenge/forbid results, and global
  unconditional `REFERENCE_*` mapping is prohibited.

For `Verified GSKU Resolver`:

- Transport uses resolver-only `X-Verified-Gsku-Resolver-Credential-Id` and
  `X-Verified-Gsku-Resolver-Credential` headers. The first is non-secret; the second is sensitive and never logged,
  returned, placed in DTOs or committed configuration.
- Platform validates active/previous secret eligibility with timing-safe comparison and derives exactly
  `ConsumerServiceCode = DITENMDMSERVICE` and `AllowedAudience = VERIFIED_GSKU_RESOLVE` from server configuration.
  Credential configuration and authentication results contain no `ConsumerTenantId`. FU16 module-registration credential,
  shared internal keys and AuthService token issuance are not reused or modified.
- MDM forwards the current inbound Bearer JWT only for this resolver request. Platform independently validates it,
  accepts exact `actor_type = tenant_user` and derives tenant only from one non-empty `tenant_id`. `platform_admin`,
  `partner_admin`, missing/malformed/multiple claim and `X-Tenant-Id` fail before MediatR.
- Service credential answers which service/audience may call; validated JWT answers which current
  tenant user delegated the call; ACTIVE assignment answers which set that tenant may read. None substitutes for
  another. The controller establishes `ITenantContext` itself because generic `/api/internal` middleware bypass is
  explicitly non-evidence.
- Raw JWT and resolver secret are transport-only sensitive values. They are absent from DTOs, logs, responses,
  persistence, audit metadata and exception text, and per-call headers are disposed on every exit.
- Missing assignment is a non-leaking business 404, not a permission 403. A credential cannot choose the reference
  tenant, create an assignment, access generic stewardship routes or mutate catalog/publish state.

For `Verified GSKU Catalog Operational Readiness & Bounded UoM Enumeration`:

- The Development runner has no HTTP surface and accepts no caller credential. Its actor and tenant facts are trusted
  server-side operational configuration, validated before mutation; it cannot grant itself generic publish
  eligibility or assignment-administration permission.
- Enumeration reuses exactly the resolver credential/JWT contract above. No new audience, secret, API key,
  permission, tenant header or AuthService issuance is introduced. Both `pack-applicability` and `uom` assignments are
  checked before any read even though only UoM items are returned.
- The same credential can serve multiple consumer tenants because tenant comes only from each independently validated
  user JWT; per-set assignment remains the authorization decision. Scope and sensitive-header cleanup are mandatory
  on success, failure, cancellation and timeout.

## 15. Gateway / API Routing Decision

- Decision for this named step: **no gateway or route change is authorized**.
- Existing Business Reference Data controllers are implementation evidence only.
- The PSS-011 `/api/lookups/{everything}` route must not be widened or reused to hide this business provider contract.
- Any new internal/S2S or external consumer route requires a separately approved API exposure step, exact auth and
  tenant propagation contract, and integration-agent ownership for `ocelot.json`.
- Direct browser-to-5057 access is forbidden.

Decision for `Verified GSKU Resolver`:

- API/S2S: one authenticated internal Platform action is in scope; the locked route is
  `POST /api/internal/v1/reference-data/verified-gsku/resolve`. The tenant-free body contains `selections` only and
  the response uses `Response<BusinessReferenceDataVerifiedResolveResult>`.
- Tenant/auth metadata: the generic `/api/internal` bypass remains untouched. The controller requires both the
  resolver credential and Platform-validated delegated user JWT, rejects `X-Tenant-Id`, derives tenant only from
  `tenant_id`, checks exact `tenant_user`, then establishes/restores scope explicitly. The credential supplies no
  tenant; per-set assignment is the tenant authorization boundary.
- Readiness: no health endpoint change is in this step. `Program.cs` changes are limited to binding/registering the
  resolver-specific credential component. Request-time provider configuration and
  verified-publication checks fail 503. Ready-only health registration, operational probes and production activation
  remain a later Operations-owned gate; the endpoint's existence is not a readiness claim.
- Gateway: no Ocelot route is required or allowed. MDM-to-Platform uses the approved internal service address and TLS
  boundary. If infrastructure requires gateway traversal, the integration owner must provide a separate route step;
  no catch-all or browser exposure is accepted.
- Frontend/public API: none. The existing public/generic BRD controller is unchanged.

Decision for `Verified GSKU Catalog Operational Readiness & Bounded UoM Enumeration`:

- A has no route. It is a default-disabled Development-only hosted one-shot reconciliation path.
- B adds only `POST /api/internal/v1/reference-data/verified-gsku/enumerate-uom` to the existing internal controller.
- No public Gateway route, Ocelot edit, public BRD controller action, frontend proxy or browser access is allowed.
- The route is Platform-owned and is not one of MOD-0290's four public/MDM endpoints. MOD-0290 later owns only the
  typed-client/create-options consumer delta under its separately authorized step A.

## 16. Acceptance Criteria

### Superseding universal GSKU lookup acceptance

- [x] Exact universal values are code-owned by MOD-0048 and identical for every resolved tenant.
- [x] The resolver and enumeration paths perform no reference-tenant, assignment, publication, operational-
      eligibility or Mongo read for `pack-applicability` and `uom`.
- [x] Resolver credential validation, independently validated tenant JWT and the two-second endpoint budget remain.
- [x] Version identities are non-empty and deterministic; the version number is positive and changes only through a
      reviewed deployment-version change.
- [x] Unknown SetCode/value, duplicate SetCode and non-`LATEST` requests fail closed; clients cannot add or override
      values or precision metadata.
- [x] Generic PSS-012/BRD stewardship, loader, publisher and assignment behavior is not removed or broadened.

### Authorized step — `BRD Provider Internal Foundation`

- [ ] Only the exact Section 5 paths change; `Program.cs`, controller/auth/readiness/appsettings, catalog-load options,
      manifest, gateway, frontend and publish/consumer services remain unchanged.
- [ ] The existing PSS-012 entity/repository/index structure is extended in place; no parallel provider, second
      catalog model, new service or duplicate collection family is introduced.
- [ ] `BusinessReferenceDataProviderOptions.ReferenceTenantId` has no default and no fallback to
      `BusinessReferenceDataCatalogLoadOptions.TenantId`, ambient tenant, client input or a hardcoded GUID.
- [ ] `BusinessReferenceDataTenantAssignment` persists under the reference tenant, uses soft delete and expected-version
      concurrency, and has a partial unique index on `TenantId + ConsumerTenantId + SetCode` where `IsDeleted = false`.
- [ ] Assignment repository operations enforce reference-tenant and consumer-tenant isolation; stale mutations fail
      without last-write-wins and deleted/revoked records cannot be presented as active.
- [ ] `BusinessReferenceDataPublishOperation` persists the approved operation state, checkpoint, idempotency key,
      target and fencing context under the reference tenant.
- [ ] Publish-operation repository/index foundations enforce non-deleted `TenantId + IdempotencyKey` uniqueness,
      same-key/different-target replay conflict, conditional monotonic state/checkpoint persistence and re-readable
      recovery evidence.
- [ ] Foundation tests prove that no incomplete or inconsistent operation can be represented as a verified published
      result. They do not execute seed/publish behavior or claim consumer-visible publication.
- [ ] Internal option/repository registration compiles without `ValidateOnStart`, hosted services, readiness health
      registration, endpoint registration, auth behavior or runtime environment enablement.
- [ ] Unit and real-Mongo evidence in the current-step subsection of Section 17 passes; those tests are explicitly not
      production readiness, runtime enablement, endpoint/auth-envelope proof or a production catalog seed/publication.

### Draft step acceptance criteria — `BRD Verified GSKU Catalog Publication` (not authorized)

- [ ] Only the draft Section 5 exact paths change; all draft-step protected paths remain byte-for-byte unchanged.
- [ ] The new artifact contains exactly `pack-applicability` and `uom`, exactly six locked ValueCodes and no additional
      set or value; placeholders are absent and owner-approved catalog/display metadata is present.
- [ ] The loader resolves ownership exclusively from `BusinessReferenceDataProviderOptions.ReferenceTenantId`, rejects
      empty/mismatched caller tenant input and persists every set/version/value under that reference tenant.
- [ ] The loader persists the two UoM attribute definitions and each locked `DimensionCode` /
      `MaximumDecimalPrecision`; missing, unknown, duplicate, invalid-type or altered locked metadata blocks before
      publication and leaves no published claim.
- [ ] Repeated load of the byte-equivalent owner-approved artifact is idempotent: no duplicate set/version/value or
      operation is created, and the same immutable command fingerprint replays the same verified result.
- [ ] Same catalog identity with different content, or same idempotency key with different set/version/pointer/set-
      version/target-token fencing context, is a conflict and never mutates the previously claimed operation.
- [ ] The existing publish service uses `BusinessReferenceDataPublishOperation` for claim, monotonic checkpointing,
      pre-mutation fencing, post-mutation verification and recovery; the prior best-effort pointer success path cannot
      produce a successful return.
- [ ] Stale `ExpectedPublishedVersionId`, stale set `RowVersion` and stale target `ConcurrencyToken` each reject the
      publish claim, keep the operation non-`COMPLETED` and do not inspect another tenant's records.
- [ ] A crash injected after every durable checkpoint resumes the same operation/fingerprint from the persisted
      checkpoint; after pointer promotion it remains `RECOVERY_REQUIRED` until reconciliation verifies completion.
- [ ] Publication success is returned only when operation `COMPLETED/COMPLETION_VERIFIED`, parent pointer and the
      target's Published/Immutable state agree on re-read; every inconsistent state rejects a false-published claim.
- [ ] Disabled, Mock and FailClosed modes reject publication/provider-ready claims before mutation. Tests may use an
      explicit production-safe test seam approved in Section 18, but may not relabel those modes as safe or add Live
      workflow/evidence behavior.
- [ ] Real-Mongo tests prove reference-tenant ownership, cross-tenant isolation, idempotent load, durable operation,
      stale fencing and crash/recovery against MongoDB rather than mocks or in-memory substitutes.
- [ ] No worker/configuration activation, production environment enablement, controller/API/S2S/auth/health/gateway/
      frontend/MDM change, consumer resolver or production publication occurs.

### Named-step acceptance criteria — `Verified GSKU Resolver` (code-start authorized)

- [ ] Only the exact Section 5 Platform and MDM files change; committed appsettings, health, shared tenant middleware,
      catalog/publish/seed, generic PSS-012 controller/resolver, AuthService, FU16 registration credential, gateway,
      frontend, Global Product/ABB and GSKU entity/handler/validator/persistence remain unchanged.
- [ ] Resolver-audience-specific active MDM credential succeeds; missing/wrong/expired/revoked credential returns 401,
      and valid credential with wrong audience/service returns 403 before MediatR/repository access. Platform derives
      exactly `DITENMDMSERVICE` and `VERIFIED_GSKU_RESOLVE`; credential options/results contain no tenant field.
- [ ] One internal action accepts no tenant/reference tenant in any header/route/query/body, rejects `X-Tenant-Id`, and
      requires both valid resolver credential and independently Platform-validated delegated user JWT. It accepts only
      exact `tenant_user`, derives tenant only from one non-empty `tenant_id` and establishes scope before dispatch.
      Missing/invalid JWT/claim and platform/partner actors fail. The same resolver credential can serve tenant A and
      tenant B, but each succeeds only through its own ACTIVE/non-deleted assignments; a second-tenant decoy cannot
      alter the selected assignment or catalog.
- [ ] Missing/invalid `ReferenceTenantId` returns exact 503 with no fallback; each SetCode requires its own ACTIVE,
      non-deleted assignment before any set/version/value read, and assignment failure is non-leaking 404.
- [ ] Request validation permits only one or two unique `LATEST` selections from the locked two sets/six values;
      tenant, version, server-derived mode/timestamp, `PINNED`, `as-of`, scheduled-effective, scope and
      include-deprecated inputs are rejected before repository access.
- [ ] `LATEST` resolves only the current pointer when set, immutable target and a matching non-deleted
      `COMPLETED/COMPLETION_VERIFIED` publish operation agree. Status, generic loader/publisher output or legacy
      consumer result alone cannot satisfy publication proof.
- [ ] Disabled, Mock and FailClosed state, generic/legacy resolver output and generic/legacy publication result are
      rejected as false proof. Timeout/5xx, free text and hardcoded/cache fallback never produce a selection.
- [ ] A retired/deprecated or `SelectableForNew=false` value is rejected for new `LATEST` selection; replacement is
      never automatic. Submit/approval `PINNED` behavior remains a later step.
- [ ] Response contains only SetCode, ValueCode, CatalogVersionId, CatalogVersionNumber, ResolutionMode,
      ResolvedAtUtc, IsRetired and SelectableForNew. MDM caller DTOs cannot provide set/version/mode/timestamp evidence.
- [ ] Success, 401, 403, 404, 409, 503, 504, exception, cancellation and timeout restore nested reference scope, then
      consumer scope, then the prior/unresolved context deterministically; no subsequent request observes leakage.
- [ ] MDM client uses per-request credential headers, a fixed bounded call, no retry/fallback, and restores/disposes its
      request/client scope on success, mapped failure and cancellation. It forwards the already validated inbound
      Bearer JWT only on that request and never logs, returns, persists or audits the raw token or credential.
- [ ] Every failure uses the exact Section 13 `Response<T>` status, stable `REFERENCE_*` reason code and trusted
      correlation. Metadata-absent endpoints keep their existing auth/failure envelopes.
- [ ] Existing generic PSS-012 consumer methods and QMS/Legal Entity legacy paths remain green and produce no
      MOD-0290 provider-ready, verified-publication or S2S authorization evidence.
- [ ] Real Mongo, contract and integration suites in the resolver subsection of Section 17 pass with executed counts from
      `dotnet test`; mocks/in-memory stores do not substitute for Mongo assignment/publication/isolation evidence.
- [ ] No GSKU aggregate/create-flow code, credential issuer, assignment admin, usage mutation, health registration,
      gateway, frontend, production configuration or production enablement is implemented or claimed.

### Draft named-step acceptance criteria — `Verified GSKU Catalog Operational Readiness & Bounded UoM Enumeration` (no code-start)

**A — Verified GSKU Catalog Local Operational Foundation**

- [ ] Runner is default-disabled, executes only in `Development` with explicit enablement, and completes at most one
      reconciliation turn per process start; the same options in Staging/Production fail before mutation.
- [ ] Preflight validates exact existing artifact path, version `1.0.0`, owner-approved SHA-256 fingerprint, non-empty
      provider/consumer tenants, different tenant identities, trusted actor and exact required sets before mutation.
- [ ] Runtime/test seam and generic eligibility remain unchanged: Disabled/Mock/FailClosed and unproven Live cannot
      publish; the narrow Development eligibility cannot authorize any other BRD operation.
- [ ] The exact chain is loader verified method -> verified publisher -> durable checkpoints -> verified completion
      read-back -> assignment application operation; no legacy worker, public/generic controller or direct Mongo write.
- [ ] Crash/restart with the same artifact/idempotency facts resumes/reconciles the same operations and creates no
      second version, duplicate operation or duplicate assignment; partial publication is never success.
- [ ] Exactly two ACTIVE assignments exist for the explicit consumer tenant. Matching replay is idempotent; payload
      drift/inactive/revoked/deleted conflict is stable 409; reactivation requires separate expected-version approval.
- [ ] Missing/invalid/unbindable provider options never escape uncontrolled from `IOptions.Value`; provider-dependent
      requests return exact 503 while unrelated generic BRD host paths remain startable.
- [ ] Liveness remains healthy; the `ready`-only probe is non-mutating and unhealthy for invalid configuration or,
      when pilot-enabled, missing verified publications/assignments. It never invokes provisioning.
- [ ] No committed appsettings, tenant, credential or secret value is added and no operational run occurs as part of
      implementation completion.

**B — Bounded Verified UoM Enumeration**

- [ ] Exact bodyless route is `POST /api/internal/v1/reference-data/verified-gsku/enumerate-uom`; no Gateway/public
      route is present and the existing resolver credential + independently validated `tenant_user` JWT are reused.
- [ ] `X-Tenant-Id`, missing/malformed/multiple `tenant_id`, wrong actor/audience/service and invalid credential fail
      before dispatch/read; tenant is never taken from credential or request data.
- [ ] ACTIVE/non-deleted `pack-applicability` and `uom` assignments are checked before catalog reads. Missing/wrong/
      decoy assignment is non-leaking `404 REFERENCE_SET_NOT_ACCESSIBLE`.
- [ ] Pointer, exact immutable target and matching non-deleted `COMPLETED/COMPLETION_VERIFIED` operation plus locked
      artifact fingerprint are all proven; Disabled/Mock/FailClosed/unproven Live, generic publication and incomplete
      operation return exact 503.
- [ ] Success returns exactly five ACTIVE/selectable items in deterministic order with only `Code`, `DisplayText`,
      `SortOrder`, `MaximumDecimalPrecision`; precision is `C62=0`, all others `=3`, and forbidden metadata is absent.
- [ ] Retired values are excluded by failing closed rather than returning a partial/fallback list. Missing locked
      set/value maps to exact non-leaking 404; contract drift maps to 409; timeout maps to 504.
- [ ] Consumer/reference scopes and request-sensitive headers are restored/disposed after success, mapped failure,
      exception, cancellation and timeout. No hardcoded or cache fallback exists.
- [ ] No MDM typed client, `IVerifiedGskuReferenceResolver` change, MDM create-options facade/route or GSKU UI is
      implemented by this provider step.

### Planning named-step acceptance criteria — `Verified Market Catalog for LSKU Draft Identity Foundation`

- [x] `SetCode` is server-fixed to exact `market`; MOD-0048 owns lifecycle/publication and MOD-0290 is consumer-only.
- [x] `MARKET-SOURCE-01` is closed: first-phase markets are countries, the source authority is the ISO 3166
      Maintenance Agency, codes are exact ISO 3166-1 alpha-2 `^[A-Z]{2}$` values with no request normalization, and
      country-external commercial/regulatory regions are deferred.
- [x] `MARKET-ARTIFACT-01` records the current official source snapshot/date, complete active rows, declared row
      count/order, usage/license basis, immutable version identity and artifact hash before operational provisioning.
- [x] One universal/shared active catalog is returned identically to every authenticated MDM tenant; neither provider
      tenant assignment nor future tenant/Legal Entity market-operation assignment filters values.
- [x] Exact resolve accepts one canonical code only and rejects whitespace/case/alias/fuzzy variants without mutation
      or fallback.
- [x] Active enumeration is bodyless and returns only `code`, `display_text`, `sort_order`, deterministically
      ordered and within the owner-approved bound; no version/reference-tenant/credential/assignment evidence leaks.
- [x] `ACTIVE -> RETIRED` is terminal, retired codes cannot be newly selected, and published codes are never reused
      for another meaning across versions or terminal paths.
- [x] Every changed catalog is a new immutable version and verified latest reads require pointer + immutable target +
      non-deleted `COMPLETED/COMPLETION_VERIFIED` operation agreement.
- [x] Missing/retired code maps to exact non-leaking `404 REFERENCE_MARKET_NOT_FOUND`; unavailable,
      unconfigured/unpublished/inconsistent provider state maps to `503 REFERENCE_PROVIDER_UNAVAILABLE`; budget
      expiry maps to `504 REFERENCE_PROVIDER_TIMEOUT`; no partial/cached/hardcoded/free-text success exists.
- [x] Generic PSS-012 query results, PSS-011 lookups, raw Mongo documents, Disabled/Mock governance and test-only
      positive seams cannot satisfy verified market publication.
- [x] Existing Pack Applicability exact resolve and UoM exact resolve/enumeration contracts remain byte-for-byte
      compatible at the DTO/route/value/version level and their focused regressions pass.
- [x] Only the exact Section 5 market allow-list changes; MDM, Auth, Gateway, frontend, configuration and data remain
      outside code-start authority.

### Deferred full-provider acceptance criteria — not authorized by this named step

#### Identity and boundary

- [ ] `MOD-0048-FU01` remains a child of canonical `MOD-0048`; PSS-012 remains a deprecated runtime alias.
- [ ] PSS-011 controller/routes/entities and Platform system lookup behavior are unchanged.
- [ ] No MDM, GSKU or MOD-0290 semantic-validation code is included in provider scope.

#### Enterprise catalog and history

- [ ] One configured reference tenant physically owns the canonical enterprise-global catalog; no tenant-local
      semantic catalog, materialized override or client-selected catalog tenant exists.
- [ ] Server-owned tenant assignment grants read/resolve access without permitting SetCode, ValueCode, attribute,
      meaning or version mutation.
- [ ] `BusinessReferenceDataProviderOptions.ReferenceTenantId` has no default, is bound only from trusted server
      configuration and never falls back to catalog-load options or client input.
- [ ] Missing/empty/invalid provider configuration leaves the Platform host running, marks provider readiness
      unhealthy and makes every provider-dependent resolve/assignment endpoint return exact
      `503 REFERENCE_PROVIDER_CONFIGURATION_INVALID`.
- [ ] BRD readiness is registered through the actual `Program.cs` `AddDitenObservability` callback with `ready` only;
      existing live/ready paths, predicates and writers are unchanged, and invalid BRD config does not make liveness
      unhealthy.
- [ ] Assignment access is backed by a durable reference-tenant-owned record with non-deleted uniqueness on
      `ReferenceTenantId + ConsumerTenantId + SetCode`, expected-version concurrency and immediate revoke/delete.
- [ ] Consumer tenant is captured from trusted server-side `ITenantContext` before reference scope; reference tenant
      comes only from provider options; exact assignment predicate
      `ReferenceTenantId + captured ConsumerTenantId + SetCode + ACTIVE + !IsDeleted` passes before catalog read.
- [ ] Temporary reference scope restores the captured consumer scope deterministically after success, exception and
      cancellation; request/header tenant values cannot substitute either trusted identity.
- [ ] Usage registration without an ACTIVE non-deleted assignment does not authorize catalog access.
- [ ] `pack-applicability` resolves the published `SCALAR_QUANTITY_APPLIES` value.
- [ ] `uom` resolves published `C62=COUNT/0`, `GRM=MASS/3`, `KGM=MASS/3`, `MLT=VOLUME/3` and `LTR=VOLUME/3`, where
      the number is `MaximumDecimalPrecision`.
- [ ] Draft selection may use `latest`; MOD-0290 submit/approval pins the exact catalog version, and that historical pin
      remains resolvable after supersession. `as-of` and scheduled effective-period selection are not claimed or
      exposed in the first delivery.
- [ ] Retired values are rejected for new selection but remain resolvable through historical pins; same-set
      replacement is optional and ValueCode meaning is never reused.

#### Governance and publication

- [ ] Disabled and Mock modes are technically incapable of satisfying production publish or production consumer
      readiness for the two catalogs.
- [ ] Production workflow uses trusted actor identity, maker-checker separation, mandatory idempotency and durable
      decision evidence.
- [ ] Publish uses the durable idempotent recovery/reconciliation state machine defined in Section 8, resumes after a
      crash/retry at every write checkpoint and promotes `PublishedVersionId` only after all required writes are
      re-read and verified; no transaction assumption or false published claim remains.
- [ ] One durable `BusinessReferenceDataPublishOperation` owns each reference-tenant idempotency key; state and
      checkpoint follow Section 4, replay cannot change target, stale pointer/version context cannot overwrite, and
      consumer-visible publication requires `COMPLETED/COMPLETION_VERIFIED` agreement with pointer and target.
- [ ] `business_reference_data_publish_operations` has a non-deleted unique index on
      `ReferenceTenantId (TenantId) + IdempotencyKey`.
- [ ] Attribute required/type/enum/range validation runs before approval/publish.

#### Consumption and access

- [ ] MOD-0290 consumes through a documented C boundary; provider does not implement Product/SKU compatibility.
- [ ] Provider B emits the exact Section 13 envelope; MOD-0290 C maps it into its consumer failure behavior without
      changing the provider code/status or introducing semantic fallback.
- [ ] Consumer contract defines exact set/version resolution, tenant binding, access permission, timeout and stable
      failure mapping.
- [ ] Usage registration uniquely represents consumer + set + approved scope + resolution contract.
- [ ] Wrong tenant, unauthorized caller, unavailable provider or contract mismatch fails closed with no data leak,
      hardcoded value or free-text fallback.
- [ ] Every provider failure uses the exact Section 13 `Response<T>` status, error and `reason_code` mapping.
- [ ] JWT challenge emits exact `401 REFERENCE_UNAUTHENTICATED`; authenticated permission denial emits exact
      `403 REFERENCE_FORBIDDEN` only for explicit BRD endpoint metadata; both remain distinguishable from provider
      business failures.
- [ ] Metadata-absent Platform endpoints preserve their pre-existing JWT challenge and permission-denial envelopes;
      no global `REFERENCE_*` auth response is introduced.
- [ ] `Platform.BusinessReferenceData.Assignment.Manage` is enforced on a real backend assignment action, discovered
      by controller reflection, onboarded/assignable and not implied by consumer, steward or publisher permissions.
- [ ] `ReferenceDataManifestProvider` and its frontend-route-only zero-drift tests are unchanged; no backend-only
      assignment permission is represented as a synthetic frontend action.
- [ ] The PSS-011 allow-list stopgap is not treated as assignment, context-capture or scope-restoration proof.

#### Authorization separation

- [ ] Initial catalog seed/publish requires a separate user-authorized delivery step.
- [x] Governance approval alone did not authorize runtime code-start; the current code-start authority is separately
      recorded as `BRD Provider Internal Foundation` only.
- [x] The pack is `ready-for-dev` only with the exact Foundation and `Verified GSKU Resolver` named-step allow-lists,
      closed gates and their recorded user authorizations; all other FU01 implementation remains blocked.
- [ ] API exposure and production enablement remain separate gates after implementation evidence exists.

## 17. Test Expectations

### Superseding universal GSKU lookup tests

- Contract tests prove the exact one-value Pack Applicability set, five-value ordered UoM set, precision matrix,
  deterministic version identities, unsupported-value rejection and missing-tenant rejection.
- Multi-tenant tests prove different authenticated tenant contexts receive the same immutable catalog without an
  assignment or Mongo state dependency.
- Existing controller credential/JWT authorization and MDM typed-client tests remain regression gates.

Tests are created only in the exact Section 5 test files for the current named step. Later tests listed below remain
planned and are not eligible under the current status.

### Authorized foundation unit and real-Mongo tests

- `BusinessReferenceDataProviderOptionsTests.cs` (unit): `ReferenceTenantId` has no default; missing/empty/invalid
  values remain invalid; no fallback to `BusinessReferenceDataCatalogLoadOptions.TenantId`, ambient tenant, client
  input or hardcoded GUID; binding does not use startup-terminating `ValidateOnStart`.
- `BusinessReferenceDataTenantAssignmentMongoTests.cs` (real Mongo): reference-tenant ownership; partial unique
  `TenantId + ConsumerTenantId + SetCode` index for `IsDeleted = false`; create/revoke/reactivate/soft-delete;
  expected-version conditional mutation; stale-writer rejection; reference-tenant and consumer-tenant isolation;
  deleted/revoked records never satisfy an active lookup.
- `BusinessReferenceDataPublishOperationMongoTests.cs` (real Mongo): non-deleted
  `TenantId + IdempotencyKey` uniqueness; set/version/tenant isolation; durable state/checkpoint re-read; conditional
  monotonic transitions; concurrent retry fencing; same-key/different-target replay conflict; recovery evidence; and
  no completed/published claim when pointer, target and completion-verification prerequisites do not agree.
- `BusinessReferenceDataPublishStateMachineTests.cs` (unit): exact state/checkpoint vocabulary and transition guards,
  monotonic checkpoint rules, same-key/same-target replay identity, same-key/different-target conflict, stale fencing,
  post-pointer recovery requirement and rejection of a false verified-publication state. These are pure foundation
  invariants and do not invoke the existing publish service.

The Mongo tests prove persistence/index/isolation/concurrency behavior only. Passing provider-options, assignment or
publish-operation foundation tests is not evidence of catalog seed/publication, endpoint/auth envelope, readiness
health registration, S2S consumption, runtime-environment enablement or production readiness.

### Draft-step tests — `BRD Verified GSKU Catalog Publication` (not authorized)

- `BusinessReferenceDataGskuCatalogLoadMongoTests.cs` (real Mongo): loads exactly two sets/six values from the exact
  artifact; persists `DimensionCode` and `MaximumDecimalPrecision` definitions/attributes; proves configured
  reference-tenant ownership, wrong/cross-tenant exclusion, byte-equivalent replay with no duplicate version/value,
  and conflict/no-publication for altered locked content or malformed/missing/unknown metadata.
- `BusinessReferenceDataVerifiedPublishMongoTests.cs` (real Mongo): proves one durable operation per reference-tenant
  idempotency key; same immutable fingerprint replay; different fingerprint conflict; stale pointer, stale set
  `RowVersion` and stale target `ConcurrencyToken` as three separate rejection tests; other-tenant decoys never
  contribute to verification; crash injection and same-operation recovery at each checkpoint; post-pointer
  `RECOVERY_REQUIRED`; and rejection of every pointer/target/completion mismatch without a `COMPLETED` claim.
- `BusinessReferenceDataGovernanceModeTests.cs` (unit plus real-Mongo no-write assertions): Disabled, Mock and
  FailClosed each reject publication/provider-ready claims before creating or advancing an operation; the separately
  owner-approved production-safe test seam is the only eligible path and is not a Live adapter substitute.

All Mongo tests must connect to a real MongoDB instance and report the executed test count from `dotnet test` output.
Skipped tests, an unavailable Mongo server, mocks, fakes or an in-memory provider are not acceptance evidence. The
focused tests and affected Diten.Platform Domain/Application/Infrastructure/API builds must pass; `git diff --check`,
conflict-marker, trailing-whitespace and final-newline checks are mandatory. No frontend/DataTable/RESX test applies.

### Named-step tests — `Verified GSKU Resolver` (code-start authorized)

- `BusinessReferenceDataVerifiedResolveMongoTests.cs`: assignment-before-read for both sets; tenant/reference-tenant
  decoy isolation; verified latest success; pointer/immutable target/non-deleted
  `COMPLETED/COMPLETION_VERIFIED` proof; disabled/mock/failclosed rejection; retired/non-selectable new-selection
  rejection; and generic resolver/loader/publisher plus legacy publication false-proof rejection.
- `BusinessReferenceDataVerifiedResolveContractTests.cs`: exact tenant-free request and minimum response; only
  `SCALAR_QUANTITY_APPLIES` and `C62|GRM|KGM|MLT|LTR`; `LATEST` only; client tenant/set/version/mode/timestamp override
  rejection; and exact 404/409/503/504 stable `REFERENCE_*` envelopes.
- `BusinessReferenceDataTenantContextTests.cs`: Platform-validated `tenant_id` capture before reference scope;
  ACTIVE/non-deleted assignment query before every catalog query; two-tenant decoys excluded; success/failure/
  exception/cancel/timeout context restoration and parallel-scope isolation.
- `BusinessReferenceDataVerifiedResolverAuthorizationTests.cs`: trusted MDM resolver credential success;
  valid credential + valid `tenant_user` JWT success; missing/invalid/expired JWT 401; missing/malformed/multiple
  `tenant_id`, `platform_admin`, `partner_admin`, `X-Tenant-Id` and wrong audience/service 403;
  missing/wrong/expired/revoked credential 401; FU16/shared key rejected; no dispatch/read on failure. The same valid
  MDM resolver credential with tenant A and tenant B JWTs succeeds for both when each tenant has its own ACTIVE
  assignments; when only tenant A is assigned, tenant B reaches the assignment gate and receives non-leaking
  `404 REFERENCE_SET_NOT_ACCESSIBLE`.
- `VerifiedGskuResolverJwtTenantContextTests.cs`: exact `tenant_user`/single non-empty `tenant_id` extraction only after
  Platform authentication; no credential tenant parameter/equality; two distinct valid tenant JWTs can be resolved
  under the same service credential; controller-level scope establishment/restoration despite generic `/api/internal`
  bypass; generic Platform middleware remains unchanged.
- `BusinessReferenceDataVerifiedResolveMongoTests.cs` and `BusinessReferenceDataTenantContextTests.cs`: using one MDM
  resolver credential, tenant A and tenant B both succeed when each has ACTIVE/non-deleted assignments for the
  requested sets; with an assignment for tenant A only, tenant B fails closed before catalog read; assignments and
  catalog records planted for the other consumer/reference tenant remain invisible and cannot satisfy either request.
- Existing `DependencyInjectionSmokeTests.cs`: resolver-specific options/authenticator/query registrations exist
  without hosted worker, health, committed appsettings or production activation.
- MDM `PlatformVerifiedGskuResolverClientTests.cs`: typed success and minimum mapping; timeout/5xx/401/403/404/409/503/
  504 mapping; no retry/free-text/hardcoded/Mock/Disabled/FailClosed fallback; cancellation propagation; resolver-only
  credentials plus delegated Bearer on each request; no FU16 credential reuse; and request/client scope plus headers
  restored/disposed on success, mapped failure and cancellation.
- MDM `VerifiedGskuDelegatedTokenForwardingTests.cs`: only the current authenticated inbound Bearer is forwarded, only
  to the resolver call; missing token fails closed; `X-Tenant-Id` is never forwarded; raw token/credential is absent
  from DTOs, logs, responses, persistence and audit metadata; timeout/cancellation leaves no shared/default header or
  request-context residue.
- MDM `VerifiedGskuReferenceResolverContractTests.cs`: caller cannot provide SetCode, catalog version, mode or timestamp;
  adapter constructs locked SetCodes and accepts only provider-derived `LATEST` evidence.
- MDM `VerifiedGskuResolverDependencyInjectionTests.cs`: typed client/options/adapter registered only through exact
  Section 5 files with no committed secret/default, handler/entity/persistence or hosted-worker activation.
- Existing `BusinessReferenceDataGskuCatalogLoadMongoTests.cs`, `BusinessReferenceDataVerifiedPublishMongoTests.cs`,
  `BusinessReferenceDataGovernanceModeTests.cs` and the focused generic consumer tests remain regression gates; their
  success is prerequisite evidence but is not counted as S2S authorization or tenant-bound resolve proof.

All Mongo cases use a reachable real MongoDB and report the executed count from `dotnet test` output. The focused BRD
test package and affected Domain/Application/Infrastructure/API builds must pass. Skips, mocks or in-memory providers
do not close Mongo gates. Focused MDM client/contract tests are required; no GSKU aggregate, frontend, gateway,
DataTable or RESX test is added by this step.

### Draft named-step tests — `Verified GSKU Catalog Operational Readiness & Bounded UoM Enumeration` (no code-start)

- Operational eligibility unit tests cover disabled default, Development+enabled positive decision, environment-only
  rejection, Staging/Production rejection with identical options, exact fingerprint/version/path, invalid/same tenant,
  missing actor and proof that generic/runtime eligibility remains negative.
- Runner tests cover one turn per process start, exact call order, preflight-before-mutation, no legacy-worker/generic
  controller/direct-repository path, same-key replay, crash at every publish checkpoint, partial-publication rejection,
  verified completion read-back and no second version/operation/assignment.
- Assignment application unit + real-Mongo tests cover exact two-set creation, matching replay, duplicate winner,
  payload drift, inactive/revoked/deleted behavior, reference=consumer rejection, cross-tenant decoy isolation,
  expected-version reactivation boundary and assignment-before-read.
- Provider option/DI tests prove malformed binding does not escape as uncontrolled `OptionsValidationException`, exact
  request-time 503 is stable, unrelated host paths remain constructible, hosted runner defaults disabled and no
  committed values/test eligibility are registered.
- Readiness tests prove invalid configuration and incomplete pilot facts are sanitized unhealthy results, liveness is
  unchanged, the probe performs no mutation and a fully verified read-back becomes healthy.
- Enumeration contract tests assert a bodyless request and exact five-item/four-field response; forbidden tenant,
  assignment, credential, catalog-version, resolution, operation and secret/config fields are absent.
- Enumeration real-Mongo tests prove both assignments precede reads, pointer+immutable target+verified operation+
  locked fingerprint, ACTIVE/selectable filtering, deterministic order, exact precision, and 404/409/503 without
  partial/hardcoded/cache fallback for missing/retired/extra/drifted/incomplete facts.
- Enumeration authorization/context tests cover existing resolver credential/audience, independently validated
  tenant-user JWT, `X-Tenant-Id` rejection, two consumer tenants, cross-tenant decoy assignment and deterministic scope
  restoration after success/failure/cancellation/timeout; timeout is exact 504 and there is no retry.
- Focused `dotnet test services/Diten.Platform` evidence must report executed/passed/failed/skipped counts and use a
  reachable real MongoDB for Mongo cases. Relevant Domain/Application/Infrastructure/API builds must pass. This
  backend-only step requires no frontend, Gateway, DataTable or RESX verification.

### Planning named-step tests — `Verified Market Catalog for LSKU Draft Identity Foundation`

| Layer | Required cases |
|---|---|
| Unit | Exact `market` constant; owner-approved grammar/normalization validator; ordinal exact match; leading/trailing whitespace, case variant, alias and unknown code rejection; deterministic sort; duplicate/normalized-collision, empty and over-bound publication rejection; terminal retire/no-reactivation/no-reuse |
| API/contract | Exact request and response JSON; unknown/body/query/tenant/version/evidence fields rejected; enumeration has only three item fields; resolve technical evidence never appears in enumeration or a browser/business DTO; credential/JWT order; `X-Tenant-Id` rejection; context restoration; exact `401/403/404/409/503/504` envelopes; timeout/cancellation propagation |
| Real Mongo | Verified loader and durable publish only; pointer/immutable-target/`COMPLETED/COMPLETION_VERIFIED` agreement; replay/idempotency and checkpoint recovery; stale pointer/version conflict; previous-version immutable historical read; retired code excluded from latest enumeration and rejected for new exact resolve; cross-tenant identical universal result without provider assignment; legacy `market` rows/assignments cannot become proof; direct-document/unverified operation fails `503` |
| Regression | Existing verified GSKU resolver, universal Pack Applicability, exact UoM resolve/enumeration, GSKU verified publish operation/state-machine and context/auth tests all pass unchanged |

Test names must correspond to the exact Section 5 files. A `*MongoTests` class must use the repository's real-Mongo
fixture and assert persisted documents/index/state; an in-memory handler test renamed as Mongo evidence is insufficient.
Required command evidence is focused Platform build/tests plus the complete BRD regression suite, `git diff --check`,
conflict-marker, whitespace and final-newline checks.

### Deferred unit, contract and broader real-Mongo tests

- `BusinessReferenceDataProviderMongoTests.cs`: catalog SetCode uniqueness, active/soft-delete behavior, canonical
  reference-tenant isolation, version immutability, value no-reuse and existing BRD index definitions.
- `BusinessReferenceDataUomContractTests.cs`: exact two-family catalog, required UoM dimension/precision contract,
  retired-new-selection rejection, historical pinned resolution, optional same-set replacement and ValueCode no-reuse.
- `BusinessReferenceDataFailureContractTests.cs`: exact future `Response<T>` failure envelopes.
- `BusinessReferenceDataTenantContextTests.cs`: trusted consumer capture, assignment-before-read and scope restoration.
- `BusinessReferenceDataAssignmentAuthorizationTests.cs`: future resolve/assignment authorization behavior.

### Deferred authorization/integration tests — not in the current allow-list

- `BusinessReferenceDataAuthorizationEnvelopeTests.cs`: on explicit BRD endpoint metadata, missing/invalid/expired JWT
  reaches challenge and returns exact `401 REFERENCE_UNAUTHENTICATED`; authenticated missing permission reaches the
  permission-result path and returns exact `403 REFERENCE_FORBIDDEN`; both use `Response<T>` and are distinct from
  provider business failures. Metadata-absent Platform control endpoints retain their existing non-BRD envelopes.
- Existing `Authorization/HasPermissionAttributeDualReadTests.cs`: regression for authenticated permission evaluation;
  prove metadata-scoped BRD 403 and unchanged metadata-absent permission denial without treating unauthenticated
  challenge as filter-owned.
- Existing `Security/HasPermissionReflectorTests.cs`: the real assignment action's exact
  `Platform.BusinessReferenceData.Assignment.Manage` key is discovered once by controller reflection.
- `BusinessReferenceDataAssignmentPermissionOnboardingTests.cs`: backend-only reflected key reaches registry
  onboarding/assignability and is enforced only on assignment administration; consumer/steward/publisher keys do not
  imply it; `ReferenceDataManifestProvider` remains unchanged and contains no synthetic assignment action.
- `BusinessReferenceDataProviderReadinessTests.cs`: valid configuration reports healthy; missing/empty/invalid
  configuration leaves host live, reports provider readiness unhealthy and returns exact
  `503 REFERENCE_PROVIDER_CONFIGURATION_INVALID` from each provider-dependent resolve/assignment surface with no
  catalog-load fallback. Registration is proven through `Program.cs` with `ready` but not `live`; existing health
  paths, predicates and sanitized response writer remain unchanged.
- `BusinessReferenceDataGovernanceModeTests.cs`: Disabled/Mock production rejection with exact 503 behavior and valid
  production-governance registration.
- Existing `Lookups/TenantReferenceDataCrossTenantReadTests.cs`: preserve PSS-011 allow-list regression and prove it is
  not accepted as consumer capture, durable assignment or scope-restoration evidence for this provider.
- Existing `Lookups/LegalEntityBrdReferenceTests.cs`: existing allow-list and authorization regression only; it is not
  proof of the new assignment contract.
- Existing `DependencyInjectionSmokeTests.cs`: typed provider option validation, provider registration, readiness
  registration and JWT challenge configuration without startup termination for provider-option failure.
- `BusinessReferenceDataAssignmentAuthorizationTests.cs`: integration path proves assignment check precedes catalog
  read, revoke denies immediately, wrong consumer is non-leaking 404 and consumer context is restored.
- Least-privilege MOD-0290 consumer read/resolve and usage registration.
- Maker self-approval, delegated actor mismatch and request-body actor spoof rejection.
- Disabled/Mock production rejection and valid production-governance path.
- Timeout 504, unavailable/Disabled/Mock 503, non-leaking 404 and conflict 409 handling; malformed response, stale
  pinned version and duplicate request behavior.

### Deferred contract and regression tests — not in the current allow-list

- Exact `pack-applicability` and `uom` initial code catalogs; no extra initial values.
- Draft `latest` selection, submit/approval version pinning and historical pinned resolution are exact; `as-of` and
  scheduled effective-period selection are rejected/not exposed and receive no first-delivery readiness claim.
- No hardcoded/free-text fallback in provider or MOD-0290 contract tests.
- PSS-011 `/api/lookups` regression suite remains unchanged and green.
- `Diten.Platform` Domain/Application/Infrastructure/API builds pass.
- No frontend/DataTable/RESX verifier is required because `shell: none` and no UI is in scope.

Test fixtures do not constitute a production seed, published catalog or production acknowledgement.

## 18. Ready-for-dev Checklist

### Universal GSKU lookup closure

- [x] The user selected the global, code-owned, deployment-versioned model on 2026-08-07.
- [x] Internal reference-tenant creation and the two GSKU tenant assignments were cancelled.
- [x] Operational catalog load/publish is no longer a predecessor for these two SetCodes.
- [x] Exact runtime/test allow-list and immutable catalog values are recorded in Section 5.
- [x] Focused Platform and MDM consumer regressions pass from the current worktree; full Platform is 1478/1478 and
      full MDM is 271/271 in Release with zero skipped.
- [ ] Live read-only resolver/enumeration smoke is repeated after the updated Platform binary is restarted.

### Governance acceptance (`draft -> approved`)

- [x] Master 8.1 parent evidence verified at `Blueprint_Data!A49:AG49`, `SoR_Map!A212:D212` and
      `Dependencies!A108:D109`.
- [x] Registry collision check found no prior `MOD-0048-FU01` mapping.
- [x] Legacy verifier mechanically passed with `--parent MOD-0048`; it is not Master 8.1 business authority.
- [x] Frontmatter and all 20 required sections are present.
- [x] `shell: none`, `golden_reference: none`, no UI and no DataTable are explicit.
- [x] PSS-011 and MOD-0290 protected boundaries are explicit.
- [x] User locks the reference-tenant canonical catalog and server-side tenant-assignment model; tenant-local semantic
      override is prohibited.
- [x] User separates provider identity from seed configuration: a no-default
      `BusinessReferenceDataProviderOptions.ReferenceTenantId` and durable `BusinessReferenceDataTenantAssignment`
      are the approved design.
- [x] User locks the first UoM dimension and maximum-precision metadata for `C62`, `GRM`, `KGM`, `MLT`, `LTR`.
- [x] First-delivery resolution is locked: draft may use latest; submit/approval pins; historical pin resolves;
      `as-of` and scheduled effective-period selection are deferred.
- [x] Retirement/replacement/no-reuse is locked: retired values reject new selection, historical pins still resolve,
      replacement is optional within the same set and ValueCode is never reused.
- [x] Trusted-context algorithm, exact assignment predicate, deterministic scope restoration and the PSS-011
      stopgap exclusion are locked.
- [x] Provider configuration behavior is locked to host-live, unhealthy readiness and exact provider-surface 503.
- [x] Provider readiness uses the actual `Program.cs` host registration point with `ready`-only semantics.
- [x] BRD-specific 401/403 is explicit-endpoint-metadata scoped; metadata-absent Platform auth behavior is protected.
- [x] `Assignment.Manage` is backend-action/reflection onboarded and excluded from the frontend-only manifest.
- [x] `BusinessReferenceDataPublishOperation` entity, state/checkpoint lifecycle, idempotency index and proof plan are
      explicit; Mongo transaction alternatives are excluded.
- [x] Standard frontmatter records `entity_base: BaseEntity` and the planned branch without creating it.
- [x] The user approved FU01 governance scope; `approved` by itself authorized no runtime work.

### Closed pre-code gates for `BRD Provider Internal Foundation` (`approved -> ready-for-dev`)

| Foundation gate | Closure evidence |
|---|---|
| Delivery scope | The named step is exactly `BRD Provider Internal Foundation`; Section 5 contains its exhaustive file allow-list and no wildcard scope |
| User code-start authorization | The user explicitly authorized this named step as the first runtime step and no other FU01 runtime scope |
| Persistence design | Durable assignment schema/index and durable publish-operation state/checkpoint/idempotency/index contracts are approved; this step produces implementation/test evidence only |
| Provider option design | A no-default `BusinessReferenceDataProviderOptions.ReferenceTenantId` is approved and catalog-load fallback is forbidden |
| Reuse boundary | Existing PSS-012 entities, stewardship repository and Mongo index registration are reused; parallel provider/catalog/service creation is prohibited |
| Evidence classification | Internal unit/real-Mongo tests are persistence evidence only and cannot assert endpoint, publish, readiness or production enablement |
| Status gate | Pack is `ready-for-dev` solely for the exact named-step allow-list; the planned branch remains uncreated |

### Later fail-closed gates — not opened by this status

| Later gate | Required closure before a separately named step |
|---|---|
| Security / S2S / auth envelope | Service and delegated identity, least privilege, tenant binding, credential rotation/break-glass, endpoint metadata and exact 401/403 ownership approved and tested |
| Readiness endpoint | Operations ownership plus `Program.cs` ready-only registration, liveness isolation and provider-dependent 503 behavior separately authorized and proven |
| Workflow / SoD | Canonical actor identity, maker-checker separation, audit/evidence and Disabled/Mock production rejection approved |
| Seed / publish behavior | Exact initial catalogs, trusted seed source, production governance, durable recovery execution and no-false-published-claim behavior separately authorized |
| Consumer exposure | Resolve/assignment endpoints, tenant-scope algorithm, permissions, S2S contract and MOD-0290 adapter boundary separately authorized |
| Metrics / runbook | Timeout/retry policy, telemetry, alerts, recovery procedure and operational ownership accepted |
| Production enablement | Runtime environment configuration, production catalog publication, release evidence and explicit production authorization accepted |

### Draft code-start gates — `BRD Verified GSKU Catalog Publication`

This draft named step remains non-executable until its final separate user code-start authorization. The resolved
decisions below apply only to its dormant/test-invoked internal publication path; they do not authorize production
activation, a hosted worker, HTTP/S2S exposure or a live governance adapter.

| Open decision / gate | Recommendation and reason | Decision owner | State |
|---|---|---|---|
| Catalog identity and human-readable text | Locked: immutable `1.0.0`, the Section 4 audit note and exact English set/value display text; this makes load/replay deterministic and removes all placeholders | User / delivery owner | Resolved |
| Publication eligibility for this step | Locked: Disabled, Mock and FailClosed are rejected. A positive path exists only through the test-only seam below; it is not production governance evidence | User / delivery owner | Resolved for test-invoked path; production remains deferred |
| Test seam for governance-safe publication | Locked: an explicit test-only eligibility decision source is injected through Application DI and is unavailable through runtime configuration; it provides positive-path evidence without inventing a Live workflow/evidence adapter | User / delivery owner | Resolved |
| Trusted actor and SoD evidence | Deferred: real system/steward actor identity, maker-checker rule and audit/evidence identifiers are required only before production publication/enablement | Reference Data Owner + Security Owner + MOD-0023/MOD-0021 Owners | Deferred, not a named-step blocker |
| Recovery invocation and retry policy | Locked: no hosted worker or automatic runtime retry; same immutable command uses explicit replay/reconciliation through the publish service | User / delivery owner | Resolved for this step; production operations remain deferred |
| Existing-data collision policy | Locked: fail closed on any locked-code, ownership or metadata mismatch; never overwrite, quarantine or republish legacy data in this step | User / delivery owner | Resolved |
| Artifact activation boundary | Locked: artifact is dormant except in tests and an explicitly invoked internal application call; no worker, appsettings or production environment activation | User / delivery owner | Resolved |
| Separate user code-start | After all owner decisions are recorded in this pack, the user must explicitly authorize the exact named step and Section 5 draft allow-list; `ready-for-dev` for the foundation is not sufficient | User / delivery owner | Open |

### Code-start gates — `Verified GSKU Resolver`

All contract decisions are closed. On 2026-08-05 the user approved the validated-user-JWT tenant-binding algorithm,
separately authorized implementation code-start for this named step, and then approved its multi-tenant correction:
the credential has no tenant field and the JWT tenant is authorized per set by assignment. Frontmatter remains
`ready-for-dev`; authority is limited to its exact Section 5 allow-list.

| Decision / gate | Locked contract | State |
|---|---|---|
| Resolver identity and audience | One resolver-audience-specific per-service credential derives only `DITENMDMSERVICE` + `VERIFIED_GSKU_RESOLVE`; it has no tenant field/constraint and FU16 credential/shared key/AuthService issuance are not reused | **Resolved — multi-tenant correction approved** |
| Route and cardinality | `POST /api/internal/v1/reference-data/verified-gsku/resolve`; tenant-free one/two item batch; only two locked sets and six locked values | Resolved |
| Resolution lifecycle | First GSKU draft uses `LATEST` only; submit/approval `PINNED` is a later MOD-0290 step | Resolved |
| Publication proof | Pointer + immutable target + non-deleted `COMPLETED/COMPLETION_VERIFIED`; generic/legacy resolver, loader, publisher, Disabled, Mock and FailClosed are false proof | Resolved |
| Assignment ordering | ACTIVE/non-deleted assignment is checked for each SetCode before any catalog read | Resolved |
| Response and client ownership | Exact minimum eight-field response; client cannot supply set/version/mode/timestamp evidence | Resolved |
| Timeout/fallback | Two-second Platform budget, cancellation propagation, no internal retry and no semantic fallback | Resolved |
| Trusted consumer-tenant binding | MDM forwards only its already validated inbound user Bearer JWT. Platform independently validates it, accepts only exact `tenant_user`, derives tenant only from one non-empty `tenant_id`, rejects `X-Tenant-Id`, establishes context from that claim and authorizes each SetCode through that tenant's ACTIVE/non-deleted assignment | **Resolved — JWT + assignment multi-tenant model approved** |
| Context and secret hygiene | Generic Platform/MDM tenant middleware remains unchanged; controller-level `/api/internal` context is explicit; JWT/secret absent from DTO/log/response/persistence/audit; MDM and Platform restore context on success/failure/timeout/cancel | Resolved |
| Separate user code-start | User explicitly authorized `Verified GSKU Resolver` implementation on 2026-08-05, restricted to the exact Section 5 allow-list | **Authorized** |
| Multi-tenant credential correction | User explicitly rejected the single-tenant credential model and authorized the Section 5 exact runtime delta; no new MOD/FU/DCP or additional code-start decision is required | **Authorized** |

### Draft code-start gates — `Verified GSKU Catalog Operational Readiness & Bounded UoM Enumeration`

| Gate | Locked contract | State |
|---|---|---|
| A environment/enablement | Development AND explicit default-off option; Staging/Production always reject | Resolved design; not implemented |
| A artifact identity | Existing `mod-0290-gsku-reference.json`, exact version `1.0.0` and SHA-256 `e95ef856e87cfaf726b8e4c939e56499791933e69b90bc7fbb6718a949422a5d` | Verified from the current artifact; the runner must receive the same value through local config and fail on drift |
| A eligibility separation | Separate narrow pilot decision; runtime/test eligibility and generic/Live behavior unchanged | Resolved design |
| A invocation/recovery | Verified loader -> verified publisher/state machine -> completion read-back -> supported assignment command; one turn/start, same-key replay | Resolved design |
| A assignment behavior | Exactly two ACTIVE assignments; matching replay only; drift/inactive/revoked/deleted conflict; no auto-reactivation | Resolved design |
| A provider options | Stable request-time 503 without uncontrolled options activation; unrelated host paths remain startable | Resolved design |
| B route/response | Exact internal POST route; bodyless; exact five UoMs/four fields/precision; no forbidden metadata | Resolved design |
| B auth/tenant | Existing resolver credential + independently validated tenant-user JWT; no tenant in credential/request; both assignments before read | Resolved design |
| B publication proof | Locked fingerprint + pointer + immutable target + `COMPLETED/COMPLETION_VERIFIED`; no generic/cache/hardcoded fallback | Resolved design |
| B failure/context | Exact 404/409/503/504, no partial list, deterministic scope/header cleanup | Resolved design |
| Provider/consumer boundary | Platform owns enumeration and evidence; MOD-0290 later owns only MDM client/create-options/UI work | Resolved design |
| Separate user code-start | User must explicitly authorize the combined Section 5 allow-list; this authoring task is not authorization | **Open** |
| Separate operational run | After code/test evidence, BRD owner must authorize exact local environment, tenant IDs, artifact fingerprint and actor; no run is implied by code completion | **Open** |
| Production readiness | Real Live workflow/evidence, production configuration, release and operational evidence require separate approval | **Blocked/deferred** |

This pack retains its existing status. The table above overrides any inference that `ready-for-dev` frontmatter or a
different completed/authorized named step grants code-start or operational authority to this new named step.

### Planning gates — `Verified Market Catalog for LSKU Draft Identity Foundation`

| Gate | Required proof | Authority | State |
|---|---|---|---|
| Provider source | `MARKET-SOURCE-01`: first-phase country-market semantics, ISO 3166-1 alpha-2 authority, exact `^[A-Z]{2}$` grammar/no-normalization and non-country deferral | User + MOD-0048 Reference Data Owner | **Closed 2026-08-07** |
| Separate code-start | User explicitly authorizes only the Section 5 market runtime/test allow-list; frontmatter `ready-for-dev` and other named-step authorizations do not transfer | User / delivery owner | **Closed 2026-08-07 by explicit blocker-remediation request** |
| Code evidence | Unit/contract/real-Mongo matrix passes; no MDM/Auth/Gateway/frontend/config/data delta; GSKU/UoM regressions pass; generic PSS-012 is not treated as proof | Delivery owner + provider owner review | **Completed 2026-08-07** |
| `MARKET-ARTIFACT-01` and separate operational provisioning | Artifact authoring is closed with the evidence below. Environment, trusted actor, idempotency key and one supported loader -> durable publisher/replay operation remain separately authorized operational work. No direct Mongo, hosted startup action or test-only seam | MOD-0048 Reference Data Owner + user / delivery owner | **Artifact authoring closed 2026-08-09; provisioning remains open and blocked** |
| Consumer readiness | Operational read-back proves exact resolver/enumeration `200` and negative `404/503/504`; only then may MOD-0290 request its separate LSKU code-start | MOD-0048 owner + MOD-0290 owner | Blocked on operational provisioning |

No provider-design decision remains open. The separate code-start and code-evidence gates are closed.
`MARKET-ARTIFACT-01`/operational provisioning remain open authorization and evidence gates, not design ambiguity and
not consequences of the pack's retained `ready-for-dev` status.

### MARKET-ARTIFACT-01 source correction — 2026-08-08

The canonical code authority remains the ISO 3166 Maintenance Agency, but a paid ISO Country Codes Collection is
not a prerequisite for this catalog. ISO permits free-of-charge use of ISO 3166 country codes. The immutable
snapshot source is the official UNSD M49 English table at
`https://unstats.un.org/unsd/methodology/m49/overview/`, using its `#downloadTableEN` DataTables CSV-export surface.
UNSD M49 supplies the `ISO-alpha2 Code` and `Country or Area` fields; UNData permits copying and redistribution with
UNData cited as the reference.

The deterministic artifact procedure is: fetch the exact UTF-8 HTML response; retain only rows with a non-empty
`ISO-alpha2 Code`; reject non-`^[A-Z]{2}$` codes, missing `Country or Area`, duplicate alpha-2, duplicate M49 code,
or an accepted cardinality outside `1..300`; sort by numeric M49 code then ordinal alpha-2; emit UTF-8 LF-indented
JSON. Snapshot identity is `UNSD-M49-YYYY-MM-DD`; source and artifact SHA-256 values are mandatory evidence.
The following first-retrieval record is historical/superseded for artifact materialization. It yielded 248 source rows and 248 accepted rows, with source
SHA-256 `748f6ff7380c8a50ea9448f068b79e3a1ee31be63207249e8cc89bf1eb969d11` and generated-artifact SHA-256
`e6a3d467bc4066e9cf223819400d97943304e61897cd37f7edd476927144bbca`. It is superseded by the 2026-08-09
materialization evidence below; MARKET-ARTIFACT-01 is not open for artifact authoring. Operational provisioning remains separately unauthorized.

### MARKET-ARTIFACT-01 materialization evidence — 2026-08-09

This section supersedes the incomplete 2026-08-08 materialization statement above. The artifact-authoring scope is
closed; it did not invoke a loader, publisher, runner, MongoDB, configuration, process or any provisioning path.

- **Artifact:** `services/Diten.Platform/src/Diten.Platform.API/Seed/business-reference-data/mod-0290-market-reference.json`
  is one UTF-8 (no BOM), LF-terminated JSON catalog using the existing `BusinessReferenceData` loader schema only.
  It has `module: BusinessReferenceData`, one `set_code: market`, `catalog_version: UNSD-M49-2026-08-08`, no invented
  metadata fields, and exactly 249 active values.
- **Authority and attribution:** ISO 3166 Maintenance Agency is the code authority. UNSD M49 English `Country or Area`
  text supplies the common display names; attribution is to the UN Statistics Division M49 table. ISO OBP
  browser-visible officially-assigned count is 249. The control-tower exception removes any raw ISO OBP HTTP hash
  requirement because automated OBP retrieval returned 403; no control was bypassed and no paid export was acquired.
- **Raw-source evidence:** accepted M49 raw HTML SHA-256 is
  `748f6ff7380c8a50ea9448f068b79e3a1ee31be63207249e8cc89bf1eb969d11`.
  The normalized M49 248-row UTF-8/LF `code<TAB>display_name<TAB>numeric` set SHA-256 is
  `65572ea7c5da8218083820f6af9a15c78a514f095505ef8297af830b976a2eb4`.
  The combined normalized 249-row set (M49 plus exact `TW<TAB>Taiwan (Province of China)<TAB>158`) SHA-256 is
  `96f9133c8eefefdcb1ce25e7e014adc9a15c62669dc4769dd0c2b64ec34dac29`.
- **Exact-set proof:** `ISO \\ M49 = { TW }`; `M49 \\ ISO = empty`. The 248 common values use UNSD ISO-alpha2
  plus UNSD English display text. The only ISO-only active value is `TW`, `Taiwan (Province of China)`, numeric `158`.
  Codes are exact `^[A-Z]{2}$`, unique, and the final display-name ordinal / value-code ordinal tie-break ordering
  assigns unique sequential `sort_order` values `1..249`.
- **Artifact-byte proof:** after writing, an independent second read computed SHA-256
  `b94c45280195b0cb5faa155656c4690938790144d148fba279d2232204360039`.
  JSON escapes non-ASCII display characters but parses to the exact Unicode UNSD English strings; bytes are UTF-8,
  BOM-free and LF-only.
- **Non-provisioning guard:** no loader/publisher/runner execution, Mongo write, tenant/credential/secret mutation,
  `Program.cs` or configuration mutation, process mutation, MDM/Auth/Gateway/frontend change, staging, commit or push
  occurred. Runner code-start still requires separate exact allow-list authorization, runner design/test gates, and a
  separate operational approval naming environment, trusted actor and idempotency key.

### Implemented named-step evidence — 2026-08-09

- Isolated Platform API Release build:
  `dotnet build services/Diten.Platform/src/Diten.Platform.API/Diten.Platform.API.csproj -c Release
  --artifacts-path .work/market-operational-final-artifacts --nologo`: **0 errors, 7 warnings**. The warnings are
  pre-existing and outside this named-step allow-list.
- Focused operational command selected `VerifiedMarketOperationalEligibilityTests`,
  `VerifiedMarketOperationalProvisioningRunnerTests`, `BusinessReferenceDataVerifiedMarketOperationalMongoTests`
  and the exact market operational DI smoke test: **26 passed, 0 failed, 0 skipped**.
- Complete Business Reference Data command selected `FullyQualifiedName~BusinessReferenceData` plus the named
  operational classes: **165 passed, 0 failed, 0 skipped**.
- Full Platform solution command
  `dotnet test services/Diten.Platform/Diten.Platform.sln -c Release
  --artifacts-path .work/market-operational-full-suite-artifacts`: **1536 passed, 0 failed, 0 skipped**.
- Real-Mongo evidence includes reference-tenant-only storage, consumer A/B identical enumeration, consumer decoy
  isolation, exact 249-value market load/publish, `TW` read-back, namespace-prefixed operation identity, same-fingerprint
  single-operation replay, distinct-namespace identity separation, same-key/different-fingerprint conflict, injected
  checkpoint crash/recovery, zero market tenant assignments, immutable version history, stale-pointer rejection and
  required `COMPLETED/COMPLETION_VERIFIED` proof. Mongo was `mongodb://127.0.0.1:27017`; no skip, fake or in-memory
  substitute was used.
- Regression evidence in the complete BRD/full suite covers generic PSS-012 fail-closed behavior, verified GSKU
  operational/publish/state-machine behavior, verified market resolve/enumerate/authorization and exact
  `404/409/503/504` mappings.
- No configuration/data mutation or operational provisioning was performed; `MARKET-ARTIFACT-01` is closed for
  artifact authoring and the separate Local Development operational run remains unauthorized/open. Production
  enablement remains prohibited and separately gated.

### Evidence required to complete the authorized foundation step

| Evidence gate | Required proof |
|---|---|
| Provider options | No default, invalid-value classification, no catalog-load/ambient/client/hardcoded fallback and no `ValidateOnStart` |
| Tenant assignment | Real-Mongo partial uniqueness, soft delete/revoke, expected-version concurrency and reference/consumer cross-tenant isolation |
| Publish operation | Real-Mongo idempotency uniqueness, durable state/checkpoint persistence, replay conflict, fencing and no false verified-publication claim |
| Internal DI/build | Only internal option/repository wiring is registered and the affected Diten.Platform projects/tests compile |

Passing this foundation evidence completes only the named internal step. It does not close any later gate or constitute
runtime readiness, a catalog seed/publication, consumer exposure or production readiness.

## 19. Implementation Notes

- Master 8.1 inspection on 2026-08-07 confirmed MOD-0048 reference-code-set ownership and MOD-0290 Product/Item/SKU
  ownership, but returned no `MarketCode` rows. The user subsequently closed the first-phase semantic/source decision:
  country markets use the official ISO 3166-1 alpha-2 set, while country-external commercial/regulatory regions are
  deferred. This is an explicit owner decision, not an inference from PSS-011 or tenant country settings.
- The verified-market plan reuses the existing durable loader/publish-operation/repository mechanisms and verified
  resolver security/context sequence, while keeping their files inside the exact allow-list only when modification
  is required. The existing GSKU universal catalog/controller/security files are unchanged regression references.
- Universal/shared `market` semantics do not mean tenant-assigned catalog access. Provider tenant assignments are
  bypassed for this set by contract; future tenant/Legal Entity market-operation assignment remains a separate
  business capability and cannot be inferred from catalog enumeration.
- The current PSS-012 entities already model set, version, values, attributes, effective dates, replacements and usage
  registration. Their presence is reuse evidence, not proof that this pack is implemented.
- Current consumer resolution filters to `Published` versions while publish deprecates the prior version; first-
  delivery historical pinned resolution therefore requires hardening. `as-of` remains deferred.
- Current tenant consumer access uses a reference-tenant stopgap and a narrow allow-list; adding the two SetCodes to
  that allow-list alone cannot satisfy trusted context capture, assignment-before-read, pinned version, permission or
  S2S requirements.
- `BusinessReferenceDataCatalogLoadOptions.TenantId` is currently consumed by catalog loading and the three-set
  controller stopgap. It is explicitly not the provider identity/access authority and is protected from this pack's
  provider-options implementation.
- The locked physical direction reuses `TenantScopedEntity` under one configured reference tenant. Existing ambient
  tenant filters, `ScopeType`/`ScopeKey`, catalog-load options and the controller stopgap do not yet implement the
  required reusable server-side assignment contract; this is an explicit provider-B delivery risk, not a silent
  target-model rewrite.
- The current working tree contains the FU01 typed provider options, assignment and publish-operation
  entity/repository/index/DI foundation plus its focused tests. The draft named step reuses those objects; assignment
  enforcement and endpoint behavior remain later gates.
- Current code contains the verified GSKU load/publish path, durable publish-operation state/checkpoints and real-Mongo
  verified-publication tests. This is valid proof input for the narrow resolver only when the live pointer, immutable
  target and non-deleted `COMPLETED/COMPLETION_VERIFIED` operation agree; it does not make generic PSS-012 results valid.
- Current production governance resolves to Disabled behavior. This follow-up must not relabel Disabled as fail-closed
  or production-safe.
- Current publish updates version status, previous-version deprecation and parent pointer in separate operations.
  The locked idempotent recovery/reconciliation state machine and no-false-published-claim evidence are required
  before production readiness; Mongo transaction assumptions are excluded.
- Current verified loader transports attribute definitions and value attributes, obtains reference ownership from
  provider options, and invokes the durable verified publish path. The resolver reads its proof; it cannot call the
  loader, publish, repair or activate it.
- Current production governance defaults to Disabled and current non-production behavior may be Mock. Neither is
  provider-ready or publication evidence; the positive production-safe path is deliberately owner-gated in Section 18.
- Current `BusinessReferenceDataConsumerQueryService` resolves generic Published versions by scope/version/effective
  time and can expose deprecated values when requested. It does not check provider options, durable assignment,
  set pointer or completed publication operation; therefore it is reuse code, not MOD-0290 readiness evidence.
- Current `TenantResolutionMiddleware` accepts JWT `tenant_id` but may fall back to `X-Tenant-Id`, rejects non-
  `tenant_user` actors on normal tenant routes and bypasses `/api/internal`. The approved resolver does not change or
  trust that middleware: its controller independently uses the Platform-validated JWT principal, rejects the header,
  requires `tenant_user` + one `tenant_id` and then establishes scoped context solely from that claim.
- Current resolver credential options/authentication no longer carry `ConsumerTenantId`; the independently validated
  JWT tenant plus per-set assignment is the implemented multi-tenant direction. This remains reuse evidence, not
  operational catalog/assignment readiness.
- MDM's existing JwtBearer handler validates the inbound JWT and its tenant middleware already reads `tenant_id`.
  The resolver typed client forwards the same raw Bearer only on its per-call request; Platform independently validates
  it again. This closes tenant binding without a new AuthService service-token system.
- Current internal controllers commonly use a shared `X-Internal-Api-Key`, and one audit path accepts body tenant.
  Those are explicit negative references for this step and cannot establish MDM identity or consumer tenant.
- Existing `TenantScope` snapshots tenant/platform/unresolved state and restores it on disposal. The resolver algorithm
  uses an outer controller consumer scope plus an inner application reference scope; tests must prove restoration in
  reverse order on every exit.
- Current publish-operation verification requires the live set pointer and is suitable for latest proof, but a
  historical pin needs a narrowly added durable operation-by-target proof that tolerates a later legitimate pointer
  move while still requiring immutable target idempotency evidence and `COMPLETED/COMPLETION_VERIFIED`.
- The completed verified GSKU loader/publisher is explicitly separated from generic QMS/Legal Entity load/publish
  behavior. This resolver consumes only its durable proof and must not reconnect generic legacy results to provider
  readiness.
- Current `RuntimeBusinessReferenceDataPublicationEligibility` returns ineligible for every governance mode; positive
  eligibility exists only in tests. `BusinessReferenceDataCatalogLoadWorker` calls legacy `LoadFromFileAsync`, which
  rejects the verified artifact with `VERIFIED_GSKU_CATALOG_CONTRACT_REQUIRED`; no supported runtime invoker currently
  calls `LoadVerifiedGskuCatalogFromFileAsync`.
- Assignment entity/repository/index behavior exists, but callers currently create test assignments directly; there
  is no supported application command for exact two-assignment provisioning. The new named step closes that design
  gap without creating a public administration API.
- `BusinessReferenceDataStewardshipRepository` currently captures `IOptions<BusinessReferenceDataProviderOptions>.Value`
  during construction, so malformed binding can throw before handler mapping. The new named step requires a stable
  provider-configuration accessor/classification boundary while keeping unrelated generic BRD host paths startable.
- The existing verified resolver validates one supplied UoM but cannot enumerate the bounded selectable published UoM
  set required by MOD-0290 `GET /api/gskus/create-options`; provider-owned enumeration remains unimplemented.
- `target: 2026-08-02` records the governance/named-step readiness revision date; it is not a production delivery date.
- Frontmatter records planned branch `feature/pss/mod-0048-fu01-reference-data-provider`; this task does not create or
  switch to that branch.

## 20. Follow-up Items

- `MARKET-ARTIFACT-01` is closed for artifact authoring: the immutable artifact is version
  `UNSD-M49-2026-08-08`, contains 249 values, has SHA-256
  `b94c45280195b0cb5faa155656c4690938790144d148fba279d2232204360039`, and preserves
  `ISO \\ M49 = { TW }`, `M49 \\ ISO = empty`. Operational provisioning remains separately unauthorized.

### Implemented named step — `Verified Market Catalog Local Operational Foundation`

Implemented and test-closed: Development-only, default-disabled, explicit one-shot runner using
`LoadVerifiedMarketCatalogFromFileAsync -> PublishVerifiedMarketAsync -> durable replay/recovery ->
COMPLETED/COMPLETION_VERIFIED pointer plus immutable-target read-back`. Universal `market` creates no tenant assignment;
the artifact is read-only input. The exact code-start authorization was consumed only for the allow-list below; it
does not authorize an operational run.

Runtime allow-list: new `VerifiedMarketOperationalProvisioningOptions.cs`,
`IBusinessReferenceDataVerifiedMarketOperationalEligibility.cs`,
`DevelopmentBusinessReferenceDataVerifiedMarketOperationalEligibility.cs`, and
`VerifiedMarketOperationalProvisioningRunner.cs` at the exact user-specified API/Application paths; existing
market-operational-only changes to `BusinessReferenceDataCatalogLoaderService.cs`,
`IBusinessReferenceDataPublishService.cs`, `BusinessReferenceDataPublishService.cs`,
`services/Diten.Platform/src/Diten.Platform.Application/DependencyInjection.cs`, and
`services/Diten.Platform/src/Diten.Platform.API/Program.cs` (the exact API DI-registration path; no separate API DI
file exists). Read-only input: `mod-0290-market-reference.json` only.

Protected/prohibited: MDM/Auth/Gateway/frontend, appsettings/configuration files, artifact bytes,
`BusinessReferenceDataCatalogLoadWorker`, generic `LoadFromFileAsync`/`PublishAsync`, direct repository/Mongo writes,
tenant assignments, secrets/credentials, public routes, health checks, hosted legacy-worker change, and startup auto-publish.

Acceptance: fail-before-mutation outside Development, when disabled (default false), or if path/version/SHA-256,
ReferenceTenantId, trusted actor, deterministic idempotency namespace/key are invalid/missing. Same fingerprint/key
replay succeeds; same key/different fingerprint is 409; partial checkpoint recovers. Success requires COMPLETED,
COMPLETION_VERIFIED, pointer and immutable-target read-back. Staging/Production always reject.

Test allow-list: `VerifiedMarketOperationalEligibilityTests.cs`,
`VerifiedMarketOperationalProvisioningRunnerTests.cs`, `BusinessReferenceDataVerifiedMarketOperationalMongoTests.cs`,
`BusinessReferenceDataVerifiedMarketCatalogLoadMongoTests.cs`,
`BusinessReferenceDataVerifiedMarketPublishMongoTests.cs`, `BusinessReferenceDataVerifiedMarketResolveMongoTests.cs`,
`DependencyInjectionSmokeTests.cs`, existing market resolve/enumeration/authorization regressions, and existing GSKU
operational/publish/state-machine regressions. Matrix: eligibility, DI/options, artifact identity/read-only behavior,
replay/409, checkpoint recovery, verified read-back, no-assignment behavior and GSKU regression.

Code/test evidence is complete. The separately authorized Local Development operational run completed on 2026-08-09
through the explicit `--run-verified-market-provisioning` command gate. The gate is exact-argument and
Development-only, resolves the real scoped runner from DI, exits without starting the API host, and is neither a
hosted service nor startup auto-publication. Operational facts were reference tenant
`97c59330-dbc4-4665-b29c-0c26dbb5cc93`, actor `mod-0048-fu01-market-local-ops`, namespace
`mod0290-market-local-20260808`, version `UNSD-M49-2026-08-08`, and immutable artifact fingerprint
`b94c45280195b0cb5faa155656c4690938790144d148fba279d2232204360039`.

The first publish completed. The first real replay exposed an existing-version branch that omitted the market
authorization/facts/namespace and fell back to generic fail-closed eligibility. The loader branch was corrected inside
the existing allow-list and its real-Mongo test now uses runtime-negative generic eligibility so the regression cannot
be masked by a positive test seam. Final replay exited successfully. Read-back proved exactly one `market` set, one
immutable Published version with 249 values including `TW`, one namespaced publish operation in
`COMPLETED`/`COMPLETION_VERIFIED`, the set pointer targeting that version, and zero market tenant assignments.
Focused operational tests passed 26/26, all Business Reference Data tests passed 166/166, the full Platform suite
passed 1537/1537, and the isolated Release API build completed with zero errors and seven pre-existing warnings.
Production/Staging enablement remains prohibited and separately gated.
- After market code/test completion, request a separate exact operational provisioning approval. Do not provision
  through direct Mongo, startup/hosted worker, appsettings activation or a test-only production eligibility seam.
- BL-017: quantity-free, kit and packaging-hierarchy presentations; do not add additional PackApplicability values
  here without an approved scope change.
- BL-027: provider-owned legacy PSS-012 data quarantine/reapproval/migration risk assessment.
- ProductType, DosageForm, RouteOfAdministration and StrengthRepresentationType receive exact provider contracts only
  through later owner-approved scope; this pack does not infer their SetCodes/catalogs.
- Completion of this pack can close G2 only for `pack-applicability` and `uom`. Common provider mechanics may be
  reused later, but the remaining four family contracts and their G2 evidence stay open.
- Initial production catalog seed and publication are a separate authorized delivery step after provider hardening.
- API/gateway/public exposure is a separate authorized delivery step after security and tenant-binding proof.
- `Verified GSKU Catalog Operational Readiness & Bounded UoM Enumeration` is the next provider design step, but this
  revision grants neither code-start nor a local operational run. After separate authorization, substep A must close
  before substep B evidence can be accepted.
- `Verified GSKU Resolver` is code-start authorized and is the next direct implementation step using only the exact
  Section 5 allow-list.
- Submit/approval `PINNED` resolution, historical pinned behavior and persistence remain a later MOD-0290 step; they
  are not implied by verified catalog publication, the resolver seam or the seed artifact.
- `Verified GSKU Resolver` owns the narrow Platform HTTP/application contract and the MDM typed client/adapter seam,
  but not GSKU entity/create-handler/validator/persistence. The next MOD-0290 implementation step consumes that seam
  before first GSKU draft creation, then separately owns persistence of the six-field `ReferenceCatalogSelection`,
  compatibility/precision validation and later pin-at-submit/approval behavior.
- The MDM adapter forwards only the current validated Bearer token and must never forward `X-Tenant-Id`, body tenant or
  reference tenant. Resolver credential authentication does not establish tenant scope; Platform derives it only from
  the independently validated JWT claim, then the application authorizes each set with that tenant's assignment.
- Per-tenant resolver credentials are not part of this first runtime step. They require a future explicit policy and
  owner decision before they can be considered.
- Provider ready-only health registration, assignment administration/provisioning automation, resolver credential
  issuance/rotation, internal TLS deployment, operational metrics/runbook and production activation remain later
  owner gates even after resolver contract tests pass.
- MOD-0290 typed-client enumeration, `IVerifiedGskuReferenceResolver` change, create-options facade/route and GSKU UI
  remain exclusively in MOD-0290 step A and later steps; none is absorbed by this provider named step.
- Production enablement, monitoring and operational handoff occur only after G2/G3 evidence and release gates close.
- MOD-0290 C adapter/semantic-validation implementation remains in its own Module Pack and cannot be absorbed here.
