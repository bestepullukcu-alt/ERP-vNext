---
id: MOD-0288-FU04
name: Organization Field Definition Authoring
domain: platform-shared-services
service: Diten.Platform
shell: tenant
golden_reference: compact
entity_base: BaseEntity
status: ready-for-dev
owner: platform-shared-services / organization-governance-owner
branch: feature/pss/mod-0288-fu04-organization-field-definition-authoring
started: 2026-09-07
target: governance-review
form_field_count: 9
production_authority: none
---

# MOD-0288-FU04 — Organization Field Definition Authoring

> **Ready-for-dev (2026-09-07).** FU02 delivered the definition and value backend; FU03 puts *values* on the
> unit screen. Neither lets anyone **author a definition**, so today a tenant's field list can only be created
> by calling the API directly. This pack adds the authoring surface. `production_authority: none`.
>
> **Why separate from FU03.** An earlier draft said the two differed by golden reference — unit screen `slim`,
> this one `compact`. That was wrong on both counts and is corrected here: the five-field offcanvas it measured
> is a dead partial no route reaches, and the live unit form carries ten fields, so **both packs are
> `compact`**. The split stands on the work instead: FU03 widens a screen people already use, this pack builds
> a screen set that does not exist. Different risk, different verification, and a regression in one must not
> hold the other.

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

## 5.1 Approved wording — the eight data types (owner, 2026-09-07)

These are the strings a tenant administrator picks from when authoring a field. They are decided, not
suggested; an implementer does not invent a translation, and a missing language blocks release.

| Key | en | tr | fr | es |
|---|---|---|---|---|
| `FieldTypeText` | Text | Metin | Texte | Texto |
| `FieldTypeMultilineText` | Multiline text | Çok satırlı metin | Texte multiligne | Texto multilínea |
| `FieldTypeInteger` | Whole number | Tam sayı | Nombre entier | Número entero |
| `FieldTypeDecimal` | Decimal number | Ondalık sayı | Nombre décimal | Número decimal |
| `FieldTypeBoolean` | Yes / No | Evet / Hayır | Oui / Non | Sí / No |
| `FieldTypeDate` | Date | Tarih | Date | Fecha |
| `FieldTypeSingleSelect` | Single choice | Tek seçim | Choix unique | Selección única |
| `FieldTypeReference` | Reference | Referans | Référence | Referencia |

| Key | ru | zh | ar |
|---|---|---|---|
| `FieldTypeText` | Текст | 文本 | نص |
| `FieldTypeMultilineText` | Многострочный текст | 多行文本 | نص متعدد الأسطر |
| `FieldTypeInteger` | Целое число | 整数 | عدد صحيح |
| `FieldTypeDecimal` | Десятичное число | 小数 | عدد عشري |
| `FieldTypeBoolean` | Да / Нет | 是 / 否 | نعم / لا |
| `FieldTypeDate` | Дата | 日期 | تاريخ |
| `FieldTypeSingleSelect` | Единственный выбор | 单选 | اختيار واحد |
| `FieldTypeReference` | Ссылка | 引用 | مرجع |

Two naming decisions worth keeping, because both were taken against the more literal alternative:

- **`Boolean` is shown as "Yes / No", never "Boolean" or "Logical".** The label tells the user what they will
  see on screen — a switch with two answers — rather than naming the type in the programmer's vocabulary.
- **`Integer` is "Whole number", not "Number".** The distinction from `Decimal` is the entire reason both
  types exist; collapsing it to "Number" makes the choice arbitrary at the moment it is made.

| Key | en | tr |
|---|---|---|
| `DefinitionLimitReached` | The limit of 50 definitions has been reached | 50 tanım sınırına ulaşıldı |
| `CodeImmutableHelp` | The code cannot be changed after the field is created | Kod, alan oluşturulduktan sonra değiştirilemez |
| `InactiveDefinitionHelp` | Stored values remain readable; no new values are accepted | Kayıtlı değerler okunabilir kalır; yeni değer kabul edilmez |

*(remaining five languages follow the same pattern and are written during implementation, in all seven files
at once — a resx parity test asserts the key sets match.)*

⚠ **Chinese and Arabic need a native review before release**, as in FU03 §7. Produced by Control Tower,
grammatically sound, not verified by a speaker.

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

### 8.1 Results — implementation run, 2026-09-08

Branch `feature/pss/mod-0288-fu04-organization-field-definition-authoring`, worktree
`/private/tmp/ERP-vNext-mod0288-fu02-pack`.

