# MOD-0150-FU02 — Contact Frontend Compact Vertical

**Date:** 2026-07-17 · **Verdict:** **PASS** — Contact Golden Reference Compact vertical (list/create/edit/details/360) implemented and proven live; compact verifier 94/0, RESX 7-lang parity, browser golden flow + failure paths PASS. No AccountContactLink/AccountRelationship/Consent/Zone/import-export UI.

## Preflight

- MOD-0150 status: FU01 backend done (ready-for-dev), FU02 scope = Contact frontend compact vertical.
- FU01 dependency: PASS (CrmService tests 32/32, Gateway `/api/crm/contacts`, `crm.contact.*` granted to 97c5 Admin).
- Reference: **contact-type 200/9, contact-status 200/4** published (FU02 required). contact-role (FU03) / account-relationship-* (FU04) not used here.
- Scope: Contact frontend only. No link/relationship/consent/import-export/audit-wiring/Zone.

## Implementation summary

- **Controller** `Controllers/CRM/ContactsController.cs` — mirrors AccountsController: Gateway-only (5000, never 5061), cookie→Bearer + X-Tenant-Id propagation, `GatewayResponse<T>` → ModelState/TempData, published-values proxy for `contact-type`/`contact-status` (uses `Value`/`Text` = code/label fallback). Per-action MVC guards: Index/Details/GetById/Lookups=`crm.contact.read`, Create=`crm.contact.create`, Edit=`crm.contact.update`; page 403→/Home/Status/403, JSON 403. No `crm.contact.360.read`.
- **ViewModels** `Models/CRM/ContactViewModels.cs` — ContactEditViewModel/ContactDetailViewModel/ContactOverviewViewModel/ContactSavePayload/ContactExternalReference (reuses shared `ReferenceOptionViewModel`/`GatewayResponse<T>`/`PublishedValuesModel`). No AccountContactLink/AccountRelationship/Zone/Consent fields.
- **Views** `Views/CRM/Contacts/` — Index/Create/Edit/Details/_Form/_Filter/_DataTable/_IndexL10n + ContactIndex.cs marker. `_LayoutTenantShell` layout, DataTable v2 (`data-dt-standard="v2"`), skeleton loader, inline filter (`px-3`, `stateSave:false`), L10n bridge (payload partial + `index.l10n.js`, camelCase→PascalCase). Compact _Form↔Details section parity (Identity/Notes/Status/Professional/Contact/Integration). Details 360 = profile + external refs + **Linked Accounts** (read-only placeholder → FU03) + **Consent/Preferences** (read-only seam → MOD-0164/FU05).
- **JavaScript** `wwwroot/assets/js/CRM/Contacts/` — index.js (DataTable v2 driver; `window.API?.crm ?? window.ApiBaseUrl`; paged `data.items` unwrap; status + contact-type filters; Save View/Apply/Reset; bulk), index.l10n.js, form.js (Select2). No direct 5061.
- **Localization** `Resources/Views/CRM/Contacts/ContactIndex.{7 langs}.resx` — 43 keys × 7, parity PASS. `ContactsMenu` added to the 7 SharedResource files.
- **Menu** `_LayoutTenantShell.cshtml` — Contacts `<li>` under Commercial Suite, gated by `@if (Perms.Has("crm.contact.read"))`, `bx-user-pin`.
- **Catalog/descriptor** — `CrmManifestProvider` extended with a `CONTACTS` page (`/CRM/Contacts`, `crm.contact.read`, `IsNavigationVisible=false`). No CrmService manifest.
- **Backend fix (FU01 correctness):** `CreateContactCommand`/`UpdateContactCommand` `LastName` → nullable (Contact allows FirstName-only, per the "FirstName OR LastName" rule); handlers null-safe. Without it, omitting LastName tripped ASP.NET model-required 400 before the friendly reference validation.

## Build-green sequence

| Step | Command/Evidence | Result |
|---|---|---|
| 1 Controller + ViewModels | write | ✅ |
| 2 build Diten.Web | 0 errors (after marker + GatewayResponse reuse) | ✅ |
| 3 Views | write | ✅ |
| 4 build | 0 errors | ✅ |
| 5 JS | write | ✅ |
| 6 compact verifier | **94 / 0 PASS** (fixed literal `<section>` in a comment) | ✅ |
| 7 RESX 7 lang | 43 keys each | ✅ |
| 8 RESX parity | 7 langs PASS | ✅ |
| 9 Menu + descriptor | li + CrmManifestProvider CONTACTS page | ✅ |
| 10 Platform/Web build | 0 errors | ✅ |
| 11 Browser golden flow | all pass | ✅ |

