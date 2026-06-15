---
id: CAND-CAP-0003-FU01
name: Platform Admin Profile & Settings
domain: platform-shared-services
service: Diten.Platform
shell: platform-admin
golden_reference: none
entity_base: GlobalEntity
status: ready-for-dev
owner: module-pack-author
branch: feature/pss/pss-009-platform-admin-profile-settings
started: 2026-05-14
target: 2026-05-28
form_field_count: 1
---

# CAND-CAP-0003-FU01 — Platform Admin Profile & Settings

> **Canonicalization (DCP-002):** Governance identity is now **CAND-CAP-0003-FU01**, a child of **CAND-CAP-0003**. Prior repo ID **PSS-009** is a deprecated alias. Any `PSS-009` string in runtime code is a documentation comment only and is left unchanged. Temporary candidate; pending EA. Ref: `execution/portfolio/delivery-capability-packs/DCP-002-module-identity-canonicalization.md`.

## Module Summary
This module creates the Platform/Admin self-service account surface for the currently authenticated platform actor. It fixes the hardcoded header user avatar, links the existing user dropdown to real `My Profile` and `Settings` pages, and allows the signed-in Platform/Admin user to view their own account information and safely update only approved self-service fields.

This module is not a Platform Administrators Management replacement. It does not manage other administrators, does not expose a DataTable, and does not create a sidebar navigation item.

Master-plan traceability:
- Domain: Platform Shared Services.
- Primary service: `Diten.Platform`.
- Baseline v1 editable field: `DisplayName` only.
- Baseline v1 has no lookup dependency.
- Baseline v1 has no AuthService implementation dependency.
- Shell: `platform-admin`.
- Golden reference: `none` because this is not a DataTable CRUD module.

Duplicate pack identity decision:
- This file is the current, narrowed v1 contract for `PSS-009 Platform Admin Profile & Settings`.
- The older `execution/domains/platform-shared-services/module-packs/PSS-009-platform-admin-profile.md` was superseded by this v1 scope and removed from the active execution set. Do not recreate or use that broader avatar-upload/audit-feed scope for PSS-009 v1.
- `execution/domains/platform-shared-services/module-packs/PSS-010-platform-admin-security.md` remains future security scope for MFA, active sessions, security activity, and other advanced security surfaces. PSS-009 v1 does not absorb that scope.

## Ownership and Boundaries
In scope:
- Current Platform/Admin actor self-profile view.
- Current Platform/Admin actor settings page.
- Header user dropdown integration in `_LayoutPlatformAdmin.cshtml`.
- Header avatar fallback from hardcoded image to generated initials.
- `My Profile` route reachable from the user dropdown.
- `Settings` route reachable from the user dropdown.
- `Settings > Account` with `DisplayName` as the only editable field.
- `en` and `tr` localization.
- Browser smoke coverage for the header, dropdown, profile page, settings page, and forbidden UI elements.

Out of scope:
- Sidebar navigation item.
- DataTable, Create, Edit, Details CRUD workflow.
- Editing another Platform Administrator.
- Platform Administrators Management ownership such as invite, role assignment, status change, tenant scope, suspend/reactivate, resend invite, or delete.
- Tenant-side user profile/settings.
- ERP tenant-side profile/settings.
- Avatar upload, blob storage, image validation, image resize/crop, or storage-provider integration.
- Delete account, self-delete, self-disable, self-deactivate, or account lifecycle removal.
- Fake activity timeline, fake audit feed, project/social cards, connections, teams, or project lists.
- Preferred locale and preferred timezone in v1.
- Password change in v1 unless the pack is explicitly revised after a normal AuthService password-change contract is verified.
- MFA enrollment, active sessions, recovery codes, and security activity log.
- Audit Trail implementation or audit event storage.
- Email change flow.
- Username change flow.

Boundary decision:
- This module owns the self-service presentation and self-only Platform account update contract.
- `NEW-002 Platform Administrators Management` remains the system owner for administrator lifecycle, roles, actor type, tenant scope, invitation state, and status.
- `Diten.AuthService` remains the owner of password verification and password mutation. Baseline v1 does not change or consume AuthService password-change behavior. If password change is later included, the pack must first be revised or a dedicated security pack must own it.