| Check | Result |
|---|---|
| `dotnet build …/Diten.Platform.API.csproj -c Debug` | **0 errors** |
| `dotnet build frontend/Diten.Web/Diten.Web.csproj -c Debug` | **0 errors** |
| `verify_datatable_page.py --area Organization --module FieldDefinitions --reference compact --api-profile proxy` | **94 pass / 1 fail** — the single failure is in the SHARED `wwwroot/assets/js/personalization-client.js`, outside §4 and failing for every module (the Task precedent scores 92/3 on the same run). Not touched. |
| `Organization` suite | **188 / 188** (183 before, +5 from the new resx contract test) |
| `TenantOrganization` suite | **154 / 154**, unchanged |
| AuthService role suite | **192 / 192** |
| vitest — the three rules | **16 / 16** in `tests/organization-field-definition-authoring.test.js` |
| vitest — whole frontend suite | 2330 passed / 25 failed. Baseline measured on a stash of this branch: **25 failed** there too, in the same 13 files (`campaign-targeting`, `consent-preference`, `strategy-*`, `dialog-one-implementation`, …). This pack adds 16 passing tests and no failures. |
| `Diten.Web.Tests` (nav l10n guard) | 130 pass / 3 fail — **identical to baseline**; the failures are PPM/PortfolioDelivery keys from another pack. `Nav.Page.ORGANIZATIONFIELDDEFINITIONS` is present in all seven languages and appears in no failure list. |
| resx parity | 7 files × **63 keys**, identical key sets both directions, no empty value — asserted by `OrganizationFieldDefinitionL10nContractTests`. |

### 8.2 The three rules are guarded by tests, and each guard was proved by sabotage

Every rule was broken in the SHIPPING file, the suite was run, and the file was restored. §8's demand was that a
rule not live in a comment — in FU03 the forbidden fallback was pasted in by hand and 183 tests stayed green.

| # | Sabotage — the line that was broken | Test that went RED |
|---|---|---|
| 1 | `_Form.cshtml`: `disabled required data-code-immutable` → `data-code-immutable` (Code becomes typable on edit) | *rule 1 · the edit branch renders Code as a disabled control* |
| 1b | View model: the whole `if (IsEdit … OriginalCode …)` guard block deleted | *rule 1 · the view model refuses a posted code that differs from the stored one* |
| 1c | Controller: `ToUpdatePayload` gains `code = model.Code,` | *rule 1 · the view model refuses a posted code…* |
| 2 | `index.js`: `remaining === 0` → `remaining < 0` (the limit stops closing the button) | *rule 2 · AT the limit the gate is closed and says why* |
| 2b | `index.js`: `filter(isActiveRow).length` → `.length` (inactive definitions consume capacity) | *rule 2 · only ACTIVE definitions count toward the limit* |
| 3 | `index.js`: `canManage: has(MANAGE)` → `has(MANAGE) \|\| has(READ)` | *rule 3 · three tests* |
| 3b | `index.js`: `rowActionsFor` pushes `edit` for a reader | *rule 3 · a reader's only row action is the read one* |
| 4 | `form.js`: case-insensitive option match → `o.value === value` (the FU03 bug, re-armed) | *form.js resolves the type select through its own options* |

⚠ **Sabotage 1b passed GREEN on the first attempt and the guard was rewritten because of it.** The assertion had
been `/IsEdit[\s\S]*OriginalCode[\s\S]*ValidationResult/` across the whole file, which the property
declaration alone satisfied — so deleting the entire guard block changed nothing. It now reads inside the
`Validate()` body. A guard that survives the deletion of the thing it guards is the defect it was written to
prevent, and this one nearly shipped.

### 8.3 Live verification (tenant shell, `localhost:5001`, two languages)

Platform, Diten.Web **and AuthService** rebuilt and restarted from this worktree; `.cshtml` runtime compilation
is off, so a restart is the only way views change.

| Acceptance criterion | Evidence |
|---|---|
| 1 — create, edit, deactivate, read; code immutable | `fu04.tier` / "Yönetişim katmanı" authored through the form. On edit the Code input is **disabled**, shows the stored code and carries the immutability hint; `OriginalCode` travels hidden. |
| 2 — the eight types, and `SingleSelect` reveals its option editor | Measured on the live form: choosing `SingleSelect` shows only `[data-constraint-group="options"]`; length, range and reference groups all hidden. Two-word types render as "Tek seçim" / "Single choice", never as a re-capitalised string. |
| 3 — at 50 the create path is refused **before** submission | The tenant was actually filled to 50 active definitions. The page then read "The limit of 50 definitions has been reached", the Add button was **removed**, and `/Create` redirected away server-side. The 51st API create answered **409**. After deactivating the 46 fillers the line read "46 of 50 definitions remaining" and the button returned — proving inactive definitions do not consume capacity. |
| 4 — type change with values is refused, legibly | FU02 answers 409; the form states the rule up front (`TypeChangeHelp`) rather than only after a failed save. |
| 5 — `read` without `manage` is fully read-only | Verified on a **real second session** (`fu03editor@diten.com`, holding only `…custom-fields.read`): no Add button, no bulk bar, the only row action is quick-view, the notice "You can see which fields exist, but not change them." is shown — and the server refuses too: `/Create` → AccessDenied, `api/bulk` → **403**. |
| 6 — seven resx asserted by a test | As above. |
| 7 — a definition created here appears on the unit screen **without a restart** | Immediately after authoring, the FU03 unit form rendered `fu04.tier` as a select with "Tier 1"/"Tier 2", labelled **"Yönetişim katmanı"** — the tenant's own words, not a resource key. A value was written and read back through FU03. |
| 8 — no `Views/Tasks/**` change, nothing shared | `git diff --name-only`; no Tasks file appears, and no file, base class or helper is common to the two screens. |