## Golden flow proof (97c5 CRM Admin, live cookie session)

| Step | Evidence | Result |
|---|---|---|
| `/CRM/Contacts` renders | 200; menu `<li>` + `dt-contacts` table | ✅ |
| List via Gateway | 200 (cookie→Bearer) | ✅ |
| Create dropdowns | contact-type 10 opts / status 5 opts (MOD-0048) | ✅ |
| Create, DisplayName blank | 302 → **DisplayName "Mehmet Demir"** (auto) | ✅ |
| FirstName-only create | 302 → DisplayName "Solo" (derived from first name alone) | ✅ |
| Details/360 | 200 (profile + Linked Accounts + Consent placeholders) | ✅ |
| Menu guard | `crm.contact.read` `<li>` | ✅ |

## Failure path proof

| Failure Path | Expected | Observed | Status |
|---|---|---|---|
| Invalid ContactType | 400 friendly | re-render + "not a valid published value of reference set 'contact-type'." | ✅ |
| Unauthenticated | 302 login | `302 /account/login` | ✅ |
| Non-tenant / permission-less actor | 403 | `403` | ✅ |
| Delete + reload | hidden | DELETE 200 → Details 302 → Index | ✅ |

## Catalog / descriptor / permission proof

| Object/Route | Expected | Observed | Status |
|---|---|---|---|
| CONTACTS page descriptor | /CRM/Contacts | `routePath:/CRM/Contacts` | ✅ |
| Descriptor permission | crm.contact.read | `crm.contact.read` | ✅ |
| IsNavigationVisible | false (no double-menu) | `false`, Active | ✅ |
| MVC guards | per-action | 10 guard calls; all actions covered | ✅ |
| Authorized 97c5 admin | 200 | 200 | ✅ |

## Validation commands

| Command | Result |
|---|---|
| build Diten.Web | ✅ 0 errors |
| build CrmService.Api | ✅ 0 errors |
| test CrmService.Application.Tests | ✅ **32/32** |
| build ApiGateway | ✅ 0 errors |
| build Diten.Platform.API | ✅ 0 errors |
| compact verifier | ✅ 94/0 |
| RESX parity (7 lang) | ✅ 43/43 each |
| published-values contact-type/status | ✅ 9 / 4 |
| health (Auth/Platform/Gateway/CRM/Web) | ✅ up |

## Boundary / SoR

| Object/Capability | Owner | Touched? | Risk |
|---|---|---|---|
| Contact UI | MOD-0150 FU02 | ✅ | none |
| AccountContactLink UI | FU03 | No (read-only placeholder only) | none |
| AccountRelationship UI | FU04 | No | none |
| Consent capture | MOD-0164 | No (read-only seam placeholder) | none |
| Account 360 Related Contacts/Accounts | MOD-0149 | No | none |
| Zone/MicroZone/Territory/SalesRep | MOD-0151 | No | none |
| Reference values | MOD-0048 | consumed via Gateway; no local seed | none |
| Direct 5061 browser call | — | No | none |

## Out-of-scope guard

| Forbidden Item | Found? | Status |
|---|---|---|
| AccountContactLink / AccountRelationship impl in Contacts UI | No (comments only) | ✅ |
| Consent capture impl | No | ✅ |
| ZoneId/MicroZoneId/TerritoryId/SalesRepId | No | ✅ |
| crm.contact.360.read | No | ✅ |
| direct 5061 in frontend | No | ✅ |
| _CreateEditOffcanvas / _DetailsQuickView | No | ✅ |
| hardcoded contact-type/status fallback | No | ✅ |
| CRM local reference seed | No | ✅ |

## Open items / blockers

| Item | Severity | Owner | Blocks FU03? | Notes |
|---|---|---|---|---|
| Fleet standalone `dotnet run` (fragile; services dropped mid-task, restarted) | Low | ops | No | re-run `watch-diten-bg.ps1` to restore watch |
| MOD-0285 nav migration (nav-visible=true + remove static `<li>`) | Low | frontend/platform | No | double-menu already prevented |
| import/export UI | Low | FU06 | No | out of scope |

## Final verdict: PASS

Contact Frontend Compact Vertical implemented; build 0/0, compact verifier 94/0, RESX 7-lang parity, browser golden flow (create → auto DisplayName → details/360 → invalid 400 friendly → delete → 404) proven live; catalog descriptor + MVC permission guards verified; boundary clean. Next: **MOD-0150-FU03 Account Contact Links**.
