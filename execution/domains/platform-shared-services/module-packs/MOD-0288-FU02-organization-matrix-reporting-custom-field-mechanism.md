---
id: MOD-0288-FU02
name: Organization Matrix Reporting & Custom Field Mechanism
domain: platform-shared-services
service: Diten.Platform
shell: none
golden_reference: none
entity_base: BaseEntity
status: ready-for-dev
owner: platform-shared-services / organization-governance-owner
branch: feature/pss/mod-0288-fu02-organization-matrix-custom-fields
started: 2026-09-07
target: governance-review
approved: 2026-09-07
form_field_count: 0
production_authority: none
---

# MOD-0288-FU02 — Organization Matrix Reporting & Custom Field Mechanism

> **Draft (2026-09-07):** This pack records a backend-only extension of MOD-0288. It creates no deployment,
> migration, production activation or frontend authority. `production_authority: none`; default-off/non-activating.
> The HCM owner approved the extension and answered all three §7 questions on 2026-09-07, and decision 1 in §8 is
> closed. See §21 for the HCM approval addendum (two-line/two-manager consequence, `SafeDisplayMetadata`
> prohibition, future reporting-line work).
>
> **All five semantic decisions in §8 are closed** (2026-09-07). Decision 1 closed on the HCM owner's answer;
> decisions 2–5 closed on user approval after Control Tower review, each with an addition or correction rather than
> a bare acceptance: no fallback when the administrative parent is null; the cycle rule's rationale corrected from
> traversal safety to ambiguity; a mandatory field-count limit and value classification the precedent never had;
> and `IsQueryable` reframed from a technical restriction to a product flag, because one multikey index already
> makes every field queryable.
>
> **Decision 1 was reopened and reversed by the PPM review**, and this is the single most important line in the
> pack. It briefly read "approval follows the functional line". It now reads: **FU02 changes approval behaviour not
> at all.** Code says MOD-0024 passes only a candidate hint and MOD-0023 resolves authority; PPM says a record's
> Sponsor and Portfolio owner are not derivable from anyone's position manager. The second line is a **reporting
> relationship**, and the pack asserts nothing about escalation — while explicitly leaving the door open for a
> future, deliberate approval policy to use it (§21.4).
>
> **§5 now carries an exact file allowlist**, built by reading the tree. It exposed a path in the earlier draft that
> does not exist: `Diten.Platform.Api/Controllers/TenantOrganizationController.cs` was wrong in casing, in folder
> and in filename. An implementation started on that string would have failed on its first edit.

## 1. Module Summary

The manager-controlled `GMG-CGV-LOG-0005 Group Organizational Unit Register` demonstrates two gaps in the
current organization model: Organization Units need a second reporting line, and tenant organization
administrators need governed custom field definitions rather than a code change for every additional datum.

The observed register contains 13 units and 20 columns; those figures describe the supplied evidence and are
not runtime database counts. Existing fields cover name/code/parent, Legal Entity, Cost Centre, Site,
effective date, status and Unit Head. This follow-up addresses only:

1. a second Organization Unit reporting line for matrix reporting; and
2. a TaskFieldDefinition-derived definition/value mechanism for Organization Unit custom fields.

The seven observed governance fields—Permanent OU ID, Regulatory role / independence requirement,
Accountable executive, Approval reference, Charter reference, IT-directory mapping status and Evidence
reference—are acceptance use cases for the mechanism, not seven hardcoded OrganizationUnit properties.

International product rationale supports a relationship/configuration model: SAP OM models multiple A/B
relationships such as reports-to and belongs-to; Oracle HCM distinguishes line, project and functional manager
types; Workday separates Supervisory and Matrix Organizations. SAP Infotypes, Oracle Flexfields and Workday
Custom Objects likewise demonstrate customer-configurable fields. These are design rationale only, not external
runtime dependencies or permission to copy proprietary schemas.

## 2. Ownership and Boundaries

MOD-0288-FU02 owns:

- the optional second reporting relationship of an Organization Unit;
- validation and traversal semantics for both reporting lines;
- tenant-scoped Organization Unit custom field definitions and typed values;
- definition lifecycle, validation and safe query projection;
- additive organization read contracts needed by approved consumers.

It does not own approvals, HR policy, Position matrix membership, Legal Entity, Cost Centre, document/evidence
storage, directory synchronization or historical schema reconstruction. MOD-0023 remains approval owner;
MOD-0031 remains evidence-link owner; HCM remains employee/employment owner.

## 3. Owned Objects

- **OrganizationUnit matrix relationship:** additive optional reference on the existing aggregate; not a new
  organization tree or duplicate Organization Unit.
- **OrganizationUnitFieldDefinition:** tenant-scoped configuration derived from the existing
  `TaskFieldDefinition` pattern: stable normalized code, localized/display label contract, typed data kind,
  required/active/queryable controls, constraints, ordering, audit, soft delete and optimistic version.
- **OrganizationUnitFieldValue:** value bound to one same-tenant Organization Unit and one active definition;
  typed validation is server-side. Values are not arbitrary top-level BSON properties.

No new Person, Position, PositionAssignment, User, LegalEntity, Approval, Evidence or HR aggregate is owned.

## 4. Entity Fields

### Existing OrganizationUnit additive field

| Field | Required | Draft meaning |
|---|---:|---|
| `AdministrativeParentOrganizationUnitId` | No | Proposed second reporting line; `null` means no administrative line. It must never repeat the primary/functional parent merely to fill a value. Final naming is an owner gate. |

The existing `ParentOrganizationUnitId` is the **functional** reporting line. This interpretation was accepted on
2026-09-07 together with §8 decision 1 (HCM semantic split: functional = work allocation); it is no longer a gate.

### OrganizationUnitFieldDefinition

| Field | Required | Rule |
|---|---:|---|
| `Id`, `TenantId`, audit/soft-delete/version | Yes | Existing `BaseEntity` tenant semantics. `TenantId` never enters request DTOs. |
| `Code` | Yes | Normalized immutable key; unique per tenant among non-deleted definitions. |
| `Name` | Yes | Display name; localized presentation strategy is an owner decision before any UI follow-up. |
| `DataType` | Yes | Closed set, exhaustive at v1: `Text`, `MultilineText`, `Integer`, `Decimal`, `Boolean`, `Date`, `SingleSelect` (options declared on the definition), `Reference` (to an Organization Unit or a Position, same tenant). **Nothing else** — no free-form JSON, no expression, no executable/script type, no arbitrary reference target. Adding a ninth type is a follow-up pack, because each type carries validation, query and index consequences. |
| `IsRequired` | Yes | Controls validation of active Organization Units without inventing retroactive values. |
| `IsActive` | Yes | Inactive definitions accept no new values; historical stored values remain readable under policy. |
| `IsQueryable` | Yes | Explicit opt-in for supported equality/filter reporting; does not imply arbitrary indexing. |
| `ValidationRules` | No | Bounded declarative constraints appropriate to `DataType`; no code expressions. |
| `DisplayOrder` | Yes | Stable non-negative ordering. |

### OrganizationUnitFieldValue

| Field | Required | Rule |
|---|---:|---|
| `OrganizationUnitId` | Yes | Same-tenant, non-deleted Organization Unit. |
| `DefinitionId` | Yes | Same-tenant, active definition. |
| typed value | Conditional | Exactly one representation compatible with definition `DataType`; blank and null follow requiredness. |
| audit/soft-delete/version | Yes | Tenant isolation, audit and optimistic concurrency. |

The seven register governance fields are seed/example scenarios for definition/value tests only. This pack does
not hardcode seven properties, definitions, values, IDs or production seeds.