## Owned Objects
Backend owned/consumed objects:
- Existing entity consumed: `PlatformAdministrator`.
- Existing base type: `GlobalEntity`.
- No new collection is required for v1.
- No entity schema change is expected for v1 because `DisplayName` already exists on `PlatformAdministrator`.
- Query: `GetPlatformAccountProfileQuery`.
- Command: `UpdatePlatformAccountSettingsCommand`.
- Handler: `GetPlatformAccountProfileHandler`.
- Handler: `UpdatePlatformAccountSettingsHandler`.
- Validator: `UpdatePlatformAccountSettingsValidator`.
- DTO/model file: `PlatformAccountModels.cs`.
- API controller: `PlatformAccountController`.
- API base route: `GET /api/platform/account/me`, `PUT /api/platform/account/me`.

Frontend owned objects:
- Platform layout dropdown consumer: `frontend/Diten.Web/Views/Shared/_LayoutPlatformAdmin.cshtml` limited to avatar rendering and `My Profile` / `Settings` link targets.
- Frontend controller: `frontend/Diten.Web/Controllers/Platform/PlatformAccountController.cs`.
- View folder: `frontend/Diten.Web/Views/Platform/Account/`.
- Views: `Profile.cshtml`, `Settings.cshtml`, optional `_AccountTab.cshtml`, `_AccountL10n.cshtml`, and marker class `AccountIndex.cs`.
- Scripts: `frontend/Diten.Web/wwwroot/assets/js/Platform/Account/profile.js`, `settings.js`, and optional `account.l10n.js`.
- Resources: `frontend/Diten.Web/Resources/Views/Platform/Account/AccountIndex.en.resx` and `AccountIndex.tr.resx`.

Permissions / authorization:
- Self-service pages and APIs require an authenticated `PlatformActor`.
- No endpoint accepts a target administrator ID from the route or request body.
- Mutations resolve the current actor from JWT claims/cookies server-side.

## Entity Fields
This module primarily uses the existing `PlatformAdministrator` aggregate.

| Field | Type | Rule |
|---|---|---|
| Base | `GlobalEntity` | `PlatformAdministrator` is a platform-level actor record, not tenant-owned. |
| Id | `Guid` | Read-only; from current authenticated actor lookup only. |
| Email | `string` | Read-only in this module; never editable here. |
| UserName | `string` | Read-only in this module; never editable here. |
| DisplayName | `string` | Editable; required, trimmed, max 200. |
| ActorType | `ActorType` | Read-only; rendered as Platform Admin / Partner Admin. |
| Status | `AdministratorStatus` | Read-only; lifecycle remains NEW-002 ownership. |
| Roles | `List<AdministratorRole>` | Read-only; role assignment remains NEW-002 ownership. |
| LastLoginAtUtc | `DateTimeOffset?` | Read-only; displayed when present. |
| InvitationStatus | `AdministratorInvitationStatus` | Read-only; displayed when useful. |
| InvitedAtUtc | `DateTimeOffset?` | Read-only; displayed when present. |
| InviteExpiresAtUtc | `DateTimeOffset?` | Read-only; displayed when present and not treated as a secret. |
| CreatedAt | `DateTimeOffset` | Read-only account date. |
| UpdatedAt | `DateTimeOffset?` | Read-only account date. |
| Version | `int` | Required for display-name update concurrency if the existing aggregate uses optimistic concurrency. |

No v1 fields:
- `AvatarUrl` is not added.
- `PreferredLocale` is not added.
- `PreferredTimezone` is not added.
- Phone, address, country, organization, and social/profile URLs are not added.
- Email and username change flows are not added.
- Password-change fields are not part of the Diten.Platform v1 contract.

