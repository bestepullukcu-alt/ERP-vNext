---
id: MOD-0288-FU03
name: Organization Unit Screen — Matrix Line and Custom Field Values
domain: platform-shared-services
service: Diten.Platform
shell: tenant
golden_reference: compact
entity_base: BaseEntity
status: ready-for-dev
owner: platform-shared-services / organization-governance-owner
branch: feature/pss/mod-0288-fu03-organization-unit-screen
started: 2026-09-07
target: governance-review
form_field_count: 11
production_authority: none
---

# MOD-0288-FU03 — Organization Unit Screen: Matrix Line and Custom Field Values

> **Draft (2026-09-07).** FU02 delivered the backend for a second reporting line and a custom field
> mechanism, and deliberately shipped no UI (`shell: none`). Today a tenant administrator cannot see or set
> either one: the API accepts them, nothing on screen offers them. This pack closes that for the **unit
> screen**. It creates no deployment or production authority; `production_authority: none`.
>
> ⚠ **Corrected 2026-09-07 — the first draft measured a dead file.** It classified the unit screen as `slim`
> from `_CreateEditOffcanvas.cshtml` and its five fields. That partial is called from nowhere:
> `OrganizationUnitsController.Create` and `.Edit` both return `Form.cshtml`, a full page. The live form
> carries 4 inputs, 5 selects and 1 textarea — **ten fields, `compact`** — and Positions and
> PositionAssignments route to `Form.cshtml` too. Everything below follows the live surface.
>
> **Why still separate from MOD-0288-FU04.** No longer the golden reference — both are `compact` — but the
> work: FU03 widens a screen people already use, FU04 creates a screen set that does not exist. Different risk,
> different verification, and a regression in one must not block the other.

## 1. Module Summary

Three things reach the Organization Unit screen:

1. the **administrative reporting line** delivered by FU02, beside the existing functional one;
2. the `Group function` unit type, the third gap in the manager's register (`GMG-CGV-LOG-0005`), which is one
   enum value and seven labels and belongs here because it opens the same form and the same resource files;
3. **custom field values** on a unit — the definitions themselves are authored in FU04; this pack only renders
   and writes values against whatever definitions exist.

## 2. Ownership and Boundaries

MOD-0288-FU03 owns: the Organization Unit list, offcanvas and details surfaces; the `Group function` enum
value and its seven labels; the dynamic rendering, client validation and submission of custom field values on
a unit.

It does not own: definition authoring (FU04), the FU02 backend contracts, approval behaviour, Position or
PositionAssignment screens.

## 3. Owned Objects

Razor views and their L10n partials under `Views/Organization/OrganizationUnits/`, the matching resx set, the
unit page JavaScript, and one enum value in `OrganizationEnums.cs`.

## 4. Entity Fields

No new entity and no schema change. FU02 already added `AdministrativeParentOrganizationUnitId` and both field
collections; this pack only surfaces them.

One enum value is added:

| Enum | Change |
|---|---|
| `OrgUnitType` | add `GroupFunction`. Existing members (`Department`, `Division`, `Branch`, `Team`, `HQ`) keep their order and meaning. |

⚠ Append it — do not reorder. The enum is persisted by value, and reordering silently rewrites the type of
every stored unit.

## 5. Repo Scope

Exact allowlist, built from the tree on 2026-09-07. No wildcards.

**Existing files that change**

| File | Change |
|---|---|
| `services/Diten.Platform/src/Diten.Platform.Domain/Entities/Organization/OrganizationEnums.cs` | append `GroupFunction` to `OrgUnitType` |
| `frontend/Diten.Web/Views/Organization/OrganizationUnits/Form.cshtml` | administrative-parent select; custom-value container. **The live create/edit surface** — both actions return it. |
| `frontend/Diten.Web/Views/Organization/OrganizationUnits/Details.cshtml` | both lines, line-qualified; custom values |
| `frontend/Diten.Web/Views/Organization/OrganizationUnits/_DataTable.cshtml` | administrative parent as an optional column |
| `frontend/Diten.Web/Views/Organization/OrganizationUnits/_Filter.cshtml` | filter by administrative parent |
| `frontend/Diten.Web/Views/Organization/OrganizationUnits/_FormL10n.cshtml` | new labels |
| `frontend/Diten.Web/Views/Organization/OrganizationUnits/_DetailsL10n.cshtml` | new labels |
| `frontend/Diten.Web/Views/Organization/OrganizationUnits/_IndexL10n.cshtml` | new labels |
| `frontend/Diten.Web/Resources/Views/Organization/OrganizationUnits/OrganizationUnitsIndex.{en,tr,fr,es,zh,ar,ru}.resx` | **seven files**, same keys in all seven |

