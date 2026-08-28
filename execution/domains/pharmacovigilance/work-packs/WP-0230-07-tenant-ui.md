---
id: WP-0230-07
title: Tenant UI - Golden Reference Compact
module: MOD-0230
service: Diten.PvgService
depends_on: [WP-0230-06]
gate: build/test only
status: ready
estimate: 2 d
---

# WP-0230-07 - Tenant UI (Golden Reference Compact)

## Objective

Build the MOD-0230 tenant surface as a Golden Reference **Compact** module: Index with DataTable, separate
Create / Edit / Details pages, filter and form partials, localisation, and a same-origin MVC proxy controller.

Compact - not Slim - because `form_field_count: 16` (> 8) and regulated intake needs reviewable
Create/Edit/Details surfaces rather than an Index-hosted offcanvas.

## Preconditions

- [ ] WP-06 gateway route live and smoke-tested.
- [ ] A tenant user with `pvg.case-intake-triage.*` permissions exists for manual verification.

## File manifest

```text
frontend/Diten.Web/Controllers/
└── CaseIntakeTriageController.cs                       route: /Pharmacovigilance/CaseIntakeTriage

frontend/Diten.Web/Models/CaseIntakeTriage/
└── CaseIntakeTriageViewModels.cs                       list, edit, detail view models

frontend/Diten.Web/Views/Pharmacovigilance/CaseIntakeTriage/
├── Index.cshtml
├── Create.cshtml
├── Edit.cshtml
├── Details.cshtml
├── _Form.cshtml
├── _Filter.cshtml
├── _DataTable.cshtml
├── _IndexL10n.cshtml
└── CaseIntakeTriageIndex.cs                            resource marker class

frontend/Diten.Web/wwwroot/assets/js/Pharmacovigilance/CaseIntakeTriage/
├── index.js
├── index.l10n.js
└── form.js

frontend/Diten.Web/Resources/Views/Pharmacovigilance/CaseIntakeTriage/
└── CaseIntakeTriageIndex.{en,tr,ar,es,fr,ru,zh}.resx   all seven, matching the Golden Compact set
```

Reference implementation to copy structurally:
`frontend/Diten.Web/Views/DevEnablement/GoldenReferenceCompact/**` and
`frontend/Diten.Web/Controllers/GoldenReferenceCompactController.cs`.

## Implementation spec

### Layout and routing

- Every `.cshtml` sets `Layout = "_LayoutTenantShell";` explicitly. This is a tenant operational surface, not platform admin.
- MVC route `/Pharmacovigilance/CaseIntakeTriage`; views returned by explicit path, as the Golden Compact controller does.
- **Compact must not include** `_CreateEditOffcanvas.cshtml` or `_DetailsQuickView.cshtml` - those are Slim-only.

### API profile - same-origin MVC proxy

Browser JavaScript calls **the MVC controller**, which calls the Gateway server-side with the bearer token.

```text
browser ──► /Pharmacovigilance/CaseIntakeTriage/...  (same origin, MVC)
                     │
                     └──► http://localhost:5000/api/pv-case-intake-triage   (Gateway)
                                    └──► http://localhost:5011/api/v1/...   (PvgService)
```

Browser JS must **never** call the Gateway directly and must **never** call port 5011. Read `GatewayUrl` from
configuration exactly as `GoldenReferenceCompactController` does.

### PHI in the UI - the part that differs from the Golden Reference

The Golden Reference has no sensitive fields. MOD-0230 has five. The list and detail surfaces must render what
the API returns and nothing more.

| Field | Index / DataTable | Details | Create / Edit |
|---|---|---|---|
| `PatientSubjectCode` | **column absent** | masked | input present |
| `EventOnsetDate` | **column absent** | masked | input present |
| `AdverseEventNarrative` | **column absent** | masked | textarea present |
| `TriageReason` | **column absent** | masked | textarea present |
| `ReporterContactSummary` | **column absent** | masked | input present |
| `SourceReference`, `SuspectProductText`, `RouteTargetQueue` | masked | shown | input present |
| `CaseNumber`, `IntakeChannel`, `SourceType`, `ReporterType`, `Seriousness`, `IntakePriority`, `ReceivedAtUtc`, `TriageOutcome`, `LifecycleState` | shown | shown | per form rules |

The PHI columns are **absent from the DataTable definition**, not hidden with CSS or `visible: false`. A hidden
column still ships the value to the browser and into the DOM, and DataTables export/print would surface it.

Client-side rendering must never reconstruct a masked value, and no PHI may appear in a tooltip, `title`
attribute, `data-*` attribute, print view, or client-side export.