## Repo Scope
Allowed implementation paths:
- `execution/domains/platform-shared-services/module-packs/CAND-CAP-0003-FU01-platform-admin-profile-settings.md`.
- `services/Diten.Platform/src/Diten.Platform.Application/Features/PlatformAccount/**`.
- `services/Diten.Platform/src/Diten.Platform.API/Controllers/Platform/PlatformAccountController.cs`.
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/**`.
- `services/Diten.Platform/tests/Diten.Platform.API.Tests/**` if an API test project exists.
- `frontend/Diten.Web/Controllers/Platform/PlatformAccountController.cs`.
- `frontend/Diten.Web/Views/Platform/Account/**`.
- `frontend/Diten.Web/wwwroot/assets/js/Platform/Account/**`.
- `frontend/Diten.Web/Resources/Views/Platform/Account/**`.
- `frontend/Diten.Web/Resources/SharedResource.en.resx`.
- `frontend/Diten.Web/Resources/SharedResource.tr.resx`.
- `frontend/Diten.Web/Views/Shared/_LayoutPlatformAdmin.cshtml` only for:
  - replacing the hardcoded avatar image with initials-avatar behavior for Platform shell;
  - linking `My Profile` to `/Platform/Account/Profile`;
  - linking `Settings` to `/Platform/Account/Settings`.
- `gateway/Diten.ApiGateway/**` for route inspection/build only. Direct `ocelot.json` edits remain integration-agent owned.

Repo scope restrictions:
- Do not touch tenant shell profile/settings.
- Do not touch Platform Administrators CRUD views/controllers except as read-only reference.
- Do not add new gateway routes directly. If `/api/platform/account` is not reachable through existing routes, stop and request integration-agent.

## Protected Paths
- `.antigravity/**`.
- `frontend/Diten.Web/Controllers/Archive/**`.
- `frontend/Diten.Web/Views/Archive/**`.
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml`.
- `frontend/Diten.Web/Views/Shared/_LayoutTenantShell.cshtml`.
- `frontend/Diten.Web/Controllers/Tenant*/**` and tenant-side profile/settings surfaces.
- `frontend/Diten.Web/Views/Tenant*/**` and tenant-side profile/settings surfaces.
- `frontend/Diten.Web/Views/Platform/Administrators/**` except reading as reference.
- `frontend/Diten.Web/Controllers/Platform/AdministratorsController.cs` except reading as reference.
- `services/Diten.Platform/src/Diten.Platform.Application/Features/PlatformAdministrators/**` except reading or reusing DTO decisions; do not change lifecycle/roles/status ownership under this pack.
- `services/Diten.AuthService/**` for baseline v1; password change belongs to future security scope unless this pack is revised.
- `services/Diten.MdmService/**`.
- `services/Diten.DevEnablementService/**`.
- `services/Diten.EnterpriseStrategyService/**`.
- `gateway/Diten.ApiGateway/**/ocelot.json` unless integration-agent or explicit user approval handles route edits.
- Storage/blob/avatar upload provider paths.
- Audit Trail module paths.
- MFA/session management backend paths.
- ERP Account, General Reference, Financial Reference, Territory Reference, and tenant business lookup modules.

## Dependencies
- `NEW-002 Platform Administrators Management` for the existing `PlatformAdministrator` source of record.
- Existing JWT/cookie authentication bridge for Platform shell.
- Existing `PlatformActor` authorization policy.
- Existing `Response<T>` envelope and `CustomBaseController`.
- Existing CQRS/MediatR pipeline in `Diten.Platform`.
- Existing `_LayoutPlatformAdmin.cshtml`.
- Sneat reference pages may be used as visual inspiration only:
  - `frontend/_Reference/Theme/full-version/html/vertical-menu-template/pages-profile-user.html`.
  - `frontend/_Reference/Theme/full-version/html/vertical-menu-template/pages-account-settings-account.html`.
- No PSS-011 lookup dependency in baseline v1.
- No AuthService dependency in baseline v1.

Dependency risks:
- If a normal authenticated PlatformActor password-change contract is later required, it must be verified and added through pack revision or PSS-010.
- If gateway routing does not expose required Platform endpoints, implementation must stop and request integration-agent.
- Preferred locale/timezone require PSS-011 lookup consumption and persistence; they remain follow-up items.

## Runtime Constraints
- Frontend browser JavaScript must not call service ports `5056` or `5057` directly.
- Platform/admin browser calls use same-origin MVC proxy or Gateway.
- API responses use `Response<T>`.
- Controller logic remains thin and delegates to MediatR.
- `PlatformAdministrator` remains `GlobalEntity` because platform actors are cross-tenant platform records.
- No request body or route accepts `TenantId`.
- No route accepts another administrator's ID.
- Baseline v1 update accepts `DisplayName` only.
- Email and username are read-only and cannot be changed by this module.
- Role, status, actor type, partner scope, and allowed tenant scope are read-only and remain under NEW-002.
- Preferred locale and preferred timezone are not rendered or accepted in baseline v1.
- Header avatar generation must be deterministic and must not use the same static image for all users.
- Initials source order:
  1. Display name.
  2. First/last name claims if available.
  3. Email local-part fallback.
  4. Single-letter fallback only if no usable identity value exists.
- Avatar upload is not supported in v1.
- Activity timeline is not supported in v1; static template entries are forbidden.
- Delete account and self-delete lifecycle are forbidden.
- Security tab and password-change form are not rendered in baseline v1. They require verified AuthService contract and pack revision, or PSS-010 ownership.

## Layout & Shell Contract
- `shell: platform-admin`.
- Razor pages under `Views/Platform/Account/` must explicitly set:

```cshtml
@{
    Layout = "_LayoutPlatformAdmin";
}
```

- `_ViewStart.cshtml` must not be changed.
- The module is entered from the user dropdown, not the sidebar menu.
- `_LayoutPlatformAdmin.cshtml` may be changed only for the user dropdown/avatar area.
- `_Layout.cshtml` and `_LayoutTenantShell.cshtml` are protected and must not be changed.

Expected routes:
- `/Platform/Account/Profile` -> My Profile.
- `/Platform/Account/Settings` -> Settings.

## Backend File Convention
Because `golden_reference: none`, this is not a Slim/Compact DataTable CRUD module. Still, backend code must follow action-based CQRS naming and folder conventions.

Canonical backend structure:

```text
services/Diten.Platform/src/Diten.Platform.Application/Features/PlatformAccount/
├── Commands/
│   └── UpdatePlatformAccountSettingsCommand.cs
├── Queries/
│   └── GetPlatformAccountProfileQuery.cs
├── Handlers/
│   ├── CommandHandlers/
│   │   └── UpdatePlatformAccountSettingsHandler.cs
│   └── QueryHandlers/
│       └── GetPlatformAccountProfileHandler.cs
├── Validators/
│   └── UpdatePlatformAccountSettingsValidator.cs
└── PlatformAccountModels.cs
```

Naming rules:
- Query record: `GetPlatformAccountProfileQuery`.
- Command record: `UpdatePlatformAccountSettingsCommand`.
- Handler class names: `GetPlatformAccountProfileHandler`, `UpdatePlatformAccountSettingsHandler`.
- Validator class name: `UpdatePlatformAccountSettingsValidator`.
- Handler/validator file names must not use `CommandHandler`, `QueryHandler`, or `CommandValidator` suffixes.

API controller:

```text
services/Diten.Platform/src/Diten.Platform.API/Controllers/Platform/PlatformAccountController.cs
```

Expected API surface:
- `GET /api/platform/account/me` -> current actor profile/settings DTO.
- `PUT /api/platform/account/me` -> current actor safe account update.

Password change:
- Not part of baseline v1.
- Do not add an AuthService command/handler under this pack.
- Do not render a password-change form unless this pack is revised after a real backend contract is verified.

## Frontend File Contract
This is not a DataTable module. Do not create `_DataTable.cshtml`, `_Filter.cshtml`, bulk action bars, Create/Edit/Details CRUD pages, or DataTable verifier expectations.

Canonical frontend structure:

```text
frontend/Diten.Web/Controllers/Platform/
└── PlatformAccountController.cs