**New files**

| File | Purpose |
|---|---|
| `frontend/Diten.Web/wwwroot/assets/js/Organization/organization-unit-custom-fields.js` | render definitions, read and validate values — adapted from `Tasks/form-page.js`, not shared with it |

**Protected**: every FU02 backend file, all Position/PositionAssignment views, `_Layout.cshtml`, gateway,
`.antigravity/**`, other packs.

## 6. Layout & Shell Contract

`shell: tenant` → `_LayoutTenantShell`, already used by the unit views. `golden_reference: compact`: create
and edit are the full page `Form.cshtml`, reached through the `Create` and `Edit` actions. **No offcanvas is
introduced or revived.**

Field count after this pack: **11 fixed fields** — ten live today plus the administrative parent.

⚠ **Custom value inputs are dynamic and are not part of that count.** Their number depends on how many
definitions the tenant authored; they render into a container rather than being declared in the form. A full
page absorbs them far better than the abandoned offcanvas would have — which is the second reason the earlier
`slim` classification was wrong rather than merely imprecise.

**Dead partials.** `_CreateEditOffcanvas.cshtml` is referenced from nowhere, and the same pattern exists under
Positions and PositionAssignments. Deleting them is NOT in this pack's scope: they belong to the other two
screens as much as to this one, and removing a file nothing calls still deserves its own decision. Recorded so
the next reader does not mistake them for the surface being changed — which is precisely what happened to this
pack's first draft.

## 7. Localization — the seven-language gate

Every user-visible string lands in **all seven** resx files: `en, tr, fr, es, zh, ar, ru`. Platform modules
take two languages; this is a tenant module and takes seven.

Keys follow the existing pattern measured in the tree — `OrgUnitTypeDepartment`, `OrgUnitTypeDivision`,
`OrgUnitTypeBranch`, `OrgUnitTypeTeam`, `OrgUnitTypeHQ` — so the new type is `OrgUnitTypeGroupFunction`.

**Approved wording (owner, 2026-09-07).** These are the strings, not suggestions. An implementer does not
invent a translation; a missing language is a blocker, not a TODO.

| Key | en | tr | fr |
|---|---|---|---|
| `FunctionalParentLabel` | Functional parent unit | İşlevsel üst birim | Unité parente fonctionnelle |
| `AdministrativeParentLabel` | Administrative parent unit | İdari üst birim | Unité parente administrative |
| `OrgUnitTypeGroupFunction` | Group function | Grup fonksiyonu | Fonction de groupe |
| `CustomFieldsSectionTitle` | Custom fields | Özel alanlar | Champs personnalisés |
| `CustomFieldRequiredError` | This field is required | Bu alan zorunludur | Ce champ est obligatoire |
| `CustomFieldTypeError` | The value does not match the field type | Değer alan tipine uymuyor | La valeur ne correspond pas au type du champ |

| Key | es | ru |
|---|---|---|
| `FunctionalParentLabel` | Unidad superior funcional | Функциональное вышестоящее подразделение |
| `AdministrativeParentLabel` | Unidad superior administrativa | Административное вышестоящее подразделение |
| `OrgUnitTypeGroupFunction` | Función de grupo | Групповая функция |
| `CustomFieldsSectionTitle` | Campos personalizados | Пользовательские поля |
| `CustomFieldRequiredError` | Este campo es obligatorio | Это поле обязательно |
| `CustomFieldTypeError` | El valor no coincide con el tipo de campo | Значение не соответствует типу поля |

| Key | zh | ar |
|---|---|---|
| `FunctionalParentLabel` | 职能上级单位 | الوحدة الأم الوظيفية |
| `AdministrativeParentLabel` | 行政上级单位 | الوحدة الأم الإدارية |
| `OrgUnitTypeGroupFunction` | 集团职能 | وظيفة المجموعة |
| `CustomFieldsSectionTitle` | 自定义字段 | حقول مخصصة |
| `CustomFieldRequiredError` | 此字段为必填项 | هذا الحقل مطلوب |
| `CustomFieldTypeError` | 值与字段类型不匹配 | القيمة لا تطابق نوع الحقل |

`AdministrativeParentHelp` — the one string that must not be shortened, because it states the rule FU02 §8
decision 2 exists to protect:

| | |
|---|---|
| **en** | If left empty, there is no administrative line. It does not fall back to the functional parent. |
| **tr** | Boş bırakılırsa idari hat tanımsızdır. İşlevsel üst birimin yerine geçmez. |
| **fr** | Si laissé vide, il n'y a pas de ligne administrative. Elle ne se rabat pas sur l'unité parente fonctionnelle. |
| **es** | Si se deja vacío, no hay línea administrativa. No recurre a la unidad superior funcional. |
| **ru** | Если оставить пустым, административная линия отсутствует. Она не заменяется функциональной. |
| **zh** | 留空表示没有行政线。不会回退到职能上级单位。 |
| **ar** | إذا تُرك فارغًا، فلا يوجد خط إداري. ولا يعود إلى الوحدة الأم الوظيفية. |

⚠ **Chinese and Arabic need a native review before release.** The grammar is sound and the terms are the
standard corporate ones, but the wording here was produced by Control Tower and not verified by a speaker.
Recorded as a release gate rather than a silent risk — an unreviewed governance label is worse than an
obviously missing one, because nobody looks at it twice.

⚠ The existing `Parent` label is renamed to the functional form. A screen showing "Üst birim" beside "İdari üst
birim" tells the reader the first one is not a line — which is exactly the confusion FU02 §21.1 exists to
prevent.

## 8. Validation Rules

| Concern | Rule |
|---|---|
| Administrative parent | Optional. Same tenant. **May equal the functional parent when the administrator chooses it** (FU02 §12) — the UI must not block that, and must never pre-fill it. |
| Empty administrative parent | Renders as empty. **No fallback to the functional parent**, in the form or in details. |
| Cycles / depth | Server decides. The client shows the returned reason code; it does not re-implement the graph rules. |
| Custom values | Client validates required and type for a better message; the server remains the authority and its rejection wins. |
| `IsQueryable` | Only queryable definitions appear as filters. A non-queryable field is still shown and editable. |
| Classification | A value whose definition carries a restricted classification renders only with the matching read grant; absent it, the field is hidden — not shown blank. |

## 9. Authorization

Keys are FU02 §14 and are not re-invented:

| Surface | Key |
|---|---|
| See both lines, see values | `platform.organization-units.read` |
| Edit ordinary attributes | `platform.organization-units.update` |
| **Change either reporting line** | `platform.organization-units.reporting-line.update` |
| See which definitions exist | `platform.organization-units.custom-fields.read` |
| Write a value | `platform.organization-units.custom-fields.write-value` |

⚠ A user with `update` but not `reporting-line.update` sees both lines **read-only** while the rest of the
form stays editable. Hiding the whole form, or showing an editable control that fails on save, are both wrong.

## 10. Acceptance Criteria

1. A unit can be created and edited with an administrative parent, and with none.
2. Clearing the administrative parent leaves it empty everywhere; no surface substitutes the functional one.
3. Both lines carry line-qualified labels on every surface — list, offcanvas, details, filter.
4. `Group function` is selectable, persists, and reads back correctly in all seven languages.
5. Custom values render from definitions, validate client-side, submit, and read back.
6. A tenant with zero definitions sees no empty custom-field section.
7. Permission split behaves as §9 states, verified for each of the three write keys.
8. Restricted-classification values are hidden without the grant, not shown blank.
9. Seven resx files carry identical key sets — asserted by a test, not by inspection.
10. No approval, Position, PositionAssignment or FU02 backend file changes.

## 11. Test Expectations

- `dotnet build services/Diten.Platform/src/Diten.Platform.API/Diten.Platform.API.csproj -c Debug`
- Existing `TenantOrganization` suite stays green (154 at FU02 commit `c4eb4089`).
- A resx parity test: all seven files, identical keys, no empty values.
- Live verification is **mandatory and not replaceable by tests**: log in, create a unit with both lines,
  clear the administrative one, switch language, and read the screen. This repository has shipped two bugs
  past 1500 green tests; a screen is proven by being used.

## 12. Out of Scope

- Definition authoring — **MOD-0288-FU04** (`compact`).
- Org-chart visualisation of either line.
- Position screens, approval behaviour, any FU02 backend change.
- Historical/effective-dated schema versioning.

## 13. Ready-for-dev Checklist

- [x] Shell and actor decided: `tenant` / tenant organization administrator (FU02 §14).
- [x] Golden reference decided by measuring the LIVE form: 10 + 1 = 11, `compact`.
- [x] First draft's dead-file error corrected in place and recorded, not quietly fixed.
- [x] Exact allowlist replaces planning roots (§5).
- [x] Permission keys taken from FU02 §14, not re-invented.
- [x] Seven-language label wording approved by the owner (2026-09-07); strings are in §7.
- [ ] Native review of the Chinese and Arabic strings before release (§7).
- [ ] Separate implementation and production authority; this pack grants neither.
