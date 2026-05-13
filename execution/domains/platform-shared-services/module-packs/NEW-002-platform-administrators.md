---
id: NEW-002
name: Platform Administrators Management
domain: platform-shared-services
service: Diten.Platform
shell: platform-admin
golden_reference: slim
entity_base: GlobalEntity
status: in-progress
owner: codex
branch: feature/pss/new-002-platform-administrators
started: 2026-05-12
target: 2026-05-20
form_field_count: 7
---

# NEW-002 - Platform Administrators Management

## Module Summary
Platform Administrators Management answers the platform-level question "who is a platform admin?". It owns CRUD, invite, status, role assignment, tenant-scope metadata, and read surfaces for cross-tenant `PlatformAdmin` actors and scoped `PartnerAdmin` actors.

This is a draft module pack. Runtime implementation must not begin until user review changes `status` to `approved` or `ready-for-dev`.

Master-plan traceability:
- Source module: `docs/platform/master-plan.md`, NEW-002 - Platform Administrators Management.
- Wave: W1-*.
- Priority: High.
- Master-plan state before implementation: Missing, 0%.

## Ownership and Boundaries
- In-scope:
  - Platform-level administrator source of record.
  - `PlatformAdmin` and `PartnerAdmin` actor type distinction.
  - Partner scope and explicit allowed tenant scope for PartnerAdmin users.
  - Hardcoded administrator roles.
  - Invite lifecycle metadata and resend behavior.
  - Platform Admin UI list, filters, KPI cards, Slim create/edit offcanvas, quick view/details surface, role assignment, and audit-ready metadata.
  - Create-only temporary password provisioning with manual and server-generated password modes.
  - AuthService platform user provisioning, first-login forced password change, platform forgot password, and reset password flow.
  - Platform invitation/reset email templates using the current SMTP configuration.
  - Platform API endpoints and frontend proxy through Gateway.
  - Permission definitions for `Platform.Administrators.*`.
  - Localization for `en` and `tr`.
- Out-of-scope:
  - Tenant admin user management inside `Tenant`.
  - Partner Management entity CRUD; `PartnerId` is a forward reference.
  - Real MOD-0027 notification provider integration beyond the current SMTP/template implementation.
  - Full MOD-0021 audit event store implementation.
  - RBAC/ABAC engine redesign from MOD-0018.
  - Tenant impersonation from NEW-004.

## Owned Objects
- Entity:
  - `PlatformAdministrator`.
- Enums:
  - `ActorType`: `PlatformAdmin`, `PartnerAdmin`.
  - `AdministratorStatus`: `Active`, `Suspended`, `Disabled`.
  - `AdministratorRole`: `SuperAdmin`, `BillingAdmin`, `SupportAdmin`, `ReadOnly`.
  - `AdministratorInvitationStatus`: `PendingInvitation`, `Invited`, `Accepted`, `Expired`.
- Repository:
  - `IPlatformAdministratorRepository`.
  - `PlatformAdministratorRepository`.
  - Mongo collection: `platform_administrators`.
- Commands:
  - `InvitePlatformAdministratorCommand`.
  - `UpdatePlatformAdministratorCommand`.
  - `SuspendPlatformAdministratorCommand`.
  - `ReactivatePlatformAdministratorCommand`.
  - `DeletePlatformAdministratorCommand`.
  - `AssignPlatformAdministratorRolesCommand`.
  - `ResendPlatformAdministratorInviteCommand`.
- Queries:
  - `GetPlatformAdministratorsQuery`.
  - `GetPlatformAdministratorByIdQuery`.
  - `GetPlatformAdministratorStatsQuery`.
- Handlers:
  - `InvitePlatformAdministratorHandler`.
  - `UpdatePlatformAdministratorHandler`.
  - `SuspendPlatformAdministratorHandler`.
  - `ReactivatePlatformAdministratorHandler`.
  - `DeletePlatformAdministratorHandler`.
  - `AssignPlatformAdministratorRolesHandler`.
  - `ResendPlatformAdministratorInviteHandler`.
  - `GetPlatformAdministratorsHandler`.
  - `GetPlatformAdministratorByIdHandler`.
  - `GetPlatformAdministratorStatsHandler`.