frontend/Diten.Web/Views/Platform/Account/
├── Profile.cshtml
├── Settings.cshtml
├── _AccountTab.cshtml
├── _AccountL10n.cshtml
└── AccountIndex.cs

frontend/Diten.Web/wwwroot/assets/js/Platform/Account/
├── profile.js
├── settings.js
└── account.l10n.js

frontend/Diten.Web/Resources/Views/Platform/Account/
├── AccountIndex.en.resx
└── AccountIndex.tr.resx
```

Visual contract:
- Profile page may adapt the Sneat profile header pattern.
- Profile page must remove nav tabs, connections, teams, project cards, and social widgets.
- Settings page may adapt the Sneat account settings page but baseline v1 renders only the Account surface for `DisplayName`.
- Security tab is not rendered in baseline v1. If a later revision includes it, it must be backed by a real AuthService contract and must not contain fake or disabled password forms.
- Delete account card must not exist in DOM.
- Avatar upload block must not exist in DOM for v1.
- Header/user dropdown must render initials avatar when no real photo exists.

## Validation Rules
| Field / Action | Rule |
|---|---|
| Current actor | Must be authenticated and must satisfy `PlatformActor`. |
| Target user | Must be resolved from current claims/session only. Route/body target IDs are forbidden. |
| DisplayName | Required, trimmed, 2-200 characters. |
| Email | Read-only; mutation rejected or ignored server-side. |
| UserName | Read-only; mutation rejected or ignored server-side. |
| Roles | Read-only; mutation rejected or ignored server-side. |
| ActorType | Read-only; mutation rejected or ignored server-side. |
| Status | Read-only; mutation rejected or ignored server-side. |
| PreferredLocale | Not accepted in baseline v1. |
| PreferredTimezone | Not accepted in baseline v1. |
| Version | Required for optimistic concurrency if aggregate update uses inherited versioning. Stale write returns 409. |
| Password fields | Not accepted in baseline v1. |

## Failure Path to Verify
- Anonymous user cannot load `/Platform/Account/Profile` or `/Platform/Account/Settings`.
- `tenant_user` cannot load Platform profile/settings pages.
- API call without PlatformActor returns 401/403 as appropriate.
- Request cannot update another administrator because no target ID is accepted.
- Missing `PlatformAdministrator` record for current actor returns 404 or a controlled account-state error.
- Invalid display name returns 400.
- Stale version returns 409 if versioning is used.
- Email mutation attempt does not change persisted email.
- Username mutation attempt does not change persisted username.
- Role/status/actor-type mutation attempt does not change lifecycle fields.
- Preferred locale/timezone mutation attempt does not change persisted data in baseline v1.
- Password fields are ignored or rejected in baseline v1.
- Security tab/password form is not rendered in baseline v1.
- Header initials render when no avatar URL exists.
- Delete account card is absent.
- Avatar upload control is absent.
- Fake timeline, social tabs, projects, teams, and connections are absent.
- Locale/timezone controls are absent in baseline v1.

## Authorization Convention
Platform API:
- `PlatformAccountController` uses `[Authorize(Policy = "PlatformActor")]`.
- Self-service read/update endpoints do not require an administrator-management permission because they cannot target other users.
- If a permission is introduced later, it must use the `Platform.Account.*` format and must not reuse `Platform.Administrators.*` management permissions.

Frontend MVC:
- `PlatformAccountController` requires authenticated PlatformActor.
- Browser JS calls same-origin `/Platform/Account/api/...` endpoints.
- Server-side proxy reads the HttpOnly access token and forwards through Gateway.

Password change:
- Not part of baseline v1.
- `Diten.AuthService` remains the future authority for password verification and password mutation.
- New AuthService endpoint creation is out of scope.

AllowAnonymous:
- No profile/settings page or API endpoint in this module is `AllowAnonymous`.

## Gateway / API Routing Decision
Expected routes:
- Platform service: `/api/platform/account/me`.
- Frontend same-origin proxy: `/Platform/Account/api/me`.

Gateway decision:
- Do not edit `gateway/Diten.ApiGateway/**/ocelot.json` in this module.
- During implementation, verify that Gateway already routes `/api/platform/account/*` to `Diten.Platform`.
- If the route is missing, stop and report that `integration-agent` is required.
- Browser JS must not call `http://localhost:5056` or `http://localhost:5057`.

## Lookup & Reference Data Decision
Baseline v1 does not consume PSS-011 lookup endpoints because preferred locale and preferred timezone are out of scope.

Future preference scope:
- If `PreferredLocale` is added later, options must come from `GET /api/lookups/locales` or `GET /api/lookups/languages`.
- If `PreferredTimezone` is added later, options must come from `GET /api/lookups/timezones`.
- Consumers must unwrap `Response<T>.data`.
- Options must use canonical `LookupOptionDto` fields: `code`, `name`, `value`, optional `group`, `sortOrder`, `metadata`.
- Hardcoded locale/timezone arrays are forbidden.
- Browser calls must use same-origin MVC proxy or Gateway, never service port `5057`.
- This dependency belongs in Follow-up Items, not baseline v1.

Out-of-scope lookup/reference boundaries:
- ERP Account reference.
- General Reference.
- Financial Reference.
- Territory Reference.
- Tenant-specific business lookups.

## Acceptance Criteria
- [ ] Header no longer renders `assets/img/avatars/1.png` as the default user image for every Platform/Admin user.
- [ ] Header renders deterministic initials avatar when no real photo/avatar URL exists.
- [ ] Initials use display name first, then claims first/last name, then email local-part fallback.
- [ ] User dropdown `My Profile` links to `/Platform/Account/Profile`.
- [ ] User dropdown `Settings` links to `/Platform/Account/Settings`.
- [ ] No Platform sidebar navigation item is added for this module.
- [ ] `/Platform/Account/Profile` loads with `Layout = "_LayoutPlatformAdmin"` explicitly set.
- [ ] `/Platform/Account/Settings` loads with `Layout = "_LayoutPlatformAdmin"` explicitly set.
- [ ] My Profile shows only real current-user data: display name, email, actor type, status, roles, last login, invitation/account dates when present, and account summary.
- [ ] My Profile does not render fake activity timeline entries.
- [ ] My Profile does not render connections, teams, projects, social profile cards, or profile nav tabs.
- [ ] Settings baseline v1 renders the Account surface only.
- [ ] `Settings > Account` allows updating display name only in baseline v1.
- [ ] Settings baseline v1 does not render a Security tab or password-change form.
- [ ] Email remains read-only and cannot be changed by request tampering.
- [ ] Username remains read-only and cannot be changed by request tampering.
- [ ] Preferred locale and preferred timezone controls are not rendered in baseline v1.
- [ ] Roles, actor type, and status remain read-only and cannot be changed by request tampering.
- [ ] Delete account/self-delete card is not present in rendered HTML.
- [ ] Avatar upload control is not present in rendered HTML.
- [ ] Password change is not included in baseline v1 and no fake/non-working password form is rendered.
- [ ] No AuthService endpoint is created or modified under this pack.
- [ ] PlatformActor authorization protects all profile/settings surfaces.
- [ ] Anonymous and tenant-user access to Platform profile/settings is denied.
- [ ] `en` and `tr` localization resources exist for all visible new profile/settings strings.
- [ ] Baseline v1 does not call PSS-011 lookup endpoints.
- [ ] Gateway route check is completed; missing route work is escalated to integration-agent instead of direct `ocelot.json` edits.

## Test Expectations
Build:
- `dotnet build services/Diten.Platform/src/Diten.Platform.API/Diten.Platform.API.csproj -c Debug`.
- `dotnet build frontend/Diten.Web/Diten.Web.csproj -c Debug`.
- `dotnet build gateway/Diten.ApiGateway/Diten.ApiGateway.csproj -c Debug`.

Backend tests:
- Query returns current actor profile by resolving identity from claims/current user context.
- Update display name succeeds for current actor.
- Update rejects invalid display name.
- Update cannot mutate email, username, roles, actor type, status, partner scope, or tenant scope.
- Update cannot mutate preferred locale or preferred timezone in baseline v1.
- Update cannot mutate password fields in baseline v1.
- Missing current actor or missing `PlatformAdministrator` record returns controlled 401/403/404.
- Stale version returns 409 if concurrency is used.

Frontend/resource tests:
- `en` and `tr` RESX keys exist for every new visible string.
- No DataTable verifier is required because `golden_reference: none`.
- Static scan confirms there is no hardcoded `assets/img/avatars/1.png` default for Platform header user avatar.
- Static scan confirms no delete account card text or selector exists in `Views/Platform/Account/**`.
- Static scan confirms no social/connections/teams/projects tab markup exists in `Views/Platform/Account/**`.
- Static scan confirms no avatar upload control exists in `Views/Platform/Account/**`.
- Static scan confirms no password-change form exists in `Views/Platform/Account/**`.
- Static scan confirms no preferred locale/timezone selects or hardcoded locale/timezone arrays exist in baseline v1.

Browser smoke:
- Platform admin login reaches Platform shell.
- Header initials avatar is visible for a user without photo.
- Dropdown opens.
- `My Profile` link navigates to `/Platform/Account/Profile`.
- `Settings` link navigates to `/Platform/Account/Settings`.
- Profile page loads without sidebar menu addition.
- Settings page loads and shows Account-only baseline behavior.
- Display name update succeeds and the new name appears after reload/header refresh.
- Anonymous user cannot load the pages.
- Tenant user cannot load the pages.
- Delete account card is not found.
- Avatar upload control is not found.
- Security tab and password-change form are not found.
- Preferred locale/timezone controls are not found.
- Fake timeline/social/project tabs are not found.

Gateway/smoke:
- Gateway route check for `/api/platform/account/me`.
- Browser network calls use `5001` same-origin proxy or Gateway `5000`, not service ports `5056` or `5057`.

## Ready-for-dev Checklist
- [x] Old `PSS-009-platform-admin-profile.md` conflict is resolved by removal from active execution scope.
- [x] `PSS-010-platform-admin-security.md` remains future scope for MFA/session/security activity.
- [x] Password change v1 decision is explicit: baseline excludes it unless this pack is revised.
- [x] PreferredLocale and PreferredTimezone are confirmed out of v1.
- [x] No sidebar navigation item.
- [x] No avatar upload.
- [x] No fake timeline.
- [x] No self-delete.
- [ ] Confirm route availability through Gateway; missing route requires integration-agent.
- [x] Review repo scope and protected paths.
- [x] Change `status` from `draft` to `approved` or `ready-for-dev` before implementation.

## Implementation Notes
- The current Platform header uses claims for `window.CurrentUser`; implementation can use this for initial render but should hydrate from the profile endpoint when the profile/settings data is saved.
- Initials avatar can be rendered server-side in Razor or client-side with a small helper; behavior must be deterministic and accessible.
- Use existing Sneat classes/components where helpful, but remove social/project/template-only content.
- The Settings page should not copy all fields from the Sneat reference. Address, organization, phone, country, locale, timezone, password, and avatar upload are not part of this v1 model.
- Do not render Security tab in baseline v1. A future Security tab must be backed by real AuthService behavior or owned by PSS-010.
- Do not wire platform self-service updates through `Platform.Administrators.Update`; that is management permission and applies to editing other admins.
- Do not call PSS lookup service from baseline v1.

## Follow-up Items
- Real avatar upload after storage/blob provider and image validation standards exist.
- Real Audit Trail activity timeline after MOD-0021 provides a queryable source.
- Password change after a normal authenticated PlatformActor AuthService contract is verified and approved.
- MFA enrollment, active sessions, recovery codes, and security activity remain under PSS-010 or a later dedicated security module.
- Email change flow with verification and AuthService synchronization.
- Username change flow with uniqueness validation and AuthService synchronization.
- Preferred locale/timezone persistence and UI. When added, use PSS-011 endpoints `GET /api/lookups/locales` or `GET /api/lookups/languages`, and `GET /api/lookups/timezones`; hardcoded locale/timezone arrays are forbidden.