## 5. Repo Scope

This authoring turn changes only this pack. The exact allowlist below replaces the earlier planning roots and was
built by reading the current tree on 2026-09-07, not by pattern-guessing. **No wildcards.**

⚠ The previous draft named `Diten.Platform.Api/Controllers/TenantOrganizationController.cs`. **No such file
exists.** Three things were wrong at once: the project folder is `Diten.Platform.API` (upper case), controllers
sit under a `Platform/` subfolder, and the file is named for the resource. The real path is in the table below.
Any allowlist built on the old string would have failed on the first edit.

**Existing files that change**

| File | Change |
|---|---|
| `services/Diten.Platform/src/Diten.Platform.Domain/Entities/Organization/OrganizationUnit.cs` | add `AdministrativeParentOrganizationUnitId` (nullable) |
| `services/Diten.Platform/src/Diten.Platform.Domain/Repositories/IOrganizationUnitRepository.cs` | traversal contract for the second line |
| `services/Diten.Platform/src/Diten.Platform.Application/Features/TenantOrganization/TenantOrganizationContracts.cs` | line-qualified parent fields (§21.1) |
| `.../TenantOrganization/Commands/CreateOrganizationUnitCommand.cs` | second parent input |
| `.../TenantOrganization/Commands/UpdateOrganizationUnitCommand.cs` | second parent input |
| `.../TenantOrganization/Handlers/CommandHandlers/CreateOrganizationUnitCommandHandler.cs` | validate + persist second line |
| `.../TenantOrganization/Handlers/CommandHandlers/UpdateOrganizationUnitCommandHandler.cs` | validate + persist second line |
| `.../TenantOrganization/Handlers/CommandHandlers/OrganizationUnitCycleGuard.cs` | per-line and combined-graph cycles; batched hop reads |
| `.../TenantOrganization/Validators/CreateOrganizationUnitCommandValidator.cs` | §12 second-parent rules |
| `.../TenantOrganization/Validators/UpdateOrganizationUnitCommandValidator.cs` | §12 second-parent rules |
| `.../TenantOrganization/Handlers/QueryHandlers/GetOrganizationUnitByIdQueryHandler.cs` | expose both lines |
| `.../TenantOrganization/Handlers/QueryHandlers/GetOrganizationUnitsQueryHandler.cs` | expose both lines |
| `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Repositories/OrganizationUnitRepository.cs` | second-line reads |
| `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Schema/PlatformSchemaManifest.Organization.cs` | indexes for the second line and the field-value multikey |
| `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Schema/PlatformCollections.cs` | two new collection constants |
| `services/Diten.Platform/src/Diten.Platform.API/Controllers/Platform/OrganizationUnitsController.cs` | second-line fields; custom field endpoints |
| `services/Diten.Platform/src/Diten.Platform.Application/DependencyInjection.cs` | FU02 registrations only |
| `services/Diten.Platform/src/Diten.Platform.Infrastructure/DependencyInjection.cs` | FU02 registrations only |

**New files**

| File | Purpose |
|---|---|
| `.../Domain/Entities/Organization/OrganizationFieldDefinition.cs` | definition entity |
| `.../Domain/Entities/Organization/OrganizationFieldValue.cs` | value entity |
| `.../Domain/Repositories/IOrganizationFieldDefinitionRepository.cs` | definition repository contract |
| `.../Domain/Repositories/IOrganizationFieldValueRepository.cs` | value repository contract |
| `.../Application/Features/TenantOrganization/Commands/CreateOrganizationFieldDefinitionCommand.cs` | definition create |
| `.../Application/Features/TenantOrganization/Commands/UpdateOrganizationFieldDefinitionCommand.cs` | definition update |
| `.../Application/Features/TenantOrganization/Commands/DeactivateOrganizationFieldDefinitionCommand.cs` | definition deactivate |
| `.../Application/Features/TenantOrganization/Commands/SetOrganizationFieldValueCommand.cs` | value write |
| `.../Application/Features/TenantOrganization/Handlers/CommandHandlers/CreateOrganizationFieldDefinitionCommandHandler.cs` | sealed handler |
| `.../Application/Features/TenantOrganization/Handlers/CommandHandlers/UpdateOrganizationFieldDefinitionCommandHandler.cs` | sealed handler |
| `.../Application/Features/TenantOrganization/Handlers/CommandHandlers/DeactivateOrganizationFieldDefinitionCommandHandler.cs` | sealed handler |
| `.../Application/Features/TenantOrganization/Handlers/CommandHandlers/SetOrganizationFieldValueCommandHandler.cs` | sealed handler |
| `.../Application/Features/TenantOrganization/Queries/GetOrganizationFieldDefinitionsQuery.cs` | definition read |
| `.../Application/Features/TenantOrganization/Queries/GetOrganizationFieldValuesQuery.cs` | value read |
| `.../Application/Features/TenantOrganization/Handlers/QueryHandlers/GetOrganizationFieldDefinitionsQueryHandler.cs` | sealed handler |
| `.../Application/Features/TenantOrganization/Handlers/QueryHandlers/GetOrganizationFieldValuesQueryHandler.cs` | sealed handler |
| `.../Application/Features/TenantOrganization/Validators/CreateOrganizationFieldDefinitionValidator.cs` | no `Command` suffix, per §10 |
| `.../Application/Features/TenantOrganization/Validators/UpdateOrganizationFieldDefinitionValidator.cs` | — |
| `.../Application/Features/TenantOrganization/Validators/DeactivateOrganizationFieldDefinitionValidator.cs` | — |
| `.../Application/Features/TenantOrganization/Validators/SetOrganizationFieldValueValidator.cs` | — |
| `.../Application/Features/TenantOrganization/Services/OrganizationFieldDefinitionRules.cs` | rules adapted from `TaskFieldDefinitionRules` — a separate file, never shared with Tasks |
| `.../Infrastructure/Persistence/Repositories/OrganizationFieldRepositories.cs` | both repository implementations |

**Tests**

| File | Covers |
|---|---|
| `services/Diten.Platform/tests/Diten.Platform.Application.Tests/TenantOrganization/OrganizationMatrixReportingTests.cs` | second line: optional, self rejected, deliberate equal-parent accepted, per-line and cross-line cycles, depth 32, concurrent-parent race |
| `.../TenantOrganization/OrganizationFieldDefinitionTests.cs` | code normalization, closed type set, immutability, deactivation, count limit, classification |
| `.../TenantOrganization/OrganizationFieldValueTests.cs` | typing, one active value per unit/definition, tenant isolation, CAS, soft delete |
| `.../TenantOrganization/OrganizationFieldQueryTests.cs` | equality / `in` / prefix-contains on the value collection, `400` for a filter naming a non-queryable definition, refusal of range on mixed `ValueType`, paging bound 200 |
| `.../TenantOrganization/InMemoryTenantOrganizationRepositories.cs` | *(existing)* extend fakes for the two new repositories |
| `services/Diten.Platform/tests/Diten.Platform.Application.Tests/TenantOrganization/OrganizationFu02BoundaryArchitectureTests.cs` | **two mandatory architecture assertions**: no FU02 file writes to `SafeDisplayMetadata` (§21.2), and no FU02 file references `TaskAssignmentScopeResolver`, `TaskApprovalService` or workflow candidate resolution (§16 criterion 4) |

**This pack file itself is in scope for the implementation turn** — §19 Implementation Notes is updated as the work
lands. No other pack, registry or `.antigravity` file is touched.

Schema/index changes are explicit and exhaustive:

- one index on `OrganizationUnit` over `(TenantId, AdministrativeParentOrganizationUnitId)`, tenant-first;
- one compound index on the value collection over `(TenantId, DefinitionId, Value)`, tenant-first, serving filters;
- one unique index on the definition collection over `(TenantId, Code)` filtered to non-deleted rows, mirroring
  `ux_organization_units_tenant_code_active`;
- one unique index on the value collection over `(TenantId, OrganizationUnitId, DefinitionId)` filtered to
  non-deleted rows — this is what enforces "one active value per unit per definition", in the database rather than
  in a handler that a second writer can race;
- one index on the value collection over `(TenantId, OrganizationUnitId)` for per-unit reads.

Every key is tenant-first, matching `multi-tenancy.md`. Uniqueness is partial on non-deleted rows so a soft-deleted
definition does not block reuse of its code. **No other index is authorized**, and no index is created outside
`PlatformSchemaManifest.Organization.cs`.

## 6. Protected Paths

- `.antigravity/**`, `AGENTS.md`, registry, domain config and other module packs.
- `services/Diten.HcmService/**`, `services/Diten.AuthService/**`, `services/Diten.MdmService/**` and all other services.
- Existing MOD-0024 TaskFieldDefinition runtime files: reference pattern only; they are not edited or coupled.
- MOD-0023 approval implementation, MOD-0031 evidence implementation and document storage.
- Position/PositionAssignment ownership and any second Position-to-OrganizationUnit membership.
- Gateway/`ocelot.json`, all frontend/shared layouts and localization resources.
- Appsettings, launchSettings, secrets, credentials, deployment and production activation.
- Unrelated Platform features, permission evaluation and shared authorization infrastructure.

## 7. Dependencies

- Parent MOD-0288 is `done`; FU01 is `done` and remains unchanged.
- Existing TaskFieldDefinition domain/application/repository/schema/test implementation is the in-repository
  pattern to adapt semantically, without importing Task ownership or sharing its persistence collection.
- HCM consumes organization identity additively: `EmploymentRecordResponse` carries `LegalEntityId`,
  `OrganizationUnitId`, `PositionId`; `IReferenceValidationClient` validates Organization Unit and Position.
  Current evidence shows identity/referenceability consumption, not reads of organization custom fields.

The three HR owner answers were mandatory blockers for `ready-for-dev`. They were received and recorded on
2026-09-07; each was independently re-measured against `services/Diten.HcmService/src` the same day.

| # | Question | HCM owner answer (2026-09-07) | Independent measurement |
|---:|---|---|---|
| 1 | Is the existing validation response sufficient, or must it expose which reporting line was validated? | **Sufficient for the first phase.** No reporting-line indicator is added to the validation response. | N/A (semantic answer). |
| 2 | Will HCM read either reporting line for manager/reporting behavior? | **No.** HCM does not read a reporting line today. | `ReportsTo` / `ManagerId` / `reporting line` grep over HCM source: 0 hits. |
| 3 | Does any other HCM path read concrete Organization Unit fields beyond identity/referenceability? | **No.** HCM consumes organization identity only. | Organization field-name reads (parent, cost centre, unit head, site, name/code projections): 0 hits. Identity references: `LegalEntityId` 16 · `OrganizationUnitId` 7 · `PositionId` 7. |

Disposition: HCM compatibility is **identity-only**. No HCM contract, DTO or behavior changes in FU02 and no HCM
file may change. HCM's own caveat is binding: if reporting-line semantics are ever needed by HCM, they are
requested as a separate endpoint/DTO/decision (§21.3), not folded into FU02.

## 8. Runtime Constraints and Five Semantic Decisions

**All five semantic decisions are CLOSED as of 2026-09-07.** Decision 1 closed on the HCM owner's answer;
decisions 2–5 closed on user approval after Control Tower review, and each carries an addition or correction made
during that review rather than a bare acceptance. These are approved business semantics, not recommendations.

Two of the closures were reached by measuring the in-repository precedent rather than by reasoning about it — the
`TaskFieldValue` array shape and the absence of any field-count limit in `TaskFieldDefinitionRules`. Where the
precedent left a gap, this pack closes it instead of inheriting it.

| # | Semantic decision | Recommended draft | Unresolved owner gate |
|---:|---|---|---|
| 1 | Which line drives approval escalation? | **CLOSED 2026-09-07 — none. FU02 does not change approval behaviour at all.** An earlier closure said "the functional line" and it was withdrawn the same day against code evidence: `TaskApprovalService` passes `ApprovalManagerUserId` to MOD-0023 as a **candidate hint only** — its own comment reads *"MOD-0024 never decides authority"* — and MOD-0023 resolves the actual approver through `RuntimeAssignmentSnapshot`. Task **scope** resolution (`TaskAssignmentScopeResolver`, walking `Position.ReportsToPositionId`) and **approver** determination are separate mechanisms, and the withdrawn closure described them as one. Beyond that, PPM shows the general rule would be false: its transitions require a record's **Sponsor** and **Portfolio owner**, who are not derivable from anyone's position manager. FU02 therefore adds the second line as a **reporting relationship** and asserts nothing about escalation. It equally does not forbid a future, explicit approval policy from consuming these relationships — see §21.4. | Closed by HCM owner answer, PPM review and user approval. |
| 2 | Is the second line required? | **CLOSED 2026-09-07 — no.** `AdministrativeParentOrganizationUnitId` is nullable and the functional parent is never duplicated into it as a synthetic value; duplication would make "genuinely two lines" indistinguishable from "defaulted". **Addition on closure:** when the administrative parent is null the administrative manager is *undefined* — there is **no fallback to the functional line**. A fallback collapses two lines into one and destroys the distinction the feature exists for. Surfaces show nothing, not a substitute. | Closed by user approval. |
| 3 | Cycle and depth | **CLOSED 2026-09-07 — depth 32 per line, self/cycle rejected per line, and a cross-line cycle in the combined typed graph also rejected** (A-functional-B / B-administrative-A is invalid). Depth 32 matches the existing single line. **Rationale correction on closure:** the reason is **ambiguity, not traversal safety.** Approval traverses *neither* line — it walks `Position.ReportsToPositionId` (§8 decision 1) — so a cross-line cycle would overflow nothing; it would instead produce a structure whose meaning nobody can state. Recording the wrong reason invites the wrong relaxation later. Fail-closed is deliberate: tightening after data exists is impossible, loosening is cheap. | Closed by user approval. |
| 4 | Who defines custom fields? | **CLOSED 2026-09-07 — authorized tenant organization administrators**, tenant-scoped; Platform supplies mechanism and policy, never tenant field content. **Three additions on closure, each closing a gap the precedent left open:** (a) **permission keys are defined in §14 and nowhere else** — the earlier `organization.field-definition.*` set named here is withdrawn; it lacked the `platform.` prefix and did not match the catalog family measured in the tree; (b) a **definition-count limit per tenant is mandatory — 50 active definitions**, with `409` beyond it — `TaskFieldDefinition` caps sections (`MaxSections = 6`) but leaves field count unbounded, and that omission is not to be copied: an unbounded count breaks both the screen and the query; (c) each value carries **`Classification` / access state**, exactly as `TaskFieldValue` already does. The governance fields this mechanism exists for — Regulatory role, Accountable executive, Evidence reference — are precisely the sensitive ones, and classification added later means retro-classifying every stored value. | Closed by user approval. |
| 5 | Are values queryable? | **CLOSED 2026-09-07 — reframed, and the storage shape settled with it.** Values live in **their own collection**, one document per unit+definition, as §4 defines them — not embedded in the unit. This differs from the `TaskFieldValue` precedent, which embeds an array inside `task_items`, and the difference is deliberate: a separate document gives each value its own version for CAS and its own soft-delete, and keeps the unit document from growing with every governance field. The consequence for querying is that there is **no `$elemMatch`** — an earlier draft said there was, carried over from the embedded precedent. A plain compound index on `(TenantId, DefinitionId, Value)` serves the filters. Fifty definitions still cost **one** index, not fifty; measured index counts are `organization_units` 3 and `task_items` 6 against Mongo's 64-per-collection ceiling, so the ceiling is not in play. `IsQueryable` is therefore **not a performance gate** — nothing is technically unfilterable. It is a **governed choice** about which fields the tenant has decided are filter-worthy, and it is **enforced server-side**: a filter naming a non-queryable definition returns `400`, never a silently narrowed result. Silent omission is the dangerous option, because a wrong result set looks exactly like a correct one. §16 criterion 9 states the same behaviour; §14 states the permissions. **The real technical caveat is different:** `Value` holds mixed kinds (text, number, date) in one field, so equality and prefix/contains are safe while range operators (`<`, `>`, sorting) must be resolved per `ValueType` or refused — a range query across mixed types compares BSON type order, not values. **Supported at v1:** equality, `in`, and case-insensitive prefix-anchored contains on string types only; range and sort on a single explicitly-typed definition; nothing else. Paging is bounded at 200 rows per page. | Closed by user approval. |