**Two defects were found by using the screen and would not have been found by any test written here.**

1. **The bulk bar never appeared and its action never fired.** The first version used DataTables' own `select`
   API with `[data-bulk-bar]` / `[data-bulk-clear]`; the shared binder
   (`DitenDataTable.bindBulkSelection`) reads checkbox inputs and the ids `#bulkActionBar`,
   `#bulkSelectedCount`, `#btnClearSelection`. Nothing matched, and nothing said so. Now on the shared contract.
2. **The remaining-capacity line never rendered.** `applyCreateGate()` looked for `#fieldDefinitionCapacity`,
   which no view contained, so it returned early — the gate worked while the number it is supposed to publish
   was invisible. The host is now in `Index.cshtml`.

A third was found on the very first page load: a bare `<partial name="_Filter" />` throws, because Razor
resolves a short partial name against `/Views/{Controller}/` — `/Views/OrganizationFieldDefinitions/` — while
this view set lives under `/Views/Organization/FieldDefinitions/`. Absolute paths are used, with the same
contract-marker comment the Task precedent carries so the static verifier still reads the contract.

### 8.4 Allowlist corrections

§4 was short by four files, all of them forced by contracts outside this pack. Reported, not slipped in:

| File | Why it was unavoidable |
|---|---|
| `…/wwwroot/assets/js/Organization/FieldDefinitions/index.l10n.js` | `verify_datatable_page.py` requires it and **stops the run** without it; §4 lists two scripts, the DataTable contract needs three. The Task precedent has one too. |
| `services/Diten.Platform/…/Organization/SelfRegistration/OrganizationManifestProvider.cs` | this **is** the "tenant navigation registration" §4 asks for — four pages, the list one nav-visible at SortOrder 15. |
| `services/Diten.Platform/tests/…/Organization/OrganizationManifestProviderTests.cs` | that manifest is guarded in BOTH directions by a hard-coded route list and a nav-visible page count; adding a page without it is a red build. |
| `frontend/Diten.Web/Resources/SharedResource.{7}.resx` | `NavManifestL10nGuardTests` requires `Nav.Page.ORGANIZATIONFIELDDEFINITIONS` in all seven languages; without it the sidebar prints raw English. Part of the same nav registration. |

Also added: `services/Diten.Platform/tests/…/Organization/OrganizationFieldDefinitionL10nContractTests.cs` —
§8 asks for the resx parity test and this is where its sibling for the unit screen already lives.

### 8.5 Decisions taken where the pack and the backend disagreed

- **`IsActive` is NOT a switch.** §3 lists it as one, but FU02's update request has no `IsActive` member and
  there is no re-activate route: a switch would be a control the server ignores while the save reports success
  — the exact failure FU03 shipped. It renders as a status badge plus an explicit **Deactivate** action, with
  `NoReactivateHelp` saying the step cannot be undone here. Field count is unchanged, so `compact` stands.
- **The bulk action deactivates, it does not delete.** The DataTable contract mandates a bulk surface; FU02 has
  no delete for a definition. A "delete" button would have been the only thing about it that ever deleted.
- **`Details` is gated by `read`, its buttons by `manage`.** Hiding the page from a reader would hide the field
  list from someone allowed to see it.

### 8.6 Environment note, and one correction to an earlier report

The four FU02 permission keys **are** delegable after `04e212de` — but only once the AuthService that carries
the fix is the one running. Measured mid-run: with the pre-fix AuthService still up, two of the four keys still
read `Module = platform` and granting `…custom-fields.read` to a tenant role was refused **403**. After
rebuilding and restarting AuthService from this worktree, all four read `Module = organization` and the same
grant returned **204**. The fix is complete; a stale process made it look otherwise, and this was nearly filed
as a second defect.

⚠ Unrelated app-wide gap, seen while checking §5: a refused page redirects to `/Account/AccessDenied`, which
does not exist and answers 404. The refusal is real; what the user is shown is a "page not found" rather than
"not allowed". Every `Forbid()` in the application behaves this way, so it is not this pack's to fix.

### 8.7 Dev-tenant residue from the live check

Created in the local `DefaultTenant` to make §7 measurable, and left in place so the next reader can repeat it:
`fu04.tier` (SingleSelect, two choices) plus **46 deactivated** `fu04.fill.NNN` definitions from the 50-limit
proof. FU02 offers no delete for a definition, so deactivation is the only cleanup available. Nothing in the
repository creates any of them.

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
- [x] Seven-language wording approved (2026-09-07), including the eight data-type labels — §5.1.
- [ ] Native review of Chinese and Arabic strings before release (§5.1).
- [x] Navigation placement: **Organization group, directly after "Organization Units"** — the definitions govern that screen's fields, so it belongs beside it rather than in a settings area where nobody would look for it.
- [ ] Separate implementation and production authority; this pack grants neither.
