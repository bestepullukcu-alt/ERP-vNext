# MOD-0149 — Platform Catalog / Navigation Descriptor / MVC Permission Hardening

**Date:** 2026-07-17 · **Verdict:** **PASS** — per-action MVC permission guards added to `AccountsController`, the CRM catalog item reconciled to code-owned (SelfRegistered), and the `/CRM/Accounts` page descriptor created and verified. No backend Account business logic, no new CRM feature, no CrmService manifest.

## Scope

Hardening of the already-live MOD-0149 Account UI on the platform-catalog / navigation-descriptor / MVC-permission side. No Account business logic; no Contact/Territory/Visit/Lead/Opportunity/Campaign; no CRM local seed / hardcoded lookup fallback; no Zone/MicroZone/Territory/SalesRep field.

## Findings

| Finding | Evidence | Severity | Decision |
|---|---|---|---|
| `AccountsController` actions had only `[Authorize]` — no per-action permission guard (direct-URL access relied on menu gating + backend) | pre-change controller | High | **Fixed** — inline `PermissionClaims.HasPermission` per action (repo standard) |
| Frontend has no `[HasPermission]` attribute; the standard is an inline `PermissionClaims.HasPermission(User, …)` check (16 controllers) | `Security/PermissionClaims.cs`, HCM/ReferenceData/Workflow controllers | Info | Use the inline standard, not a fake attribute |
| Page-deny convention: bare `StatusCode(403)` → `UseStatusCodePagesWithReExecute("/Home/Status/{0}")`; a `ForbidResult` would 302 to the cookie scheme's (unmapped) AccessDenied path | `Filters/ShellAccessFilter.cs:67-69`, `Program.cs:298` | Info | Pages → 403; JSON → 403+message (HCM standard) |
| CRM catalog item existed as **Origin=Manual**, `IsTenantAssignable=true`, 0 page descriptors; ModuleCode `CRM` (permission module is `crm.account`) | live `module-catalog?search=crm` | Medium | Reconcile via Platform manifest provider; **do not** force a manifest into CrmService |
| No CRM `IModuleManifestProvider` registered (Workflow/Organization/DocumentManagement/ReferenceData/AccessGovernance/TenantSettings only) | `Application/DependencyInjection.cs:205-212` | Medium | Add `CrmManifestProvider` (Platform-side, like Organization/Workflow) |
| Active MOD-0285 DynamicModuleMenu renders every entitled nav-visible page and is unaware of the static menu → a nav-visible CRM page would double-render | `DynamicModuleMenuViewComponent.cs` | Medium | Register the page `IsNavigationVisible=false`; MOD-0285 nav migration = follow-up |
| Catalog `Service=DITENPPMSERVICE` (legacy/wrong) | live catalog | Low | SOFT/operator-owned; not overwritten on re-push — follow-up |

## Remediation

### MVC permission hardening
`AccountsController` now guards every action with the repo-standard inline check:

| Route/Action | Required permission | Deny |
|---|---|---|
| GET `/CRM/Accounts` (Index) | `crm.account.read` | 403 → `/Home/Status/403` |
| GET/POST `/CRM/Accounts/Create` | `crm.account.create` | 403 |
| GET/POST `/CRM/Accounts/Edit/{id}` | `crm.account.update` | 403 |
| GET `/CRM/Accounts/Details/{id}` | `crm.account.read` (backend `/overview` also enforces `crm.account.overview.read`) | 403 |
| GET `/CRM/Accounts/get/{id}` (JSON) | `crm.account.read` | 403 + message |
| GET `/CRM/Accounts/lookups` (JSON) | `crm.account.read` | 403 + message |

Delete / attribute / hierarchy have **no MVC action** (the browser calls the Gateway directly for delete; attribute/hierarchy have no frontend surface) — those stay enforced by CrmService `[HasPermission("crm.account.delete|attribute.manage|hierarchy.manage")]`. This is defence-in-depth: menu UX gate → MVC per-action guard → CrmService authoritative `[HasPermission]`.