- Validators:
  - `InvitePlatformAdministratorValidator`.
  - `UpdatePlatformAdministratorValidator`.
  - `AssignPlatformAdministratorRolesValidator`.
- Models:
  - `PlatformAdministratorsModels.cs` contains list/detail/stats DTOs and filter payloads.
- API endpoints:
  - `POST /api/platform/administrators/generate-password`.
  - `GET /api/platform/administrators`.
  - `GET /api/platform/administrators/stats`.
  - `GET /api/platform/administrators/{id}`.
  - `POST /api/platform/administrators`.
  - `PUT /api/platform/administrators/{id}`.
  - `POST /api/platform/administrators/{id}/suspend`.
  - `POST /api/platform/administrators/{id}/reactivate`.
  - `DELETE /api/platform/administrators/{id}`.
  - `POST /api/platform/administrators/{id}/roles`.
  - `POST /api/platform/administrators/{id}/resend-invite`.
- Frontend:
  - Route: `/Platform/Administrators`.
  - Auth routes: `/platform/login`, `/platform/forgot-password`, `/platform/reset-password`, `/platform/change-password`.
  - View folder: `Views/Platform/Administrators/`.
  - Scripts: `wwwroot/assets/js/Platform/Administrators/`.
  - Resources: `Resources/Views/Platform/Administrators/`.
- Permissions:
  - `Platform.Administrators.Read`.
  - `Platform.Administrators.Create`.
  - `Platform.Administrators.Update`.
  - `Platform.Administrators.Suspend`.
  - `Platform.Administrators.AssignRoles`.

## Entity Fields
`PlatformAdministrator` contract:

| Field | Type | Rules |
|---|---|---|
| Base | `GlobalEntity` | Platform-level record; not tenant-owned. |
| Email | `string` | Required, trimmed, normalized lowercase for uniqueness, max 256. |
| DisplayName | `string` | Required, trimmed, max 200. |
| ActorType | `ActorType` | Required. `PartnerAdmin` requires `PartnerId`. |
| PartnerId | `Guid?` | Required only when `ActorType = PartnerAdmin`; forward reference until Partner Management exists. |
| AllowedTenantIds | `List<Guid>` | Required non-empty list for `PartnerAdmin`; empty list is rejected to avoid accidental all-tenant access. |
| Status | `AdministratorStatus` | Required. Invite creates `Status = Active`; login gating uses `InvitationStatus`. |
| Roles | `List<AdministratorRole>` | Required; at least one valid role. |
| LastLoginAtUtc | `DateTimeOffset?` | Read-only login metadata. |
| InvitationStatus | `AdministratorInvitationStatus` | Required. Invite creates `PendingInvitation`. |
| InvitedAtUtc | `DateTimeOffset?` | Set on invite and resend. |
| InviteToken | `string?` | Stub token until MOD-0027; never exposed in list DTOs. |
| InviteExpiresAtUtc | `DateTimeOffset?` | Defaults to seven days after invite/resend. |
| CreatedBy | `string?` | Current actor name/email from `ICurrentUserContext`. |
| UpdatedBy | `string?` | Current actor name/email from `ICurrentUserContext`. |
| Version | `int` | Technical concurrency field inherited from `GlobalEntity` if available; update conflicts return 409. |

Index requirements:
- Unique case-insensitive effective index on normalized `Email`.
- Compound query index on `Status` and `ActorType`.
- Single query index on `PartnerId`.
- Soft-deleted records must not appear in list/query/stat results.

DTO/request rules:
- `TenantId` is never accepted from create/update/filter form payloads.
- `InviteToken` is not exposed in list DTOs.
- Detail DTO may expose invite expiry/status but not secrets unsuitable for UI.