### DataTable

Follow `.antigravity/rules/frontend-datatable-template.md` and the Golden Compact `index.js`. Server-side
paging, sorted by `ReceivedAtUtc` desc. Row actions: **View**, **Edit**, **Triage**, **Route**.

**No Delete row action. No bulk-select. No bulk-delete toolbar. No Export toolbar button.** The Golden
Reference template ships all of these - remove them rather than hiding them.

**Route action**: wire the button and its permission gate, then surface the API's denial as a normal error
toast. In slice 1 it will always deny, because the MOD-0023 queue registry does not exist. That is correct
behaviour, not a bug to work around. Do not add a client-side queue list.

### Localisation

All user-visible strings via `IStringLocalizer` and the `.resx` set - seven languages, matching the Golden
Compact file set. No hardcoded English in `.cshtml` or `.js`. `index.l10n.js` carries DataTable strings, per
`.antigravity/rules/dynamic-localization-standard.md`.

### Permission-gated rendering

Toolbar and row actions render only when the user holds the matching key: `.create`, `.update`, `.triage`,
`.route`. Server-side authorization stays authoritative - hiding a button is UX, not security.

## Forbidden

- Editing `frontend/Diten.Web/Views/Shared/_Layout.cshtml` (**FROZEN**) or anything under `Controllers/Archive/**` or `Views/Archive/**`.
- Any menu entry or navigation registration - that is WP-08 and is currently blocked.
- Browser JS calling the Gateway or port 5011 directly.
- A hidden-but-present PHI column, or PHI in `title` / `data-*` / tooltip / print / client export.
- Delete, bulk-delete, archive, or export UI of any kind.
- `_CreateEditOffcanvas.cshtml` or `_DetailsQuickView.cshtml`.
- Hardcoded user-visible strings.
- A client-side route-target queue list.

## Acceptance criteria

- [ ] All 12 Compact files plus 7 `.resx` created; no Slim-only files.
- [ ] Every view sets `Layout = "_LayoutTenantShell";`.
- [ ] All five PHI/PII fields absent from the DataTable **definition**.
- [ ] No browser request goes anywhere but the same origin (verified in devtools Network).
- [ ] Row actions: View, Edit, Triage, Route only.
- [ ] Route action renders and surfaces the denial cleanly.
- [ ] All strings localised in all seven languages.
- [ ] `frontend/Diten.Web` builds; DataTable verifier passes.
- [ ] `_Layout.cshtml` unmodified.

## Tests

```bash
dotnet build frontend/Diten.Web/Diten.Web.csproj -v q
python3 .antigravity/scripts/verify_datatable_page.py . --module CaseIntakeTriage
python3 .antigravity/scripts/verify_all.py .
```

Manual, with devtools open:

1. Index renders; no request to `:5000` or `:5011` from the browser.
2. Create → record appears with a server-generated `CaseNumber`.
3. Details → PHI fields render masked, never raw.
4. Triage → succeeds, state moves to `Triaged`.
5. Route → denies with a clean error toast.
6. No Delete or Export control anywhere in the UI.
7. Switch language → all labels translate.
8. `Ctrl-U` / DOM inspect on Index → no PHI value present in the page source.

## Agent prompt

> Implement WP-0230-07 in `/Users/natig/Projects/ERP-vNext-recovery`.
>
> Read first: `execution/domains/pharmacovigilance/work-packs/WP-0230-07-tenant-ui.md`, the
> **Layout & Shell Contract**, **Frontend File Contract**, and **Entity Fields** sections of
> `execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md`,
> `.antigravity/rules/{frontend-standards,frontend-datatable-template,frontend-form-template,frontend-details-template,views-organization,frontend-js-standard,localization-standard,dynamic-localization-standard}.md`.
>
> Copy the structure from `frontend/Diten.Web/Views/DevEnablement/GoldenReferenceCompact/**` and
> `Controllers/GoldenReferenceCompactController.cs` - but strip the Delete row action, bulk-select,
> bulk-delete toolbar, and Export button. Do not hide them; remove them.
>
> The five PHI/PII fields must be **absent from the DataTable column definition**, not hidden. A hidden column
> still ships the value into the DOM.
>
> Browser JS calls only the same-origin MVC controller. Never the Gateway, never port 5011.
>
> The Route action will always deny in slice 1 because MOD-0023 does not exist. Surface the denial as an error
> toast. Do not add a client-side queue list to make it work.
>
> Do not touch `_Layout.cshtml` (frozen), `Archive/**`, or add any menu entry.
>
> Report the build output, the DataTable verifier result, and confirm from devtools that no cross-origin
> request is made.
