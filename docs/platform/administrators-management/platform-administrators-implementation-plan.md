# Platform Administrators Management (NEW-002) — Faz Faz Implementation Planı

## Context

Bugün ERP-vNext'te **"Kim platform admin?"** sorusunun cevabı yok. Sadece tenant başına `TenantAdminUser` (Tenant.cs içinde nested) tanımı var; platform-level admin'lerin (cross-tenant erişimli **PlatformAdmin** ve scope'lu **PartnerAdmin**) CRUD'u eksik. Multi-partner / white-label senaryosu için kritik blocker.

Master-plan'da **NEW-002**, Wave W1-* (cross-cutting blocker), Priority High, Status 🔴 Missing (%0).

**Kapsam kararı (kullanıcı onayı ile):**
- **Slim Golden Reference** (~7 form alanı: Email, DisplayName, ActorType, PartnerId?, AllowedTenantIds?, Status, Roles) → offcanvas create/edit
- **Hardcoded role enum**: `AdministratorRole { SuperAdmin, BillingAdmin, SupportAdmin, ReadOnly }`
- **Invite email**: Stub log + DB outbox queue (MOD-0027 gelince adapter eklenir; mevcut `RegisterTenantCommandHandler` zaten bu placeholder pattern'i kullanıyor)
- **Detail page**: 3 tab — Profile / Roles / Audit
- **Lokalizasyon**: en + tr (Platform standardı)

**Hedef:** Production-ready Slim DataTable modülü + invite outbox stub + audit-ready handler iskeleti.

---

## Architecture Decisions (Golden Reference + Keşiften Çıkanlar)

> **Standart referans:** Golden Reference Slim. Folder/naming/partial yapısı sapma olmadan kopyalanır.
> - Backend: `services/Diten.DevEnablementService/src/Diten.DevEnablementService.Application/Features/GoldenReferenceSlim/`
> - Frontend: `frontend/Diten.Web/Views/DevEnablement/GoldenReferenceSlim/`

| Karar | Sebep | Referans |
|---|---|---|
| `shell: platform-admin` + tüm .cshtml'lerde `Layout = "_LayoutPlatformAdmin"` AÇIKÇA | Platform admin shell zorunluluğu (`views-organization.md:36-41`); canlı örnek `Views/Platform/Tenants/Index.cshtml:7` | Module pack frontmatter |
| `golden_reference: slim` | 7 form alanı (≤8 → Slim) | Sayım kuralı |
| `entity_base: GlobalEntity` (NOT EntityBase) | Platform admin cross-tenant kayıt, TenantId yok | `Tenant.cs:7` |
| Backend folder: `Commands/`, `Queries/`, `Handlers/CommandHandlers/`, `Handlers/QueryHandlers/`, `Validators/` (5 klasör) | Golden Reference Slim birebir | `.../Features/GoldenReferenceSlim/` |
| Handler naming: `{Verb}{Module}Handler` (**Command/Query suffix YOK**) | Golden Reference — `CreateGoldenReferenceSlimHandler.cs` | Slim handler dosyası |
| Validator naming: `{Verb}{Module}Validator` (**Command suffix YOK**) | Golden Reference convention | `CreateGoldenReferenceSlimValidator.cs` |
| `{Module}Models.cs` TEK dosyada DTO/ViewModel | Golden Reference — `GoldenReferenceSlimModels.cs` | Slim Application |
| Frontend partials (Slim): Index, _Filter, _DataTable, _IndexL10n, **_CreateEditOffcanvas**, **_DetailsQuickView**, {Module}Index.cs | Golden Reference Slim — QuickView de var | Slim Views klasörü |
| Route: `/api/platform/administrators` | Master-plan L476 + Platform convention | Master-plan §NEW-002 |
| Permission: `Platform.Administrators.*` (NOT `Modules.*`) | Platform service controller'ı (`erp-architecture.md` permission format kuralı) | — |
| Frontend route: `/Platform/Administrators` | _LayoutPlatformAdmin.cshtml activeController matching | `Views/Shared/_LayoutPlatformAdmin.cshtml:175` |
| `CreatedBy/UpdatedBy` **string** (Guid değil) | BaseEntity field tipi string — ActorName (email/displayName) yazılır | `Diten.Platform.Common/Persistence/BaseEntity.cs:6` |
| Collection: `platform_administrators` | snake_case_plural | — |

---

## Phase 1 — Module Pack (Sözleşme)

**Amaç:** Geliştirme öncesi single-source-of-truth sözleşmesini hazırla.

1. `module-pack-author` ajanını çağır:
   ```
   @[.antigravity/agents/module-pack-author.md]

   Platform Administrators Management (NEW-002) için module pack hazırla.
   Kod yazma. Domain: Platform. form_field_count: 7. golden_reference: GoldenReferenceSlim.
   Status: draft.
   ```
2. Module pack dosyası: `execution/domains/platform-shared-services/module-packs/NEW-002-platform-administrators.md`
3. Kullanıcı incelemesi → status `ready-for-dev` veya `approved`
4. Bu fazda **kod yok**, sadece pack onayı.

**Çıktı:** Onaylı module pack. Sonraki tüm fazlar bu pack'in acceptance criteria'sına göre çalışır.

---

## Phase 2 — Domain Layer (Entities & Enums)

**Klasör:** `services/Diten.Platform/src/Diten.Platform.Domain/`

**Dosyalar:**
- `Entities/PlatformAdministrator.cs` — `sealed class PlatformAdministrator : GlobalEntity`
  - Alanlar: `Email`, `DisplayName`, `ActorType` (enum int), `PartnerId?` (Guid?), `AllowedTenantIds` (List<Guid>), `Status` (enum int), `Roles` (List<int> — AdministratorRole), `LastLoginAtUtc` (DateTimeOffset?)
  - Invite alanları: `InvitationStatus` (enum int), `InvitedAtUtc`, `InviteToken?`, `InviteExpiresAtUtc?`
- `Enums/PlatformAdministratorEnums.cs` — `ActorType { PlatformAdmin=1, PartnerAdmin=2 }`, `AdministratorStatus { Active=1, Suspended=2, Disabled=3 }`, `AdministratorRole { SuperAdmin=1, BillingAdmin=2, SupportAdmin=3, ReadOnly=4 }`, `AdministratorInvitationStatus { PendingInvitation=1, Invited=2, Accepted=3, Expired=4 }`
- `Repositories/IPlatformAdministratorRepository.cs` — interface, custom methods: `GetByEmailAsync`, `ExistsByEmailAsync`, `UpdateStatusAsync`, `QueryAsync(filter, paging, sort)`

**Kural:** Domain'de `MongoDB.Driver` import **YASAK** (master-plan 7.3).

---

## Phase 3 — Persistence Layer

**Klasör:** `services/Diten.Platform/src/Diten.Platform.Infrastructure/`

**Dosyalar:**
- `Repositories/PlatformAdministratorRepository.cs` — `: GlobalRepository<PlatformAdministrator>`, collection adı `"platform_administrators"`
  - `QueryAsync(filter, page, pageSize, sort)`: regex email/name search, status filter, ActorType filter, paging
  - `ExistsByEmailAsync(email, excludeId, ct)`: case-insensitive
  - MongoDB index: unique on `Email` (lowercase normalized), compound on `(Status, ActorType)`, single on `PartnerId`
- DI kaydı: `DependencyInjection.cs` — `services.AddScoped<IPlatformAdministratorRepository, PlatformAdministratorRepository>()`

**Referans:** `TenantRegistryRepository.cs:9-114` birebir pattern.

---

## Phase 4 — Application Layer / Commands (CQRS — Golden Reference Slim birebir)

**Klasör:** `services/Diten.Platform/src/Diten.Platform.Application/Features/PlatformAdministrators/`

**Golden Reference Slim birebir folder yapısı:**
```
Features/PlatformAdministrators/
├── Commands/
├── Queries/
├── Handlers/CommandHandlers/
├── Handlers/QueryHandlers/
├── Validators/
└── PlatformAdministratorsModels.cs    (TEK dosyada DTO'lar)
```

### Commands (`/Commands/` — her dosyada bir sealed record)
- `InvitePlatformAdministratorCommand.cs` — Email, DisplayName, ActorType, PartnerId?, AllowedTenantIds?, Roles[]
- `UpdatePlatformAdministratorCommand.cs` — Id, DisplayName, AllowedTenantIds?, Roles[]
- `SuspendPlatformAdministratorCommand.cs` — Id, Reason
- `ReactivatePlatformAdministratorCommand.cs` — Id
- `DeletePlatformAdministratorCommand.cs` — Id (soft delete)
- `BulkDeletePlatformAdministratorCommand.cs` — Ids[]
- `AssignRolesCommand.cs` — Id, Roles[]
- `ResendInviteCommand.cs` — Id

### Command Handlers (`/Handlers/CommandHandlers/` — naming: `*Handler.cs`, suffix YOK)
- `InvitePlatformAdministratorHandler.cs`
- `UpdatePlatformAdministratorHandler.cs`
- `SuspendPlatformAdministratorHandler.cs`
- `ReactivatePlatformAdministratorHandler.cs`
- `DeletePlatformAdministratorHandler.cs`
- `BulkDeletePlatformAdministratorHandler.cs`
- `AssignRolesHandler.cs`
- `ResendInviteHandler.cs`

**Handler kuralları (Golden Reference):**
- `sealed class {Verb}{Module}Handler : IRequestHandler<{Verb}{Module}Command, Response<T>>`
- `ICurrentUserContext` inject
- İlk satır: `ArgumentNullException.ThrowIfNull(request)`
- Guard clauses: email unique, partnerId tutarlılığı (PartnerAdmin → PartnerId zorunlu), AllowedTenantIds Guid valid
- `CreatedBy = _currentUser.ActorName` (Invite handler)
- `UpdatedBy = _currentUser.ActorName` (Update handler)
- Return: Create → `Response<Guid>.Success(id, 201)`; Update/Suspend/Reactivate/Delete → `Response<NoContent>.Success(..., 204)`

### Validators (`/Validators/` — naming: `*Validator.cs`, suffix YOK)
- `InvitePlatformAdministratorValidator.cs` — Email format + max length, DisplayName required, ActorType enum valid, PartnerAdmin → PartnerId NotEmpty
- `UpdatePlatformAdministratorValidator.cs` — DisplayName required
- `AssignRolesValidator.cs` — Roles non-empty, enum valid

### Invite Email Stub
- Handler: `InvitePlatformAdministratorCommandHandler` içinde:
  - Token = `Guid.NewGuid()`, ExpiresAt = `UtcNow.AddDays(7)`
  - Status = `PendingInvitation`
  - DB'ye yaz
  - `_logger.LogInformation("Invite email queued for {Email}, token={Token}", ...)` — stub
  - **Opsiyonel:** `outbox_events` benzeri collection'a kayıt at (RegisterTenantCommandHandler:280-308 pattern)
- MOD-0027 gelince: stub yerine `INotificationService.SendAsync("platform.admin.invite", ...)` çağrısı eklenir.

**Referans:** `Features/Tenants/Handlers/SuspendTenantCommandHandler.cs:11-44`, `RegisterTenantCommandHandler.cs:280-308` (outbox stub).

---

## Phase 5 — Application Layer / Queries (CQRS — Golden Reference Slim birebir)

**Klasör:** Aynı feature klasörü.

### Queries (`/Queries/` — her dosyada bir sealed record)
- `GetPlatformAdministratorListQuery.cs` — Search?, Status?, ActorType?, PartnerId?, Page=1, PageSize=20, Sort="-createdAt"
- `GetPlatformAdministratorByIdQuery.cs` — Id
- `GetPlatformAdministratorStatsQuery.cs` — KPI cards için

### DTOs (`PlatformAdministratorsModels.cs` — Golden Reference Slim pattern: TEK dosyada)
- `PlatformAdministratorListItemDto` — Id, Email, DisplayName, ActorType, Status, Roles, LastLoginAtUtc
- `PlatformAdministratorDetailDto` — full fields + AllowedTenantIds + Audit fields
- `PlatformAdministratorStatsDto` — Total, Active, Suspended, Disabled, PendingInvitation counts
- `PlatformAdministratorFilterRequest` (filter parametreleri)

### Query Handlers (`/Handlers/QueryHandlers/` — naming: `*Handler.cs`, Query suffix YOK)
- `GetPlatformAdministratorListHandler.cs` → `Response<PagedResult<PlatformAdministratorListItemDto>>`
- `GetPlatformAdministratorByIdHandler.cs` → `Response<PlatformAdministratorDetailDto>`
- `GetPlatformAdministratorStatsHandler.cs` → `Response<PlatformAdministratorStatsDto>`

**Referans:** `Features/SubscriptionPlans/Queries/GetSubscriptionPlansQuery.cs:7-8`, `PagedResult<T>` (TenantContracts.cs:33-38).

---

## Phase 6 — API Controller (Platform Service)

**Klasör:** `services/Diten.Platform/src/Diten.Platform.API/Controllers/Platform/`

**Dosya:** `PlatformAdministratorsController.cs`

```csharp
[ApiController]
[Route("api/platform/administrators")]
[Authorize(Policy = "PlatformActor")]
public sealed class PlatformAdministratorsController : CustomBaseController
{
    // GET /api/platform/administrators
    [HttpGet] [HasPermission("Platform.Administrators.Read")]

    // GET /api/platform/administrators/stats
    [HttpGet("stats")] [HasPermission("Platform.Administrators.Read")]

    // GET /api/platform/administrators/{id}
    [HttpGet("{id:guid}")] [HasPermission("Platform.Administrators.Read")]

    // POST /api/platform/administrators  → invite
    [HttpPost] [HasPermission("Platform.Administrators.Create")]

    // PUT /api/platform/administrators/{id}
    [HttpPut("{id:guid}")] [HasPermission("Platform.Administrators.Update")]

    // POST /api/platform/administrators/{id}/suspend
    [HttpPost("{id:guid}/suspend")] [HasPermission("Platform.Administrators.Suspend")]

    // POST /api/platform/administrators/{id}/reactivate
    [HttpPost("{id:guid}/reactivate")] [HasPermission("Platform.Administrators.Suspend")]

    // DELETE /api/platform/administrators/{id}
    [HttpDelete("{id:guid}")] [HasPermission("Platform.Administrators.Update")]

    // POST /api/platform/administrators/{id}/roles
    [HttpPost("{id:guid}/roles")] [HasPermission("Platform.Administrators.AssignRoles")]

    // POST /api/platform/administrators/{id}/resend-invite
    [HttpPost("{id:guid}/resend-invite")] [HasPermission("Platform.Administrators.Create")]
}
```

- Controller ince: her endpoint `_mediator.Send(request, ct)` + `CreateActionResultInstance(response)`.
- `CustomBaseController` (`API/Controllers/Common/CustomBaseController.cs:7-25`) status-code → `IActionResult` mapping.

**Referans:** `Controllers/Admin/TenantsController.cs:13-179`.

---

## Phase 7 — Gateway Route

**Dosya:** `gateway/Diten.ApiGateway/ocelot.json` (veya equivalent route config)

- Yeni route: `/api/platform/administrators/{everything}` → Platform service (port mevcut Platform service'inin portuna göre, AGENTS.md/domain-config.md'de yazılı).
- Auth header forward, Bearer token propagation.

**Referans:** Mevcut Tenants route entry.

---

## Phase 8 — Frontend Proxy Controller (Diten.Web)

**Dosya:** `frontend/Diten.Web/Controllers/Platform/AdministratorsController.cs`

```csharp
[Authorize(Policy = "PlatformActor")]
[Route("Platform/[controller]")]
public sealed class AdministratorsController : Controller
{
    // Razor views
    [HttpGet] Index();
    [HttpGet("{id:guid}")] Details(Guid id);

    // Proxy endpoints (DataTable + actions)
    [HttpGet("api")] ListProxy();
    [HttpGet("api/stats")] StatsProxy();
    [HttpGet("api/{id:guid}")] DetailProxy();
    [HttpPost("api")] CreateProxy();
    [HttpPut("api/{id:guid}")] UpdateProxy();
    [HttpPost("api/{id:guid}/suspend")] SuspendProxy();
    [HttpPost("api/{id:guid}/reactivate")] ReactivateProxy();
    [HttpDelete("api/{id:guid}")] DeleteProxy();
    [HttpPost("api/{id:guid}/roles")] AssignRolesProxy();
    [HttpPost("api/{id:guid}/resend-invite")] ResendInviteProxy();
}
```

- `ProxyGatewayAsync()` helper (mevcut TenantsController:291-309 pattern) Gateway 5000'e forward + Bearer header.

**Referans:** `Controllers/TenantsController.cs:1-365`.

---

## Phase 9 — Frontend Views (Slim Pattern — Golden Reference birebir)

**Klasör:** `frontend/Diten.Web/Views/Platform/Administrators/`

### ZORUNLU LAYOUT KURALI
Tüm `.cshtml` dosyalarında ilk Razor blokta layout AÇIKÇA yazılır (frontmatter `shell: platform-admin` → `_LayoutPlatformAdmin`):

```cshtml
@{
    ViewData["Title"] = Localizer["AdministratorsTitle"].Value;
    Layout = "_LayoutPlatformAdmin";   // ← ZORUNLU
}
```

Bu hem Index.cshtml hem Details.cshtml için zorunlu. Atlanırsa `_ViewStart.cshtml` default'una düşer ve view-organization standart ihlali olur.

### Zorunlu dosyalar (Golden Reference Slim + _DetailsQuickView):
- `Index.cshtml` — Layout AÇIKÇA + 4 KPI card (Total / Active / Suspended / PendingInvitation) + Filter partial + DataTable partial + L10n partial + offcanvas partial'lar (CreateEdit + QuickView)
- `_Filter.cshtml` — inline collapsible filter (ActorType select, Status select, Role select) — Select2 multi
- `_DataTable.cshtml` — `<table id="dt-administrators" data-dt-standard="v2">` + skeleton loader + kolonlar: checkbox, Email, DisplayName, ActorType, Roles, Status, LastLogin, Actions
- `_IndexL10n.cshtml` — `<script id="administrators-l10n" type="application/json">@Json.Serialize(new {...})</script>`
- `_CreateEditOffcanvas.cshtml` — width 480px, form: Email (disabled on edit), DisplayName, ActorType select, PartnerId (conditional), AllowedTenantIds (Tagify multi), Roles (Select2 multi). Save/Cancel buttons.
- `_DetailsQuickView.cshtml` — **Golden Reference Slim'de zorunlu** — offcanvas QuickView (read-only detay), Edit'e link
- `Details.cshtml` — Layout AÇIKÇA + 3 tab (Bootstrap nav-tabs): Profile / Roles / Audit
- `AdministratorsIndex.cs` — empty marker class (`namespace Diten.Web.Views.Platform.Administrators { public sealed class AdministratorsIndex {} }`)

### Tab içerikleri (Details.cshtml):
- **Profile tab:** read-only Email, DisplayName edit, ActorType (read-only), Status badge, Suspend/Reactivate/Delete buttons
- **Roles tab:** current roles list + AssignRoles offcanvas trigger. Multi-select Roles.
- **Audit tab:** placeholder (MOD-0021 öncesi) — LastLogin, InvitedAt, AcceptedAt, CreatedBy, UpdatedBy. PartnerAdmin için AllowedTenantIds list view.

**Referans:** `Views/Platform/Tenants/Index.cshtml:1-85`, `_Filter.cshtml:1-52`, `_DataTable.cshtml:1-36`, `_IndexL10n.cshtml:1-202`. Sneat template: `_Reference/Theme/.../app-user-list.html:1902-1928` (KPI + DataTable + Offcanvas layout).

---

## Phase 10 — Frontend JS

**Klasör:** `frontend/Diten.Web/wwwroot/assets/js/Platform/Administrators/`

- `index.js` — DataTable init:
  - `endpoint = '/Platform/Administrators/api'`
  - `stateSave: false`, Save View column indexes
  - KPI stats fetch from `/Platform/Administrators/api/stats`
  - Action handlers: Edit (offcanvas open), Suspend (SweetAlert2 confirm → POST), Reactivate, Delete (confirm), Resend invite
  - Bulk delete via `data-administrator-bulk-delete`
- `index.l10n.js` — `#administrators-l10n` payload → `window.L10n` bridge (PascalCase normalization)
- `details.js` — tab switching, AssignRoles submit, Status badge update

**Referans:** `wwwroot/assets/js/Platform/Tenants/index.js` (~250 satır), `index.l10n.js:1-69`.

---

## Phase 11 — Layout / Menu Item

**Dosya:** `frontend/Diten.Web/Views/Shared/_LayoutPlatformAdmin.cshtml`

- Line ~199 (TenantSecurity menu item'ından sonra, `</ul>` öncesi) ekle:
  ```cshtml
  <li class="menu-item @(activeController == "Administrators" ? "active" : "")">
    <a href="@Url.Action("Index","Administrators",new{area=""})" class="menu-link">
      <i class="menu-icon icon-base bx bx-user-check"></i>
      <div data-i18n="Administrators">@SharedLocalizer["Administrators"]</div>
    </a>
  </li>
  ```

---

## Phase 12 — Lokalizasyon (en + tr)

**Klasör:** `frontend/Diten.Web/Resources/Views/Platform/Administrators/`

- `AdministratorsIndex.en.resx` — ~60 key (PageTitle, PageDescription, KPI labels, Filter labels, DataTable column headers, Action labels, Confirmation messages, Validation messages, Roles enum labels, ActorType enum labels, Status enum labels, InvitationStatus enum labels)
- `AdministratorsIndex.tr.resx` — birebir Turkish parite

**Yedi dil tuzağına dikkat:** Master-plan 7.15 — Platform tarafı **sadece en + tr** (ar/es/fr/ru/zh **YOK**).

**Shared additions:** `SharedResource.en.resx` ve `SharedResource.tr.resx`'e menu key'i: `Administrators`.

---

## Phase 13 — Permission Seed

- Yeni permission'ları sisteme tanıt (mevcut permission seed mekanizması neyse o):
  - `Platform.Administrators.Read`
  - `Platform.Administrators.Create`
  - `Platform.Administrators.Update`
  - `Platform.Administrators.Suspend`
  - `Platform.Administrators.AssignRoles`
- `HasPermissionAttribute` (`API/Security/HasPermissionAttribute.cs:7-48`) JWT claims'inden okuyor; platform_admin actor_type → otomatik pass.

---

## Phase 14 — Testing

### Unit Tests
**Proje:** `tests/Diten.Platform.UnitTests` (varsa) veya yeni proje
- Validator testleri: her validator için happy + sad path (Email format, ActorType enum, PartnerAdmin → PartnerId required)
- Handler testleri (mock repository + `ICurrentUserContext`):
  - InviteHandler: email unique check, status=PendingInvitation, token üretiliyor mu
  - SuspendHandler: status transition, CreatedBy/UpdatedBy set
  - GetByIdHandler: not found → 404

### Integration Tests
**Proje:** `tests/Diten.Platform.IntegrationTests` (varsa)
- MongoDB testcontainer üzerinden controller + repository round-trip
- 5 endpoint için happy path: List, GetById, Create, Suspend, Delete

### Frontend Smoke
- Browser smoke test: 5001 frontend + 5000 gateway up → `/Platform/Administrators` aç → DataTable load + filter + create offcanvas + suspend confirmation çalışıyor

---

## Phase 15 — Verification (Build + Verifiers + RESX)

```bash
# Build (3 ayrı proje)
dotnet build services/Diten.Platform/src/Diten.Platform.API/Diten.Platform.API.csproj -c Debug
dotnet build frontend/Diten.Web/Diten.Web.csproj -c Debug
dotnet build gateway/Diten.ApiGateway/Diten.ApiGateway.csproj -c Debug

# DataTable Slim verifier
python3 .antigravity/scripts/verify_datatable_page.py . --area Platform --module Administrators --reference slim

# RESX checker
python3 .antigravity/skills/i18n-localization/scripts/resx_sharedresource_checker.py .

# Quality gate
/quality-gate-datatable Administrators --reference slim
```

**Kabul beklentisi:**
- 3 build PASS
- DataTable verifier PASS
- RESX checker PASS (en + tr parite)
- Smoke: list, filter, KPI refresh, offcanvas create, suspend confirm, details tabs

### Ek Yapısal Kontroller (Golden Reference Uyumu)

```bash
# Layout AÇIKÇA yazılı mı? (Platform admin shell zorunluluğu)
grep -l 'Layout = "_LayoutPlatformAdmin"' frontend/Diten.Web/Views/Platform/Administrators/*.cshtml
# Beklenen: Index.cshtml ve Details.cshtml dahil tüm .cshtml dosyaları listede

# Backend folder yapısı Golden Reference Slim ile birebir mi?
ls services/Diten.Platform/src/Diten.Platform.Application/Features/PlatformAdministrators/
# Beklenen klasörler: Commands/, Queries/, Handlers/CommandHandlers/, Handlers/QueryHandlers/, Validators/
# Beklenen dosya: PlatformAdministratorsModels.cs

# Handler naming: suffix YOK kontrolü
ls services/Diten.Platform/src/Diten.Platform.Application/Features/PlatformAdministrators/Handlers/CommandHandlers/ | grep -E '(Command|Query|Request)Handler\.cs$'
# Beklenen: boş çıktı (suffix'li handler dosyası YOKsa OK)

# Frontend Slim partial seti tam mı?
ls frontend/Diten.Web/Views/Platform/Administrators/
# Beklenen: Index.cshtml, _Filter.cshtml, _DataTable.cshtml, _IndexL10n.cshtml,
#           _CreateEditOffcanvas.cshtml, _DetailsQuickView.cshtml, Details.cshtml, AdministratorsIndex.cs
```

---

## Critical Files (Modifiye/Eklenecek)

### Backend — services/Diten.Platform/src/
- `Diten.Platform.Domain/Entities/PlatformAdministrator.cs` *(yeni)*
- `Diten.Platform.Domain/Enums/PlatformAdministratorEnums.cs` *(yeni)*
- `Diten.Platform.Domain/Repositories/IPlatformAdministratorRepository.cs` *(yeni)*
- `Diten.Platform.Infrastructure/Repositories/PlatformAdministratorRepository.cs` *(yeni)*
- `Diten.Platform.Infrastructure/DependencyInjection.cs` *(modifiye — DI ekle)*
- `Diten.Platform.Application/Features/PlatformAdministrators/Requests/Commands/*.cs` *(7 dosya, yeni)*
- `Diten.Platform.Application/Features/PlatformAdministrators/Requests/Queries/*.cs` *(3 dosya, yeni)*
- `Diten.Platform.Application/Features/PlatformAdministrators/Handlers/*.cs` *(10 dosya, yeni)*
- `Diten.Platform.Application/Features/PlatformAdministrators/Validators/*.cs` *(3-4 dosya, yeni)*
- `Diten.Platform.Application/Features/PlatformAdministrators/Dtos/*.cs` *(3 dosya, yeni)*
- `Diten.Platform.API/Controllers/Platform/PlatformAdministratorsController.cs` *(yeni)*

### Gateway
- `gateway/Diten.ApiGateway/ocelot.json` *(modifiye — route ekle)*

### Frontend — frontend/Diten.Web/
- `Controllers/Platform/AdministratorsController.cs` *(yeni)*
- `Views/Platform/Administrators/Index.cshtml` *(yeni)*
- `Views/Platform/Administrators/_Filter.cshtml` *(yeni)*
- `Views/Platform/Administrators/_DataTable.cshtml` *(yeni)*
- `Views/Platform/Administrators/_IndexL10n.cshtml` *(yeni)*
- `Views/Platform/Administrators/_CreateEditOffcanvas.cshtml` *(yeni)*
- `Views/Platform/Administrators/Details.cshtml` *(yeni)*
- `Views/Platform/Administrators/AdministratorsIndex.cs` *(yeni)*
- `Views/Shared/_LayoutPlatformAdmin.cshtml` *(modifiye — menu item)*
- `wwwroot/assets/js/Platform/Administrators/index.js` *(yeni)*
- `wwwroot/assets/js/Platform/Administrators/index.l10n.js` *(yeni)*
- `wwwroot/assets/js/Platform/Administrators/details.js` *(yeni)*
- `Resources/Views/Platform/Administrators/AdministratorsIndex.en.resx` *(yeni)*
- `Resources/Views/Platform/Administrators/AdministratorsIndex.tr.resx` *(yeni)*
- `Resources/Views/Shared/SharedResource.{en,tr}.resx` *(modifiye — menu key)*

### Module Pack
- `execution/domains/platform-shared-services/module-packs/NEW-002-platform-administrators.md` *(yeni, Phase 1)*

---

## Yeniden Kullanılacak Mevcut Bileşenler

| Bileşen | Path | Kullanım |
|---|---|---|
| `GlobalEntity`, `BaseEntity` | `Diten.Platform.Common/Persistence/BaseEntity.cs:3-14` | Entity inheritance |
| `GlobalRepository<T>` | `Repositories.cs:52` | Repository base + soft delete filter |
| `CustomBaseController` | `API/Controllers/Common/CustomBaseController.cs:7-25` | Status code → IActionResult |
| `Response<T>` envelope | `Application/Common/Response.cs:3-23` | Tüm handler return |
| `ICurrentUserContext` | `Application/Contracts/ICurrentUserContext.cs:3-14` | Audit fields (ActorName) |
| `HasPermissionAttribute` | `API/Security/HasPermissionAttribute.cs:7-48` | Permission gate |
| `PagedResult<T>` | `Features/Tenants/TenantContracts.cs:33-38` | Query pagination response |
| `_LayoutPlatformAdmin.cshtml` | `Views/Shared/_LayoutPlatformAdmin.cshtml:170-205` | Layout + sidebar |
| `GoldenReferenceSlim` | `frontend/Diten.Web/Models/GoldenReferenceSlim/` | Frontend partial pattern reference |
| Tenants Index | `Views/Platform/Tenants/Index.cshtml` ve partials | Frontend yapısı için live golden reference |
| TenantsController proxy | `Controllers/TenantsController.cs:1-365` | Frontend proxy pattern + ProxyGatewayAsync helper |
| Tagify | `Security.cshtml:11,157` | AllowedTenantIds multi-tag input |
| Outbox stub pattern | `Features/Tenants/Handlers/RegisterTenantCommandHandler.cs:280-308` | Invite email placeholder |

---

## Dependencies & Future Work

**Bu plan tek başına tamamlanabilir.** Aşağıdaki bağımlılıklar **stub'lanır**, gelecek waves'de gerçek implementation eklenir:

| Bağımlılık | Bu fazda | Gelecek (master-plan) |
|---|---|---|
| MOD-0027 Notification Service | Console log + DB outbox queue stub | Gerçek email gönderim (Liquid template + SMTP/SendGrid adapter) |
| MOD-0021 Audit Trail | Audit tab placeholder (mevcut Created/Updated alanları) | `AuditBehavior` pipeline + audit event collection |
| NEW-001 Secrets Management | `appsettings.Development.json` | Vault/AWS Secrets adapter |
| MOD-0018 RBAC Enforcement | Sadece `[HasPermission]` attribute | `[RequiresModule]` + `[RequiresFeature]` |

**Master-plan status update:** Phase 15 PASS sonrası `docs/platform/master-plan.md` L162, L1778 → NEW-002 Status `🟠 In Progress` veya `✅ Done %100`'a güncellenecek.

---

## Verification Summary (End-to-End)

1. **Build** — 3 proje (Platform.API + Diten.Web + Diten.ApiGateway) `dotnet build -c Debug` PASS
2. **Verifier** — `verify_datatable_page.py --area Platform --module Administrators --reference slim` PASS
3. **RESX** — `resx_sharedresource_checker.py` PASS (en + tr parite)
4. **Quality gate** — `/quality-gate-datatable Administrators --reference slim` PASS (DataTable v2 marker, inline filter, skeleton loader, Save View, _CreateEditOffcanvas.cshtml)
5. **Smoke test** — Frontend 5001 + Gateway 5000 ayağa kalkmış halde:
   - `/Platform/Administrators` → liste yüklenir, 4 KPI doğru sayı, filter çalışır
   - "Add Administrator" offcanvas → PlatformAdmin invite → DB'de PendingInvitation status + log
   - PartnerAdmin invite + AllowedTenantIds seçimi
   - Suspend / Reactivate confirmation → status değişimi
   - Details page → 3 tab geçişi, AssignRoles offcanvas çalışıyor
   - Resend invite → log + UpdatedAt güncellenir
   - Soft delete → liste'den çıkar, DB'de `IsDeleted=true`
6. **Permission test** — JWT'de `Platform.Administrators.Read` olmayan kullanıcı → 403; `actor_type=platform_admin` claim'li kullanıcı → her permission'a pass