General constraints: single Mongo database with tenant isolation; cross-tenant references return non-disclosing
`404`; soft delete and CAS/version are mandatory; no dynamic code/expression execution; schema/value limits and
query complexity are fail-closed; no production seeds or automatic conversion of existing fields.

### Considered and rejected — a site dimension on the uniqueness key

`GMG-ITG-MTX-0001` §1A records that six ERP departments (MFG, PACK, WH, LOG, ENG, FAC) appear once in the vendor
catalogue but twice in the register, and warns that loading them as supplied *"would merge two sites' manufacturing
into one directory object each"*. Read alone, that reads like a schema gap here: `OrganizationUnit` is unique on
`(TenantId, Code)` via `ux_organization_units_tenant_code_active`, with `LocationCode` outside the key — so the same
code genuinely cannot exist at two sites.

Measured against the register itself (`GMG-CGV-LOG-0005` v0.18, 50 units), **no schema change is needed**. The
register carries zero duplicate abbreviations; it separates sites in the code and in the legal entity:

    OU-0028 MTO-MYG  LE-003 Miquel y Garriga     OU-0032 MTO-PL  LE-004 Grand Medical Poland
    OU-0029 WD-MYG   LE-003                      OU-0033 WD-PL   LE-004
    OU-0031 EMF-MYG  LE-003                      OU-0035 EMF-PL  LE-004
    OU-0037 QC-MYG   LE-003                      OU-0038 QC-PL   LE-004

The cardinality finding is about the **vendor catalogue** — the spreadsheet prepared for loading — not about the
register and not about this schema. `LocationCode` already carries the register's `Site` column. Adding
`LocationCode` or `LegalEntityId` to the uniqueness key would therefore solve a problem the data does not have,
while making every existing code ambiguous about which axis disambiguates it.

Recorded so the question is not reopened from the audit text alone. If a future register does introduce a repeated
code across sites, this is the decision to revisit — with that data in hand, not before.

## 9. Layout & Shell Contract

`shell: none`. This pack is backend-only and adds no Razor page, DataTable or form. Therefore
`golden_reference: none`, `form_field_count: 0`, layouts, RESX and DataTable verifiers are N/A. Any future UI
requires a separate approved follow-up and must choose `_LayoutPlatformAdmin.cshtml` or
`_LayoutTenantShell.cshtml` based on the approved actor surface; `_Layout.cshtml` remains frozen.

## 10. Backend File Convention

Use the current TenantOrganization CQRS separation: `Commands/`, `Queries/`,
`Handlers/CommandHandlers/`, `Handlers/QueryHandlers/`, `Validators/`, contracts/mappers and focused services.
Commands/queries are sealed records; handlers are sealed classes without `CommandHandler`/`QueryHandler` naming
drift; validators omit `Command` suffix. Derive field-definition rules from TaskFieldDefinition behavior while
keeping distinct Organization Unit entities, repositories, collections and permissions.

## 11. Frontend File Contract

No frontend file is authorized. Matrix traversal and custom definition/value APIs are backend contracts only in
this pack. No hardcoded seven-field UI, JSON editor or browser-side validation is permitted.

## 12. Validation Rules

| Concern | Required validation |
|---|---|
| Second parent | Optional GUID; same tenant/Legal Entity as child; exists, active/non-deleted; **not self**. **May equal the primary parent when an administrator sets it deliberately** — one unit genuinely holding both responsibilities for another is a real structure, and rejecting it would refuse valid data. What is forbidden is the *system* writing that value: no code path copies the functional parent into the administrative slot, and no default, migration or import produces the pair. The rule bans manufactured duplication, not a human's deliberate choice. |
| Matrix graph | Depth ≤32 per line and combined graph; cycles rejected fail-closed. Depth 32 mirrors the existing guards (`OrganizationUnitCycleGuard`, `TaskAssignmentScopeResolver`, `OrgDataScopeResolver`, `GetManagerChainQueryHandler`, `PositionReferenceGuard`, `SensitiveFieldRedactor`). The **combined-graph** rejection is a separate business rule, not a consequence of the per-line one: approval traverses neither line, so a cross-line cycle would overflow nothing — it would produce a structure whose meaning nobody can state. Recorded as *ambiguity prevention*, so a future relaxation is argued on that ground and not on a performance ground that was never true. |
| Graph concurrency | Per-document CAS does **not** protect the graph. Two concurrent parent updates can each be acyclic in isolation and cyclic together, because `OrganizationUnitCycleGuard` reads the chain and then writes. ⚠ **The service runs as more than one process**, so a `lock`, a semaphore or any in-memory serialization is *not* a solution — it protects one instance while the other writes the other half of the cycle. Re-reading the chain before the write is not sufficient evidence either; that is the same read-then-write window, only narrower. The guarantee must live where all processes meet: a database-level guard — a single-document compare-and-set on a per-tenant structure token that every parent mutation must win, or an equivalent conditional write — with `409` for the loser. Whatever is chosen, a test must demonstrate it with two genuinely concurrent writers, not two sequential calls. Note also the read cost: the guard issues one `GetByIdAsync` per hop, so depth 32 is 32 round trips and two lines double it; batch the hop reads or cache within one validation pass. |
| Definition code | Required, normalized, immutable, tenant-unique among non-deleted definitions. |
| Definition type | Member of closed supported data-type set; cannot change incompatibly after values exist. |
| Required/active | Requiredness and deactivation transitions preserve stored data and prevent invalid new writes. |
| Value | Same-tenant unit/definition; exactly typed; constraints enforced server-side; one active value per unit/definition. |
| Query | Only active `IsQueryable` definitions and approved typed operators; bounded paging/limits. |
| Concurrency | Expected version required for updates/deletes; stale writes return `409`. |

## 13. Failure Path to Verify

- Missing/cross-tenant/soft-deleted parent, definition or unit → non-disclosing `404`.
- Self-reference, per-line cycle, combined cross-line cycle or depth overflow → `400`.
- **A deliberately equal second parent is NOT a failure path** — it returns success. §12 allows an administrator to
  set both lines to the same unit; only system-manufactured duplication is forbidden. An earlier draft listed
  "same primary/secondary target" as a `400` here, which contradicted §12 and is removed.
