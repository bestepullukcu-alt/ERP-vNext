# MOD-0149 — Frontend Compact Vertical + Browser Golden Flow

**Date:** 2026-07-17 · **Verdict:** **PASS** — Golden Reference Compact frontend vertical delivered, tenant-shell menu added, and the browser golden flow proven end-to-end through the Gateway on the live-built binary.

## Scope

MOD-0149 Account Foundation **frontend only**. Backend/Gateway/reference chain was already live-proven (see [correct-tenant-create-smoke](./mod-0149-correct-tenant-create-smoke.md)). No backend Account business logic was extended; no Contact/Consent/Territory/Zone/MicroZone/SalesRep; no CRM local seed; no hardcoded reference fallback; no direct call to 5061; no offcanvas/quickview.

## API profile decision

No frontend JS in the repo sends an `Authorization` header (`diten-datatable.getAuthHeaders` adds only `X-Tenant-Id`). The Gateway bridges the `access_token` cookie to Bearer (`Program.cs` cookie→Bearer promotion), and cookies are port-agnostic, so the browser DataTable can call the Gateway directly with `credentials:'include'` — the established **direct-gateway** profile. Reference dropdowns and page data that require the tenant claim go through the same-origin **MVC proxy** (`AccountsController`), which forwards Bearer + `X-Tenant-Id` server-side.

## Implementation

| Layer | Files |
|---|---|
| Controller | `Controllers/CRM/AccountsController.cs` — Gateway-only proxy; `AuthTokenCookies.GetAccessToken` Bearer + `X-Tenant-Id` from `tenant_id` claim; `GatewayResponse<T>`→ModelState/TempData; `LoadReferenceOptionsAsync` reads MOD-0048 `published-values?scope_key=<tenant>` (empty→controlled dependency message, never a local fallback) |
| ViewModels | `Models/CRM/AccountViewModels.cs` — 17 user fields; **no** ZoneId/MicroZoneId/TerritoryId/SalesRepId; per-module `GatewayResponse<T>`; `PublishedValueItemModel` binds both `code`/`label` and `valueCode`/`displayName` |
| Views | `Views/CRM/Accounts/{Index,Create,Edit,Details,_Form,_Filter,_DataTable,_IndexL10n}.cshtml` + `AccountIndex.cs` — compact full-page Create/Edit + Details/360; `data-dt-standard="v2"`; skeleton; inline filter `px-3`; `_Form`↔`Details` section parity |
| JavaScript | `wwwroot/assets/js/CRM/Accounts/{index.js,index.l10n.js,form.js}` — DataTable v2, `window.API?.crm ?? window.ApiBaseUrl`, paged `dataSrc`, Save View/Apply/Reset/bulk contracts; no direct 5061 |
| Localization | `Resources/Views/CRM/Accounts/AccountIndex.{en,tr,fr,es,zh,ar,ru}.resx` — 49 keys each, 7-language parity; menu keys `CommercialSuite`/`AccountsMenu` added to 7 `SharedResource.*.resx` |
| Menu | `Views/Shared/_LayoutTenantShell.cshtml` — Commercial Suite section + Accounts `<li>` gated by `@if (Perms.Has("crm.account.read"))` |
| Registry | `execution/registries/module-implementation-status.md` — MOD-0149 → Backend+Frontend 90% |

## Build-green sequence

| Step | Evidence | Result |
|---|---|---|
| Controller + ViewModels → build | Diten.Web build (scratchpad output to avoid running-app lock) | ✅ 0 errors |
| Views → build | Diten.Web build | ✅ 0 errors |
| JS → compact verifier | `verify_datatable_page.py --area CRM --module Accounts --reference compact` | ✅ **94 passed / 0 failed** |
| 7 resx → parity | key-set diff across 7 languages | ✅ 49/49 each |
| Menu → build | Diten.Web build after `_LayoutTenantShell` + shared resx | ✅ 0 errors |

A verifier section-parity failure was root-caused to a Razor comment literally containing `<section>`, which the verifier's regex mis-parsed as an opening tag; rephrasing the comment fixed it (94/0).

## Browser golden flow (tenant-97c5 CRM Admin, live cookie session)

| Step | Evidence | Result |
|---|---|---|
| Login `/account/login` (tenant 97c5) | 200, `tenant_id=97c5…`, user `c5769c62…`, role Admin, cookie set | ✅ |
| `/CRM/Accounts` loads | 200 (65 KB) | ✅ |
| `/CRM/Accounts/lookups` (proxy→MOD-0048) | **9 account-type + 5 account-status** from published-values | ✅ |
| List via Gateway (`/api/crm/accounts`) | 200 (cookie→Bearer bridged) | ✅ |
| Create, AccountCode empty | 302→Index; list shows **`ACC-2026-000002`** (auto-gen) | ✅ |
| Details/360 | 200 (72 KB); AccountCode + address + `MOD-0151` coverage placeholder (read-only) | ✅ |
| Persist after reload | list total=1, Details renders | ✅ |

## Failure paths

| Failure Path | Expected | Observed | Status |
|---|---|---|---|
| Duplicate AccountCode | 409 user-friendly | Create re-rendered with "already exists for this tenant." in validation-summary | ✅ |
| Invalid AccountType | 400 user-friendly | "not a valid published value of reference set 'account-type'." | ✅ |
| Unknown / cross-tenant id | 404 | Details → 302 redirect to Index (TempData error) | ✅ |
| Soft-deleted reload | hidden | DELETE 200 → Details 302 (hidden) → list total=0 | ✅ |
| Unauthorized | backend enforces | routes 302→login unauthenticated; CrmService `[Authorize]`+`[HasPermission]` authoritative | ✅ |

## Bug fixed mid-flow

Initial `lookups` returned empty. Root cause: the MOD-0048 published-values items expose `code`/`label` (+`isActive`/`sortOrder`), but the consumer model bound `valueCode`/`displayName` → all values filtered out. Fixed by binding both shapes (`Value`/`Text` resolve `code`||`valueCode`, `label`||`displayName`). Re-verified live: 9/5 populated.

## Guards

| Guard | Result |
|---|---|
| ZoneId / MicroZoneId / TerritoryId / SalesRepId in Accounts frontend | ✅ only in the "deliberately NOT present" doc comment |
| `crm.account.360.read` | ✅ none |
| direct `:5061` in frontend JS/views | ✅ only in the "never called directly" doc comment |
| `_CreateEditOffcanvas` / `_DetailsQuickView` | ✅ none |
| hardcoded account-type/status option lists | ✅ none |

## Environment note

`dotnet watch` could not hot-swap the running Web binary (the running process locked `bin/Diten.Web.dll`, the known MSB3021 lock), so the app was restarted from a fresh build to prove the flow. The Web app on :5001 is currently a standalone `dotnet run` (not under the `watch-diten-bg.ps1` watch job). Re-run `watch-diten-bg.ps1` to restore fleet-managed hot-reload.

## Verdict: PASS

Frontend compact vertical + tenant-shell menu implemented; all static gates green (build 0/0, compact verifier 94/0, 7-language RESX parity); browser golden flow and every failure path proven live through the Gateway on the freshly-built binary. Credentials used as runtime input only, masked here, never persisted.
