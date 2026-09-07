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
| `DataType` | Yes | Closed, versioned set derived from the TaskFieldDefinition mechanism; no executable/script type. |
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
| `.../Application/Features/TenantOrganization/Commands/` — `CreateOrganizationFieldDefinitionCommand.cs`, `UpdateOrganizationFieldDefinitionCommand.cs`, `DeactivateOrganizationFieldDefinitionCommand.cs`, `SetOrganizationFieldValueCommand.cs` | definition lifecycle + value write |
| corresponding handlers under `.../Handlers/CommandHandlers/` | one per command, sealed |
| `.../Application/Features/TenantOrganization/Queries/` — `GetOrganizationFieldDefinitionsQuery.cs`, `GetOrganizationFieldValuesQuery.cs` | reads |
| corresponding handlers under `.../Handlers/QueryHandlers/` | one per query |
| corresponding validators under `.../Validators/` | one per command, no `Command` suffix |
| `.../Application/Features/TenantOrganization/Services/OrganizationFieldDefinitionRules.cs` | rules adapted from `TaskFieldDefinitionRules` — a separate file, not a shared one |
| `.../Infrastructure/Persistence/Repositories/OrganizationFieldRepositories.cs` | both repository implementations |

**Tests**

| File | Covers |
|---|---|
| `services/Diten.Platform/tests/Diten.Platform.Application.Tests/TenantOrganization/OrganizationMatrixReportingTests.cs` | second line: optional, self rejected, deliberate equal-parent accepted, per-line and cross-line cycles, depth 32, concurrent-parent race |
| `.../TenantOrganization/OrganizationFieldDefinitionTests.cs` | code normalization, closed type set, immutability, deactivation, count limit, classification |
| `.../TenantOrganization/OrganizationFieldValueTests.cs` | typing, one active value per unit/definition, tenant isolation, CAS, soft delete |
| `.../TenantOrganization/OrganizationFieldQueryTests.cs` | `$elemMatch` equality/contains, refusal of range on mixed `ValueType`, paging bounds |
| `.../TenantOrganization/InMemoryTenantOrganizationRepositories.cs` | *(existing)* extend fakes for the two new repositories |
| an architecture test asserting no FU02 code writes to `SafeDisplayMetadata` (§21.2) | exact path agreed at implementation handoff |

Schema/index changes are explicit: second-line index on `OrganizationUnit`, and one multikey index on the value
collection over `DefinitionCode` + `Value` (§8 decision 5). No other index is authorized.

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
| 3 | Cycle and depth | **CLOSED 2026-09-07 — depth 32 per line, self/cycle rejected per line, and a cross-line cycle in the combined typed graph also rejected** (A-functional-B / B-administrative-A is invalid). Depth 32 matches the existing single line. **Rationale correction on closure:** the reason is **ambiguity, not traversal safety.** Approval traverses only the functional line, so a cross-line cycle would not overflow anything; it would instead produce a structure whose meaning nobody can state. Recording the wrong reason invites the wrong relaxation later. Fail-closed is deliberate: tightening after data exists is impossible, loosening is cheap. | Closed by user approval. |
| 4 | Who defines custom fields? | **CLOSED 2026-09-07 — authorized tenant organization administrators**, tenant-scoped; Platform supplies mechanism and policy, never tenant field content. **Three additions on closure, each closing a gap the precedent left open:** (a) **permission keys** follow `permission-key-standard`, named `organization.field-definition.*` (`read` / `manage`), not invented at implementation time; (b) a **definition-count limit per tenant is mandatory** — `TaskFieldDefinition` caps sections (`MaxSections = 6`) but leaves field count unbounded, and that omission is not to be copied: an unbounded count breaks both the screen and the query; (c) each value carries **`Classification` / access state**, exactly as `TaskFieldValue` already does. The governance fields this mechanism exists for — Regulatory role, Accountable executive, Evidence reference — are precisely the sensitive ones, and classification added later means retro-classifying every stored value. | Closed by user approval. |
| 5 | Are values queryable? | **CLOSED 2026-09-07 — reframed.** The precedent stores values as an array of `{DefinitionCode, ValueType, Value, Classification, AccessState, Redacted}`, so a **single multikey index** on `DefinitionCode` + `Value` makes every field queryable through `$elemMatch`. Fifty fields cost **one** index, not fifty; measured index counts are `organization_units` 3 and `task_items` 6 against Mongo's 64-per-collection ceiling, so the ceiling is not in play. `IsQueryable` is therefore **not a technical restriction** and must not be written as one — it is a product flag meaning *"offer this field in the filter UI"*. Written as a technical limit, it leaves a future reader asking "why can't I filter this — is an index missing?" with nobody able to answer. **The real technical caveat is different:** `Value` holds mixed kinds (text, number, date) in one field, so equality and contains are safe while range operators (`<`, `>`, sorting) must be handled per `ValueType` or refused. | Closed by user approval. |