- Duplicate normalized definition code/value cardinality → `409`.
- Invalid type, incompatible value, blank required value or unsupported query operator → `400`.
- Stale definition/value/unit version → `409`; no partial write.
- Unauthorized actor → `403`; missing authentication → `401`.
- Malformed stored definition/value or indeterminate traversal → fail closed; never silently omit corruption.

## 14. Authorization Convention

Actor is an authenticated tenant administrator with explicit Platform permissions. Keys follow the family already
in the catalog — measured, not invented: `platform.organization-units.{read,create,update,delete,archive}` and
`platform.organization.read-manager-chain` exist today, so FU02 extends that family rather than opening a new one.

**This table is the single source for permission keys in this pack.** §8 and §18 defer to it; an earlier draft
carried a second, differently-shaped set (`organization.field-definition.*`) and that set is withdrawn — it
lacked the `platform.` prefix and did not match the catalog shape.

| Capability | Key | New? |
|---|---|---|
| Read a unit, including **both** reporting lines | `platform.organization-units.read` | existing |
| Update a unit's ordinary attributes (name, description, cost centre…) | `platform.organization-units.update` | existing |
| **Change a reporting line** (functional or administrative) | `platform.organization-units.reporting-line.update` | **new** |
| Read custom field **definitions** | `platform.organization-units.custom-fields.read` | **new** |
| Create / update / deactivate **definitions** | `platform.organization-units.custom-fields.manage` | **new** |
| Write a custom field **value** on a unit | `platform.organization-units.custom-fields.write-value` | **new** |

Four separations are deliberate:

- **Reporting line is not an ordinary attribute.** Moving a unit under a different parent is a structural
  decision; the manager's own control matrix (`GMG-CGV-MTX-0002` sheet 02) gives "transfer to another parent" its
  own approval route, distinct from "rename". A permission that lets someone rename a unit must not silently let
  them re-parent it.
- **Defining a field ≠ filling it in.** `…custom-fields.manage` changes the shape of the tenant's data model;
  `…custom-fields.write-value` records one datum. Most users need only the second.
- **Reading a definition ≠ managing it**, so that a screen can render the field list without granting authorship.
- **Existing organization update does not imply any of the three new keys.** Whoever can update a unit today gains
  nothing new by default.

Values whose definition carries a restricted `Classification` require the same read permission **plus** the
classification's own read grant; storing a classification without enforcing it on read is decoration, not control.

No role-name bypass, no client-supplied TenantId, no HCM permission is authorized.

## 15. Gateway / API Routing Decision

No Gateway change is authorized. Existing Platform route family should carry later endpoints if the exact API
contract is approved. Any new Ocelot mapping is a separate integration-agent task. Service consumers use
Gateway/approved internal contracts and never access another service database.

## 16. Acceptance Criteria

1. A unit can retain only its primary parent; the second parent remains null without duplication.
2. A valid same-tenant second reporting line can be created, updated, read and cleared with CAS protection.
3. Per-line and combined cycles—including A functional→B and B administrative→A—are rejected; depth 32 is enforced.
4. **Approval behaviour is unchanged and this is verified, not assumed.** A regression test proves that adding,
   changing or clearing either reporting line leaves task assignment and approval routing byte-identical: the same
   candidate is passed to MOD-0023, the same approver resolves, the same scope is computed. No FU02 code reads a
   reporting line for an approval decision, and an architecture test asserts that no FU02 file references
   `TaskAssignmentScopeResolver`, `TaskApprovalService` or the workflow candidate resolution path. *(This criterion
   replaces an earlier one that required approval to follow the functional line — see §8 decision 1.)*
5. Authorized tenant administrators can define, update, deactivate and read typed Organization Unit fields;
   unauthorized actors cannot.
6. Definitions and values are tenant-isolated, audited, soft-deleted and concurrency-safe.
7. The seven supplied governance fields can be represented as configured definitions/values without seven
   hardcoded OrganizationUnit properties or production seeds.
8. Requiredness, typing, constraints, immutable code and incompatible type-change behavior are enforced.
9. `IsQueryable` is **enforced server-side**, and the pack settles what it means. A filter naming a definition with
   `IsQueryable=false` is rejected with `400`, not silently ignored — silent omission would return a wrong result
   set that looks correct. The flag is *not* a performance gate: one multikey index already covers every field, so
   nothing is technically unfilterable. It is a governed choice — which fields the tenant has decided are
   filter-worthy — and the API refuses the rest so that screens and integrations cannot drift apart.
   *(An earlier draft called it a UI-only flag in §8 while §12/§16 enforced it server-side; the server-side
   behaviour is the single definition.)* Paging is bounded; unbounded result sets are refused.
10. Existing Organization Unit, Position, PositionAssignment and HCM identity/reference-validation contracts
    remain backward compatible.
11. No Position gains a second OrganizationUnit membership; no historical schema versioning is introduced.
12. No TaskFieldDefinition runtime file, HCM file, frontend, Gateway or other protected path changes.

## 17. Test Expectations

- `dotnet build services/Diten.Platform/src/Diten.Platform.API/Diten.Platform.API.csproj -c Debug`
  *(the project folder is `Diten.Platform.API`, upper case — an earlier draft wrote `.Api` and the command
  would not have resolved.)*
- `dotnet test services/Diten.Platform/tests/Diten.Platform.Application.Tests` plus exact focused tests.
- Unit tests for field rules, typed values, definition lifecycle, authorization and CAS.
- ⚠ **Mongo tests follow DB-010** (`mongo-indexing.md`), which an earlier draft of this pack contradicted by
  asking for "dynamic/disposable" databases. The rule is the opposite and the reason is mechanical: a database per
  test class multiplied by the full schema exhausts the process file limit, `mongod` dies by `fassert`,
  `DisposeAsync` never runs, and the next run starts on the wreckage — **while the tests are still green**, which
  is what makes the diagnosis expensive. Required pattern instead:
  - one **shared** database, a fresh `TenantId` per test — isolation the same way production does it;
  - request only the needed profile, e.g.
    `await PlatformSchemaManifest.ApplyAsync(database, new[] { SchemaProfile.Organization });`
    — never `MongoDbIndexConfigurations.EnsureIndexesAsync`, which is the production startup path;
  - a test whose subject is genuinely not tenant-scoped (a database-wide rule, an idempotent seed) takes its own
    database with a **fixed suffix**, never a GUID.
- Exhaustive two-line graph tests for self, per-line, combined-line cycles and depth boundary 31/32/33.
- Backward compatibility tests for existing MOD-0288 APIs and HCM identity/referenceability response shape.
- Architecture tests proving no HCM/database coupling, no executable dynamic expression and no protected-path drift.
- `git diff --check`, exact implementation allowlist, secret scan and artifact scan.

## 18. Ready-for-dev Checklist

Status is `ready-for-dev` as of 2026-09-07, reached the way `module-pack-standard.md` requires: every gate below
closed *before* the status moved, not alongside it. The one remaining `[ ]` is the standing statement that this
pack grants neither implementation nor production authority on its own — it is not a gate, it is a limit.

The route here was not clean and the record keeps it. The pack was promoted to `ready-for-dev` earlier the same
day with four decisions still open, on a Control Tower instruction that weighed the HCM answers and overlooked
them. It was reverted to `draft`, because marking open items "preflight blockers" in the body cannot substitute
for the status field: an agent reads the frontmatter, takes the authority, and never reaches the caveat. The
decisions were then closed on their merits, and the promotion happened afterwards.

