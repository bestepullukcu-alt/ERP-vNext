---
id: MOD-0288-FU04
name: Organization Field Definition Authoring
domain: platform-shared-services
service: Diten.Platform
shell: tenant
golden_reference: compact
entity_base: BaseEntity
status: draft
owner: platform-shared-services / organization-governance-owner
branch: feature/pss/mod-0288-fu04-organization-field-definition-authoring
started: 2026-09-07
target: governance-review
form_field_count: 9
production_authority: none
---

# MOD-0288-FU04 — Organization Field Definition Authoring

> **Draft (2026-09-07).** FU02 delivered the definition and value backend; FU03 puts *values* on the unit
> screen. Neither lets anyone **author a definition**, so today a tenant's field list can only be created by
> calling the API directly. This pack adds the authoring surface. `production_authority: none`.
>
> **Why separate from FU03.** Measured on 2026-09-07: the unit offcanvas is five fields (`slim`), the
> definition form is seventeen controls in the Task precedent (`compact`). `module-pack-standard` line 109
> makes a pack imitate one golden reference exactly; the two classes cannot share a pack.

## 1. Module Summary

A tenant organization administrator defines the fields their Organization Units carry — code, label, type,
requiredness, whether it is offered as a filter, its classification and its order — without a code change.
This is the mechanism the manager's seven governance fields (Permanent OU ID, Regulatory role, Accountable
executive, Approval reference, Charter reference, IT-directory mapping, Evidence ref) are configured through,
rather than seven hardcoded properties.

## 2. The precedent is in this repository — copy it, do not design it

`Views/Tasks/FieldDefinitions/` is the same problem already solved, and `golden_reference: compact` means its
structure is imitated exactly rather than approximated:

| Task precedent | Organization equivalent |
|---|---|
| `Controllers/TaskFieldDefinitionsController.cs` | `Controllers/OrganizationFieldDefinitionsController.cs` |
| `Models/TaskFieldDefinitions/TaskFieldDefinitionViewModels.cs` | `Models/OrganizationFieldDefinitions/OrganizationFieldDefinitionViewModels.cs` |
| `Views/Tasks/FieldDefinitions/{Index,Create,Edit,Details}.cshtml` | `Views/Organization/FieldDefinitions/` same four |
| `Views/Tasks/FieldDefinitions/{_Form,_DataTable,_Filter,_IndexL10n}.cshtml` | same four |
| `Views/Tasks/FieldDefinitions/TaskFieldDefinitionsIndex.cs` | `OrganizationFieldDefinitionsIndex.cs` |
| `Resources/.../TaskFieldDefinitionsIndex.{7}.resx` | `OrganizationFieldDefinitionsIndex.{7}.resx` |
| `wwwroot/assets/js/Tasks/FieldDefinitions/{index,form}.js` | `js/Organization/FieldDefinitions/{index,form}.js` |

⚠ **Imitate, do not share.** No file is reused across Tasks and Organization, and no shared base is extracted.
FU02 §7 already fixed this boundary for the backend and the reason is the same here: a shared authoring screen
couples two modules' lifecycles, and the first divergent requirement then breaks both.

## 3. Entity Fields

No new entity. FU02 defined `OrganizationFieldDefinition`; this pack renders it.

Form carries **9 fields** — over the slim ceiling of eight, which is what makes this pack `compact`:

| Field | Control | Note |
|---|---|---|
| `Code` | text | normalized, immutable after creation — read-only on edit |
| `Name` | text | tenant's own words, not a resource key |
| `DataType` | select | closed set of eight (FU02 §4) |
| `IsRequired` | switch | — |
| `IsActive` | switch | inactive accepts no new values; stored values stay readable |
| `IsQueryable` | switch | server-enforced (FU02 §8 decision 5) — the help text must not call it a UI-only flag |
| `Classification` | select | FU02 §8 decision 4 |
| `DisplayOrder` | number | non-negative |
| `ValidationRules` | type-dependent group | bounded, declarative; no expression input |

`SingleSelect` reveals an option editor; no other type does.

## 4. Repo Scope

Exact allowlist. No wildcards. All new except the two noted.

**New**