General constraints: single Mongo database with tenant isolation; cross-tenant references return non-disclosing
`404`; soft delete and CAS/version are mandatory; no dynamic code/expression execution; schema/value limits and
query complexity are fail-closed; no production seeds or automatic conversion of existing fields.

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
| Graph concurrency | Per-document CAS does **not** protect the graph. Two concurrent parent updates can each be acyclic in isolation and cyclic together, because `OrganizationUnitCycleGuard` reads the chain and then writes. Implementation must close this — either by serializing parent mutations per tenant, or by re-validating the full chain inside the write's optimistic-concurrency boundary and returning `409` on loss. Note also the read cost: the guard issues one `GetByIdAsync` per hop, so 32 depth is 32 round trips and two lines double it; batch the hop reads or cache within one validation pass. |
| Definition code | Required, normalized, immutable, tenant-unique among non-deleted definitions. |
| Definition type | Member of closed supported data-type set; cannot change incompatibly after values exist. |
| Required/active | Requiredness and deactivation transitions preserve stored data and prevent invalid new writes. |
| Value | Same-tenant unit/definition; exactly typed; constraints enforced server-side; one active value per unit/definition. |
| Query | Only active `IsQueryable` definitions and approved typed operators; bounded paging/limits. |
| Concurrency | Expected version required for updates/deletes; stale writes return `409`. |

## 13. Failure Path to Verify

- Missing/cross-tenant/soft-deleted parent, definition or unit → non-disclosing `404`.
- Self-reference, same primary/secondary target, per-line cycle, combined cross-line cycle or depth overflow → `400`.
- Duplicate normalized definition code/value cardinality → `409`.
- Invalid type, incompatible value, blank required value or unsupported query operator → `400`.
- Stale definition/value/unit version → `409`; no partial write.
- Unauthorized actor → `403`; missing authentication → `401`.
- Malformed stored definition/value or indeterminate traversal → fail closed; never silently omit corruption.

## 14. Authorization Convention

Actor is an authenticated tenant administrator with explicit Platform permissions. Recommended keys for owner
review are `platform.organization-units.matrix.read/update` and
`platform.organization-units.custom-fields.read/manage`; these are proposals, not catalog authority. Existing
organization read/update permission does not automatically grant custom-field definition management. No role
name bypass, client-supplied TenantId or HCM permission is authorized.

## 15. Gateway / API Routing Decision

No Gateway change is authorized. Existing Platform route family should carry later endpoints if the exact API
contract is approved. Any new Ocelot mapping is a separate integration-agent task. Service consumers use
Gateway/approved internal contracts and never access another service database.

## 16. Acceptance Criteria

1. A unit can retain only its primary parent; the second parent remains null without duplication.
2. A valid same-tenant second reporting line can be created, updated, read and cleared with CAS protection.
3. Per-line and combined cycles—including A functional→B and B administrative→A—are rejected; depth 32 is enforced.
4. Approval-chain consumption resolves the **functional** line by default through the MOD-0023 policy selector;
   the default comes from policy/configuration (no code literal), an explicit `administrative` selection is
   honoured, and a malformed or unknown selector fails closed.
5. Authorized tenant administrators can define, update, deactivate and read typed Organization Unit fields;
   unauthorized actors cannot.
6. Definitions and values are tenant-isolated, audited, soft-deleted and concurrency-safe.
7. The seven supplied governance fields can be represented as configured definitions/values without seven
   hardcoded OrganizationUnit properties or production seeds.
8. Requiredness, typing, constraints, immutable code and incompatible type-change behavior are enforced.
9. Querying is available only for approved types/operators when `IsQueryable=true`, with bounded paging;
   non-queryable fields cannot be filtered.
10. Existing Organization Unit, Position, PositionAssignment and HCM identity/reference-validation contracts
    remain backward compatible.
11. No Position gains a second OrganizationUnit membership; no historical schema versioning is introduced.
12. No TaskFieldDefinition runtime file, HCM file, frontend, Gateway or other protected path changes.

## 17. Test Expectations