## Repo Scope
- `execution/domains/platform-shared-services/module-packs/NEW-002-platform-administrators.md`.
- `services/Diten.Platform/src/Diten.Platform.Domain/**`.
- `services/Diten.Platform/src/Diten.Platform.Application/Features/PlatformAdministrators/**`.
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/**`.
- `services/Diten.Platform/src/Diten.Platform.API/**`.
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/**`.
- `services/Diten.AuthService/**` for platform admin provisioning, forced password change, forgot/reset password, and remember-me token lifetime only.
- `frontend/Diten.Web/Controllers/Platform/AdministratorsController.cs`.
- `frontend/Diten.Web/Views/Platform/Administrators/**`.
- `frontend/Diten.Web/wwwroot/assets/js/Platform/Administrators/**`.
- `frontend/Diten.Web/Resources/Views/Platform/Administrators/**`.
- `frontend/Diten.Web/Resources/Views/Shared/SharedResource.en.resx`.
- `frontend/Diten.Web/Resources/Views/Shared/SharedResource.tr.resx`.
- `frontend/Diten.Web/Views/Shared/_LayoutPlatformAdmin.cshtml` for Platform Admin menu item only.
- `gateway/Diten.ApiGateway/**` for route validation and coordination only; direct `ocelot.json` modification remains integration-agent owned unless explicitly approved.

## Protected Paths
- `.antigravity/**`.
- `frontend/Diten.Web/Controllers/Archive/**`.
- `frontend/Diten.Web/Views/Archive/**`.
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml`.
- `gateway/Diten.ApiGateway/**/ocelot.json` unless handled by integration-agent or explicit user approval.
- `services/Diten.AuthService/**` outside the platform admin password provisioning and platform auth page scope.
- `services/Diten.DevEnablementService/**`.
- `services/Diten.EnterpriseStrategyService/**`.
- `services/Diten.MdmService/**`.
- Any non-Platform domain service internals.

## Dependencies
- Platform & Shared Services domain config.
- Existing `GlobalEntity` and `GlobalRepository<T>` pattern for platform-level data.
- Existing Platform service CQRS/MediatR pipeline.
- Existing `Response<T>` envelope and `CustomBaseController` response mapping.
- Existing JWT actor type and `[HasPermission]` authorization infrastructure.
- Existing Platform Admin layout and live Platform shell behavior.
- Golden Reference Slim backend and frontend file contract.
- MOD-0027 Notification Service is future work; this module uses log/outbox placeholder behavior until then.
- MOD-0021 Audit Trail is future work; this module exposes audit-ready metadata and UI placeholders until then.
- Future Partner Management module owns partner records; this module stores `PartnerId` as a forward reference.

## Runtime Constraints
- Frontend must call Gateway port `5000`; it must not call Platform service port `5057` directly.
- API base route is `/api/platform/administrators`.
- Frontend base route is `/Platform/Administrators`.
- API responses use `Response<T>` and `CustomBaseController`.
- API controller requires `[Authorize(Policy = "PlatformActor")]`.
- Mutating endpoints require matching `Platform.Administrators.*` permissions.
- MongoDB is the persistence store.
- Soft delete is mandatory; no hard delete endpoint is exposed.
- `PlatformAdministrator` uses `GlobalEntity` because platform admins are cross-tenant platform records, not tenant-owned records.
- Domain layer must not import `MongoDB.Driver`.
- Action-based separation follows Golden Reference Slim exactly.
- Invite creates `Status = Active` and `InvitationStatus = PendingInvitation`; login/session eligibility checks `InvitationStatus` and status together.
- `PartnerAdmin` with empty `AllowedTenantIds` is rejected; explicit tenant scope is required.
- Concurrency uses the inherited technical `Version` field when available. Stale writes return 409 and UI must require reload.
- Invite email behavior is a stub: enqueue an outbox event if an outbox seam exists; otherwise log with `LogInformation`.
- Create invite provisions a matching AuthService platform user with a temporary password.
- Temporary password may be manually entered or generated server-side; client-side random generation is forbidden.
- First platform login with a temporary password must return a forced-password-change signal and must not allow normal platform navigation until changed.
- Platform forgot/reset password uses single-use reset token metadata and must not reveal whether an email exists.
- `rememberMe=true` on login issues a 30-day refresh token; default remains the shorter existing lifetime.
- DataTable contract v2 is mandatory with `data-dt-standard="v2"`.
- Localization scope is `en` and `tr` only for Platform UI.

## Layout & Shell Contract
- `shell: platform-admin`.
- Every `.cshtml` file under `Views/Platform/Administrators/` must explicitly include:

```cshtml
@{
    Layout = "_LayoutPlatformAdmin";
}
```

- View folder: `frontend/Diten.Web/Views/Platform/Administrators/`.
- Frontend route: `/Platform/Administrators`.
- Platform shell live reference: `frontend/Diten.Web/Views/Platform/Tenants/`.
- Golden Reference structural reference: `frontend/Diten.Web/Views/DevEnablement/GoldenReferenceSlim/`.

## Backend File Convention
Golden Reference Slim structure must be copied:

```text
services/Diten.Platform/src/Diten.Platform.Application/Features/PlatformAdministrators/
├── Commands/
├── Queries/
├── Handlers/
│   ├── CommandHandlers/
│   └── QueryHandlers/
├── Validators/
└── PlatformAdministratorsModels.cs
```

Naming:
- Command records: `{Verb}PlatformAdministratorCommand` or `{Verb}PlatformAdministrator{Qualifier}Command`.
- Query records: `GetPlatformAdministrator{Qualifier}Query`.
- Handler classes: `{Verb}PlatformAdministratorHandler` or `{Verb}PlatformAdministrator{Qualifier}Handler`.
- Validator classes: `{Verb}PlatformAdministratorValidator` or `{Verb}PlatformAdministrator{Qualifier}Validator`.

Forbidden:
- `Requests/Commands` or `Requests/Queries` folders.
- `*CommandHandler`, `*QueryHandler`, or `*RequestHandler` names.
- `*CommandValidator` names.
- Multiple public request/handler/validator classes in one file.

## Frontend File Contract
Slim file set for `Views/Platform/Administrators/`:
- `Index.cshtml`.
- `_Filter.cshtml`.
- `_DataTable.cshtml`.
- `_IndexL10n.cshtml`.
- `_CreateEditOffcanvas.cshtml`.
- `_DetailsQuickView.cshtml`.
- `AdministratorsIndex.cs`.

Script/resource set:
- `wwwroot/assets/js/Platform/Administrators/index.js`.
- `wwwroot/assets/js/Platform/Administrators/index.l10n.js`.
- `Resources/Views/Platform/Administrators/AdministratorsIndex.en.resx`.
- `Resources/Views/Platform/Administrators/AdministratorsIndex.tr.resx`.

Index contract:
- Absolute partial paths are used.
- Bulk action bar uses `DataTableBulkActionBarViewModel`.
- Render order is filter, bulk action bar, DataTable, offcanvas panels.
- `_CreateEditOffcanvas.cshtml` hosts create/edit.
- `_DetailsQuickView.cshtml` hosts quick profile/roles/audit preview.
- Compact-only `Create.cshtml`, `Edit.cshtml`, `Details.cshtml`, and `_Form.cshtml` are not part of this Slim module.

## Validation Rules
| Field | Required | Rule | DB-level | Pre-check |
|---|---|---|---|---|
| Email | Yes | Email format, trim, lowercase normalize, max 256 | Unique normalized email | `ExistsByEmailAsync` |
| DisplayName | Yes | Trim, max 200 | None | Validator |
| ActorType | Yes | Valid enum only | None | Validator |
| PartnerId | For PartnerAdmin | Non-empty Guid when `ActorType = PartnerAdmin` | Future FK/lookup when Partner Management exists | Validator |
| AllowedTenantIds | For PartnerAdmin | Non-empty list of valid Guids | None | Validator and optional tenant lookup |
| Status | Yes | Valid enum only | Indexed | Validator |
| Roles | Yes | Non-empty list, all values valid enum | None | Validator |
| Reason | Suspend only | Required for suspend, max 500 | None | Validator |
| Version | Update/delete/status actions | Must match persisted technical version when concurrency is enabled | Concurrency check | Repository/service check |

## Failure Path to Verify
- Duplicate email: create/update returns 409, record is not persisted, UI shows field-level duplicate email error.
- Manual temporary password that violates policy returns validation error and does not create the platform administrator.
- Generated temporary password is produced by server endpoint and accepted by create flow.
- First login with temporary password redirects to `/platform/change-password` before any normal Platform page.
- Reset token is single-use and expires.
- Remember Me creates a 30-day platform refresh token/cookie.
- Missing PartnerAdmin `PartnerId`: validator returns 400 and save is blocked.
- PartnerAdmin empty `AllowedTenantIds`: validator returns 400 and save is blocked.
- Invalid role value: validator returns 400 and save is blocked.
- Unauthorized actor: missing permission returns 403; non-platform actor cannot access Platform Admin route.
- Concurrency conflict: stale update/status/delete returns 409 and UI requires reload.
- Missing record or soft-deleted record: get/update/status/delete returns 404.
- Invite resend for accepted invite: behavior must be explicit; default is reject with 409 unless implementation documents otherwise.

### Admin Safety Guardrails (master-plan §7.21)
- **Self-action:** Current user kendi `Delete` / `Suspend` / `Disable` / role-remove command'larını çağırdığında handler 409 + `"You cannot perform this action on your own account."` döner; kayıt değişmez.
- **Last SuperAdmin (delete/suspend):** Sistemde 1 adet `status=Active` + `Roles` içinde `"SuperAdmin"` PlatformAdmin kaldığında bu kişiyi delete/suspend etmek 409 + `"At least one active Super Admin must remain in the system."` döner.
- **Last SuperAdmin (role-remove):** Sistemdeki tek aktif SuperAdmin'in `Roles` listesinden `"SuperAdmin"` çıkarılmaya çalışıldığında 409 ile reddedilir (mesaj aynı).
- **Role self-downgrade:** Current user `AssignRoles` komutuyla kendi `Roles` listesinden `"SuperAdmin"` çıkarmaya çalıştığında 409 + `"You cannot remove your own administrative role."` döner.
- **Bulk-self filter:** `BulkDeletePlatformAdministratorCommand` / `BulkSuspendPlatformAdministratorCommand` payload'ında current user ID varsa sessizce ayıklanır; response `SkippedSelfIds: [currentUserId]` içerir, HTTP 200.
- **PartnerAdmin scope self-removal:** PartnerAdmin actor `UpdateAllowedTenants` ile mevcut erişim kaynağı tenant'ı listeden çıkardığında 409 + `"You cannot remove yourself from a tenant you currently operate."`. (PlatformAdmin'in başkasının scope'unu daraltması bu kuralı tetiklemez.)
- **PartnerAdmin son-admin senaryosu:** Backend reddetmez — partner'ın son PartnerAdmin'i silindiğinde 200 döner. UI tarafında SweetAlert confirm gösterilir.

## Authorization Convention
- API policy: `[Authorize(Policy = "PlatformActor")]`.
- Permission format: `Platform.{Resource}.{Action}`.
- Resource: `Administrators`.
- Permissions:
  - `Platform.Administrators.Read`.
  - `Platform.Administrators.Create`.
  - `Platform.Administrators.Update`.
  - `Platform.Administrators.Suspend`.
  - `Platform.Administrators.AssignRoles`.
- `actor_type=platform_admin` follows existing platform super-admin behavior.
- `partner_admin` must have explicit permissions and must be constrained by partner/tenant scope where applicable.

### Admin Safety Guardrails enforcement
- All mutating handlers (`Delete`, `Suspend`, `Reactivate`, `AssignRoles`, `UpdateAllowedTenants`, `BulkDelete`, `BulkSuspend`) MUST invoke `IActorSafetyGuard` from `Diten.Platform.Common.Security` per master-plan §7.21.
- Guard call sequence in each handler: `Authorization (attribute) → IActorSafetyGuard.* → business validation → repository`.
- Manual `if (id == _currentUser.UserId) return Fail(...)` blocks are **forbidden** — the guard is the single enforcement point.
- `IPlatformAdministratorRepository.CountActiveSuperAdminsAsync(CancellationToken)` is the canonical "last SuperAdmin" probe; the guard depends on it.

## Gateway / API Routing Decision
- Decision: Gateway route is required unless an existing catch-all already safely covers `/api/platform/administrators`.
- Frontend calls Gateway port `5000` only.
- Required upstream path: `/api/platform/administrators/{everything}` plus base path handling if Ocelot config needs separate entries.
- Downstream service: Diten.Platform service.
- `gateway/Diten.ApiGateway/**/ocelot.json` is protected; route addition is an integration-agent or explicitly approved task.
- OPTIONS/preflight behavior must be preserved if explicit routes are added.

## Acceptance Criteria
- [ ] `POST /api/platform/administrators` creates an invite record with normalized unique email, `Status = Active`, `InvitationStatus = PendingInvitation`, roles, explicit scope, invite expiry, and audit fields.
- [ ] Create flow provisions the matching AuthService platform user with temporary password, sends invite email, and marks password change required.
- [ ] Create form supports manual temporary password and server-generated password modes; password fields are hidden in edit mode.
- [ ] Platform first login with temporary password redirects to forced password change and blocks normal platform navigation until password changes.
- [ ] Platform forgot/reset password flow sends a reset email, accepts a valid single-use token, and rejects expired/reused tokens.
- [ ] Remember Me on platform login produces a 30-day refresh token/cookie.
- [ ] Duplicate email create/update attempts return 409 and do not persist a duplicate record.
- [ ] `GET /api/platform/administrators` returns a paged DataTable-compatible list with search, status, actor type, partner, and role filtering.
- [ ] `GET /api/platform/administrators/stats` returns Total, Active, Suspended, Disabled, and PendingInvitation counts excluding soft-deleted records.
- [ ] `GET /api/platform/administrators/{id}` returns detail data including allowed tenant scope, roles, invite status, and audit-ready fields.
- [ ] Update, suspend, reactivate, assign roles, resend invite, and delete endpoints enforce validation, permission, soft delete, and concurrency behavior.
- [ ] PartnerAdmin validation requires `PartnerId` and non-empty `AllowedTenantIds`.
- [ ] Platform API endpoints enforce `PlatformActor` and the `Platform.Administrators.*` permission set.
- [ ] Frontend `/Platform/Administrators` uses DataTable v2 with `data-dt-standard="v2"`, KPI cards, filters, bulk action bar, Slim create/edit offcanvas, quick view, skeleton loader, and row actions.
- [ ] All `Views/Platform/Administrators/*.cshtml` files explicitly include `Layout = "_LayoutPlatformAdmin"`.
- [ ] Slim frontend file set includes `_CreateEditOffcanvas.cshtml` and `_DetailsQuickView.cshtml`; Compact-only create/edit/details pages are not used.
- [ ] Invite/resend invite logs queue intent and writes outbox metadata when an outbox seam is available.
- [ ] Localization resources exist for `en` and `tr` with key parity.
- [ ] DataTable verifier passes: `python3 .antigravity/scripts/verify_datatable_page.py . --area Platform --module Administrators --reference slim`.
- [ ] Quality gate passes: `/quality-gate-datatable Administrators --reference slim`.

### Admin Safety Guardrails AC (master-plan §7.21)
- [ ] Self-action reddi: current user kendi PlatformAdmin'ini delete/suspend edemez (409 + i18n mesaj).
- [ ] Last SuperAdmin reddi: sistem en az 1 active SuperAdmin kalacak şekilde delete/suspend/role-remove engellenir (409).
- [ ] Bulk filtresi: `BulkDelete` ve `BulkSuspend` payload'ı current user ID içerse bile sessizce ayıklanır, response `SkippedSelfIds[]` içerir (200).
- [ ] Role self-downgrade reddi: kullanıcı `AssignRoles` ile kendi `"SuperAdmin"` rolünü kaldıramaz (409).
- [ ] PartnerAdmin scope self-removal reddi: PartnerAdmin `UpdateAllowedTenants` ile kendi erişim kaynağı tenant'ını listeden çıkaramaz (409).
- [ ] PartnerAdmin son-admin senaryosu: backend rejection YOK; UI'da SweetAlert confirm modal gösterilir.
- [ ] Tüm guard çağrıları `IActorSafetyGuard` üzerinden — handler içinde manuel self-check YASAK.
- [ ] Frontend defense: current user satırında Delete/Suspend butonu disabled; bulk select'te current user row check edilemez (tooltip ile sebep gösterilir).

## Test Expectations
- Unit tests:
  - Invite validator accepts valid PlatformAdmin and PartnerAdmin inputs.
  - Invite validator rejects invalid email, missing display name, invalid actor type, missing PartnerAdmin `PartnerId`, empty PartnerAdmin `AllowedTenantIds`, and empty roles.
  - Assign roles validator rejects empty or invalid roles.
  - Invite handler rejects duplicate email and creates pending invite metadata.
  - Suspend/reactivate handlers apply expected status changes, reason/audit behavior, and `UpdatedBy`.
  - Get-by-id handler returns not found for missing or soft-deleted records.
- Repository/application tests:
  - List excludes soft-deleted records.
  - Email uniqueness is case-insensitive.
  - Search/filter/page/sort behavior matches DataTable needs.
  - PartnerAdmin allowed tenant scope is persisted and returned.
  - Stale version update returns 409 when concurrency is enabled.
- Build checks:
  - `dotnet build services/Diten.Platform/src/Diten.Platform.API/Diten.Platform.API.csproj -c Debug`.
  - `dotnet build frontend/Diten.Web/Diten.Web.csproj -c Debug`.
  - `dotnet build gateway/Diten.ApiGateway/Diten.ApiGateway.csproj -c Debug`.
- Frontend checks:
  - DataTable Slim verifier passes for Platform Administrators.
  - RESX parity passes for `en` and `tr`.
  - Grep confirms every `Views/Platform/Administrators/*.cshtml` file has `Layout = "_LayoutPlatformAdmin"`.
  - Browser smoke opens `/Platform/Administrators`, loads KPI cards and table, opens create/edit offcanvas, runs filter, opens quick view, and exercises role/status actions.
- Permission checks:
  - Missing `Platform.Administrators.Read` returns 403 for non-super platform actors.
  - `actor_type=platform_admin` follows existing platform-admin permission behavior.
- Admin Safety Guardrails (master-plan §7.21):
  - Self-delete: current user `Delete /api/platform/administrators/{currentUserId}` → 409, kayıt değişmez.
  - Self-suspend: current user `POST /suspend` kendisine → 409.
  - Last SuperAdmin delete: tek aktif SuperAdmin'i delete eden istek → 409, repository call yapılmaz.
  - Last SuperAdmin role-remove: tek aktif SuperAdmin'in `AssignRoles` çağrısı `"SuperAdmin"`'i çıkardığında → 409.
  - Bulk self-filter: `BulkDelete` payload `[a, b, currentUserId]` → 200 + `SkippedSelfIds: [currentUserId]` + a/b silinmiş.
  - Bulk all-self: `BulkDelete` payload `[currentUserId]` → 200 + `SkippedSelfIds: [currentUserId]` + 0 etki.
  - Role self-downgrade: current user `AssignRoles` ile kendi `Roles` listesinden `"SuperAdmin"` çıkarmaya çalıştığında → 409.
  - PartnerAdmin scope self-removal: PartnerAdmin `UpdateAllowedTenants` ile kendi erişim kaynağı tenant'ı çıkardığında → 409.
  - PartnerAdmin son-admin happy path: PlatformAdmin partner'ın son PartnerAdmin'ini silerken backend → 200 (UI confirm ayrı).

## Ready-for-dev Checklist
- [ ] Golden Reference Slim live backend and frontend code were read before implementation.
- [ ] Frontmatter includes `service`, `shell`, `golden_reference`, `entity_base`, and `form_field_count`.
- [ ] Layout & Shell Contract explicitly requires `_LayoutPlatformAdmin`.
- [ ] Backend File Convention uses `Commands`, `Queries`, `Handlers/CommandHandlers`, `Handlers/QueryHandlers`, `Validators`, and `PlatformAdministratorsModels.cs`.
- [ ] Handler names do not use `CommandHandler`, `QueryHandler`, or `RequestHandler` suffixes.
- [ ] Frontend File Contract lists the complete Slim file set.
- [ ] Validation Rules cover email, actor type, PartnerAdmin scope, roles, suspend reason, and concurrency.
- [ ] Failure Path to Verify covers duplicate, missing, unauthorized, concurrency, not found, and resend edge cases.
- [ ] Authorization Convention lists policy, permission format, permissions, and actor behavior.
- [ ] Gateway routing decision is explicit and respects protected `ocelot.json` ownership.
- [ ] Acceptance Criteria are testable.
- [ ] Test Expectations cover unit, repository/application, build, verifier, RESX, smoke, and permission checks.

## Implementation Notes
- The implementation plan originally referenced `execution/domains/Platform/...`; this pack corrects the path to `execution/domains/platform-shared-services/...`.
- `NEW-002` is kept as the canonical master-plan id even though many existing PSS packs use `PSS-*` or `MOD-*`.
- The master-plan sample says `PlatformAdministrator : EntityBase`; implementation must use `GlobalEntity` because platform administrators are platform-level cross-tenant records.
- Roles are hardcoded enum values for this module, not free-form strings.
- Status and invitation state are distinct: invite creates `Status = Active` plus `InvitationStatus = PendingInvitation`.
- PartnerAdmin empty `AllowedTenantIds` is rejected by default for safety.
- Permission seed belongs to the AuthService/permission registry flow and must be coordinated as an integration task.
- Audit tab is populated from existing metadata and remains future-ready for MOD-0021.
- **Admin Safety Guardrails (master-plan §7.21):**
  - `IActorSafetyGuard` is introduced as part of this module under `services/Diten.Platform.Common/Security/`. NEW-002 is the first consumer; future admin modules (MOD-0018 RBAC, etc.) will reuse the same service without re-implementing.
  - `IPlatformAdministratorRepository.CountActiveSuperAdminsAsync(ct)` is added as a custom repository extension solely to support the guard's last-SuperAdmin probe.
  - Bootstrap: the very first PlatformAdmin (cold start) cannot be invited via UI — it is provisioned via seed/runbook from NEW-001 vault. Document this in the Platform runbook.
  - Frontend: `_RowActions.cshtml` partial and bulk select header must read `window.currentUserId` (already exposed in `_LayoutPlatformAdmin` per existing convention) and disable Delete/Suspend on the current user row plus exclude it from bulk checkbox.

## Follow-up Items
- After user review, update `status` to `approved` or `ready-for-dev` before implementation.
- During implementation, update this pack to `in-progress`, then `review`, then `done`.
- Integrate real notification delivery when MOD-0027 is available.
- Replace audit placeholder with MOD-0021 audit trail events when that service is available.
- Add Partner Management lookup/validation when the partner SoR module exists.
- Update `docs/platform/master-plan.md` NEW-002 status after implementation verification passes.