- [x] User approves promotion (2026-09-07, HCM approval addendum); Organization Governance Owner approval is
      carried by the same instruction.
- [x] MOD-0288-FU02 canonical parent/name preflight passes.
- [x] Parent MOD-0288 and FU01 are `done`.
- [x] HCM owner approves the extension (2026-09-07).
- [x] All three HCM questions in §7 have written answers, independent measurement and compatibility disposition.
- [x] Decision 1 is closed with rationale (§8): **FU02 changes approval behaviour not at all.** It was briefly
      closed the other way — "approval follows the functional line" — and withdrawn against code evidence and the
      PPM review the same day.
- [x] Decisions 2–5 in §8 are explicitly accepted or replaced (2026-09-07, user approval after Control Tower
      review; each closure carries an addition or correction, not a bare acceptance).
- [x] Exact permission keys and actor model are approved — **§14 is the single source**; six keys, three of them
      new, in the `platform.organization-units.*` family already present in the catalog. Actor is an authorized
      tenant organization administrator. A per-tenant limit of 50 active definitions is mandatory (§8 decision 4).
- [x] Query operators, bounds and index plan are approved (§8 decision 5): values live in their own collection, so
      a plain compound index on `(TenantId, DefinitionId, Value)` serves filters — no `$elemMatch`, no index per
      field. Supported: equality, `in`, prefix-anchored contains on string types; range and sort only on a single
      explicitly-typed definition; everything else refused. Paging bound 200. `IsQueryable=false` returns `400`,
      never a silently narrowed result. Measured index counts (`organization_units` 3, `task_items` 6) leave
      Mongo's 64-per-collection ceiling far off.
- [x] Exact implementation file allowlist replaces §5 planning roots (2026-09-07). Built from the current tree:
      18 existing files, the new definition/value files, and four test files, with no wildcards. The correction it
      forced is recorded in §5 — the previously named controller path does not exist in the tree.
- [ ] Separate explicit implementation and production authority are recorded; this pack alone grants neither.

**Withdrawn, not merely reclassified.** An earlier revision listed "MOD-0023 records an approval-line selector
contract, default `functional`" as an item here. There is no such selector and none is required: FU02 changes no
approval behaviour (§8 decision 1), so nothing downstream needs to choose a line. A measurement confirms it —
`reportingLine` / `lineSelector` / `functional` return **zero matches** across MOD-0023. Should an approval policy
ever want to consume a reporting line, that is a MOD-0023 decision with its own pack, and FU02 neither performs it
nor blocks it.

## 19. Implementation Notes

Do not invent a generic dynamic-property framework. Adapt the proven TaskFieldDefinition mechanics—normalized
tenant-unique codes, closed types, declarative validation, lifecycle, repository/index discipline and focused
tests—to Organization Units. Do not share task collections or create a dependency from organization code onto
the Tasks feature.

Measurement must be performed at implementation preflight; this pack intentionally records commands, not
unverified measured counts:

```bash
mongosh --quiet diten_personalization_dev --eval \
  'print(db.organization_units.countDocuments({})+" / "+db.positions.countDocuments({}))'

grep -rn "OrganizationUnitId" services/Diten.HcmService/src --include='*.cs'
```

The first command measures current Organization Unit/Position counts in the named local database. The second
enumerates HCM source reads/usages; neither result is asserted by this draft.

### 19.1 Implementation turn — 2026-09-07

**Preflight, run rather than quoted.** `organization_units` 15 / `positions` 14 in `diten_personalization_dev`;
`OrganizationUnitId` appears 7 times across `services/Diten.HcmService/src`, matching the figure §7 recorded, so
HCM's consumption is still identity-only and no HCM file changed.

**Build:** `dotnet build services/Diten.Platform/src/Diten.Platform.API/Diten.Platform.API.csproj -c Debug` —
0 warnings, 0 errors.

**Tests:** the TenantOrganization namespace is 153/153 green. The full Platform application suite is
4,049 tests with 63 distinct failures, and **every one of them is present on the pre-change baseline** — a
detached worktree at `56851b39` was built and run for exactly this comparison. The set difference in the
regression direction is empty. The failures are BusinessReferenceData Mongo-harness tests, DocumentManagement
lifecycle/manifest tests and `Mod0029Fu29aEndpointAttributionTests`; none touches organization.

⚠ **An environment finding that will bite the next implementation turn, and it is DB-010 exactly.** The
`BusinessReferenceData` test harness still opens a database per run with a GUID suffix — the pattern §17
forbids and for the stated reason. After this turn's repeated suite runs the local mongod held 47 such
databases (`diten_platform_brd_itest_gsku_*`, `_asn_*`, `_pub_*`), and the full suite degraded from 3m12s to
not finishing at all. mongod itself stayed healthy and answered `ping`, with no long-running operations, so the
cost is the accumulated file handles rather than a crash — the slow half of the failure DB-010 describes. The
FU02 tests themselves stay at well under a second. **Nothing was deleted:** the residue is another module's
test data and its removal is that owner's call, not this turn's. It is recorded here so the next full-suite
measurement on this machine is not misread as an FU02 regression.

**Every rule was sabotaged and the falling test recorded** — a rule whose sabotage stays green is not being
guarded. Two of the first twelve sabotages stayed green, and neither meant what it looked like: the graph
concurrency sabotage had broken the `$inc` branch while the test's two writers both race on the `_id` INSERT
branch (the first mutation of a tenant finds no token document), and the value-uniqueness sabotage had broken a
handler while the test measured the repository. Both were re-aimed rather than explained away, and the tests
were strengthened in the process — the race now runs twelve rounds so that both CAS branches are exercised and
a round that happens to serialize cannot hide a broken guard, and value uniqueness is now proven against a real
Mongo unique partial index rather than against a fake that agrees with it.

⚠ **One limit, stated rather than glossed.** The unique index's *declaration* cannot be sabotage-proven on its
own: `PlatformSchemaContractMongoTests` compares what the manifest declares against what Mongo built, and both
sides read the same manifest, so removing `Unique = true` moves them together. What IS proven is the behaviour —
a real duplicate insert is refused, a soft-deleted row frees the pair again, and the repository reports the
refusal as `false` rather than throwing or storing a second row.

### 19.1a Sabotage proofs — which line was broken, which test fell

Fourteen rules, each broken in production code, the named test observed RED, the break reverted and the test
observed GREEN again.

| Rule | Line broken | Test that fell |
|---|---|---|
| Combined-graph cycle | `Edges`: administrative edge suppressed | `A_cross_line_cycle_is_rejected_even_though_each_line_alone_is_acyclic` |
| Depth bound | `MaxDepth = 32` → `64` | `Depth_is_bounded_at_thirty_two_ancestors` |
| Batched hop reads | level `GetByIdsAsync` → one call per node | `The_guard_reads_one_level_per_round_trip_rather_than_one_node_per_hop` |
| Reporting-line permission split | the `403` guard disabled | `Ordinary_update_may_rename_a_unit_but_not_re_hang_it` |
| Structure token — INSERT branch | duplicate-key catch returns `true` | `Two_concurrent_reparentings_cannot_both_land` |
| Structure token — `$inc` branch | `MatchedCount == 1` → `true` | `Two_concurrent_reparentings_cannot_both_land` |
| 50 active definitions | `ValidateDefinitionCount` returns `null` | `The_fifty_first_active_definition_is_refused` |
| `IsQueryable` server-side | the `!IsQueryable` branch disabled | `A_filter_naming_a_non_queryable_definition_is_a_400_and_not_a_narrowed_result` |
| Strict enum parsing | classification parse falls back to default | `An_unrecognised_classification_is_refused_rather_than_silently_normal` |
| Classification on read | `mayRead` hard-coded `true` | `A_restricted_value_is_omitted_from_the_payload_without_the_grant` |
| One value per unit+definition | duplicate-key catch condition disabled | `The_database_itself_refuses_a_second_value_for_the_same_unit_and_definition` |
| Type change with values stored | the `AnyForDefinition` guard disabled | `The_type_cannot_change_once_a_value_exists` |
| `SafeDisplayMetadata` ban (§21.2) | untyped dictionary added to `OrganizationFieldValue` | `No_organization_code_carries_data_through_an_untyped_string_dictionary` |
| Approval-path isolation (§16.4) | *(guarded by the same architecture test file)* | `No_organization_code_references_the_approval_or_task_scope_path` |