- `dotnet build services/Diten.Platform/src/Diten.Platform.Api/Diten.Platform.Api.csproj -c Debug`.
- `dotnet test services/Diten.Platform/tests/Diten.Platform.Application.Tests` plus exact focused tests.
- Unit tests for field rules, typed values, definition lifecycle, authorization and CAS.
- Dynamic/disposable Mongo tests for tenant isolation, uniqueness, indexes and query bounds once exact index
  authority is approved; no fixed shared test database.
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
- [x] Decision 1 (approval line = functional) is closed with rationale (§8).
- [x] Decisions 2–5 in §8 are explicitly accepted or replaced (2026-09-07, user approval after Control Tower
      review; each closure carries an addition or correction, not a bare acceptance).
- [x] Exact permission keys and actor model are approved — `organization.field-definition.read` /
      `organization.field-definition.manage` per `permission-key-standard`, actor = authorized tenant organization
      administrator, with a mandatory per-tenant definition-count limit (§8 decision 4).
- [x] Query operators, bounds and index plan are approved (§8 decision 5): one multikey index on
      `DefinitionCode` + `Value` served through `$elemMatch`; equality and contains supported; range and sort
      handled per `ValueType` or refused. Derived from the measured precedent shape, not from projected workload —
      measured index counts (`organization_units` 3, `task_items` 6) leave Mongo's 64-per-collection ceiling far off.
- [x] Exact implementation file allowlist replaces §5 planning roots (2026-09-07). Built from the current tree:
      18 existing files, the new definition/value files, and four test files, with no wildcards. The correction it
      forced is recorded in §5 — the previously named controller path does not exist in the tree.
- [ ] Separate explicit implementation and production authority are recorded; this pack alone grants neither.

**Reclassified — not a gate on FU02.** The MOD-0023 approval-line selector contract (default `functional`,
configuration-sourced) is owned by MOD-0023, not by this pack. FU02 establishes the two lines in the organization
model; MOD-0023 consumes them. FU02 code can be written and merged before that contract is recorded, provided it
hardcodes neither line (§8 decision 1). Holding FU02 for another module's contract would block the producer on its
consumer.

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

## 20. Follow-up Items

- Separate small change for Organization Unit type `Group function` plus its seven-language label; it neither
  waits for nor belongs to FU02.
- Historical/effective-dated custom schema versioning remains excluded and needs a separate pack.
- True matrix Position membership (one Position belonging to two Organization Units) remains excluded.
- UI authoring for matrix/custom fields requires a separate pack after actor and shell decisions.
- ~~Record the three HCM owner answers~~ — done 2026-09-07 (§7). No additive read projection is required for the
  first phase; a later HCM reporting-line need is §21.3, a separate endpoint/DTO/decision.
- Record MOD-0023 approval-line selector ownership and contract before any approval consumer work; the line
  itself is decided (functional, §8 decision 1), only the selector contract shape remains.
- Screen-level line/label registry (§21.1) must be extended by every future pack that surfaces a manager or
  parent derived from either reporting line.
- Obtain separately approved exact file/index/migration allowlists and implementation authority.

## 21. HCM Approval Addendum (2026-09-07)

Recorded from the HCM owner's approval and the user's instruction of 2026-09-07. These are binding pack
constraints, not implementation notes.

### 21.1 Known consequence — two lines, two managers

The two reporting lines produce **two different managers for the same employee**. The Task Center (MOD-0024 via
MOD-0023 approval) follows the functional line; if HCM later looks at the administrative line, the manager names
will differ. **This is not a defect; it is the design.** The first reader who sees two names without this
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
3. **Delegation is stored but never consumed.** `TaskItem.DelegationAllowed` is documented as
   *"Policy flag only. Delegation ELIGIBILITY remains MOD-0018's decision"*, and MOD-0018 (RBAC/ABAC
   Authorization) is still `ready-for-dev` — unwritten.
4. **Multiple and sequential approvers** — unverified here. `snapshot.ResolvedPrincipalId` reads as a single
   resolved principal; whether MOD-0023 supports the "Sponsor + Portfolio owner, some sequential" shape is an
   open measurement owned by the MOD-0023/PPM side, not closed by this pack.

**Sequencing.** The missing work is on a different axis from FU02 — FU02 extends *unit → unit*, PPM needs
*record → role → person* — so they share no entity or collection and can proceed in parallel. Item 1 should
copy the proven `PositionAssignment` shape (role + validity dates + `Acting`/`Delegated` + derived status)
rather than invent a second pattern. Item 3 depends on MOD-0018 and has the longest queue; item 1 must land
before it, since 3 consumes what 1 stores.