| File |
|---|
| `frontend/Diten.Web/Controllers/OrganizationFieldDefinitionsController.cs` |
| `frontend/Diten.Web/Models/OrganizationFieldDefinitions/OrganizationFieldDefinitionViewModels.cs` |
| `frontend/Diten.Web/Views/Organization/FieldDefinitions/Index.cshtml` |
| `frontend/Diten.Web/Views/Organization/FieldDefinitions/Create.cshtml` |
| `frontend/Diten.Web/Views/Organization/FieldDefinitions/Edit.cshtml` |
| `frontend/Diten.Web/Views/Organization/FieldDefinitions/Details.cshtml` |
| `frontend/Diten.Web/Views/Organization/FieldDefinitions/_Form.cshtml` |
| `frontend/Diten.Web/Views/Organization/FieldDefinitions/_DataTable.cshtml` |
| `frontend/Diten.Web/Views/Organization/FieldDefinitions/_Filter.cshtml` |
| `frontend/Diten.Web/Views/Organization/FieldDefinitions/_IndexL10n.cshtml` |
| `frontend/Diten.Web/Views/Organization/FieldDefinitions/OrganizationFieldDefinitionsIndex.cs` |
| `frontend/Diten.Web/Resources/Views/Organization/FieldDefinitions/OrganizationFieldDefinitionsIndex.{en,tr,fr,es,zh,ar,ru}.resx` — **seven files** |
| `frontend/Diten.Web/wwwroot/assets/js/Organization/FieldDefinitions/index.js` |
| `frontend/Diten.Web/wwwroot/assets/js/Organization/FieldDefinitions/form.js` |

**Existing that change**

| File | Change |
|---|---|
| tenant navigation registration for the new page | one entry, under the existing Organization group |

**Protected**: every `Views/Tasks/**` file, all FU02 backend files, FU03 unit-screen files, `_Layout.cshtml`,
gateway, `.antigravity/**`.

## 5. Authorization

FU02 §14, not re-invented:

| Surface | Key |
|---|---|
| List and read definitions | `platform.organization-units.custom-fields.read` |
| Create, update, deactivate | `platform.organization-units.custom-fields.manage` |

⚠ `read` alone renders the list and details with **no** create button, no row actions and no editable control.
A user who can see which fields exist is not thereby allowed to change the tenant's data model.

## 6. Validation Rules

| Concern | Rule |
|---|---|
| Code | Required, normalized, unique per tenant among non-deleted. **Immutable after creation** — read-only on edit, not merely un-submitted. |
| Type change | Rejected once values exist. The client warns; the server decides and its `409` wins. |
| Definition count | **50 active per tenant** (FU02 §8 decision 4). The UI shows remaining capacity and refuses the create button at the limit rather than failing on save. |
| Deactivation | Allowed with existing values; the screen states plainly that stored values remain readable and no new ones are accepted. |
| `ValidationRules` | Bounded declarative inputs per type. **No free-text expression field** — FU02 forbids executable rules. |

## 7. Acceptance Criteria

1. A definition can be created, edited, deactivated and read; code is immutable after creation.
2. The eight data types are offered and only those; `SingleSelect` reveals its option editor.
3. At 50 active definitions the create path is refused with an explanation, before submission.
4. Type change with existing values is refused and the reason is legible.
5. `read` without `manage` yields a fully read-only surface — no create, no row action, no editable control.
6. Seven resx files carry identical key sets, asserted by a test.
7. A definition created here appears on the unit screen (FU03) without a restart or cache flush.
8. No `Views/Tasks/**` file changes and nothing is shared with the Task authoring screens.

## 8. Test Expectations

- `dotnet build services/Diten.Platform/src/Diten.Platform.API/Diten.Platform.API.csproj -c Debug`
- `verify_datatable_page.py --reference compact` passes for the new Index.
- resx parity test across the seven files.
- ⚠ Live verification is mandatory: author a definition, fill it on a unit, switch language, confirm the label
  is the tenant's own words and not a resource key. The FU02 backend carries an explicit split between
  `LabelResourceKey` and `LabelText` precisely because conflating them puts a raw key on screen.

## 9. Out of Scope

- Value entry on units — FU03.
- Any FU02 backend change.
- Retroactive population of values for existing units.
- Import/export of definitions.
- Definition versioning or history.

## 10. Ready-for-dev Checklist

- [x] Golden reference decided by measurement: 9 form fields > 8 → `compact`.
- [x] Precedent identified file-by-file (§2); nothing designed from scratch.
- [x] Exact allowlist (§4); permission keys from FU02 §14.
- [ ] Seven-language wording approved, including the eight data-type labels.
- [ ] Navigation placement confirmed by the owner (Organization group, position in the list).
- [ ] Separate implementation and production authority; this pack grants neither.