### 19.2 Deviations from §5, stated rather than absorbed

Four, and each is a consequence the allowlist did not anticipate rather than a widening of scope.

1. **A third collection constant.** §5 says `PlatformCollections.cs` gains "two new collection constants"; it
   gains three. §12 requires the graph guarantee to live "where all processes meet — a database-level guard, a
   single-document compare-and-set on a per-tenant structure token", and a document needs a collection. It is
   `organization_structure_tokens`, keyed `_id` = TenantId, so Mongo's implicit `_id` index is the only index it
   uses and **no index outside §5's exhaustive list was created.**

2. **A second repository interface, in the allowlisted file.** `IOrganizationReportingGraphRepository` is
   declared inside `IOrganizationUnitRepository.cs` — no new file. Widening `IOrganizationUnitRepository` itself
   would have forced unrelated test doubles across the suite to grow three members they have no opinion about,
   and a double that grows a member it does not care about grows it as a stub. A stub that answers "token 0,
   nothing changed" is exactly the wrong answer for a guard that depends on it.

3. **Two test files outside the allowlist changed, mechanically.**
   `TenantOrganization/TenantOrganizationRulesTests.cs` and
   `TenantOrganization/OrganizationEnterpriseFieldsTests.cs` — constructor arity only, since the two
   Organization Unit handlers now also take the graph repository. No assertion, expectation or status code in
   either file was altered.

4. **Three allowlisted files were NOT changed, because they needed nothing.**
   `Diten.Platform.Application/DependencyInjection.cs` (MediatR and FluentValidation register handlers and
   validators by assembly scan), and both Organization Unit query handlers (they project through
   `TenantOrganizationMapper.ToDto`, which now carries both lines, so exposing the second line took no edit
   there).

### 19.3 One thing the pack got wrong about its own permission split

§14 separates "rename a unit" from "move a unit", and the obvious implementation — a second endpoint that takes
the same `OrganizationUnitRequest` — enforces only half of it. The holder of
`platform.organization-units.reporting-line.update` could then rename the unit, move it to another legal entity
or retire it: one permission, every field, granted by the shape of a request rather than by anyone's decision.

The endpoint therefore takes `OrganizationUnitReportingLinesRequest` — two ids and nothing else — and its
handler reads every other field back from storage before delegating to the ordinary update path. There is no
code path from that request to any other property, which is a stronger statement than a validation rule.

### 19.4 Decisions the implementation had to make, and why

- **Cycle, depth and self-reference keep answering `409`, not the `400` §13 lists.** §16 criterion 10 requires
  existing contracts to stay backward compatible, and the live endpoint answers 409 for exactly these inputs
  today. Two existing tests assert it. Criterion 10 outranks a status code in a failure-path list, so the code
  was left alone and this is recorded instead of silently resolved.
- **The reporting-line permission check runs AFTER validation, not before.** An invalid parent — cycle,
  cross-legal-entity, missing — answers precisely what it answered before FU02. Only a *valid* move by a caller
  without `platform.organization-units.reporting-line.update` is the new `403`. Putting the 403 first would
  have changed the reply to inputs that were already being refused, and it discloses nothing either way, since
  anyone who can update a unit can already read the tree.
- **A line sent unchanged is not a change.** An editor that round-trips the whole record keeps working under
  `…update` alone; only an actual move needs the new key. Without this, §14's separation would have broken
  every existing Organization Unit edit screen for a rule about moving.
- **Create is not guarded by the structure token, and the proof is short.** A new unit's id is unknown to every
  other writer, so nothing can point at it and no path can return to it — a create cannot close a cycle.
  Guarding it would turn a fifty-unit import into forty-nine `409`s to buy nothing.
- **A new caveat the pack did not state.** §8 decision 5 permits range and sort on "a single explicitly typed
  definition". Implementing it exposed a second problem: values are stored as canonical strings, and for
  `Integer`/`Decimal` the lexicographic order is not the numeric one — `"10"` sorts before `"9"`. A parallel
  numeric field would fix it and would need an index §5 does not authorize, so **v1 refuses numeric range and
  sort** rather than answering them wrongly. Same reasoning as `IsQueryable`: a wrong ordering looks exactly
  like a right one.
- **Classification is enforced on read through `IActorPermissionContext`**, with the required key DERIVED from
  the classification (`platform.organization-units.custom-fields.read.{classification}`) rather than
  enumerated. §14 fixes six keys and then requires "the classification's own read grant" without naming it;
  deriving keeps the two from drifting when a classification is added. A restricted value the caller may not
  read is **omitted** from the payload and the row is flagged `Redacted` — the row itself is still returned,
  because dropping it would narrow the result silently.
- **The concurrency test needs its own threads.** A `Barrier` blocks the thread that reaches it, and under the
  full suite the thread pool is already saturated by other classes, so two `Task.Run` writers deadlocked
  waiting for a pool that grows one thread per second — the suite hung rather than failed, which is the worse
  outcome because it looks like slowness. The writers now start with `TaskCreationOptions.LongRunning`.
  Recorded because the next concurrency test in this repository will hit exactly the same thing.
- **No regex in `ValidationRules`.** It is the one "declarative" constraint that is really a program, and an
  administrator-supplied one is a denial of service with a friendly name. Length, range, options and reference
  target are data the server compares against.

## 20. Follow-up Items

- Separate small change for Organization Unit type `Group function` plus its seven-language label; it neither
  waits for nor belongs to FU02.
- Historical/effective-dated custom schema versioning remains excluded and needs a separate pack.
- True matrix Position membership (one Position belonging to two Organization Units) remains excluded.
- UI authoring for matrix/custom fields requires a separate pack after actor and shell decisions.
- ~~Record the three HCM owner answers~~ — done 2026-09-07 (§7). No additive read projection is required for the
  first phase; a later HCM reporting-line need is §21.3, a separate endpoint/DTO/decision.
- ~~Record MOD-0023 approval-line selector ownership and contract~~ — **withdrawn 2026-09-07.** No selector exists
  and none is needed: FU02 changes no approval behaviour (§8 decision 1, §21.4). If a future approval policy wants
  to read a reporting line, that is a MOD-0023 pack of its own.
- PPM's record-scoped responsibility, delegation and multi-approver needs (§21.4) — four separate gaps, none of
  them FU02's, sequenced there.
- Screen-level line/label registry (§21.1) must be extended by every future pack that surfaces a manager or
  parent derived from either reporting line.
- Obtain separately approved exact file/index/migration allowlists and implementation authority.

## 21. HCM Approval Addendum (2026-09-07)

Recorded from the HCM owner's approval and the user's instruction of 2026-09-07. These are binding pack
constraints, not implementation notes.