### Catalog / tenant assignability
`CRM` catalog item was already `IsTenantAssignable=true` (the earlier "false / 0 pages" finding is resolved). Added `Features/Crm/SelfRegistration/CrmManifestProvider.cs` (registered in `Application/DependencyInjection.cs`). At Platform startup `PlatformModuleSelfRegistrationWorker` reconciled it: **Origin Manual→SelfRegistered**, HARD identity refreshed, SOFT operator fields (Domain/Service/DisplayName/SortOrder/IsTenantAssignable) preserved.

### Page / navigation descriptor
One page descriptor created — `PageCode=ACCOUNTS`, route `/CRM/Accounts`, `RequiredPermission=crm.account.read`, `IsNavigationVisible=false`, `PageType=List`, Active. Its permission was synced to the AuthService catalog. Nav-visible is deliberately false so it does not double-render under the MOD-0285 DynamicModuleMenu while the static tenant-shell `<li>` exists.

## Verification

| Object | Expected | Observed | Result |
|---|---|---|---|
| Self-registration | reconcile CRM | log: `Self-registered module "CRM" ("updated"). Pages=1 Actions=0 PermissionsSynced=1` | ✅ |
| Catalog Origin | SelfRegistered | `origin:"SelfRegistered"` | ✅ |
| Catalog IsTenantAssignable | true (preserved) | `isTenantAssignable:true` | ✅ |
| Page descriptor | /CRM/Accounts + crm.account.read | `by-module/CRM` → ACCOUNTS, `crm.account.read`, `isNavigationVisible:false`, Active | ✅ |
| Dynamic nav (no double-menu) | CRM absent | `/api/platform/navigation/menu` contains no CRM | ✅ |
| Static menu | renders for admin | `href="/CRM/Accounts"` present | ✅ |

| Check (live) | Evidence | Result |
|---|---|---|
| Authorized 97c5 admin (`crm.account.*`) | Index/Create/lookups = 200 (happy path intact) | ✅ |
| Unauthenticated | `/CRM/Accounts` → 302 `/account/login` | ✅ |
| Non-tenant / permission-less actor | Index + lookups = 403 | ✅ |
| Per-permission discrimination (tenant_user with read-but-not-create → Create 403) | code-verified (`PermissionClaims` standard); a limited tenant user was not provisioned to exercise it live | ⚠️ code-verified |

## Validation Commands

| Command | Result |
|---|---|
| build `Diten.Web` | ✅ 0 errors |
| build `Diten.Platform.API` | ✅ 0 errors (compiled to scratchpad; running instance held bin) |
| build `Diten.CrmService.Api` | ✅ 0 errors |
| test `Diten.CrmService.Application.Tests` | ✅ 19/19 |
| build `Diten.ApiGateway` | ✅ 0 errors (unchanged) |

## Guards

| Item | Found? | Status |
|---|---|---|
| Action with only `[Authorize]` | No — all 8 guarded | ✅ |
| `crm.account.360.read` | No (only "never" comment) | ✅ |
| direct `:5061` in frontend | No (only doc comment) | ✅ |
| ZoneId/MicroZoneId in Accounts UI | No | ✅ |
| fake module/page descriptor | No — real reconcile, real descriptor | ✅ |
| CrmService manifest forced | No — provider lives in Platform | ✅ |

## Open Items / Follow-ups

| Item | Severity | Owner | Blocks Release? |
|---|---|---|---|
| MOD-0285 nav migration: flip `/CRM/Accounts` descriptor to `IsNavigationVisible=true` + remove static `<li>` (single data-driven nav) | Medium | frontend/platform | No |
| Catalog `Service=DITENPPMSERVICE` legacy value (SOFT/operator-owned; manifest can't overwrite on re-push) | Low | operator | No |
| Module-code `CRM` vs permission-module `crm.account` naming reconciliation | Low | platform gov | No |
| Web + Platform currently standalone `dotnet run` (fleet watch could not hot-swap the locked bin); re-run `watch-diten-bg.ps1` to restore fleet-managed hot-reload | Low | user/ops | No |
| Per-permission-discrimination live test needs a limited tenant user (read-but-not-create) | Low | QA | No |

## Verdict: PASS

MVC per-action permission guards, catalog tenant-assignability + code-owned reconciliation, and the `/CRM/Accounts` page descriptor are correct and verified live. Credentials used as runtime input only, masked, never persisted.