### 21.1 Known consequence — two lines, two managers

The two reporting lines produce **two different managers for the same employee**. Neither of them is the approver:
task approval resolves through `Position.ReportsToPositionId` and MOD-0023, untouched by this pack (§8 decision 1).
The divergence appears wherever the two lines are *displayed* — an org chart, a unit detail page, an export — and
if HCM later adopts the administrative line, its manager name will differ from the functional one shown elsewhere.
**This is not a defect; it is the design.** The first reader who sees two names without this
explanation will open it as a bug, so every surface must declare which line it shows and under which label.

| Surface | Line shown | User-facing label (EN / TR) | Notes |
|---|---|---|---|
| Task Center approval chain (MOD-0024 → MOD-0023) | **None — unchanged by FU02** | — | Corrected 2026-09-07. An earlier row here claimed the chain was "derived from `ParentOrganizationUnitId` traversal"; code does not support it. MOD-0024 passes a candidate hint, MOD-0023 resolves authority, and scope resolution walks `Position.ReportsToPositionId`. FU02 touches none of it. |
| Organization Unit read contracts (MOD-0288 API) | Both, explicitly named | *Functional parent* / *İşlevsel üst birim* · *Administrative parent* / *İdari üst birim* | Never a bare "parent" or "manager" field without the line qualifier. |
| Organization chart / unit detail screens | Both, explicitly named | same pair as above | This is where the two lines are actually consumed: showing the structure the manager's register records. |
| HCM employee/employment surfaces | None today | — | HCM reads identity only (§7). If HCM adopts the administrative line later, it is labelled *Administrative manager* / *İdari amir* and never *manager* alone. |
| PPM records | None — record-scoped, not org-scoped | — | PPM resolves Sponsor / Portfolio owner per record (§21.4). A position manager is not a substitute for either. |

Rules that follow:

- No screen, DTO, export or notification may present a manager or parent unit without naming the line.
- A single unqualified "Manager" label is a defect when both lines exist.
- Localized labels above are the seven-language key set for any future UI pack; FU02 itself ships no UI or RESX.

### 21.2 Prohibition — `SafeDisplayMetadata` is not a transport for organization data

`ReferenceValidationItem.SafeDisplayMetadata` in
`services/Diten.HcmService/src/Diten.HcmService.Application/Features/CoreHrEmployeeMaster/CoreHrEmployeeMasterModels.cs`
is an `IReadOnlyDictionary<string, string>`. Placing the matrix line, a parent unit id/name or any custom field
value into that dictionary is technically possible and tempting: no DTO changes, no test breaks.

**It is forbidden.** No new organization information — reporting line, administrative parent, custom field
definition or value, or any FU02-derived datum — may be carried through `SafeDisplayMetadata` or any other
untyped string dictionary on the validation or draft contracts.

Reason: an untyped dictionary is not a contract. A key placed there is undocumented six months later, has no
version, no validator and no consumer test. HCM's own caveat points the same way: reporting-line semantics, if
ever needed, are designed as a separate endpoint/DTO/decision.

When new information is genuinely required: **an explicit field, an explicit DTO, an explicit version.**
Implementation must add an architecture/contract test asserting that no FU02 code writes to
`SafeDisplayMetadata`, and reviewers must reject any PR that does.

### 21.3 Known next step (out of FU02 scope) — HCM reporting-line semantics

HCM stated that if it ever needs reporting-line semantics, it will request them as a **separate endpoint, DTO
and decision**. This is recorded so FU02's design does not close that door:

- FU02 traversal and read contracts must be line-qualified (§21.1) so a later HCM read can select a line
  without renaming existing fields.
- FU02 must not add an HCM-facing projection, flag or metadata key in anticipation of this need.
- When requested, it becomes its own follow-up pack (candidate id `MOD-0288-FU03`) with HCM as requesting owner
  and MOD-0288 as contract owner; it is not a scope extension of FU02.

### 21.4 PPM review addendum (2026-09-07) — the boundary of this pack

Reviewed against the manager's answer document v0.9 §10 D-07 and the PPM standard §13.4. Binding boundary:

> FU02 supplies Organization Unit reporting relationships and the custom field mechanism. It does not change
> existing task assignment or approval behaviour. It does not forbid a future, explicit approval policy from
> consuming these relationships. It does not make PPM's record-scoped responsibility, delegation and approver
> resolution look satisfied.

The last clause is the important one. PPM §13.4 requires, per record, participants such as **Sponsor** and
**Portfolio owner**, some of them sequential, and a preparer may not approve their own submission. None of those
is derivable from a person's position manager, so an organization-shaped answer must never be presented as
PPM's answer.

**What already exists** — measured, so the follow-up work does not rebuild it:

| Need | State | Evidence |
|---|---|---|
| Preparer cannot approve own submission | **Enforced** | `WorkflowTaskTransitionSupport.cs` — `"Submitter cannot approve their own workflow"`, HTTP 409, `WorkflowReasonCodes.SodViolation` |
| Delegation kinds | Defined | `AssignmentType { Primary, Secondary, Acting, Delegated }` |
| Validity dating | Defined | `PositionAssignment.EffectiveFrom` (required) + `EffectiveTo`; `AssignmentDerivedStatus { Planned, Active, Ended }`, derived and never stored |

**What is missing** — none of it belongs to FU02:

1. **Record-scoped roles.** `OwnerUserId` appears in 23 places but carries one dimension: who owns it. PPM needs
   *who sponsors this record*, *who owns this portfolio*, *which function receives it*. No role-bearing
   responsibility record exists.
2. **The SoD guard is narrower than PPM's rule.** It compares `instance.StartedBy` — whoever *started the
   workflow*. PPM's preparer is whoever *prepared the record*. If A prepares and B starts, A can still approve.
3. **Delegation of an approval task works; what is missing is narrower than an earlier draft claimed.**
   That draft said delegation was "stored but never consumed" and that MOD-0018 was "unwritten". Both were wrong
   and the PPM review caught them. Re-measured:
   - `DelegateWorkflowTaskCommand` / `Validator` / `DelegateWorkflowTaskHandler` all exist, and
     `WorkflowTaskTransitionSupport.DelegateAsync` is a real implementation — it rejects delegating to oneself
     (`WorkflowDelegateSameActorInvalid`, 409), enforces idempotency by key, and writes a transition log.
   - MOD-0018 permission **claims are live and consumed across services**; what is reserved is the ABAC slice
     (`MOD-0018-FU15`, row-level scoping), not authorization as a whole.

   The real gap is the join, not the parts: `AssignmentType.Acting` / `Delegated` on a **position assignment**
   — organizational, dated, with a derived status — is not what approver resolution consults, and an
   organization-side deputy therefore does not become an approval-side delegate. Whether it should is a decision
   for MOD-0023 and MOD-0288 together, and it is not FU02's to make.
4. **Multiple and sequential approvers** — unverified here. `snapshot.ResolvedPrincipalId` reads as a single
   resolved principal; whether MOD-0023 supports the "Sponsor + Portfolio owner, some sequential" shape is an
   open measurement owned by the MOD-0023/PPM side, not closed by this pack.

**Sequencing.** The missing work is on a different axis from FU02 — FU02 extends *unit → unit*, PPM needs
*record → role → person* — so they share no entity or collection and can proceed in parallel. Item 1 should
copy the proven `PositionAssignment` shape (role + validity dates + `Acting`/`Delegated` + derived status)
rather than invent a second pattern. Item 3 depends on MOD-0018 and has the longest queue; item 1 must land
before it, since 3 consumes what 1 stores.
