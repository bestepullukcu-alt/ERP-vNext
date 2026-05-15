# ERP-vNext SaaS Platform — Tam Kapsamlı Master Geliştirme Planı

> **Bu döküman, SaaS Platform/Admin tarafının tüm modüllerinin referans planıdır.**
> Her modülü geliştirirken bu planın ilgili bölümünü AI'ya verin.
> Bir modül bittiğinde aşağıdaki "Status" alanlarını manuel güncelleyin.
> ERP/tenant kullanım tarafı, ERP içi modüller (workflow/document/HR/Finance vb.) **kapsam dışıdır.**

**Hedef Kayıt Yeri:** `docs/platform/master-plan.md`
**Versiyon:** 1.0
**Tarih:** 2026-05-11

---

## 0. Bu Planı AI ile Nasıl Kullanırsın

### Senaryo A — Yeni bir modülün detay planını çıkarmak
AI'ya şöyle yaz:
```
Sana ERP-vNext Platform Master Plan'ı veriyorum (aşağıda).
Sıradaki modül olarak MOD-XXXX'i (Wave X-X) geliştireceğim.
Bu modülün:
1. Detaylı domain modelini (entity field'ları, enum'lar, validation kuralları)
2. CQRS Request/Handler/Validator sınıflarını (action-based separation, her biri ayrı dosya — bkz Bölüm 7.2)
3. Controller endpoint'lerini (route + auth policy + permission)
4. Frontend view yapısını (Index/Create/Edit/Details + partial'lar)
5. Acceptance criteria checklist'ini
6. Test plan'ını (unit + integration)
çıkar. Master Plan'daki Cross-cutting Standartları zorunlu uygula.
[Master plan içeriğini buraya yapıştır]
```

### Senaryo B — Yapılmış bir modülün eksikliklerini doğrulamak
```
Master Plan'da MOD-XXXX "tamamlanmış" görünüyor ama %85.
Repo'daki şu dosyaları okuyup [dosya listesi]:
- Master Plan'daki "Eksikler" listesinin hâlâ geçerli olup olmadığını söyle
- Bulduğun yeni eksikleri raporla
- Acceptance criteria'yı tek tek check et
```

### Senaryo C — Bir modül implementation'ı tamamlandığında code review
```
MOD-XXXX'i implement ettim. Master Plan'daki:
- Domain modeli, API surface, acceptance criteria'ya uyum
- Cross-cutting standartlara uyum (soft delete, concurrency, localization, permission, audit)
- Anti-pattern'lere düşüp düşmediği
açılarından review yap. Diff: [git diff veya dosya listesi]
```

### Plan Güncelleme Kuralı
Bir modül bittiğinde:
1. İlgili modülün **Status** alanını `Done` yap
2. **What's done** listesini gerçek implementation'a göre güncelle
3. **What's missing** listesinden tamamlananları sil
4. Bölüm 9'daki master tabloyu güncelle

---

## 1. Proje Mimari Çerçevesi (Tüm Modüller İçin Bağlam)

### 1.1 Teknoloji Stack
| Katman | Teknoloji |
|---|---|
| Backend Runtime | .NET 8 |
| Backend Pattern | Clean Architecture + DDD + CQRS (MediatR) |
| Persistence | MongoDB (logical multi-tenancy via TenantId) |
| Validation | FluentValidation |
| API Gateway | Ocelot (`http://localhost:5000`) |
| Auth | JWT Bearer, `actor_type` claim ile platform/tenant ayrımı |
| Frontend | ASP.NET Core MVC + Razor SSR |
| UI | Bootstrap 5 + jQuery + DataTables v2 + Notyf + SweetAlert2 + Select2 |
| Lokalizasyon | IHtmlLocalizer + .resx (**Platform: 2 dil — en, tr**) |
| Event Bus (hedef) | TBD — RabbitMQ veya MassTransit (henüz yok) |
| Job Scheduler (hedef) | TBD — Hangfire / Quartz (henüz yok) |

### 1.2 Mikroservis Sınırları
- **Diten.Platform** — Platform/Admin domain (tenant, plan, feature, catalog, entitlement)
- **Diten.AuthService** — Auth, user, role, permission, login policies
- **Diten.Web** — Frontend (proxy to Gateway)
- **Diten.MdmService**, **Diten.DevenService** — Tenant-side ERP servisleri (KAPSAM DIŞI)

### 1.3 Multi-Tenancy
- **Platform Tenant ID:** `00000000-0000-0000-0000-000000000001` (hardcoded; Platform context flag)
- **Tenant Resolution:** `TenantResolutionMiddleware` (`X-Tenant-Id` header VEYA JWT `tenant_id` claim)
- **Admin path:** `/api/admin/*` ve `/api/platform/*` → X-Tenant-Id GÖNDERİLEMEZ → `TenantContext.SetPlatformContext()`
- **Actor types:** `platform_admin`, `partner_admin`, `tenant_user`

### 1.4 Authorization
- Policy: `[Authorize(Policy = "PlatformActor")]` → `actor_type` ∈ {platform_admin, partner_admin}
- Permission: `[HasPermission("Platform.X.Y")]` → JWT `permission` claim'inden okur
- Platform admin → her permission'a otomatik izin

### 1.5 Standart Kod Konvansiyonu

**5 Katmanlı Mimari (her mikroserviste zorunlu):**
1. `<Service>.Api` — Presentation (Controllers, Middleware, Swagger)
2. `<Service>.Application` — Orchestration (CQRS Request/Handler, Mapping, Validation)
3. `<Service>.Domain` — Core (Entities, Enums, Domain Exceptions, Repository Interfaces)
4. `<Service>.Persistence` — Data Access (MongoDB.Driver burada hapsedilir, DbContext, Repository sınıfları, TenantFilter)
5. `<Service>.Infrastructure` — Cross-Cutting (Mail, SMS, FileStorage, AI, HTTP Clients)

**Bağımlılık Akışı:** `Api → Application → Domain` (Domain hiçbir şeyi referans alamaz)
**MongoDB import yasağı:** `MongoDB.Driver` ve `MongoDB.Bson` SADECE Persistence'da. Domain'de yasak (istisna: `BsonRepresentation` attribute).

**Dosya Şablonu (Golden Reference Slim/Compact birebir — bkz `.antigravity/rules/module-pack-standard.md`):**
```
services/Diten.Platform/src/
├── Diten.Platform.Api/Controllers/Platform/{Module}Controller.cs
├── Diten.Platform.Application/Features/{Module}/
│   ├── Commands/Create{Entity}Command.cs               ← sealed record, her command ayrı dosya
│   ├── Commands/Update{Entity}Command.cs
│   ├── Commands/Delete{Entity}Command.cs
│   ├── Commands/BulkDelete{Entity}Command.cs
│   ├── Queries/Get{Entity}ListQuery.cs                 ← sealed record, her query ayrı dosya
│   ├── Queries/Get{Entity}ByIdQuery.cs
│   ├── Handlers/CommandHandlers/Create{Entity}Handler.cs   ← class, suffix YOK
│   ├── Handlers/CommandHandlers/Update{Entity}Handler.cs
│   ├── Handlers/CommandHandlers/Delete{Entity}Handler.cs
│   ├── Handlers/CommandHandlers/BulkDelete{Entity}Handler.cs
│   ├── Handlers/QueryHandlers/Get{Entity}ListHandler.cs
│   ├── Handlers/QueryHandlers/Get{Entity}ByIdHandler.cs
│   ├── Validators/Create{Entity}Validator.cs           ← Command suffix YOK
│   ├── Validators/Update{Entity}Validator.cs
│   └── {Entity}Models.cs                               ← TEK dosyada tüm DTO/ViewModel'ler
├── Diten.Platform.Domain/
│   ├── Entities/{Entity}.cs                        ← EntityBase'den miras
│   ├── Enums/{Entity}Enums.cs                      ← Lookup code'lar için
│   └── Repositories/I{Entity}Repository.cs         ← Sadece custom queryler için
├── Diten.Platform.Persistence/
│   ├── Repositories/{Entity}Repository.cs          ← RepositoryBase<T>'den miras
│   └── Configurations/{Entity}Configuration.cs     ← Mongo index'leri
└── Diten.Platform.Infrastructure/
    ├── Services/...                                ← INotificationService, IStorageService impl.
    └── Clients/...                                 ← IUserServiceClient impl.

frontend/Diten.Web/
├── Controllers/Platform/{Module}Controller.cs      ← Proxy to Gateway
├── Views/Platform/{Module}/
│   ├── Index.cshtml, Create.cshtml, Edit.cshtml, Details.cshtml
│   ├── _Form.cshtml, _DataTable.cshtml, _Filter.cshtml
│   └── _IndexL10n.cshtml
├── Resources/Views/Platform/{Module}/{Module}Index.{en|tr}.resx   ← Sadece 2 dil
└── wwwroot/assets/js/Platform/{Module}/*.js
```

**Kural:** Bir dosyada birden fazla public class **YASAK** (Command, Query, Handler veya DTO grup dosyası yok — istisna `{Entity}Models.cs` DTO dosyası, Golden Reference pattern).

**Naming kuralları (Golden Reference birebir):**
- Command record: `{Verb}{Entity}Command` (ör. `CreateTenantCommand`)
- Query record: `Get{Entity}{Qualifier}Query` (ör. `GetTenantByIdQuery`)
- Handler class: `{Verb}{Entity}Handler` (**Command / Query / Request suffix YOK**, ör. `CreateTenantHandler`)
- Validator class: `{Verb}{Entity}Validator` (**Command suffix YOK**, ör. `CreateTenantValidator`)
- Referans canlı kod: `services/Diten.DevEnablementService/.../Features/GoldenReferenceSlim/`

**EntityBase sınıf adı (servis bazlı):**
- `Diten.MdmService`, `Diten.DevEnablementService`, `Diten.AuthService` → `EntityBase`
- `Diten.Platform` tenant-aware kayıt → `BaseEntity` (eşdeğer kontrat)
- `Diten.Platform` cross-tenant katalog → `GlobalEntity : BaseEntity` (gerekçeli)

---

## 2. Modül Envanteri — Tek Bakışta Master Tablo

Durum kodları: ✅ Done · 🟡 Partial · 🔴 Missing · 🟠 In Progress

| ID | Modül | Wave | Öncelik | Status | % |
|---|---|---|---|---|---|
| **MOD-0043/44/46** | Tenant Management (toplu kayıt — alt parçalara bölündü, aşağı bakınız) | — | — | 🟡 | 88 |
| **MOD-0043** | Tenant Architecture Foundation | — | 🟠 High | 🟠 | 80 |
| **MOD-0044** | Tenant Manager (Backend) | — | 🟠 High | 🟢 | 82 |
| **MOD-0046** | Tenant Core UI | W3-A | 🟠 High | 🟢 | 80 |
| **MOD-0046-QG** | Tenant Quota Governance UI | W3-A | 🟡 Medium | 🟡 | 55 |
| **PSS-006** | Subscription Plan Catalog | — | — | ✅ | 96 |
| **PSS-007** | Subscription Feature Mgmt | — | — | 🟡 | 90 |
| **PSS-005** | Module Catalog | — | — | ✅ | 93 |
| **MOD-0298** | Tenant Module Entitlement | — | — | 🟡 | 87 |
| **MOD-0297** | Tenant Subscription Lifecycle | — | — | 🟡 | 82 |
| **PSS-004** | Tenant Login & Security | — | — | 🟡 | 86 |
| **PSS-011** | Lookups / Reference Data | — | — | 🟢 | 93 |
| **PSS-009** | Platform Admin Profile & Settings | — | 🟡 Medium | 🟠 | 89 |
| **PSS-008** | Module Details Assignment Inspection | — | 🟡 Medium | 🟡 | 65 |
| **PSS-010** | Platform Admin Password & MFA Security | — | 🟠 High | 🟡 | 60 |
| **MOD-0012** | Secrets & Configuration Vault | W1-* | 🔴 Blocker | 🟢 | 85 |
| **MOD-0014** | Module Boundary Registry | W1-B | 🟠 High | 🔴 | 0 |
| **MOD-0023** | Workflow Designer (Approvals/SLAs) | W1 | 🟠 High | 🔴 | 0 |
| **MOD-0024** | Task & Checklist Engine | W1-W2 | 🟠 High | 🔴 | 0 |
| **MOD-0031** | Evidence Linking Service | W1 | 🟡 Medium | 🔴 | 0 |
| **MOD-0037** | Integration Monitoring & Reconciliation | W2-W3 | 🟡 Medium | 🔴 | 0 |
| **NEW-001** | Secrets Management (legacy ID; bkz. MOD-0012) | W1-* | 🔴 Blocker | ⚠️ | — |
| **NEW-002** | Platform Administrators Mgmt | W1-* | 🟠 High | 🟢 | 95 |
| **MOD-0009** | Tenant Registry Lifecycle Events | W1-A | 🔴 Blocker | 🟡 | 50 |
| **MOD-0008** | Module Catalog Assignable Expose | W1-B | 🔴 Blocker | 🟡 | 80 |
| **MOD-0018** | RBAC / Entitlement Enforcement | W1-B | 🔴 Blocker | 🟡 | 20 |
| **MOD-0026** | Background Job Scheduler | W1-C | 🔴 Blocker | 🔴 | 0 |
| **MOD-0035** | Event Bus / Internal Events | W1-C | 🔴 Blocker | 🔴 | 0 |
| **MOD-0027** | Notification / Email Service | W1-D | 🔴 Blocker | 🔴 | 0 |
| **MOD-0263** | External Messaging Provider | W1-D | 🔴 Blocker | 🔴 | 0 |
| **MOD-0028** | Document / Evidence Metadata | W2-A | 🟠 High | 🔴 | 0 |
| **MOD-0266** | Blob / File Storage Provider | W2-A | 🟠 High | 🔴 | 0 |
| **MOD-0262** | External Document Provider | W2-A | 🟠 High | 🔴 | 0 |
| **MOD-0021** | General Audit Trail | W2-B | 🟠 High | 🟢 | 98 |
| **MOD-0287** | User Notification Preferences | W2-C | 🟠 High | 🔴 | 0 |
| **MOD-0034** | Webhook Delivery | W2-C | 🟠 High | 🔴 | 0 |
| **NEW-003** | Notification Template Mgmt UI | W2-D | 🟠 High | 🔴 | 0 |
| **NEW-004** | Tenant Impersonation Tooling | W2-D | 🟡 Medium | 🔴 | 0 |
| **MOD-0032** | API Gateway Hardening | W3-A | 🟠 High | 🟡 | 65 |
| **MOD-0033** | Consumer / Quota Model | W3-A | 🟠 High | 🟡 | 78 |
| **MOD-0046+** | Tenant Core UI Extensions | W3-A | 🟠 High | 🟡 | 60 |
| **MOD-0299** | SaaS Billing & Invoicing | W3-B | 🟠 High | 🔴 | 0 |
| **MOD-0041** | Logging / Monitoring | W3-C | 🟡 Medium | 🟠 | 20 |
| **MOD-0042** | Alerting / Incident Runbooks | W3-C | 🟡 Medium | 🔴 | 0 |
| **MOD-0265** | SIEM / Observability Provider | W3-C | 🟡 Medium | 🔴 | 0 |
| **MOD-0038** | Event Taxonomy / Naming | W3-D | 🟡 Medium | 🔴 | 0 |
| **MOD-0039** | Schema Compatibility Governance | W3-D | 🟡 Medium | 🔴 | 0 |
| **MOD-0002** | Interface Registry | W3-E | 🟡 Medium | 🟢 | 80 |
| **MOD-0003** | Data Contract Registry | W3-E | 🟡 Medium | 🔴 | 0 |

---

## 3. MEVCUT MODÜLLER (Yapılmış / Yarım Kalmış)

### 3.1 Tenant Management
**ID:** MOD-0043 / MOD-0044 / MOD-0046 (foundation/manager/UI)
**Status:** 🟡 Partial (%88)
**Purpose:** Platform admin'in tüm tenant'ları yönetmesi — list, create, branding, security, lifecycle (provision/suspend/cancel).

**What's done:**
- ✅ Backend CRUD: [services/Diten.Platform/src/Diten.Platform.API/Controllers/Admin/TenantsController.cs](services/Diten.Platform/src/Diten.Platform.API/Controllers/Admin/TenantsController.cs)
- ✅ Endpoint'ler: GetTenants, GetStats, GetDetail, RegisterTenant, UpdateTenant, UpdateBranding, UpdateLoginSettings, Lifecycle
- ✅ Frontend Index (DataTable v2 + KPI cards), Create, Details (4 tab), Security
- ✅ Bulk delete, individual suspend/reactivate
- ✅ Branding (logo+favicon upload)
- ✅ Commercial tab'ları (subscription + entitlements)
- ✅ Lokalizasyon: en, tr

**What's missing:**
- 🔴 Trial expiry otomasyonu (hosted service — bkz MOD-0026)
- 🔴 Auto-suspend logic (PastDue → Suspended)
- 🔴 "System Monitoring" tab placeholder — gerçek metric yok
- 🔴 Audit timeline tab'ı boş (bkz MOD-0021)
- 🔴 Tenant impersonation (bkz NEW-004)
- 🔴 Logo storage base64 olarak DB'de — provider seam'i lazım (bkz MOD-0266)
- 🟡 Lokalizasyon: en + tr var, sapma yok (bu modül uyumlu)

**%100 için kalanlar:**
- [ ] MOD-0026 ile `TrialExpiryScanJob`, `PastDueAutoSuspendJob` ve `CancelAtPeriodEndJob` üretime alınmalı.
- [ ] MOD-0035 ile tenant create/suspend/reactivate/cancel event'leri outbox üzerinden yayınlanmalı.
- [ ] MOD-0021 audit deep-link ve tenant detail audit timeline gerçek veriyle bağlanmalı.
- [ ] MOD-0266/MOD-0028 sonrası logo/favicon base64 storage'dan document/blob provider'a taşınmalı.
- [ ] Tenant Index/Create/Details/Security için gateway smoke + browser smoke + DataTable v2 doğrulaması eklenmeli.

**Critical files:**
- Backend: `services/Diten.Platform/src/Diten.Platform.API/Controllers/Admin/TenantsController.cs`
- Backend: `services/Diten.Platform/src/Diten.Platform.Domain/Entities/Tenant.cs`
- Frontend: `frontend/Diten.Web/Views/Platform/Tenants/`
- L10n: `frontend/Diten.Web/Resources/Views/Platform/Tenants/`

---

### 3.2 Subscription Plan Catalog
**ID:** PSS-006
**Status:** ✅ Done (%96)
**Purpose:** SaaS abonelik planlarının (FREE/STARTER/PRO/ENTERPRISE) tanımı; fiyat, quota, feature, modül kapsamı.

**What's done:**
- ✅ Full CRUD + Activate/Deactivate
- ✅ PlanFeatureMapping ve EntitlementMappings entegrasyonu
- ✅ Currency lookup
- ✅ Frontend grid card view (Index), Create/Edit form
- ✅ FluentValidation, RowVersion concurrency

**What's missing:**
- 🟢 Hardcoded fallback currency listesi (`["USD","EUR","TRY","GBP"]`) — PSS-011 ile kaldırıldı; gateway/HTTP smoke ile kalıcı doğrulanmalı
- 🔴 Plan upgrade/downgrade workflow (proration, period mid-change)
- 🔴 Billing entegrasyonu (bkz MOD-0299)
- 🔴 409 conflict resolution UI yok (RowVersion mismatch'te form re-load)
- 🟢 Lokalizasyon: en + tr var (uyumlu)

**%100 için kalanlar:**
- [ ] RowVersion mismatch için kullanıcıya mevcut kayıtla yeniden yükleme/merge seçeneği sunan 409 conflict UI tamamlanmalı.
- [ ] Plan upgrade/downgrade akışı MOD-0297 + MOD-0299 ile proration ve period-mid-change kurallarıyla bağlanmalı.
- [ ] WebApplicationFactory integration testleri ve gateway smoke testleri eklenmeli.

**Critical files:**
- Backend: `services/Diten.Platform/src/Diten.Platform.API/Controllers/Platform/SubscriptionPlansController.cs`
- Backend: `services/Diten.Platform/src/Diten.Platform.Domain/Entities/SubscriptionPlan.cs`
- Frontend: `frontend/Diten.Web/Views/Platform/SubscriptionPlans/`

---

### 3.3 Subscription Feature Management
**ID:** PSS-007
**Status:** 🟡 Partial (%90)
**Purpose:** Plan'lardan bağımsız feature kataloğu, FeatureCategory, Plan↔Feature mapping matrisi.

**What's done:**
- ✅ FeatureDefinition + FeatureCategory + PlanFeatureMapping entity'leri
- ✅ CRUD + Archive + GetPlanMappings
- ✅ RowVersion concurrency
- ✅ Frontend grid view + offcanvas editors (_FeatureEditor, _CategoryEditor)
- ✅ Duplicate code prevention (FeatureCode/Slug unique)

**What's missing:**
- 🔴 **Runtime enforcement YOK** — Feature tanımlanıyor, plan'a mapleniyor, ama hiçbir endpoint feature'ı check etmiyor (bkz MOD-0018)
- 🔴 Feature usage analytics
- 🔴 AuthService permission generation entegrasyonu
- 🔴 Tenant self-service feature toggle
- 🟢 Lokalizasyon: en + tr var (uyumlu)

**Critical files:**
- Backend: `services/Diten.Platform/src/Diten.Platform.API/Controllers/Platform/SubscriptionFeaturesController.cs`
- Backend: `services/Diten.Platform/src/Diten.Platform.Domain/Features/SubscriptionFeatures/`
- Frontend: `frontend/Diten.Web/Views/Platform/SubscriptionFeatures/`

---

### 3.4 Module Catalog
**ID:** PSS-005 / MOD-0008
**Status:** ✅ Done (%93) — projedeki en olgun modül
**Purpose:** ERP modüllerinin merkezi katalog kaydı; ModuleCode, Domain, Service, Version, IsCoreModule, IsTenantAssignable.

**What's done:**
- ✅ Full CRUD + soft delete
- ✅ Module pages (ModulePageDescriptor) + page actions
- ✅ Assignment overview/plans/tenants endpoint'leri
- ✅ Frontend Index, Create, Edit, Details, PageDetails
- ⚠️ Lokalizasyon: 7 dil yapılmış (en, tr, ar, es, fr, ru, zh) — **bu OVER-ENGINEERING.** Yeni modüllerde sadece en + tr yap; bu modül istisna.

**What's missing:**
- 🔴 Module dependency resolution (örn. HR → OrgHierarchy şart)
- 🔴 Module versioning / migration
- 🟡 ModulePageDescriptor sahipliği tartışmalı — bu Platform değil tenant deployment metadata'sı olmalı
- 🔴 Assignable expose contract MOD-0018 için stabilize edilmedi

**%100 için kalanlar:**
- [ ] Assignable module read contract MOD-0018 ve MOD-0298 tarafından kullanılan stabil bir interface olarak sabitlenmeli.
- [ ] Module dependency graph, compatibility/version range ve migration metadata alanları tamamlanmalı.
- [ ] Catalog cache invalidation create/update/deactivate/delete aksiyonlarında test edilmeli.
- [ ] ModulePageDescriptor ownership kararı netleştirilmeli; Platform'da kalacaksa acceptance criteria güncellenmeli, taşınacaksa migration planı yazılmalı.

**Critical files:**
- Backend: `services/Diten.Platform/src/Diten.Platform.API/Controllers/Platform/ModuleCatalogController.cs`
- Backend: `services/Diten.Platform/src/Diten.Platform.Domain/Entities/ModuleCatalogItem.cs`
- Frontend: `frontend/Diten.Web/Views/Platform/ModuleCatalog/`
- Docs: `docs/platform/module-catalog/`

---

### 3.5 Tenant Module Entitlements
**ID:** MOD-0298
**Status:** 🟡 Partial (%87)
**Purpose:** Tenant başına modül erişim haklarının yönetimi: plan-projection vs physical override.

**What's done:**
- ✅ TenantModuleEntitlement entity + EntitlementSource enum (ManualOverride/Addon/Trial/System)
- ✅ Add/Enable/Disable/UpdateExpiry/RemoveManualOverride command'ları
- ✅ Effective access evaluator (precedence rules)
- ✅ Tenant Details → Commercial → Module Entitlements tab
- ✅ Add modal (offcanvas)

**What's missing:**
- 🔴 **`RefreshProjection` endpoint logic yarım** — contract var, implementation incomplete
- 🔴 Plan değişikliği event-driven invalidation YOK (bkz MOD-0035)
- 🔴 Cache TTL strategy yok
- 🔴 Bulk operations
- 🔴 Audit instrumentation (bkz MOD-0021)

**Critical files:**
- Backend: `services/Diten.Platform/src/Diten.Platform.API/Controllers/Platform/TenantModuleEntitlementsController.cs`
- Backend: `services/Diten.Platform/src/Diten.Platform.Application/Services/TenantModuleAccessService.cs`
- Frontend: `frontend/Diten.Web/Views/Platform/Tenants/Commercial/_ModuleEntitlementsTab.cshtml`

---

### 3.6 Tenant Subscription Lifecycle
**ID:** MOD-0297
**Status:** 🟡 Partial (%82)
**Purpose:** Tenant subscription durumu (Trialing/Active/PastDue/Cancelled/Expired/Suspended), trial dönemi, period yönetimi.

**What's done:**
- ✅ TenantSubscription entity + history list
- ✅ TenantSubscriptionStatus enum
- ✅ Trial start/end fields
- ✅ Lifecycle command'ları (Suspend/Reactivate/Cancel)
- ✅ Frontend `_PlanSubscriptionTab.cshtml`

**What's missing:**
- 🔴 **Otomatik trial expiry worker YOK** (bkz MOD-0026)
- 🔴 PastDue → Suspended otomatik geçiş yok
- 🔴 Subscription renewal flow yok
- 🔴 CancelAtPeriodEnd worker yok
- 🔴 Subscription change → billing event emit yok (bkz MOD-0035, MOD-0299)
- 🔴 Dunning flow yok

**%100 için kalanlar:**
- [ ] MOD-0026 job scheduler ile trial expiry, renewal, cancel-at-period-end ve PastDue→Suspended job'ları eklenmeli.
- [ ] MOD-0035 event bus ile subscription change/billing/dunning event'leri yayınlanmalı.
- [ ] MOD-0299 billing entegrasyonu ile renewal, overdue ve invoice state transition kuralları bağlanmalı.
- [ ] MOD-0027 notification entegrasyonu ile dunning ve trial-ending mail akışları test edilmeli.

**Critical files:**
- Backend: `services/Diten.Platform/src/Diten.Platform.Domain/Entities/TenantSubscription.cs`
- Backend: `services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Commercial/Subscriptions/`
- Frontend: `frontend/Diten.Web/Views/Platform/Tenants/Commercial/_PlanSubscriptionTab.cshtml`

---

### 3.7 Tenant Login & Security Settings
**ID:** PSS-004
**Status:** 🟡 Partial (%86)
**Purpose:** Platform admin'in tenant başına login politikalarını yönetmesi (2FA/MFA/IP whitelist/session limit/login methods).

**What's done:**
- ✅ TenantLoginSettings entity (AuthService)
- ✅ Frontend Security.cshtml + Tagify integration
- ✅ Login method toggle, MFA, lockout
- ✅ Password policy override alanları ve AuthService runtime tüketimi mevcut

**What's missing:**
- 🔴 SSO/SAML/OIDC entegrasyonu
- 🔴 Audit instrumentation
- 🔴 IP whitelist runtime enforcement test
- 🟢 Lokalizasyon: en + tr (uyumlu)

**%100 için kalanlar:**
- [ ] IP whitelist, country allowlist, MFA ve lockout kuralları için AuthService integration testleri yazılmalı.
- [ ] SSO/SAML/OIDC sağlayıcı entegrasyonu veya açıkça sonraki faz kapsamına devretme kararı eklenmeli.
- [ ] Login setting değişiklikleri MOD-0021 ile audit'e düşmeli.
- [ ] Gateway smoke ve browser smoke testleri Security ekranındaki save/validation akışını doğrulamalı.

**Critical files:**
- Backend: `services/Diten.AuthService/src/Diten.AuthService.Domain/Entities/TenantLoginSettings.cs`
- Frontend: `frontend/Diten.Web/Views/Platform/Tenants/Security.cshtml`

---

### 3.8 Lookups / Reference Data
**Status:** 🟢 Done with caveats (%93) — PSS-011 (2026-05-14). Acceptance 14/14 ✅; unit testler yazılı (9 test, [PlatformLookupProviderTests.cs](../../services/Diten.Platform/tests/Diten.Platform.Application.Tests/Lookups/PlatformLookupProviderTests.cs)). Devredilen eksikler §9.4'te: **PSS-011-FU1** (HTTP integration infra), **PSS-011-FU2** (gateway smoke), **PSS-011-FU3** (sibling test compile fixes).
**Module Pack:** [`execution/domains/platform-shared-services/module-packs/PSS-011-lookups-reference-data.md`](../../execution/domains/platform-shared-services/module-packs/PSS-011-lookups-reference-data.md)
**Purpose:** Currency, Locale, Timezone, TenantTier, FeatureCategory, ModuleDomain/Service, SubscriptionCycle, Countries için merkezi lookup API'si.

**What's done:**
- ✅ Tek canonical `LookupOptionDto { code, name, value, group?, sortOrder?, metadata? }` shape
- ✅ Currency (ISO-4217), Locale (`en`,`tr`), Timezone (IANA, UTC dahil), TenantTier, SubscriptionCycle endpoint'leri
- ✅ Module-catalog domains/services + FeatureCategory + Countries (provisioning support)
- ✅ Caching: static lookups 12h TTL, feature-categories 5m + explicit invalidation
- ✅ Controller `[AllowAnonymous]` blanket kaldırıldı → `Platform.Lookups.Read` / `PlatformActor` policy
- ✅ Frontend hardcoded `USD/EUR/TRY/GBP` fallback ([SubscriptionPlansController.cs:250-279](../../frontend/Diten.Web/Controllers/Platform/SubscriptionPlansController.cs#L250-L279)) kaldırıldı, controlled empty state
- ✅ MediatR query/handler yapısı: `Features/Lookups/Queries` + `Handlers`
- ✅ Unit test coverage: 9 test (canonical shape + no-duplicates, locales `en`/`tr`, timezones UTC, countries Platform-provisioning scope, feature-categories Active source-of-record, unknown key 404, cache hit/miss factory-call=1, cache miss + exception → no partial entry, serialization camelCase + no `tenantId`/`id`)
- ✅ Feature-category lookup PSS-007 source-of-record bağı doğrulandı (`IFeatureCategoryRepository.GetAllAsync(status: Active)`)
- ⚠️ Eksik: HTTP-level integration testleri (`WebApplicationFactory`) + gateway smoke (`curl :5000`)
- ⚠️ Sibling test dosyalarındaki compile hataları nedeniyle test runner şu an çalıştırılamıyor (kapsam dışı issue)

**Consumers (downstream impact):**
- `SubscriptionPlansController` — currency dropdown
- `ModuleCatalogController` — domain/service dropdown (proxy)
- `TenantsController` + `Platform/Tenants/create.js`, `security.js` — country/currency/locale/timezone/tier
- Tüm Platform/Admin form ekranları — `/api/lookups/*` üzerinden

**Critical files:**
- Backend: `services/Diten.Platform/src/Diten.Platform.API/Controllers/LookupsController.cs`
- Application: `services/Diten.Platform/src/Diten.Platform.Application/Features/Lookups/`
- Gateway route: `gateway/Diten.ApiGateway/ocelot.json` (`/api/lookups/{everything}`)

**%100 için kalanlar:**
- [ ] `WebApplicationFactory` tabanlı HTTP integration testleri eklenmeli.
- [ ] Gateway üzerinden `/api/lookups/*` smoke testleri CI veya smoke runner'a bağlanmalı.
- [ ] Test project sibling compile hataları temizlenip lookup testleri tam runner içinde çalıştırılmalı.

---

### 3.9 Platform Admin Profile & Settings
**Status:** 🟠 In Progress (%89) — PSS-009 (2026-05-14, implement edildi)

**Kod kanıtı:**
- Backend: `Features/PlatformAccount/{Queries,Commands,Validators,Handlers}` tamamı oluşturuldu
- Controller: `[ApiController] [Route("api/platform/account")] [Authorize(Policy="PlatformActor")]` ✅
- Frontend: `Views/Platform/Account/{Profile,Settings,_AccountL10n}.cshtml` + `AccountIndex.cs`
- JS: `profile.js`, `settings.js`, `account.l10n.js`
- Layout: `currentUserInitials` logic ([_LayoutPlatformAdmin.cshtml:43-50](../../frontend/Diten.Web/Views/Shared/_LayoutPlatformAdmin.cshtml#L43-L50)) + dropdown linkleri canlı
- Hardcoded `avatars/1.png` referansı kaynak kodda kaldırıldı (yalnızca `obj/` build cache'inde)
- Gateway routes: `/api/platform/account` + `/api/platform/account/{everything}` ([ocelot.json:164-189](../../gateway/Diten.ApiGateway/ocelot.json))
- RESX: `AccountIndex.{en,tr}.resx`
- Tests: `PlatformAccountRulesTests.cs` (4 test — pack'in önerdiği 9 senaryonun bir bölümünü kapsar)

**Eksik %11:**
- ⚠️ Backend unit test coverage kısmi (4/9 senaryo)
- ⚠️ HTTP-level integration testleri yok (`WebApplicationFactory` infra yok — PSS-011 ile aynı sistemik eksik)
- ⚠️ Browser smoke otomasyon yok (manuel test gerekli)
**Module Pack:** [`execution/domains/platform-shared-services/module-packs/PSS-009-platform-admin-profile-settings.md`](../../execution/domains/platform-shared-services/module-packs/PSS-009-platform-admin-profile-settings.md)
**Purpose:** Şu an authenticated Platform/Admin kullanıcısının self-service profil görüntüleme ve sınırlı self-update yüzeyi (yalnızca `DisplayName`). Header'daki hardcoded avatar görselini deterministic initials ile değiştirir; user dropdown'daki "My Profile" / "Settings" linkleri gerçek sayfalara bağlanır.

**Scope (v1 — bilinçli olarak dar):**
- ✅ `/Platform/Account/Profile` + `/Platform/Account/Settings` sayfaları
- ✅ Header initials avatar (hardcoded `assets/img/avatars/1.png` kaldırılır)
- ✅ User dropdown linkleri canlı route'lara bağlanır
- ✅ Self-update: yalnızca `DisplayName` (2–200 char)
- ✅ Backend: `GET/PUT /api/platform/account/me`, `Features/PlatformAccount/`, `PlatformAccountController`

**Out of scope (kasıtlı yasaklar):**
- 🚫 Avatar upload, storage provider entegrasyonu
- 🚫 Password change (PSS-010 veya pack revision gerekli)
- 🚫 MFA / active sessions / security activity (PSS-010 kapsamı)
- 🚫 Fake activity timeline, social/teams/projects tabs
- 🚫 Email/username change flow
- 🚫 PreferredLocale / PreferredTimezone (PSS-011 lookup'larıyla follow-up)
- 🚫 Başka admin'i düzenleme (NEW-002 ownership)
- 🚫 Self-delete, account lifecycle removal
- 🚫 Sidebar nav item (dropdown'dan erişilir)

**Boundary kararları:**
- `NEW-002 Platform Administrators Management` admin lifecycle/roles/status sahibi olmaya devam eder
- `Diten.AuthService` password ownership v1 dışında
- Yalnızca current actor JWT claims'den çözülür; target ID parameter ASLA kabul edilmez

**Dependencies:**
- `NEW-002` → `PlatformAdministrator` entity'si (DisplayName alanı zaten mevcut)
- `_LayoutPlatformAdmin.cshtml` (yalnızca avatar/dropdown bölgesi)
- v1'de PSS-011 lookup bağımlılığı yok, AuthService bağımlılığı yok

**Follow-up Items** (§9.4'e eklendi):
- PSS-009-FU1: Avatar upload (storage provider sonrası)
- PSS-009-FU2: Activity timeline (MOD-0021 Audit Trail sonrası)
- PSS-009-FU3: Password change (PSS-010 ile)
- PSS-009-FU4: PreferredLocale/Timezone (PSS-011 endpoint'leri ile)

**%100 için kalanlar:**
- [ ] Backend unit coverage pack'teki 9 senaryoya tamamlanmalı.
- [ ] Profile/Settings GET/PUT için HTTP integration testleri eklenmeli.
- [ ] Header initials, dropdown navigation ve yasak UI surface yokluğu için browser smoke otomasyonu yazılmalı.
- [ ] Avatar/activity/password/preference follow-up'ları ilgili provider veya modüller hazır olunca ayrı acceptance ile tamamlanmalı.

---

### 3.10 Module Details Assignment Inspection
**ID:** PSS-008
**Status:** 🟡 Partial (%70)
**Purpose:** Module Catalog detail ekranında plan ve tenant assignment durumunu inceleme; modülün hangi planlarda/tenant'larda etkin olduğunu operasyonel olarak görebilme.

**What's done:**
- ✅ Module assignment overview, plan assignments, tenant assignments ve tenant assignment detail query/controller yüzeyleri mevcut.
- ✅ Module Catalog detail tarafında assignment inspection UI izleri mevcut.
- ✅ DataTable v2 kullanılan assignment/consumer tablo yüzeyleri mevcut.

**What's missing:**
- 🔴 Assignment integrity checks henüz MOD-0018 enforcement ile contract seviyesinde bağlanmadı.
- 🔴 Plan/tenant assignment diff ve drift uyarıları yok.
- 🔴 Assignment inspection için gateway smoke, browser smoke ve WebApplicationFactory integration testleri eksik.

**%100 için kalanlar:**
- [ ] PSS-008 module pack acceptance criteria tek tek repo kanıtlarıyla doğrulanmalı.
- [ ] MOD-0018 ve MOD-0298 ile assignment inspection sonuçları aynı entitlement contract'ını kullanmalı.
- [ ] Assignment drift/diff state'leri UI'da badge ve filter olarak gösterilmeli.
- [ ] Module details assignment endpointleri için integration test ve DataTable contract doğrulaması eklenmeli.

---

### 3.11 Platform Admin Password & MFA Security
**ID:** PSS-010
**Status:** 🟡 Partial (%45)
**Purpose:** Platform admin password reset/change, forced password change, MFA ve aktif session güvenliğini Platform/Admin kullanıcıları için tamamlamak.

**What's done:**
- ✅ Platform login, forced password change, forgot/reset password ve setup-token akışları AuthService tarafında mevcut.
- ✅ Platform password reset frontend endpointleri ve reset/change password view kullanımı mevcut.
- ✅ Refresh token/session primitive'leri mevcut; platform actor token claim'leri üretiliyor.

**What's missing:**
- 🔴 Platform admin MFA challenge/login flow tenant login MFA kadar tamam görünmüyor.
- 🔴 Active sessions ekranı, session revoke ve trusted device yönetimi yok.
- 🔴 Password/security activity audit feed'i PSS-009 profile/settings ile bağlanmadı.
- 🔴 Browser smoke, AuthService integration ve gateway smoke testleri eksik.

**%100 için kalanlar:**
- [ ] Platform actor için MFA enable/disable, challenge verify/resend ve recovery-code akışı netleştirilmeli.
- [ ] `/Platform/Account/Security` veya PSS-010'a ait ayrı security ekranı active sessions, revoke, password change ve MFA state göstermeli.
- [ ] Password reset/change/MFA/session revoke aksiyonları MOD-0021 audit'e düşmeli.
- [ ] AuthService integration testleri platform-admin-only boundary, token refresh ve forced-change akışlarını kapsamalı.
- [ ] PSS-009 içindeki password/MFA out-of-scope maddeleri PSS-010 tamamlanınca kapatılmalı.

---

## 4. YENİ MODÜLLER — WAVE 1 (Blocker Foundation)

> Wave 1 paralel 4 track'e bölünebilir: A=Tenant/Subscription, B=Catalog/RBAC/Entitlement, C=Job/Event, D=Notification.
> Wave 1-* = wave-bağımsız cross-cutting (her zaman önce yapılmalı).

---

### NEW-001 — Secrets Management
**Wave:** W1-* (BLOCKER, sıfırıncı)
**Priority:** 🔴 Blocker
**Status:** 🟡 Partial (%70) — legacy ID; canonical kayıt MOD-0012 Secrets & Configuration Vault
**Owner role:** DevOps / Security

**Purpose:**
JWT secret, API key, DB connection string, SMTP password gibi tüm hassas konfigürasyonun `appsettings.json`'dan çıkarılması; secrets manager üzerinden okunması.

**What it should do:**
- Production'da Azure Key Vault / AWS Secrets Manager / HashiCorp Vault adapter'ı
- Development'ta `appsettings.Development.json` veya `dotnet user-secrets`
- Configuration provider olarak inject edilebilmeli
- Secret rotation desteği (yeni secret okuma, eski geçerli kalsın)

**Implementation contract:**
```
ISecretsProvider
  Task<string> GetSecretAsync(string key, CancellationToken ct);
  Task<IReadOnlyDictionary<string, string>> GetSecretsAsync(string prefix, CancellationToken ct);
```
- DependencyInjection.cs'de `AddSecretsProvider()`
- Program.cs Configuration.Add(secretsConfigurationSource)
- Çıkarılacak secret'lar:
  - `JwtSettings:Secret`
  - `MongoDbSettings:ConnectionString`
  - `AuthService:InternalApiKey`
  - `Smtp:Password` (MOD-0027 için)
  - Storage provider credentials (MOD-0266 için)
  - Webhook signing keys (MOD-0034 için)

**Acceptance criteria:**
- [ ] `git grep "Secret" appsettings.json` → 0 hardcoded değer
- [ ] Production env var override testleri geçer
- [ ] Secret rotation testi (eski + yeni secret aynı anda geçerli)
- [ ] Boot-time secret eksikse fail-fast (silent fallback YOK)

**Anti-patterns to avoid:**
- ❌ Secret'ı log'a yazmak
- ❌ Default value fallback (`Secret ?? "change-me"`)
- ❌ Source tree'de gerçek production secret commit

**Dependencies:** Yok (her şeyden önce)

**%100 için kalanlar:**
- [ ] NEW-001 ve MOD-0012 tek canonical ID altında birleştirilmeli; eski ID yalnız legacy alias olarak kalmalı.
- [ ] Production adapter seçimi netleşmeli: Azure Key Vault, AWS Secrets Manager veya HashiCorp Vault.
- [ ] JWT/current+previous secret rotation integration testleri yazılmalı.
- [ ] `appsettings*.json` içindeki development dışı secret fallback'leri fail-fast doğrulamayla kapatılmalı.
- [ ] SMTP, storage, webhook ve internal API key secret gereksinimleri aynı provider üzerinden okunmalı.

---

### NEW-002 — Platform Administrators Management
**Wave:** W1-*
**Priority:** 🟠 High
**Status:** 🟢 Done with caveats (%95) — kod doğrulaması 2026-05-14.

**Kod kanıtı:**
- Domain: `PlatformAdministrator` entity, `PlatformAdministratorEnums`, `IPlatformAdministratorRepository`, `PlatformAdministratorSeed` ✅
- Application Features/PlatformAdministrators: **8 Command** (Invite, Update, AssignRoles, Suspend, Reactivate, Delete, BulkDelete, ResendInvite) + Queries + Validators + Handlers + Models + Parsing + PasswordGenerator ✅
- API: `AdministratorsController` (public) + `InternalPlatformAdministratorsController` ✅
- Infrastructure: `PlatformAdministratorProvisioningService`, `PlatformAdministratorInvitationEmailService` + email template ✅
- Frontend: Slim DataTable shell tam — `Index.cshtml`, `_DataTable`, `_Filter`, `_CreateEditOffcanvas`, `_DetailsQuickView`, `_IndexL10n` + Controller + ViewModels + RESX(en+tr) + JS ✅
- Gateway: `/api/platform/administrators` + `/api/platform/administrators/{everything}` ([ocelot.json:124-152](../../gateway/Diten.ApiGateway/ocelot.json#L124)) ✅

**Eksik %5:**
- ⚠️ Audit hookup — admin create/update/suspend → audit log. MOD-0021 artık %98; NEW-002 command'larına `IAuditableCommand` + `IAuditMetadataProvider` retrofit yapılabilir (NEW-002-FU1).
- ⚠️ Test coverage doğrulanmadı (PSS-009/PSS-011 ile aynı sistemik durum).
**Owner role:** Platform UI + Auth

**Purpose:**
"Kim platform admin?" sorusunun cevabı yok. Şu an sadece tenant başına admin kullanıcı var; platform-level admin'lerin CRUD'u eksik. Multi-partner senaryosunda kritik.

**Domain entities:**
> Not: Platform admin kaydı **cross-tenant**'tır (TenantId taşımaz). Bu yüzden `GlobalEntity` kullanılır, `EntityBase` değil.
```
PlatformAdministrator : GlobalEntity
{
  Email           : string (unique, normalized)
  DisplayName     : string
  ActorType       : enum { PlatformAdmin, PartnerAdmin }
  PartnerId       : Guid? (PartnerAdmin için zorunlu)
  AllowedTenantIds: List<Guid>? (PartnerAdmin için — boşsa hepsi)
  Status          : enum { Active, Suspended, Disabled }
  Roles           : List<string> (SuperAdmin, BillingAdmin, SupportAdmin, ReadOnly)
  LastLoginAtUtc  : DateTimeOffset?
  CreatedByAdmin  : Guid (audit)
}
```

**API endpoints:**
- `GET /api/platform/administrators` — list + filter
- `GET /api/platform/administrators/{id}` — detail
- `POST /api/platform/administrators` — create (email invite)
- `PUT /api/platform/administrators/{id}` — update
- `POST /api/platform/administrators/{id}/suspend`
- `POST /api/platform/administrators/{id}/reactivate`
- `DELETE /api/platform/administrators/{id}` — soft delete
- `POST /api/platform/administrators/{id}/roles` — role assign

**Frontend:**
- `/Platform/Administrators` — Index (DataTable + KPI: total, active, suspended)
- `/Platform/Administrators/Create` — invite form
- `/Platform/Administrators/{id}` — detail (roles, audit, allowed tenants)
- Tab: "Allowed Tenants" (PartnerAdmin için tenant scope picker)

**Permissions:**
- `Platform.Administrators.Read`
- `Platform.Administrators.Create`
- `Platform.Administrators.Update`
- `Platform.Administrators.Suspend`
- `Platform.Administrators.AssignRoles`

**Acceptance criteria:**
- [ ] Platform admin CRUD complete + soft delete
- [ ] Invite email gönderimi (MOD-0027 ile entegre)
- [ ] PartnerAdmin için tenant scope filter aktif
- [ ] Audit log (MOD-0021): kim kim eklemiş, rolünü değiştirmiş
- [ ] Lokalizasyon: en + tr (Platform standardı)
- [ ] DataTable v2 + concurrency control

**Dependencies:** NEW-001 (secrets), MOD-0027 (notification — invite email)

---

### MOD-0009 — Tenant Registry Lifecycle Events
**Wave:** W1-A
**Priority:** 🔴 Blocker
**Status:** 🟡 Partial (%50) — Tenant entity/lifecycle var, event emit ve bus entegrasyonu yok

**Purpose:**
Tenant lifecycle değişikliklerini (created/activated/suspended/reactivated/cancelled) event olarak yayınla; subscription, provisioning, notification modülleri bu event'leri dinlesin.

**What it should do:**
- Her lifecycle command handler'ında `IEventBus.PublishAsync(...)` çağrısı
- Event payload contract'ı stabilize et
- Idempotent event consumer pattern'i destekle

**Event contracts:**
```
TenantCreated         { TenantId, CreatedAtUtc, PlanId, CreatedBy }
TenantActivated       { TenantId, ActivatedAtUtc, ActivatedBy }
TenantSuspended       { TenantId, SuspendedAtUtc, Reason, SuspendedBy }
TenantReactivated     { TenantId, ReactivatedAtUtc, ReactivatedBy }
TenantCancelled       { TenantId, CancelledAtUtc, EffectiveAtUtc, Reason }
TenantProvisioningCompleted { TenantId, Steps[] }
TenantProvisioningFailed    { TenantId, FailedStep, Error }
```

**Acceptance criteria:**
- [ ] 7 event tipi tanımlı + handler'larda emit ediliyor
- [ ] Subscription, notification, audit modülleri bunlara subscribe oluyor
- [ ] Event'ler audit log'a yazılıyor (correlation id ile)
- [ ] At-least-once delivery garantisi
- [ ] Test: tenant suspend → notification email tetiklendiği integration test

**Dependencies:** MOD-0035 (Event Bus)

**%100 için kalanlar:**
- [ ] Tenant lifecycle command'ları `TenantCreated/Activated/Suspended/Reactivated/Cancelled` event'lerini outbox'a yazmalı.
- [ ] MOD-0035 event bus ve idempotent consumer altyapısı tamamlanmalı.
- [ ] Subscription, notification, provisioning ve audit tüketicileri contract testleriyle bağlanmalı.
- [ ] Tenant suspend/reactivate/cancel akışları için gateway smoke + integration test eklenmeli.

---

### MOD-0008 — Module Catalog Assignable Expose
**Wave:** W1-B
**Priority:** 🔴 Blocker
**Status:** 🟡 Partial (%80)

**Purpose:**
RBAC enforcement (MOD-0018) ve entitlement service (MOD-0298) için "tenant'a atanabilir modüller" contract'ını stabilize et.

**What it should do (eksik kısım):**
- `IModuleCatalogQueryService.GetAssignableModulesAsync()` — pure read service
- Caching layer (modül kataloğu nadiren değişir)
- Module dependency graph (HR → OrgHierarchy gibi)
- Module compatibility (version range)
- Contract testler (MOD-0018 ile)

**Acceptance criteria:**
- [ ] Public contract `IPlatformCatalogContract` (Common library)
- [ ] Cache invalidation: modül create/update/deactivate → cache clear
- [ ] Module dependency: HR isteyince OrgHierarchy otomatik dahil edilebilir
- [ ] Versioning: Module v1.0.0 → v2.0.0 migration metadata

**Dependencies:** PSS-005 (mevcut)

---

### MOD-0014 — Module Boundary Registry
**Wave:** W1-B
**Priority:** 🟠 High
**Status:** 🟡 Partial (%20) — module pack mevcut; gerçek runtime/UI yüzeyi sınırlı

**Purpose:**
Domain, suite, capability ve module boundary kararlarını canonical registry olarak tutmak; yeni modüllerin ownership, dependency ve anti-duplication kontrollerine temel sağlamak.

**What's done:**
- ✅ Module pack ve kapsam tanımı mevcut.
- 🟡 Module Catalog/Interface Registry tarafında boundary bilgisini besleyebilecek bazı altyapı izleri var.

**What's missing:**
- 🔴 Boundary registry entity/API/UI kontratı tamam değil.
- 🔴 Module pack validation veya CI check ile boundary ownership enforce edilmiyor.
- 🔴 Interface Registry, Module Catalog ve future Data Contract Registry ile ilişki net değil.

**%100 için kalanlar:**
- [ ] Domain/Suite/Capability/Module boundary entity ve repository kontratı netleşmeli.
- [ ] Module pack authoring sırasında boundary registry lookup ve duplicate ownership check kullanılmalı.
- [ ] Module Catalog ve Interface Registry kayıtları boundary registry ID'leriyle ilişkilendirilmeli.
- [ ] CI veya validation script'i aynı capability/module ownership çakışmasını yakalamalı.
- [ ] Platform/Admin UI'da boundary list/detail ve ownership inspection ekranı eklenmeli.

---

### MOD-0018 — RBAC / Tenant Entitlement Enforcement
**Wave:** W1-B
**Priority:** 🔴 Blocker
**Status:** 🟡 Partial (%20 — HasPermission + entitlement read service izleri var; birleşik enforcement yok)

**Purpose:**
Permission alone yeterli değil; o permission ait olduğu modül **tenant'a açık olmadıkça** authorization fail vermeli.

**What it should do:**
- `[RequiresPermission("X")]` mevcut
- Yeni: `[RequiresModule("ModuleCode")]` — kullanıcının tenant'ında bu modül entitled değilse 403
- Birleşik: `[RequiresPermission("HR.Read", Module="HR")]`
- Authorization handler:
  1. Permission claim check
  2. `ITenantModuleAccessService.IsEntitledAsync(tenantId, moduleCode)`
  3. EffectiveAccess.Active veya EnabledByOverride değilse 403
- Feature-level enforcement: `[RequiresFeature("ADVANCED_REPORTING")]`

**Implementation contract:**
```
IEntitlementChecker
  Task<bool> IsModuleEntitledAsync(Guid tenantId, string moduleCode, CancellationToken ct);
  Task<bool> IsFeatureEnabledAsync(Guid tenantId, string featureCode, CancellationToken ct);
  Task<EntitlementCheckResult> CheckBatchAsync(...);
```

**Acceptance criteria:**
- [ ] `[RequiresModule]` ve `[RequiresFeature]` attribute'ları aktif
- [ ] Entitlement değişikliğinde cache invalidate (event-driven, MOD-0035)
- [ ] Tenant tarafındaki en az 3 controller'a uygulandı (sample)
- [ ] Audit: deny edilen access'ler log'lanıyor
- [ ] Performance: <5ms p99 (cached)

**Dependencies:** PSS-007 (mevcut), MOD-0298 (mevcut), MOD-0035 (Event Bus), MOD-0021 (Audit)

**%100 için kalanlar:**
- [ ] `[RequiresModule]`, `[RequiresFeature]` ve birleşik permission+module enforcement attribute'ları eklenmeli.
- [ ] `IEntitlementChecker` cache, batch check ve deny reason contract'ı ile tamamlanmalı.
- [ ] Entitlement değişiklikleri MOD-0035 event'leriyle cache invalidate etmeli.
- [ ] Deny edilen access denemeleri MOD-0021 audit trail'e yazılmalı.
- [ ] En az 3 tenant-side controller ve 1 platform internal endpoint üzerinde integration test yazılmalı.

---

### MOD-0026 — Background Job Scheduler
**Wave:** W1-C
**Priority:** 🔴 Blocker
**Status:** 🔴 Missing (0%)

**Purpose:**
Trial expiry, subscription renewal, quota reset, email dispatch, provisioning retry gibi periyodik/asenkron işlemler için generic background job altyapısı.

**Recommendation:** Hangfire (dashboard'lı, MongoDB destekli) veya Quartz.NET.

**What it should do:**
- Job registration (fire-and-forget, scheduled, recurring)
- Retry policy (exponential backoff)
- Dead letter queue
- Job dashboard (auth'lu)
- Distributed lock (multi-instance deployment için)

**Standard jobs (initial set):**
| Job | Schedule | Modül |
|---|---|---|
| TrialExpiryScanJob | Daily 02:00 UTC | MOD-0297 |
| SubscriptionRenewalJob | Daily 03:00 UTC | MOD-0297, MOD-0299 |
| QuotaResetJob | Monthly | MOD-0033 |
| EntitlementCacheRefreshJob | Hourly | MOD-0018 |
| WebhookRetryJob | Every 5min | MOD-0034 |
| AuditLogArchiveJob | Weekly | MOD-0021 |
| EmailDispatchJob | Every 1min | MOD-0027 |
| ProvisioningRetryJob | Every 2min | MOD-0009 |

**Acceptance criteria:**
- [ ] IBackgroundJobClient inject edilebilir
- [ ] Hangfire dashboard `[Authorize(Policy="PlatformActor")]`
- [ ] 8 standart job kayıtlı + test edilmiş
- [ ] Job failure → Audit log + Alert (MOD-0042)
- [ ] Multi-instance distributed lock

**Dependencies:** NEW-001 (secrets), MOD-0035 (events for job triggers)

---

### MOD-0035 — Event Bus / Internal Events
**Wave:** W1-C
**Priority:** 🔴 Blocker
**Status:** 🔴 Missing (0%)

**Purpose:**
Tenant lifecycle, subscription change, entitlement change gibi domain event'lerinin servisler arası transport'u.

**Recommendation:** MassTransit + RabbitMQ (production), in-memory bus (dev).

**What it should do:**
- Publish/subscribe pattern
- Consumer registration
- Retry + dead letter
- Idempotency (correlation id)
- Outbox pattern (domain commit ile event emit atomic)

**Contract:**
```
IEventBus
  Task PublishAsync<T>(T @event, CancellationToken ct) where T : IDomainEvent;

IEventHandler<T>
  Task HandleAsync(T @event, CancellationToken ct);
```

**Event catalog (initial):** Bkz MOD-0009 + ileride MOD-0038 (taxonomy).

**Acceptance criteria:**
- [ ] Outbox pattern (MongoDB collection: `outbox_events`)
- [ ] Outbox publisher worker
- [ ] At-least-once delivery
- [ ] Idempotency check on consumer side
- [ ] Dev mode in-memory bus
- [ ] Production RabbitMQ/Azure Service Bus adapter

**Anti-patterns to avoid:**
- ❌ Direct method call inter-service yerine event
- ❌ Event payload'ında full entity (sadece ID + temel field)
- ❌ Sync wait for event handler result

**Dependencies:** NEW-001

---

### MOD-0027 — Notification / Email Service
**Wave:** W1-D
**Priority:** 🔴 Blocker
**Status:** 🔴 Missing (0%)

**Purpose:**
Platform-issued transactional notification orchestration: invite, OTP, password reset, trial ending/expired, subscription renewal, payment failed.

**What it should do:**
- Notification template registry (DB-backed, bkz NEW-003)
- Channel: email (W1), SMS/push (later)
- Template rendering (Liquid veya Scriban — Razor değil, security için)
- Locale-aware (kullanıcının dilinde gönder)
- Throttling (aynı kişiye 1 dk içinde 5+ aynı tip mail gönderme)
- Retry via MOD-0026

**Domain entities:**
```
NotificationTemplate : EntityBase
{
  TemplateKey    : string (e.g. "tenant.invite.email") unique
  Channel        : enum { Email, Sms, Push, InApp }
  Locale         : string ("en", "tr", ...)
  Subject        : string (email için)
  BodyTemplate   : string (Liquid syntax)
  BodyFormat     : enum { Html, Text, Markdown }
  Variables      : List<string> (template'te kullanılan değişkenler)
  IsActive       : bool
  Version        : int
}

NotificationDispatch : EntityBase
{
  TemplateKey    : string
  RecipientType  : enum { PlatformAdmin, TenantUser, ExternalEmail }
  RecipientId    : Guid?
  RecipientEmail : string
  Locale         : string
  Variables      : Dictionary<string, object> (template'e bind)
  Status         : enum { Queued, Sent, Failed, Bounced }
  AttemptCount   : int
  SentAtUtc      : DateTimeOffset?
  CorrelationId  : Guid
}
```

**API endpoints:**
- Internal-only: `INotificationService.SendAsync(templateKey, recipient, variables)`
- Admin: `GET /api/platform/notifications/dispatches` — audit/log view

**Standard templates (initial set):**
- `platform.admin.invite`
- `tenant.invite.email`
- `tenant.welcome`
- `tenant.trial.ending.7days`
- `tenant.trial.ending.3days`
- `tenant.trial.expired`
- `tenant.subscription.renewed`
- `tenant.subscription.payment.failed`
- `tenant.subscription.suspended`
- `tenant.password.reset`
- `tenant.otp.code`

**Acceptance criteria:**
- [ ] Template registry CRUD + locale fallback (tr yoksa en kullan)
- [ ] Liquid template engine entegre
- [ ] Throttling aktif
- [ ] Bounce/failure handling
- [ ] Audit log (MOD-0021) entegrasyonu
- [ ] Variables validation: template'te kullanılmayan değişken pass edilirse warn

**Dependencies:** NEW-001, MOD-0263 (provider), MOD-0026 (retry), MOD-0035 (event-driven dispatch)

---

### MOD-0263 — External Messaging Provider Adapter
**Wave:** W1-D
**Priority:** 🔴 Blocker
**Status:** 🔴 Missing (0%)

**Purpose:**
SMTP/SendGrid/Twilio gibi mesajlaşma sağlayıcılarını MOD-0027 ardına gizlemek; provider lock-in'i önlemek.

**Contract:**
```
IMessagingProvider
  Task<DispatchResult> SendEmailAsync(EmailMessage msg, CancellationToken ct);
  Task<DispatchResult> SendSmsAsync(SmsMessage msg, CancellationToken ct);
  string ProviderName { get; }

DispatchResult { Success: bool, ProviderMessageId: string?, Error: string? }
```

**Adapter implementations:**
- `SmtpEmailProvider` (System.Net.Mail veya MailKit)
- `SendGridEmailProvider` (HTTP API)
- `TwilioSmsProvider` (HTTP API)
- `FakeProvider` (dev mode — log only)

**Configuration:**
- `Messaging:DefaultEmailProvider: "Smtp" | "SendGrid"`
- Provider seçimi runtime (factory pattern)

**Acceptance criteria:**
- [ ] En az 2 email provider çalışıyor (SMTP + SendGrid)
- [ ] Provider failure → fallback to secondary
- [ ] Webhook handling (SendGrid bounce events)
- [ ] Rate limit per-provider awareness
- [ ] Credentials secrets manager'dan (NEW-001)

**Dependencies:** NEW-001

---

## 5. YENİ MODÜLLER — WAVE 2 (High Priority Operations)

### MOD-0028 — Document / Evidence Metadata
**Wave:** W2-A
**Priority:** 🟠 High
**Status:** 🔴 Missing (0%)

**Purpose:**
Document metadata SoR — versioning, soft-delete, tenant scope, evidence links. Platform tarafında tenant logo, branding asset gibi platform-scoped document'lar için.

**Domain entities:**
```
DocumentMetadata : EntityBase
{
  TenantId       : Guid? (platform-scoped ise null)
  DocumentKey    : string (unique within scope)
  FileName       : string
  ContentType    : string
  SizeBytes      : long
  StorageKey     : string (MOD-0266 provider key)
  Version        : int
  ParentDocId    : Guid? (versioning chain)
  EvidenceLinks  : List<EvidenceLink>
  UploadedBy     : Guid
  RowVersion     : byte[]
}
```

**API endpoints:**
- `POST /api/platform/documents` — upload (multipart)
- `GET /api/platform/documents/{id}` — metadata
- `GET /api/platform/documents/{id}/download` — stream
- `DELETE /api/platform/documents/{id}` — soft delete

**Acceptance criteria:**
- [ ] Tenant logo/favicon base64'ten DocumentMetadata'ya migrate
- [ ] Version chain query (give me v3 of this doc)
- [ ] Evidence link bidirectional
- [ ] Storage seam ile decouple (MOD-0266)

**Dependencies:** MOD-0266

---

### MOD-0266 — Blob / File Storage Provider
**Wave:** W2-A
**Priority:** 🟠 High
**Status:** 🔴 Missing (0%)

**Purpose:**
Storage provider seam — Local (dev), S3, MinIO, Azure Blob desteği.

**Contract:**
```
IBlobStorageProvider
  Task<StorageKey> UploadAsync(Stream content, BlobMetadata meta, CancellationToken ct);
  Task<Stream> DownloadAsync(StorageKey key, CancellationToken ct);
  Task<string> GetSignedUrlAsync(StorageKey key, TimeSpan ttl);
  Task DeleteAsync(StorageKey key);
  Task<bool> ExistsAsync(StorageKey key);
```

**Adapter implementations:**
- `LocalFileSystemProvider` (dev)
- `S3CompatibleProvider` (AWS S3 + MinIO)
- `AzureBlobProvider`

**Acceptance criteria:**
- [ ] Tenant logo eski base64 storage'dan migrate edildi
- [ ] Signed URL desteği (security için)
- [ ] Provider failover (primary down → secondary)
- [ ] Configuration secrets manager'dan

**Dependencies:** NEW-001

---

### MOD-0262 — External Document Provider
**Wave:** W2-A / Later
**Priority:** 🟠 High
**Status:** 🔴 Missing (0%)

**Purpose:**
SharePoint, Google Drive, Dropbox gibi external document repository connector'ları.

**Scope:** Platform-side için bu likely later. Tenant-side ERP modüllerinde lazım olacak. **MVP-priority değil.**

**Skip if:** ERP modülleri başlamadıkça gerek yok.

---

### MOD-0021 — General Audit Trail
**Wave:** W2-B
**Priority:** 🟠 High (compliance varsa → P0)
**Status:** 🟢 Done with caveats (%98) — 2026-05-14/15 Phase 1-5B + 5C (H1/H2/H3/H4) implemented; kalanlar partner scope, smoke ve carry-over hardening
**Module Pack:** [`execution/domains/platform-shared-services/module-packs/MOD-0021-general-audit-trail.md`](../../execution/domains/platform-shared-services/module-packs/MOD-0021-general-audit-trail.md) (ready-for-dev, Tier 2)

**Phase kanıtları:**
- **Phase 1 — Persistence Foundation ✅**: `AuditEvent` (`BaseEntity`, immutable), `AuditEventRetentionPolicy` (`GlobalEntity`), `TenantAuditPreference` (`BaseEntity`), `AuditOutboxMessage`. Domain enums (`AuditCategory`, `AuditOperation`, `AuditOutcome`, `AuditActorType`, `AuditRedactionStatus`). 3 repository + seed (idempotent) + `AuditTenantIds.PlatformSystemTenantId`. 7 unit test (AuditPersistenceFoundationTests).
- **Phase 2 — Application Core ✅**: `IAuditService` + `AuditService` (opt-in marker, recursion guard, idempotency key), `SensitiveFieldRedactor` (nested + case-insensitive + 18 rules), `SensitiveFieldRedactionRegistry`, `AuditIdempotencyKeyBuilder` (SHA256), `AuditRecursionGuard` (AsyncLocal), `AuditRetentionPolicyResolver` (Default fallback + clamp), `AuditOutboxRepository` (IAuditOutboxWriter). 11 unit test (AuditApplicationCoreTests). H1 fix uygulandı (`AuditAppendResult.Rejected` default `ShouldBreakBusinessCommand=false`).
- **Phase 3 — AuditBehavior ✅**: `IAuditableRequest`/`IAuditableCommand`/`IAuditExcludedRequest` markers, `IAuditMetadataProvider`, `AuditRequestMetadata`, `AuditBehaviorOptions` (CriticalCategories + RequireMetadataProvider + auto-exclude narrow list), `AuditBehavior<TRequest,TResponse>` MediatR pipeline. Pipeline sırası: Validation→Logging→Exception→Audit→Performance. 16 unit test (AuditBehaviorTests). H1+H2 fix uygulandı (narrow auto-exclude + RequireMetadataProvider default true).
- **Phase 4 — Worker/Persistence ✅**: `AuditOutboxWorker : BackgroundService`, `AuditOutboxProcessor` (atomic claim via FindOneAndUpdate, stale-Processing recovery 5min, retry backoff, DeadLetter), `AuditOutboxPayloadMapper` (Bson normalization, controlled mapping exception), `SafeAuditErrorFormatter`, `AuditOutboxWorkerOptions` (BatchSize=25, MaxAttempts=5, ProcessingStaleAfter=5min). C1+H1 fix uygulandı (TenantScope snapshot+restore + Processing stuck recovery). 9 worker test + 6 TenantScope test + 10 ClaimEligibility contract test.
- **Phase 5A — Backend API ✅**: `PlatformAuditController` `[Authorize(Policy="PlatformAdminOnly")]` (Seçenek A: partner_admin engellendi). 5 endpoint (events list/detail/export/retention PUT/redact-actor). Permissions: `Platform.Audit.Read/Export/Retention.Update/RedactActor`. `IAuditMetaAuditWriter` (recursion guard scoped). Validators (ExportAuditEventsValidator, UpdateAuditRetentionValidator, RedactAuditActorValidator). Gateway routes ([ocelot.json](../../gateway/Diten.ApiGateway/ocelot.json) 5 route) integration-agent tarafından eklendi.
- **Phase 5B — Frontend UI ⚠️ (%92 fonksiyonel, 4 compliance boşluk)**: `/Platform/AuditLog` DataTable v2 + advanced filter + detail modal + CSV/JSON export. `/Platform/AuditRetention` PUT form. Frontend `PlatformAuditController` same-origin proxy. RESX en+tr (98+37 keys). `_LayoutPlatformAdmin` integration ✅.

**Phase 5C durumu (2026-05-15):**
- ✅ **H1 DONE**: Retention sayfası mevcut policy'leri GET ile yüklüyor — backend `GET /api/platform/audit/retention` + frontend `AuditRetention/index.js` load flow
- ✅ **H2 DONE**: Redact-actor UI eklendi — `index.js:505+` modal HTML (Description, Warning, Find Actor, ActorId input, Affected Records preview); `_IndexL10n.cshtml` RedactActor.* localization key'leri (en+tr)
- ✅ **H3 DONE**: Sidebar navigation entry eklendi — `_LayoutPlatformAdmin.cshtml:236-245` AuditLog + AuditRetention menü item'ları, active-state highlighting
- ✅ **H4 DONE**: `_DetailsModal.cshtml` ayrı partial'a taşındı (47 satır); Index.cshtml'de inline modal kalmadı

**Kritik files:**
- Backend: `services/Diten.Platform/src/Diten.Platform.API/Controllers/Platform/PlatformAuditController.cs`
- Application: `services/Diten.Platform/src/Diten.Platform.Application/Features/Audit/` + `Contracts/Audit/` + `Contracts/Behaviors/AuditBehavior.cs`
- Infrastructure: `services/Diten.Platform/src/Diten.Platform.Infrastructure/Services/Audit/` + `Persistence/Repositories/Audit*.cs`
- Frontend: `frontend/Diten.Web/Views/Platform/{AuditLog,AuditRetention}/` + `wwwroot/assets/js/Platform/{AuditLog,AuditRetention}/`
- Gateway: `gateway/Diten.ApiGateway/ocelot.json` (5 audit route)

**Purpose:**
Tüm platform-significant operation'lar için generic audit trail. Compliance, forensics, support investigation.

**Domain entity:**
```
AuditEvent : EntityBase
{
  CorrelationId  : Guid
  ActorType      : enum { PlatformAdmin, PartnerAdmin, TenantUser, System }
  ActorId        : Guid
  ActorEmail     : string (denormalized)
  TenantId       : Guid? (target tenant — platform admin için cross-tenant)
  EntityType     : string ("Tenant", "SubscriptionPlan", "ModuleCatalog", ...)
  EntityId       : Guid
  Operation      : enum { Create, Update, Delete, Activate, Deactivate, Suspend, Reactivate, ... }
  BeforeState    : BsonDocument? (önceki değer — sensitive field redacted)
  AfterState     : BsonDocument?
  IpAddress      : string
  UserAgent      : string
  OccurredAtUtc  : DateTimeOffset
  Metadata       : Dictionary<string, object> (free-form)
}
```

**Implementation:**
- `IAuditService.RecordAsync(...)` — async fire-and-forget
- MediatR `AuditBehavior` pipeline behavior — her command otomatik audit log'la
- Sensitive field redaction (Password, Secret, Token)
- Read-only viewer UI

**API endpoints:**
- `GET /api/platform/audit/events` — filter (date, actor, tenant, entity, operation)
- `GET /api/platform/audit/events/{id}` — detail with before/after diff
- `GET /api/platform/audit/export` — CSV/JSON export

**Frontend:**
- `/Platform/AuditLog` — DataTable + advanced filter
- Detail modal with diff visualization

**Acceptance criteria:**
- [ ] AuditBehavior pipeline behavior aktif tüm command'larda
- [ ] Read-only viewer UI (filter + diff + export)
- [ ] Sensitive field redaction (whitelist of fields)
- [ ] Retention policy (örn. 2 yıl sonra archive)
- [ ] Index'ler: ActorId, TenantId, EntityType, OccurredAtUtc

**Anti-patterns to avoid:**
- ❌ Audit yazımı sync — command response'unu yavaşlatır
- ❌ Password/Secret field'larını log'a yazmak
- ❌ Audit log'u silinebilir yapmak

**Dependencies:** NEW-001 (audit log'a yazılan IP/UA güvenli işlenmeli)

**%100 için kalanlar:**
- [ ] partner_admin audit scope desteği eklenmeli: per-tenant filter, partner-scoped export ve partner-scoped redaction.
- [ ] Audit endpointleri için WebApplicationFactory integration testleri ve gateway smoke testleri eklenmeli.
- [ ] Kritik mevcut command'lara `IAuditableCommand` + `IAuditMetadataProvider` retrofit yapılmalı (NEW-002, tenant lifecycle, entitlement, subscription).
- [ ] Plan bazlı audit retention policy follow-up'ı ayrı scope olarak tasarlanmalı: policy ownership MOD-0021 Audit Retention'da kalmalı, Subscription Plan Catalog yalnızca plan lookup/SSOT olarak tüketilmeli, Default policy fallback korunmalı ve plan bazlı policy olmayan tenant'ların mevcut davranışı bozulmamalı.
- [ ] Carry-over hardening listesi kapatılmalı: CSRF token, export streaming, CSV multiline, page-size sınırı, recursion guard depth limit, idempotency drift testleri.

---

### MOD-0287 — User Notification Preferences
**Wave:** W2-C
**Priority:** 🟠 High
**Status:** 🔴 Missing (0%)

**Purpose:**
Kullanıcı bazlı notification tercihleri — channel (email/sms/push), frequency (immediate/daily/weekly), opt-in/opt-out.

**Scope note:** Bu modül tenant kullanıcısı için. Platform tarafında çerçeve kurulur, tenant tarafı kullanır. Platform admin UI'sı şart değil.

**Domain entity:**
```
NotificationPreference : EntityBase
{
  UserId          : Guid
  TenantId        : Guid? (platform admin için null)
  NotificationType: string (e.g. "TrialEnding", "SubscriptionRenewed")
  Channels        : List<enum {Email, Sms, Push, InApp}>
  Frequency       : enum { Immediate, Daily, Weekly, Disabled }
  IsOptIn         : bool (mandatory notification'lar için override edilemez)
}
```

**Acceptance criteria:**
- [ ] User-level preference CRUD
- [ ] Tenant-level default preference
- [ ] Platform-level mandatory notification list (opt-out edilemez)
- [ ] MOD-0027 notification dispatch'i bunu check ediyor

**Dependencies:** MOD-0027

---

### MOD-0034 — Webhook Delivery
**Wave:** W2-C / W3
**Priority:** 🟠 High
**Status:** 🔴 Missing (0%)

**Purpose:**
Outbound integration — tenant'ın kendi sistemine event push edebilmesi.

**Domain entities:**
```
WebhookSubscription : EntityBase
{
  TenantId        : Guid
  Url             : string
  SecretKey       : string (HMAC signing)
  EventTypes      : List<string>
  IsActive        : bool
  HeadersOverride : Dictionary<string,string>?
}

WebhookDelivery : EntityBase
{
  SubscriptionId  : Guid
  EventType       : string
  Payload         : BsonDocument
  AttemptCount    : int
  Status          : enum { Pending, Delivered, Failed, Abandoned }
  ResponseCode    : int?
  ResponseBody    : string? (truncated)
  NextAttemptAt   : DateTimeOffset?
}
```

**Acceptance criteria:**
- [ ] HMAC signing (X-Webhook-Signature header)
- [ ] Retry policy (exponential backoff, max 5 attempts)
- [ ] Delivery log + viewer UI
- [ ] Replay functionality
- [ ] Signature secret rotation

**Dependencies:** MOD-0026 (retry job), MOD-0035 (event source), NEW-001 (secrets)

---

### NEW-003 — Notification Template Management UI
**Wave:** W2-D
**Priority:** 🟠 High
**Status:** 🔴 Missing (0%)

**Purpose:**
MOD-0027'deki NotificationTemplate'lerin platform admin tarafından UI üzerinden yönetilmesi (görmek, edit etmek, preview, locale eklemek, A/B test).

**What it should do:**
- Template list (filtered by channel, locale)
- Template editor (Monaco editor veya CodeMirror)
- Variable autocomplete
- Live preview (sample data ile render)
- Locale fork (en template'inden tr fork et)
- Version history (her save → new version)
- Template activate/deactivate

**Frontend:**
- `/Platform/NotificationTemplates` — Index
- `/Platform/NotificationTemplates/{key}` — edit + preview
- `/Platform/NotificationTemplates/{key}/versions` — history

**API endpoints:**
- `GET /api/platform/notification-templates`
- `GET /api/platform/notification-templates/{key}`
- `POST /api/platform/notification-templates/{key}/render` — preview render
- `POST /api/platform/notification-templates/{key}/test-send` — test email
- `PUT /api/platform/notification-templates/{key}`

**Acceptance criteria:**
- [ ] Template CRUD + version history
- [ ] Live preview (sample variables ile)
- [ ] Test-send (kendi email'ine gönder)
- [ ] Locale management
- [ ] Lokalizasyon: en + tr

**Dependencies:** MOD-0027

---

### NEW-004 — Tenant Impersonation / Support Tooling
**Wave:** W2-D
**Priority:** 🟡 Medium
**Status:** 🔴 Missing (0%)

**Purpose:**
Platform admin support için "şu tenant'a benim olarak gir, sorununu gör" özelliği. Tüm impersonation aksiyonları audit'lenir.

**What it should do:**
- Platform admin "Impersonate" butonu tenant detail'de
- Yeni JWT token: `actor_type=platform_admin`, `impersonating_tenant_id=X`, `impersonated_user_id=Y` claim'leri
- Banner: "ŞU AN [tenant_name] olarak inceliyorsun — destek modu"
- Tüm aksiyonlar audit'te `IsImpersonation=true` flag
- Time-limited (max 30 dakika)
- Bazı destructive aksiyonlar disabled

**Acceptance criteria:**
- [ ] Impersonate başlat/bitir
- [ ] Visual banner her sayfada
- [ ] Audit log impersonation flag ile
- [ ] Permission: `Platform.Tenants.Impersonate`
- [ ] Time-limit + auto-exit
- [ ] Destructive operation block list

**Dependencies:** MOD-0021 (audit), Auth (JWT token override)

---

## 6. YENİ MODÜLLER — WAVE 3 (Production Hardening)

### MOD-0032 — API Gateway Hardening
**Wave:** W3-A
**Priority:** 🟠 High
**Status:** 🟡 Partial (%50) — Ocelot routing, token forward, CORS whitelist başlangıcı ve secret validation var; hardening tamam değil

**Purpose:**
Gateway-level rate limit, per-tenant policies, CORS hardening, API versioning routing, request/response audit.

**What's done:**
- ✅ Ocelot routing
- ✅ Auth token forward
- ✅ Gateway JWT secret validation ve local CORS origin whitelist başlangıcı var

**What's missing:**
- 🔴 Rate limiting (per-tenant, per-endpoint)
- 🟡 CORS whitelist environment-aware hale getirilmeli
- 🔴 API version routing (`/api/v1/...`)
- 🔴 Request audit (incoming, outgoing)
- 🔴 Circuit breaker
- 🔴 IP allowlist/denylist
- 🔴 Frontend'in gateway dışı service fallback'leri kaldırılmalı

**Acceptance criteria:**
- [ ] Per-tenant rate limit (e.g. 1000 req/min)
- [ ] CORS environment-aware whitelist
- [ ] API versioning route convention
- [ ] Request audit middleware
- [ ] Polly circuit breaker upstream'a

**%100 için kalanlar:**
- [ ] Frontend proxy'lerinde `PlatformServiceUrl`/direct service-port fallback kaldırılmalı; tüm çağrılar Gateway (5000) üzerinden geçmeli.
- [ ] Rate limiting, circuit breaker, retry/timeout ve upstream health policy'leri Ocelot/Polly üzerinden test edilmeli.
- [ ] CORS origin listesi environment config'e taşınmalı; production'da wildcard kullanımı testle engellenmeli.
- [ ] Gateway smoke runner tüm platform route grupları için 200/401/403/404 beklentilerini doğrulamalı.

---

### MOD-0033 — Consumer / Quota Model
**Wave:** W3-A
**Priority:** 🟠 High
**Status:** 🟡 Partial (%78) — QuotaUsage + QuotaEvent entities, QuotasController + InternalQuotasController, Application Features/Quotas, atomic consume ve reset/recalculate command'ları var. Scheduler, dashboard ve notification pending.

**Purpose:**
Plan'da tanımlı quota'ların (users, storage, API calls) runtime enforcement.

**Domain entities:**
```
QuotaDefinition (Plan level — SubscriptionPlan.DefaultQuotas içinde var)

QuotaUsage : EntityBase
{
  TenantId        : Guid
  QuotaKey        : string ("users", "storageGb", "apiCallsPerMonth")
  CurrentValue    : decimal
  LimitValue      : decimal
  PeriodStart     : DateTimeOffset
  PeriodEnd       : DateTimeOffset
  LastUpdatedUtc  : DateTimeOffset
}

QuotaEvent : EntityBase
{
  TenantId        : Guid
  QuotaKey        : string
  Delta           : decimal
  Reason          : string
  OccurredAtUtc   : DateTimeOffset
}
```

**Contract:**
```
IQuotaService
  Task<bool> TryConsumeAsync(Guid tenantId, string quotaKey, decimal amount, CancellationToken ct);
  Task<QuotaStatus> GetStatusAsync(Guid tenantId, string quotaKey);
  Task ReleaseAsync(Guid tenantId, string quotaKey, decimal amount);
```

**Standard quotas:**
- `users.max` — user create öncesi check
- `storage.gb.max` — file upload öncesi check
- `api.calls.per.month` — gateway-level check
- `modules.max` — entitlement add öncesi check

**Acceptance criteria:**
- [ ] Atomic consume (race condition'a karşı)
- [ ] Period reset (monthly quotas için)
- [ ] Quota breach → notification (MOD-0027) + warning UI
- [ ] Quota usage dashboard
- [ ] Soft warning (80% kullanım) + hard limit (100%)

**Dependencies:** MOD-0026 (reset jobs), MOD-0027 (alerts), MOD-0021 (audit)

**%100 için kalanlar:**
- [ ] MOD-0026 ile monthly/periodic quota reset job üretime alınmalı.
- [ ] Quota breach ve 80% warning notification akışları MOD-0027 ile bağlanmalı.
- [ ] Tenant detail üzerinde quota dashboard gerçek `QuotaUsage` verisiyle gösterilmeli.
- [ ] Atomic consume/release/reset akışları için concurrency integration testleri yazılmalı.
- [ ] Gateway-level API quota enforcement MOD-0032 ile bağlanmalı.

---

### MOD-0046+ — Tenant Core UI Extensions
**Wave:** W3-A
**Priority:** 🟠 High
**Status:** 🟡 Partial (%60)

**Purpose:**
Platform admin'in tenant detail sayfasındaki eksik tab'ları doldurmak: subscription, entitlement, quota usage, document/logo, audit links.

**What's missing:**
- 🔴 "System Monitoring" tab gerçek metric ile (last login, active users, storage, API calls)
- 🟡 Quota usage tab (MOD-0033) gerçek dashboard seviyesine taşınmalı
- 🔴 Audit link tab (MOD-0021 deep-link)
- 🔴 Document/Logo tab (MOD-0028)

**Acceptance criteria:**
- [ ] System Monitoring real-time metric
- [ ] Quota dashboard per-tenant
- [ ] Audit link with pre-filter
- [ ] Document management

**Dependencies:** MOD-0021, MOD-0033, MOD-0028

**%100 için kalanlar:**
- [ ] System Monitoring tab gerçek health, last-login, active-users, storage ve API-call metriklerini göstermeli.
- [ ] Quota tab MOD-0033 verisiyle grafik/KPI ve warning state içermeli.
- [ ] Audit tab MOD-0021'e tenant pre-filter deep-link üretmeli.
- [ ] Document/Logo tab MOD-0028/MOD-0266 ile dosya metadata ve signed URL kullanmalı.
- [ ] Tenant Details için browser smoke testleri tüm tab'ların boş/loaded/error state'lerini doğrulamalı.

---

### MOD-0299 — SaaS Billing & Invoicing
**Wave:** W3-B
**Priority:** 🟠 High
**Status:** 🔴 Missing (0%)

**Purpose:**
SaaS subscription için invoice generation, payment tracking, billing cycle. **ERP billing modülünden (MOD-0169) BAĞIMSIZ.**

**Domain entities:**
```
SaasInvoice : EntityBase
{
  InvoiceNumber   : string (unique, sequential)
  TenantId        : Guid
  SubscriptionId  : Guid
  PeriodStart     : DateTimeOffset
  PeriodEnd       : DateTimeOffset
  Currency        : string
  Subtotal        : decimal
  TaxAmount       : decimal
  TotalAmount     : decimal
  Status          : enum { Draft, Issued, Paid, Overdue, Cancelled, Refunded }
  DueDate         : DateTimeOffset
  PaidAtUtc       : DateTimeOffset?
  Lines           : List<InvoiceLine>
}

InvoiceLine
{
  Description     : string
  Quantity        : decimal
  UnitPrice       : decimal
  Amount          : decimal
  PlanId          : Guid?
  AddonModuleId   : Guid?
}

SaasPayment : EntityBase
{
  InvoiceId       : Guid
  TenantId        : Guid
  Amount          : decimal
  Currency        : string
  Method          : enum { Stripe, ManualWire, Other }
  ProviderRef     : string?
  Status          : enum { Pending, Succeeded, Failed, Refunded }
  ReceivedAtUtc   : DateTimeOffset
}

BillingCycle : EntityBase (tenant subscription'a bağlı)
{
  TenantId        : Guid
  SubscriptionId  : Guid
  CycleStart      : DateTimeOffset
  CycleEnd        : DateTimeOffset
  Status          : enum { Active, Closed, Failed }
}
```

**Payment provider integration:** Stripe / Paddle / Iyzico (adapter pattern benzeri MOD-0263).

**Acceptance criteria:**
- [ ] Invoice generation (subscription period end → invoice oluştur)
- [ ] Payment provider entegrasyonu (en az 1)
- [ ] Manual payment recording
- [ ] Overdue → dunning flow + notification (MOD-0027)
- [ ] PDF invoice download
- [ ] Tax rate per-country support
- [ ] Refund flow

**Dependencies:** MOD-0297 (subscription source), MOD-0027, MOD-0026, NEW-001

---

### MOD-0041 — Logging / Monitoring
**Wave:** W3-C
**Priority:** 🟡 Medium
**Status:** 🟡 Partial (%35 — ILogger ve health baseline var, structured observability yok)

**Purpose:**
Structured logging, correlation propagation, health checks, metrics, distributed tracing baseline.

**Recommendation:** Serilog + OpenTelemetry + Prometheus + Grafana.

**Acceptance criteria:**
- [ ] Serilog ile structured JSON log
- [ ] CorrelationId middleware (request → log → event → audit chain)
- [ ] Health check endpoint'leri (`/health`, `/health/ready`, `/health/live`)
- [ ] Prometheus metrics export
- [ ] OpenTelemetry tracing (Jaeger/Zipkin)

**%100 için kalanlar:**
- [ ] Serilog JSON logging ve correlation-id propagation tüm servislerde standardize edilmeli.
- [ ] `/health/live` ve `/health/ready` ayrımı MongoDB, gateway upstream ve background worker dependency'lerini kapsamalı.
- [ ] OpenTelemetry traces ve Prometheus metrics endpoint'leri deploy profillerine eklenmeli.
- [ ] Dashboard/runbook linkleri MOD-0042 ile bağlanmalı.

---

### MOD-0042 — Alerting / Incident Runbooks
**Wave:** W3-C
**Priority:** 🟡 Medium
**Status:** 🔴 Missing (0%)

**Purpose:**
Provisioning failure, email dispatch failure, job failure, subscription expiry error, quota breach için alert.

**Acceptance criteria:**
- [ ] Alert rule definitions (config-driven)
- [ ] Channel: email, slack webhook
- [ ] Runbook link per alert type
- [ ] De-duplication

**Dependencies:** MOD-0041, MOD-0027

---

### MOD-0265 — SIEM / Observability Provider
**Wave:** W3-C / Later
**Priority:** 🟡 Medium
**Status:** 🔴 Missing (0%)

**Purpose:**
External observability/SIEM export — Datadog, New Relic, Splunk, ELK adapter.

**Skip if:** Production SOC2/ISO 27001 hedefi yoksa MVP'de gereksiz.

---

### MOD-0038 — Event Taxonomy / Naming
**Wave:** W3-D
**Priority:** 🟡 Medium
**Status:** 🔴 Missing (0%)

**Purpose:**
Event isimlendirme standardı, lifecycle semantiği, ownership.

**Convention proposal:**
```
{aggregate}.{action}.{tense}
örnekler:
  tenant.created
  tenant.activated
  subscription.renewed
  entitlement.granted
  invoice.issued
  payment.failed
```

**Acceptance criteria:**
- [ ] Naming convention doc
- [ ] Event catalog (machine-readable, JSON)
- [ ] Ownership mapping (event → service)
- [ ] Linter (event name validation in CI)

---

### MOD-0039 — Schema Compatibility / Contract Governance
**Wave:** W3-D
**Priority:** 🟡 Medium
**Status:** 🔴 Missing (0%)

**Purpose:**
Event/API schema versioning, backward compatibility, breaking change detection.

**Recommendation:** Avro/Protobuf schema registry veya JSON Schema + CI check.

**Acceptance criteria:**
- [ ] Schema registry (versioned)
- [ ] Breaking change CI check
- [ ] Consumer compatibility matrix

---

### MOD-0002 — Interface Registry
**Wave:** W3-E
**Priority:** 🟡 Medium
**Status:** 🟡 Partial (%70) — InterfaceRegistry domain, controller, application features, frontend, import/review/deprecate yüzeyi ve test izleri var. OpenAPI ingestion, ownership ve deprecation policy hardening pending.

**Purpose:**
API endpoint sahipliği, version, consumer, compatibility metadata kaydı.

**Acceptance criteria:**
- [ ] API catalog (OpenAPI specs)
- [ ] Endpoint ownership
- [ ] Deprecation policy

**%100 için kalanlar:**
- [ ] OpenAPI ingestion gerçek servis swagger dokümanlarından otomatik discovery batch üretmeli.
- [ ] Endpoint ownership ve consumer dependency matrisi zorunlu alan olarak enforce edilmeli.
- [ ] Deprecation policy, review workflow ve audit event'leri MOD-0021 ile bağlanmalı.
- [ ] Schema compatibility çıktıları MOD-0039 Data Contract Registry ile ilişkilendirilmeli.
- [ ] Gateway smoke ve UI smoke testleri import/review/deprecate akışlarını doğrulamalı.

---

### MOD-0003 — Data Contract Registry
**Wave:** W3-E
**Priority:** 🟡 Medium
**Status:** 🔴 Missing (0%)

**Purpose:**
DTO/event schema registry, compatibility metadata.

**Acceptance criteria:**
- [ ] DTO catalog
- [ ] Schema versioning
- [ ] Producer/consumer mapping

---

## 7. CROSS-CUTTING STANDARTLAR (Tüm Modüller İçin Geçerli)

Bu standartlar **her modülde zorunlu.** AI'ya prompt verirken bu bölümü mutlaka dahil edin.
Referanslar: `.antigravity/rules/handler-design.md`, `.antigravity/rules/erp-architecture.md`, `.antigravity/rules/pipeline-behaviors.md`, `.antigravity/rules/entity-base-template.md`, `.antigravity/rules/response-envelope.md`, `.antigravity/rules/repository-standard.md`.

### 7.1 Naming Conventions
- **Module ID:** MOD-XXXX (yöneticinin verdiği) veya PSS-XXX (Platform Shared Services)
- **Entity:** PascalCase, singular (`SubscriptionPlan`, not `SubscriptionPlans`)
- **Field:** PascalCase, global isimler (`Code`, NOT `PlateCode`/`CityCode`)
- **Collection (MongoDB):** snake_case, plural (`platform_subscription_plans`)
- **Command (record):** `{Verb}{Entity}Command` → `CreateSubscriptionPlanCommand`, `UpdateTenantCommand`, `DeleteFeatureCommand`
- **Query (record):** `Get{Entity}{Qualifier}Query` → `GetTenantListQuery`, `GetTenantByIdQuery`
- **Handler (class, suffix YOK):** `{Verb}{Entity}Handler` → `CreateSubscriptionPlanHandler`, `GetTenantByIdHandler`
- **Validator (class, suffix YOK):** `{Verb}{Entity}Validator` → `CreateSubscriptionPlanValidator`
- **Controller route:** `/api/platform/{kebab-case-resource}` → `/api/platform/subscription-plans`
- **Frontend route:** `/Platform/{PascalCase}` → `/Platform/SubscriptionPlans`
- **Permission:** `Modules.{ModuleName}.{Action}` veya `Platform.{Resource}.{Action}` → `Platform.SubscriptionPlans.Create`
- **Event:** `{aggregate}.{action}.{tense}` → `tenant.created`, `subscription.renewed`
- **Feature code:** `FEATURE_XXX` uppercase
- **Feature slug:** `feature-xxx` lowercase-kebab
- **Module code:** Uppercase short (`HR`, `MDM`, `CRM`)
- **Private field:** `_camelCase` (`_repository`, `_currentUser`)

### 7.2 Action-Based File Separation (ZORUNLU — Golden Reference birebir)
- **Her Command için ayrı dosya.** Grup dosyası (`ProductCommands.cs`) **YASAK.**
- **Her Query için ayrı dosya.**
- **Her Handler için ayrı dosya.** `CreateProductHandler.cs`, `UpdateProductHandler.cs` ayrı (Command/Query suffix YOK).
- **Her Validator için ayrı dosya.** `CreateProductValidator.cs` (Command suffix YOK).
- **Bir dosyada birden fazla public class YASAK.**
- **Folder yapısı:** `Commands/`, `Queries/`, `Handlers/CommandHandlers/`, `Handlers/QueryHandlers/`, `Validators/`
- **DTO'lar TEK dosyada:** `Application/Features/{Module}/{Module}Models.cs` (Golden Reference pattern).
- **Referans canlı kod:** `services/Diten.DevEnablementService/.../Features/GoldenReferenceSlim/`

### 7.3 EntityBase Zorunlu Alanlar

> **Sınıf adı servis bazlı değişir, kontrat aynıdır:**
> - `Diten.MdmService`, `Diten.DevEnablementService`, `Diten.AuthService` → sınıf adı **`EntityBase`**
> - `Diten.Platform` tenant-aware kayıt → sınıf adı **`BaseEntity`** (eşdeğer)
> - `Diten.Platform` cross-tenant katalog → **`GlobalEntity : BaseEntity`** (gerekçeli, ör. `Tenant`, `SubscriptionPlan`, `PlatformAdministrator`)
>
> Module pack frontmatter `entity_base` alanı somut sınıf adını yazar.

Tüm entity'ler base sınıftan miras almalı:
```csharp
public sealed class SubscriptionPlan : EntityBase
{
    public string Code { get; set; }
    public string Name { get; set; }
    // ...
    // EntityBase'den geliyor (otomatik):
    //   Id          : Guid
    //   TenantId    : Guid (multi-tenant izolasyon)
    //   IsDeleted   : bool (soft delete flag)
    //   DeletedAt   : DateTimeOffset?
    //   CreatedAt   : DateTimeOffset (UTC)
    //   UpdatedAt   : DateTimeOffset? (UpdateAsync içinde set)
}
```
**Opsiyonel audit alanları** (iş modüllerinde manuel ekle):
- `CreatedBy : Guid?` (`[BsonRepresentation(BsonType.String)]`)
- `UpdatedBy : Guid?` (`[BsonRepresentation(BsonType.String)]`)

**Yasaklar:**
- ❌ Entity içinde `Id`, `TenantId`, `IsDeleted`, `CreatedAt` tekrar tanımlamak
- ❌ Domain entity'de `using MongoDB.Driver` (BsonRepresentation hariç)
- ❌ Ülke/bölge spesifik field adı (`PlateCode` → `Code` kullan)

### 7.4 Handler Tasarımı — Tek Sorumluluk

**Bir handler şunu yapar:**
1. `ArgumentNullException.ThrowIfNull(request)` (ilk satır)
2. Guard clause'lar (null, duplicate, varlık, tenant kontrolü)
3. Entity kur veya güncelle
4. Repository üzerinden persist et
5. `Response<T>` döndür

**Handler'a Giremeyen Sorumluluklar:**
| Yasak | Doğru Yer |
|---|---|
| Email/SMS gönderme | `INotificationService` (Infrastructure) |
| Dış servis HTTP çağrısı | `I{X}ServiceClient` interface (Application/Abstractions → Infrastructure/Clients) |
| Child entity upsert + parent persist (birlikte) | Ayrı command veya alt servis |
| OpenAI / AI servisi çağrısı | `IAiService` interface |
| Dosya/blob yükleme | `IStorageService` interface (MOD-0266) |
| Domain event dispatch | `IDomainEventDispatcher` veya `IEventBus` (MOD-0035) |

**Zorunlu:**
- Handler ilk satırı: `ArgumentNullException.ThrowIfNull(request);`
- İş modülünde Create handler: `entity.CreatedBy = _currentUser.UserId`
- İş modülünde Update handler: `entity.UpdatedBy = _currentUser.UserId`
- `UpdatedAt` → `RepositoryBase.UpdateAsync` içinde otomatik (handler'da tekrar yazma)
- Başarı/hata için `throw` değil `Response<T>.Fail()` / `Response<T>.Success()`
- Update/Delete/Patch → `Response<NoContent>` (NOT `Response<bool>`)
- Create → `Response<Guid>` (status 201)

**Guard Clause Şablonu:**
```csharp
public async Task<Response<Guid>> Handle(CreateXRequest request, CancellationToken ct)
{
    ArgumentNullException.ThrowIfNull(request);

    if (!await _categoryRepository.ExistsAsync(request.CategoryId, ct))
        return Response<Guid>.Fail("Category not found.", 404);

    if (await _repository.ExistsByCodeAsync(request.Code.Trim(), null, ct))
        return Response<Guid>.Fail($"Code '{request.Code}' already exists.", 409);

    var entity = new X
    {
        Code      = request.Code.Trim(),
        Name      = request.Name.Trim(),
        CreatedBy = _currentUser.UserId,  // ZORUNLU iş modülünde
    };

    var created = await _repository.CreateAsync(entity, ct);
    return Response<Guid>.Success(created.Id, 201);
}
```

### 7.5 ICurrentUserContext (Iş Modüllerinde Zorunlu)
```csharp
// Application/Interfaces/ICurrentUserContext.cs
public interface ICurrentUserContext
{
    Guid UserId { get; }
    string UserName { get; }
}
```
- DTO'dan `UserId` alma **YASAK** — `ICurrentUserContext` inject et.
- Lookup/system modüllerde opsiyonel; iş modüllerinde zorunlu.

### 7.6 Pipeline Behaviors (4 Zorunlu, Sıra Önemli)
Her mikroservis Program.cs'de bu sıra ile kayıtlı olmalı:
1. `ValidationBehavior` — FluentValidation otomatik tetikleme
2. `LoggingBehavior` — Request/response log
3. `ExceptionHandlingBehavior` — Beklenmedik exception → 500 envelope
4. `PerformanceBehavior` — Slow request detect

### 7.7 Response Envelope (Zorunlu)
```csharp
public class Response<T> {
    public bool IsSuccessful { get; init; }
    public T? Data { get; init; }
    public List<string> Errors { get; init; }
    public int StatusCode { get; init; }

    public static Response<T> Success(T data, int statusCode = 200);
    public static Response<T> Fail(string error, int statusCode);
}

public record NoContent;  // Update/Delete için
```
- Beklenen iş hataları (404, 409, 400) → `Response<T>.Fail()`
- Beklenmedik exception → `ExceptionHandlingBehavior` 500'e sarar
- `throw` sadece kritik infrastructure hatasında (DB unreachable, vb.)

### 7.8 Repository Standardı
- **Generic `IRepository<T>` zorunlu, specific repository YASAK.**
- Implementation `RepositoryBase<TEntity>`'den miras almalı.
- TenantFilter + SoftDelete otomatik (RepositoryBase seviyesinde).
- Sadece custom query (örn. `ExistsByCodeAsync`) gerekirse `IRepository<T>` extension method olarak ekle veya domain-specific interface tanımla (sadece o ek metodu içersin).

**Yasaklar:**
- ❌ Handler'da doğrudan `HttpClient` (interface kullan)
- ❌ Handler'da doğrudan `IMongoCollection<T>` (repository kullan)
- ❌ Specific repository (TenantRegistryRepository gibi) — sadece istisnai durumda

### 7.9 Soft Delete
- Tüm CRUD'larda `IsDeleted = true` + `DeletedAt = UtcNow`
- `DeleteAsync` içinde her ikisi de set edilir
- Hard delete endpoint **expose etmeyin**
- Repository sorguları silinmiş kayıtları otomatik filtreler

### 7.10 Concurrency Control
- Her update'lenebilir entity'de `byte[] RowVersion`
- Update request handler'da yenile: `existing.RowVersion = Guid.NewGuid().ToByteArray();`
- Mismatch → `Response<NoContent>.Fail("Concurrency conflict.", 409)`
- UI tarafında 409 → re-fetch + retry flow (henüz eksik, planda P1)

### 7.11 Validation (FluentValidation)
- Validator dosyası: `Validators/{RequestName}Validator.cs`
- `ValidationBehavior` MediatR pipeline'da → handler'a girmeden tetiklenir
- **Yasaklar:**
  - ❌ FluentValidation'da olan kontrolü handler'da tekrar yazmak
  - ❌ Validator'da iş mantığı (sadece schema/format/required kuralları)

### 7.12 Authorization
- Tüm Platform controller: `[Authorize(Policy = "PlatformActor")]` veya inherit edilen `CustomBaseController`
- Action-level: `[HasPermission("Platform.X.Action")]` veya `[HasPermission("Modules.{ModuleName}.{Action}")]`
- Actions: `Read`, `Create`, `Update`, `Delete`, `BulkDelete`
- MOD-0018 sonrası: `[RequiresModule("CODE")]`, `[RequiresFeature("FEATURE_X")]`
- `[AllowAnonymous]` sadece public health check için kabul

### 7.13 Magic String Yasağı (Domain Enum)
Lookup `Code` değerleri (status, lifecycle, type) handler/validator'da string literal olarak kullanılamaz:
```csharp
// ❌ YASAK
if (entity.Status == "ACTIVE") { ... }

// ✅ DOĞRU — Domain/Enums/...Enums.cs
public enum SubscriptionStatusCode { PendingProvisioning=0, Active=2, ... }
if (entity.StatusCode == (int)SubscriptionStatusCode.Active) { ... }
```

### 7.14 Audit Instrumentation
- MOD-0021 öncesi: handler içinde manuel `_auditService.RecordAsync(...)` (varsa)
- MOD-0021 sonrası: `AuditBehavior` pipeline behavior otomatik
- Sensitive field redact: `Password`, `Secret`, `Token`, `ApiKey`, `ConnectionString`

### 7.15 Lokalizasyon (Platform: 2 Dil)
- **Platform tarafı için sadece `en` ve `tr`.** (Daha fazla dil over-engineering.)
- Resource dosyaları: `Resources/Views/Platform/{Module}/{ViewName}.{en|tr}.resx`
- Razor: `@inject IHtmlLocalizer<{ViewClass}> Localizer`
- Shared: `@inject IHtmlLocalizer<SharedResource> SharedLocalizer`
- JS: `_IndexL10n.cshtml` partial → `window.L10n` object
- Standart referans: `.antigravity/rules/localization-standard.md`

> **Not:** PSS-005 Module Catalog 7 dil ile yapılmış — bu over-engineering kararıydı. Yeni modüllerde **sadece en + tr.**

### 7.16 Async + CancellationToken
- Tüm I/O `async` olmalı
- `CancellationToken` en alt katmana kadar geçirilmeli
- `CancellationToken ct` parametresi her async metodda

### 7.17 Frontend Conventions
- Layout: `_LayoutPlatformAdmin.cshtml` (sidebar + topbar)
- DataTable: v2 standard (`dt-standard="v2"`)
- Modal/Offcanvas: Bootstrap 5 native
- Notification: Notyf (toast, 5s)
- Confirmation: SweetAlert2
- Form validation: HTML5 + Bootstrap visual feedback
- Loading: Skeleton + spinner
- Standart referanslar: `.antigravity/rules/frontend-standards.md`, `.antigravity/rules/frontend-datatable-template.md`, `.antigravity/rules/frontend-form-template.md`, `.antigravity/rules/frontend-details-template.md`, `.antigravity/rules/frontend-js-standard.md`

### 7.18 Security Checklist (her modülde sor)
- [ ] Hardcoded secret yok mu? (NEW-001 zorunlu)
- [ ] CORS environment-aware mı?
- [ ] Input sanitization (XSS, mass assignment)?
- [ ] Tenant cross-access validation? (X-Tenant-Id manipülasyon koruması)
- [ ] Sensitive data exposure (logs, responses, error messages)?
- [ ] Rate limit (public endpoint'lerde, MOD-0032 sonrası)?
- [ ] `ICurrentUserContext` kullanılıyor mu, DTO'dan UserId alınmıyor mu?

### 7.19 Testing Requirements
Her modül için minimum:
- [ ] Domain: Entity invariant + Domain enum testleri
- [ ] Application: Request handler happy path + sad path (mock repository)
- [ ] Application: Validator unit testleri (her kural için)
- [ ] Integration: Controller endpoint + RepositoryBase (MongoDB testcontainer)
- [ ] Frontend (opsiyonel): Playwright E2E happy path

### 7.20 Kontrol Listesi (Handler Code Review)
- [ ] Handler tek aggregate üzerinde mi?
- [ ] Email/SMS `INotificationService` üzerinden mi?
- [ ] Dış servis interface üzerinden mi? (`HttpClient` doğrudan yok)
- [ ] Child entity upsert ayrı command'a mı?
- [ ] İlk satır `ArgumentNullException.ThrowIfNull(request)` mi?
- [ ] İlişkili ID'lerin varlık/tenant kontrolü var mı?
- [ ] `Response<T>` döndürüyor mu? (throw yok)
- [ ] Update/Delete `Response<NoContent>` mi? (`Response<bool>` değil)
- [ ] Create handler'da `CreatedBy = _currentUser.UserId` set mi?
- [ ] Update handler'da `UpdatedBy = _currentUser.UserId` set mi?
- [ ] `ICurrentUserContext` inject mi? (DTO'dan UserId alma yok)
- [ ] FluentValidation'da olan kontrol handler'da tekrarlanmıyor mu?
- [ ] String literal status check yok mu? (Domain enum kullan)
- [ ] Async metodlarda CancellationToken propagate ediliyor mu?
- [ ] **Admin Safety Guardrails (§7.21):** Komut admin actor'leri (PlatformAdmin/PartnerAdmin/Role/Permission) etkiliyorsa self-action ve last-admin koruması var mı?
- [ ] **Bulk-action filtresi:** BulkDelete/BulkSuspend handler'ında current user ID hedeften ayıklanıyor mu?
- [ ] **Failure Path:** Self-action ve last-admin reddi için 409/422 testleri var mı?

---

### 7.21 Admin Safety Guardrails (Yeni — Zorunlu Cross-Cutting)

**Amaç:** Platform yönetim modüllerinde sistemi kilitleyecek veya yönetim erişimini kazara koparacak "actor self-protection" hatalarını engelle. Bu kural NEW-002, MOD-0018, MOD-0021 ve admin actor'leri etkileyen tüm gelecekteki modüller için **zorunludur**.

#### Korunması gereken 6 invariant

| # | Kural | Reddetme nedeni (UI/API mesajı) | HTTP |
|---|---|---|---|
| 1 | **Self-action koruması.** Giriş yapan kullanıcı kendini delete/suspend/disable/cancel/demote edemez. | `"You cannot perform this action on your own account."` | 409 |
| 2 | **Last SuperAdmin koruması.** Sistemde en az **1 adet `status=Active` + `Roles` içinde `"SuperAdmin"`** bulunan PlatformAdmin daima bulunmalıdır. Bu invariant'ı bozacak delete / suspend / role-remove işlemi reddedilir. | `"At least one active Super Admin must remain in the system."` | 409 |
| 3 | **Role self-downgrade.** Kullanıcı kendi `SuperAdmin` / yönetim rollerini kendi kendine kaldıramaz. | `"You cannot remove your own administrative role."` | 409 |
| 4 | **Bulk-action filtresi.** BulkDelete/BulkSuspend/BulkDisable komutlarında current user ID hedef listeden **otomatik ayıklanır**; sonuç payload'ı `SkippedSelfIds[]` içerir. Hata değil, atlama. | Sessiz atlama + response içinde `skipped: [self]` | 200 |
| 5 | **Permission self-revoke koruması.** Kullanıcı kendi `Platform.Administrators.*` veya `Platform.Permissions.*` permission'larını kaldıramaz. | `"You cannot revoke your own administrative permissions."` | 409 |
| 6 | **PartnerAdmin scope self-removal.** PartnerAdmin kendi `AllowedTenantIds` listesinden erişimini sağlayan tenant'ı çıkaramaz. | `"You cannot remove yourself from a tenant you currently operate."` | 409 |

> **HTTP kod kararı:** Master-plan §7.7 örnekleri 404/409/400 kullanır, 422 projede mevcut değil. Tüm guard rejection'ları **409** olarak normalleştirilmiştir. Frontend ayrım için `Response<T>.Errors[0]` mesajını gösterir; programatik ayrım gerekirse ileride envelope'a `errorCode` field'ı eklenir (şimdilik kapsam dışı).

#### Son admin tanımı + bootstrap

- **"Son" hesaplaması:** `IPlatformAdministratorRepository.CountActiveSuperAdminsAsync(CancellationToken)` → `Status == Active && Roles.Contains("SuperAdmin") && !IsDeleted` üzerinden. Sonuç `<= 1` ise hedef admin bu kişi olduğunda kural 2 tetiklenir.
- **Bootstrap (cold start):** Sistemde hiç PlatformAdmin yoksa ilk admin **seed/runbook** ile NEW-001 vault'tan provision edilir. UI üzerinden ilk admin invite edilemez (NEW-002 implementation notes'a yazılacak).
- **PartnerAdmin için bu kural geçerli DEĞİL** — aşağı bak.

#### PartnerAdmin "son admin" durumu (kural 2 muafiyeti)

PartnerAdmin'lerin sonuncusu silindiğinde **backend reddetmez**. Gerekçe: bir partner'ın son `PartnerAdmin`'i kaybolursa partner kendi tenant'larını yönetemez ama bu durum **PlatformAdmin tarafından yeni invite ile geri alınabilir** (recoverable). Son `SuperAdmin` kaybı ise non-recoverable (sistem kilitlenir) — asimetrik risk, asimetrik koruma.

**Frontend davranışı (sadece UI defense):**
- PartnerAdmin silme/suspend butonuna basıldığında, eğer bu silme partner'ın son aktif PartnerAdmin'ini götürecekse SweetAlert confirm modal'ı gösterilir: `"This is the last administrator for {PartnerName}. They will not be able to self-manage their tenants. Continue?"`
- Confirm sonrası backend normal akışla işlem yapar; backend tarafında ek kural çalıştırılmaz.

#### Implementation contract

Tek bir reusable servis, `Diten.Platform.Common`'da:

```csharp
// services/Diten.Platform.Common/Security/IActorSafetyGuard.cs
public interface IActorSafetyGuard
{
    // Tekil eylem koruması — 1, 3, 5, 6 kurallarını çalıştırır
    Task<Response<NoContent>?> EnsureNotSelfAsync(
        Guid targetActorId,
        AdminSafetyAction action,
        CancellationToken ct);

    // Last-admin koruması — kural 2
    Task<Response<NoContent>?> EnsureNotLastActiveSuperAdminAsync(
        Guid targetActorId,
        AdminSafetyAction action,
        CancellationToken ct);

    // Bulk-action filtresi — kural 4
    Task<BulkSafetyResult> FilterSelfFromBulkAsync(
        IReadOnlyCollection<Guid> targetActorIds,
        CancellationToken ct);
}

public enum AdminSafetyAction
{
    Delete, Suspend, Disable, Cancel,
    RemoveRole, RevokePermission, RemoveTenantScope
}

public sealed record BulkSafetyResult(
    IReadOnlyCollection<Guid> EffectiveTargets,
    IReadOnlyCollection<Guid> SkippedSelfIds);
```

**Handler kullanım kuralı:**
- Guard çağrısı **Authorization sonrası, business validation öncesi** yapılır.
- `EnsureNotSelfAsync` / `EnsureNotLastActiveSuperAdminAsync` `null` dönerse devam, dolu `Response<NoContent>.Fail(...)` dönerse handler doğrudan onu return eder.
- `FilterSelfFromBulkAsync` çağrısı `EffectiveTargets` boşsa handler `Response<BulkResult>.Success(skipped=[self], affected=0)` döner — hata değil.

**`ICurrentUserContext` zorunlu** — guard internal olarak inject eder; handler ek inject yapmaz.

#### Hangi komutlara uygulanır

`AdminSafetyAction`'a karşılık gelen komutlar (gelecekte modül eklendikçe büyür):

| Modül | Komut | Tetiklenen kural(lar) |
|---|---|---|
| NEW-002 PlatformAdministrators | `DeletePlatformAdministratorCommand` | 1, 2 |
| NEW-002 | `SuspendPlatformAdministratorCommand` | 1, 2 |
| NEW-002 | `BulkDeletePlatformAdministratorCommand` | 4 |
| NEW-002 | `BulkSuspendPlatformAdministratorCommand` | 4 |
| NEW-002 | `AssignRolesPlatformAdministratorCommand` | 3 |
| NEW-002 | `UpdateAllowedTenantsCommand` | 6 |
| MOD-0018 RBAC | `RevokePermissionCommand` | 5 |
| MOD-0018 | `RemoveUserFromRoleCommand` | 3 |

> Tenant-side iş modüllerinde (HR, Finance) bu kural geçerli **değildir**; sadece **admin actor**'leri (PlatformAdmin/PartnerAdmin/Role/Permission) etkileyen komutlarda zorunludur.

#### Module pack acceptance criteria şablonu

Admin actor etkileyen her module pack `Acceptance Criteria` bölümüne **birebir** şunları kopyalar:

```markdown
- [ ] Self-action reddi: current user kendi {entity}'sini delete/suspend edemez (409 + i18n mesaj)
- [ ] Last SuperAdmin reddi: sistem en az 1 active SuperAdmin kalacak şekilde delete/suspend/role-remove engellenir (409)
- [ ] Bulk filtresi: bulk komut payload'ı self ID içerse bile sessizce ayıklanır, response.skipped içerir (200)
- [ ] Role self-downgrade reddi: kullanıcı kendi yönetim rolünü kaldıramaz (409)
- [ ] Permission self-revoke reddi: kullanıcı kendi admin permission'ını kaldıramaz (409) — (uygunsa)
- [ ] PartnerAdmin scope self-removal reddi: kendi tenant erişimini kaldıramaz (409) — (uygunsa)
- [ ] §7.21 guard'ları `IActorSafetyGuard` üzerinden çağrılıyor — handler içinde manuel `if (id == currentUser.UserId)` YASAK
- [ ] PartnerAdmin son-admin senaryosu: backend rejection YOK, sadece UI confirm modal (uygunsa)
```

#### Test şablonu

Her admin komutu için minimum integration test seti:

```
1. happy path     — başka bir kullanıcıya uygulandığında 200/204
2. self-action    — current user kendine uyguladığında 409 + mesaj kontrolü
3. last-superadmin — tek aktif SuperAdmin'e delete/suspend/role-remove uygulandığında 409 + mesaj kontrolü
4. bulk-self      — bulk payload current user ID içerdiğinde response.skippedSelfIds[] döner, 200
5. partner-last   — PartnerAdmin için (uygunsa): son PartnerAdmin silindiğinde 200 (backend reddetmez)
```

#### Frontend kontrolü (defense-in-depth)

Backend zaten reddediyor; UI bunu görünür hâle getirmeli:
- DataTable row action'larda current user row'unda Delete/Suspend disable
- Bulk select'te current user row'u check edilemez (disable + tooltip)
- Backend 422/409 dönerse Notyf ile sunucudan gelen mesaj gösterilir (UI tahmin etmez)

> Frontend kontrolü tek başına yeterli **değildir**; backend guard birincil zorunluluktur.

---

## 8. AI PROMPT ŞABLONLARI

### 8.1 Yeni Modül Detaylı Planı İçin
```
Ben ERP-vNext Platform geliştiricisiyim. Sıradaki modülüm MOD-XXXX [modül adı], Wave W?-?, Öncelik [Blocker/High/Medium].

Aşağıdaki Master Plan dökümanını referans alarak şu çıktıları üret:

1. Detaylı Domain Modeli
   - Entity sınıfları (field type'ları, nullable durumu, default değerler)
   - Enum'lar
   - Repository interface
   - Validation kuralları (FluentValidation)

2. Application Layer
   - Request'ler (Command tarafı): her biri için ne yapar, validation, handler logic, guard clauses
   - Request'ler (Query tarafı): filter, paging, sort
   - Validator'lar (FluentValidation kuralları)
   - Handler'lar: ICurrentUserContext kullanımı, Response<T> dönüş, dış servis interface'leri (handler'a giremeyenleri ayrıştır)
   - Pipeline behavior uyumu (4 zorunlu)

3. API Surface
   - Controller route + auth policy + permission
   - Her endpoint için: HTTP method, route, request body, response, status codes

4. Frontend Surface
   - View dosyaları (Index/Create/Edit/Details + partial'lar)
   - JavaScript organization
   - Lokalizasyon resx dosyaları (en + tr minimum)

5. Acceptance Criteria Checklist
   - Functional requirements
   - Cross-cutting standartlara uyum (soft delete, RowVersion, audit, l10n, permission)

6. Test Plan
   - Unit testler (handler, validator)
   - Integration testler (controller + MongoDB)

7. Dependencies & Anti-patterns
   - Bağımlı olduğu mevcut servis/repository
   - Düşmemesi gereken anti-pattern'ler

Master Plan içeriği:
[BURAYA TÜM MASTER PLAN'I YAPIŞTIR]
```

### 8.2 Mevcut Modül Doğrulaması İçin
```
Master Plan'da MOD-XXXX'in status'u [Done/Partial — %X] olarak işaretli. "What's done" ve "What's missing" listeleri var.

Repodaki şu dosyaları oku:
- [dosya yolları]

Görevin:
1. "What's done" listesindeki her madde gerçekten implement edilmiş mi? Tek tek check et.
2. "What's missing" listesindeki maddeler hâlâ eksik mi yoksa eklenmiş mi?
3. Listede olmayan ama bulduğun ek eksik/sorun var mı?
4. Cross-cutting standartlara uyum:
   - Soft delete uygulanmış mı?
   - RowVersion var mı?
   - FluentValidation var mı?
   - Permission attribute'leri var mı?
   - Lokalizasyon parite (en + tr minimum)?
   - Audit instrumentation var mı?

Sonuç: Master Plan'daki status alanı için güncel % ve liste önerisi.
```

### 8.3 Implementation Tamamlandı Code Review İçin
```
MOD-XXXX'i implement ettim. Master Plan'daki spesifikasyonla karşılaştır.

Branch diff: [git diff veya dosya listesi]

Kontrol et:
1. Domain modeli plana uygun mu? Sapma varsa neden?
2. API endpoint'leri planda olan'larla eşleşiyor mu?
3. Acceptance criteria checklist'ini tek tek run:
   [planın o modülündeki listeyi yapıştır]
4. Cross-cutting standartlar (Bölüm 7) uygulanmış mı?
5. Anti-pattern var mı?
6. Test coverage yeterli mi?

Sonuç: Merge'e hazır mı, blocking issue var mı?
```

### 8.4 Modüller Arası Bağımlılık Sorgu İçin
```
Master Plan'da MOD-XXXX [Wave W?-?]'a başlamak istiyorum.
Dependencies bölümünde şu modüller listelenmiş: [liste].

Repo'da:
1. Bu dependencies tamamlanmış mı (status check)?
2. Tamamlanmamışsa MOD-XXXX'i yapmak için minimum hangi parçaları geçici olarak stub/mock'layabilirim?
3. Bağımlılığın kritikliği: tam beklemek mi gerek, paralel mi yürür?
```

---

## 9. İLERLEME TAKİP TABLOSU (Master Status)

Bu tabloyu modül bittiğinde güncelle. Bölüm 2'deki özet tablo bunun kısaltılmış hali.

### 9.1 Wave 1 — Blocker Foundation
| ID | Modül | Status | % | Sorumluluk | Hedef Tarih | Tamamlanma Notu |
|---|---|---|---|---|---|---|
| NEW-001 | Secrets Management | 🟡 | 70 | DevOps | — | MOD-0012 canonical kayıt; secrets provider/validation var, production vault adapter ve rotation testleri eksik. |
| MOD-0012 | Secrets & Configuration Vault | 🟡 | 70 | DevOps | — | NEW-001'in canonical ID karşılığı; production vault adapter, rotation ve full secret inventory testleri eksik. |
| NEW-002 | Platform Administrators Mgmt | 🟢 | 95 | Platform UI | 2026-05-20 | Kod kanıtı doğrulandı (2026-05-14): 8 command + Slim DataTable + InvitationEmailService + Gateway routes. Kalan %5: MOD-0021 audit hookup. |
| MOD-0009 | Tenant Registry Events | 🟡 | 50 | Tenant | — | Tenant lifecycle var; event emit/outbox/bus yok. |
| MOD-0008 | Module Catalog Assignable | 🟡 | 80 | Catalog | — | — |
| MOD-0014 | Module Boundary Registry | 🔴 | 0 | Architecture | — | Sadece module pack `in-progress` (frontmatter); repo'da `*ModuleBoundary*`/`*CapabilityGroup*`/`*ModuleDefinition*` HİÇBİR kod yok. Pack-only. |
| MOD-0018 | RBAC Enforcement | 🟡 | 20 | Auth | — | HasPermission + entitlement read service izleri var; RequiresModule/RequiresFeature enforcement yok. |
| MOD-0298 | Tenant Module Entitlement Refine | 🟡 | 87 | Entitlement | — | — |
| MOD-0026 | Background Job Scheduler | 🔴 | 0 | Platform | — | — |
| MOD-0035 | Event Bus | 🔴 | 0 | Platform | — | — |
| MOD-0027 | Notification Service | 🔴 | 0 | Notification | — | — |
| MOD-0263 | Messaging Provider | 🔴 | 0 | Notification | — | — |

### 9.2 Wave 2 — High Priority Operations
| ID | Modül | Status | % | Sorumluluk | Hedef Tarih | Tamamlanma Notu |
|---|---|---|---|---|---|---|
| MOD-0028 | Document Metadata | 🔴 | 0 | Document | — | — |
| MOD-0266 | Blob Storage Provider | 🔴 | 0 | Document | — | — |
| MOD-0021 | General Audit Trail | 🟢 | 98 | Audit | — | Phase 1-5B + Phase 5C implemented. Kalan %2 = partner scope, smoke/integration ve carry-over hardening. |
| MOD-0287 | User Notification Prefs | 🔴 | 0 | Notification | — | — |
| MOD-0034 | Webhook Delivery | 🔴 | 0 | Webhook | — | — |
| NEW-003 | Notification Template UI | 🔴 | 0 | Platform UI | — | — |
| NEW-004 | Tenant Impersonation | 🔴 | 0 | Platform UI | — | — |

### 9.3 Wave 3 — Production Hardening
| ID | Modül | Status | % | Sorumluluk | Hedef Tarih | Tamamlanma Notu |
|---|---|---|---|---|---|---|
| MOD-0032 | API Gateway Hardening | 🟡 | 50 | Gateway | — | Ocelot + auth forwarding + secret validation/CORS başlangıcı var; rate limit/circuit breaker/direct-service fallback cleanup eksik. |
| MOD-0033 | Consumer/Quota Model | 🟡 | 78 | Quota | — | Backend entities + controllers + atomic consume/reset command'ları var; scheduler/dashboard/notification pending. |
| MOD-0046+ | Tenant Core UI Extensions | 🟡 | 60 | Platform UI | — | Commercial/quota yüzeyleri var; monitoring, audit deep-link, document tab pending. |
| MOD-0299 | SaaS Billing & Invoicing | 🔴 | 0 | Billing | — | — |
| MOD-0041 | Logging / Monitoring | 🟡 | 35 | Ops | — | ILogger/health baseline var; structured JSON logs, metrics ve tracing pending. |
| MOD-0042 | Alerting / Runbooks | 🔴 | 0 | Ops | — | — |
| MOD-0265 | SIEM Provider | 🔴 | 0 | Ops | — | — |
| MOD-0038 | Event Taxonomy | 🔴 | 0 | Architecture | — | — |
| MOD-0039 | Schema Governance | 🔴 | 0 | Architecture | — | — |
| MOD-0002 | Interface Registry | 🟡 | 70 | Architecture | — | Domain + controller + features + frontend/review surface var; OpenAPI ingestion/ownership hardening pending. |
| MOD-0003 | Data Contract Registry | 🔴 | 0 | Architecture | — | — |

### 9.4 Mevcut Modül İyileştirme (Lokalizasyon, RowVersion UI, vb.)
| ID | İyileştirme | Status | % | Notu |
|---|---|---|---|---|
| PSS-006 | Currency hardcoded fallback kaldır | 🟢 | 100 | PSS-011 ile tamamlandı (SubscriptionPlans `return []`). |
| PSS-011-FU1 | Platform HTTP integration test infra (`WebApplicationFactory`) | 🔴 | 0 | PSS-011'in %8'i — pack dışı, MOD-0032 ile veya ayrı pack. Tüm Platform modüllerini kapsar. |
| PSS-011-FU2 | Gateway smoke test runner (`curl :5000/api/lookups/*` CI step) | 🔴 | 0 | PSS-011'in %8'i — MOD-0032 Gateway Hardening kapsamı. |
| PSS-011-FU3 | Test runner unblock — sibling modül compile hataları (ModuleCatalog, TenantHandlers test dosyaları) | 🔴 | 0 | PSS-011 dışı; test projesinin çalıştırılabilmesi için. |
| PLATFORM-TEST-1 | Platform ortak browser smoke otomasyonu | 🔴 | 0 | Tenant, ModuleCatalog, SubscriptionPlans, Administrators, AuditLog, Profile/Settings ve InterfaceRegistry için happy-path + forbidden-state kontrolü. |
| PLATFORM-TEST-2 | DataTable v2 contract doğrulaması tüm Platform tablolarına genişletilsin | 🔴 | 0 | `data-dt-standard="v2"`, bulk selection, actions, localization ve responsive state doğrulanmalı. |
| NEW-002-FU1 | NEW-002 audit hookup — Invite/Update/Suspend/Reactivate/Delete/AssignRoles command'larına audit log emisyonu | 🔴 | 0 | MOD-0021 General Audit Trail hazır; NEW-002 command'larına `IAuditableCommand` + `IAuditMetadataProvider` retrofit. NEW-002'nin kalan %5'i. |
| PSS-PLAN-RECON-1 | NEW-001 ↔ MOD-0012 ID birleştirme — master-plan §4 NEW-001 referansları MOD-0012'ye taşı; eski ID legacy olarak kalsın veya retire et | 🔴 | 0 | 9 dosyada cross-reference güncellenmeli. Sırasında master-plan §4 NEW-001 status bloku da MOD-0012 ile birleştirilmeli. |
| MOD-0021-5C-H1 | Retention sayfası mevcut policy'leri yüklesin | 🟢 | 100 | Backend `GET /api/platform/audit/retention` endpoint + frontend load flow mevcut. Tamamlandı 2026-05-15. |
| MOD-0021-5C-H2 | Redact-actor UI | 🟢 | 100 | `AuditLog/index.js:505+` modal HTML + `_IndexL10n` RedactActor.* keys. Tamamlandı 2026-05-15. |
| MOD-0021-5C-H3 | Sidebar navigation entry | 🟢 | 100 | `_LayoutPlatformAdmin.cshtml:236-245` AuditLog + AuditRetention menü item'ları + active-state. Tamamlandı 2026-05-15. |
| MOD-0021-5C-H4 | `_DetailsModal.cshtml` ayrı partial | 🟢 | 100 | 47 satır partial dosyası; Index.cshtml'den inline modal çıkarıldı. Tamamlandı 2026-05-15. |
| MOD-0021-FU-Partner | partner_admin audit scope desteği (per-tenant filter, partner-scoped redaction) | 🔴 | 0 | Şu an audit endpoint'leri yalnız platform_admin. Wave 2+ retrofit. |
| MOD-0021-FU-CarryOver | Phase 1-5A non-blocker carry-overs: registry extensibility, IActorPiiMasker extraction, RedactionStatus accurate calc, CSV multiline, page-size 500→100, login redirect on 401, CSRF token, tenant lookup select2, GUID v6/v7 desteği, verifier intentional-exception kaydı, export streaming, idempotency drift fixes, recursion guard depth limit | 🔴 | 0 | 12+ küçük iyileştirme; Phase 5C sonrası veya 6. yayın sertleştirme. |
| PSS-PLAN-RECON-2 | MOD-0043/44/46 toplu kaydı (line 168) ayrı pack'lerle çelişiyor — eski kayıt retire edilmeli veya alt-modül endeksi olarak yeniden yazılmalı | 🔴 | 0 | Şu an inventory'de hem toplu hem ayrı kayıtlar görünüyor (kasıtlı geçiş; reconciliation sonrası temizlenecek). |
| MOD-0043-DRIFT | MOD-0043 pack-audit drift: `status: done` ve audit raporu "DitenAuditService + MDM middleware eklendi" diyor ama `services/Diten.AuditService/` ve `services/Diten.MdmService/` klasörleri repo'da yok | 🔴 | 0 | Pack/audit %100 claim'ediyor; gerçek skor %75. Olası nedenler: (a) klasörler farklı branch'te kaldı, (b) sonradan silindi, (c) AuditService kapsamı MOD-0021'e devredildi ve MDM hâlâ bekliyor. Doğrulama gerekli; sonrasında pack status revize edilmeli veya eksik service'ler restore edilmeli. |
| PSS-009-T1 | PSS-009 backend test coverage'ı 4/9 → 9/9'a çıkar (anonymous deny, tenant-user deny, email/username tampering reject, role/status/actor-type tampering reject, missing actor 404, stale version 409) | 🔴 | 0 | PSS-009'un %11 eksiğinin bir parçası. Acceptance criteria'da var ama henüz test edilmedi. |
| PSS-009-T2 | PSS-009 browser smoke otomasyonu (Playwright/Cypress) — header initials, dropdown navigation, profil/settings sayfa yükleme, forbidden DOM (avatar upload, password form, delete card, fake timeline) yokluk kontrolü | 🔴 | 0 | PSS-009'un %11 eksiğinin bir parçası. Şu an manuel test gerektiriyor. |
| PSS-009-FU1 | Platform admin avatar upload (storage/blob provider + image validation) | 🔴 | 0 | Storage provider standardı sonrası. |
| PSS-009-FU2 | Platform admin activity timeline (gerçek audit feed) | 🔴 | 0 | MOD-0021 Audit Trail queryable source sağladıktan sonra. |
| PSS-009-FU3 | Platform admin password change | 🔴 | 0 | PSS-010 veya AuthService PlatformActor password-change kontratı doğrulandıktan sonra. |
| PSS-009-FU4 | PreferredLocale + PreferredTimezone (profile/settings) | 🔴 | 0 | PSS-011 `/api/lookups/locales` + `/timezones` tüketici olarak; hardcoded array yasak. |
| PSS-010-FU1 | Platform admin MFA + active sessions ekranı | 🔴 | 0 | PSS-010'un %55 eksiği: MFA enable/disable, recovery, session revoke, trusted device ve audit feed. |
| PSS-005 | 7 dil over-engineering — sadece en+tr tutulması için ek diller silinebilir mi karar | 🔴 | 0 | Opsiyonel cleanup |
| MOD-0044 | Tenant logo base64 → MOD-0266 migrate | 🔴 | 0 | MOD-0266 sonrası |
| MOD-0298 | RefreshProjection tamamla | 🔴 | 0 | MOD-0035 sonrası |
| MOD-0297-FU1 | Subscription runtime automation | 🔴 | 0 | Trial expiry, renewal, PastDue auto-suspend, cancel-at-period-end job'ları MOD-0026 ile. |
| MOD-0033-FU1 | Quota dashboard + notification | 🔴 | 0 | Period reset job, 80% warning, breach notification ve tenant detail dashboard. |
| MOD-0032-FU1 | Frontend direct-service fallback kaldır | 🔴 | 0 | Gateway zorunluluğu için `PlatformServiceUrl` fallback'leri kaldırılmalı. |
| Genel | 409 conflict UI flow | 🔴 | 0 | RowVersion kullanan formlarda reload/merge kullanıcı akışı. |
| Genel | API versioning (`/api/v1/...`) | 🔴 | 0 | MOD-0032 ile |
| Genel | Event/job altyapısı tamamlanmadan %100 verilemez | 🔴 | 0 | MOD-0035 Event Bus + MOD-0026 Background Job Scheduler; tenant/subscription/quota/entitlement otomasyonlarının ortak bağımlılığı. |
| Genel | Production provider entegrasyonları | 🔴 | 0 | MOD-0027 notification, MOD-0263 messaging, MOD-0266 blob storage, MOD-0299 billing/payment, MOD-0028 document metadata. |

---

## 10. NOTLAR & KARAR LOGu

> Bu bölümü plan yaşadıkça güncelleyin. Mimari karar, scope değişikliği, deferred itemlar.

- **2026-05-11:** İlk versiyon. Master plan oluşturuldu.
- **2026-05-11:** `ModulePageDescriptor` Platform'da olmamalı tartışması açık — Wave 2 sonrası karar.
- **2026-05-11:** `SavedViews` Platform Application'da yanlış konumlanmış — taşıma kararı bekliyor.
- **2026-05-11:** MOD-0262 (External Document Provider) MVP-priority değil, ERP modülleri başlamadıkça erteleme.
- **2026-05-11:** MOD-0265 (SIEM) MVP'de skip — SOC2 hedefi geldiğinde aktive.
- **2026-05-14:** Repo durumu plan ile mutabakat. NEW-002 backend merged (commit d3656fe). MOD-0033 PR #8 merged (entities, controllers, Features/Quotas). MOD-0002 InterfaceRegistry scaffold eklendi. Branch: features/referencedata.
- **2026-05-14:** NEW-002 reconciliation — kod doğrulaması yapıldı: status 🟠 %85 → 🟢 %95. Master-plan'da "gateway route, invite-email, audit hookup eksik" deniliyordu; gerçekte gateway routes ([ocelot.json:124-152](../../gateway/Diten.ApiGateway/ocelot.json#L124)) **var**, `PlatformAdministratorInvitationEmailService` + email template **var**, 8 command + tam Slim DataTable shell **var**. Yalnızca **MOD-0021 audit hookup** eksik (upstream bağımlılık).
- **2026-05-14:** PSS-011 Lookups / Reference Data %92 (🟠). Acceptance 14/14 ✅ (MediatR Features/Lookups, `IPlatformLookupCache`, `[Authorize(PlatformActor)]` + `Platform.Lookups.Read`, hardcoded fallback kaldırıldı, PSS-007 feature-category source-of-record bağı doğrulandı). Unit tests 9/9 yazılı. Eksik: HTTP integration + gateway smoke testleri. Canonical `LookupOptionDto`, caching (12h/5m), `[AllowAnonymous]` blanket kaldırma, hardcoded currency fallback temizleme, locale/timezone/tenant-tier/subscription-cycle endpoint'leri. Tüketici: SubscriptionPlans, ModuleCatalog, Tenants ekranları.
- **2026-05-14:** PSS-009 Platform Admin Profile & Settings %89 (🟠) — implement edildi, pack-only değil. Backend `Features/PlatformAccount/*`, `PlatformAccountController` `[Authorize(PlatformActor)]`, frontend `Views/Platform/Account/{Profile,Settings}` + JS + RESX, layout initials avatar + dropdown linkleri, gateway routes, `avatars/1.png` hardcoded referans kaldırıldı. v1 scope kasıtlı dar tutuldu: avatar upload, password change, MFA, fake timeline, locale/timezone preferences v1 dışı (sırasıyla storage standardı, PSS-010, MOD-0021, PSS-011 follow-up'larına devredildi). Eski daha geniş `PSS-009-platform-admin-profile.md` retired. Eksik %11: backend unit test coverage 4/9 senaryo + HTTP integration + browser smoke otomasyon.
- **2026-05-15:** MOD-0021 reconciliation (kod kanıtı doğrulaması). Phase 5C 4 maddenin 4'ü implemented: H1 retention GET load ✅, H2 Redact-actor UI ✅ (`index.js:505+` modal), H3 Sidebar nav ✅ (`_LayoutPlatformAdmin.cshtml:236-245`), H4 `_DetailsModal.cshtml` ✅ (47 satır partial). MOD-0021 status `🟠 90 → 🟢 98`.
- **2026-05-15:** MOD-0021 General Audit Trail Phase 1-5C implemented (🟢 %98). Phase 1 Persistence (entities + repos + seed + 7 test), Phase 2 Application Core (IAuditService + redactor + idempotency + recursion guard + retention resolver + 11 test, H1 fix), Phase 3 AuditBehavior (markers + pipeline + 16 test, H1+H2 fix), Phase 4 Worker (BackgroundService + processor + payload mapper + 25 test, C1+H1 fix), Phase 5A Backend API (PlatformAuditController PlatformAdminOnly + 5 endpoint + validators + meta-audit writer + gateway routes via integration-agent, Seçenek A), Phase 5B/5C Frontend UI (AuditLog DataTable + Retention load/update + Redact Actor + nav + modal partial). Kalan %2 = partner_admin audit scope, integration/smoke testleri ve carry-over hardening.
- **2026-05-15:** Platform module pack envanteri reconciliation. 9 yeni pack `execution/.../module-packs/` altına eklendi:
  - **MOD-0012** Secrets & Configuration Vault (review, 50%) — *NEW-001 Secrets Management'ın yeni-ID karşılığı*; iki ID coexist ediyor, NEW-001 legacy olarak işaretlendi (bkz. PSS-PLAN-RECON-1)
  - **MOD-0014** Module Boundary Registry (in-progress, 40%) — Global Domain/Suite/Capability/Module katalog omurgası
  - **MOD-0023** Workflow Designer (review, 0%) — Approvals/SLAs/Escalations
  - **MOD-0024** Task & Checklist Engine (review, 0%) — Generic task primitives
  - **MOD-0031** Evidence Linking Service (review, 0%) — Object ↔ Document linking
  - **MOD-0037** Integration Monitoring & Reconciliation (review, 0%, MVP'de deferred)
  - **PSS-008** Module Details Assignment Inspection (review, 60%)
  - **PSS-010** Platform Admin Password & MFA Security (draft, 0%) — PSS-009 follow-up'tan bağımsız pack oldu
- **2026-05-15:** Master-plan yüzdeleri repo kod kanıtına göre revize edildi ve `%100 için kalanlar` checklist'leri eklendi. Öne çıkan revizyonlar: Tenant Management 88, PSS-004 86, PSS-010 45, MOD-0012 70, MOD-0014 20, MOD-0018 20, MOD-0021 98, MOD-0032 50, MOD-0033 78, MOD-0002 70. Ortak kapanış kriterleri: HTTP integration, gateway smoke, browser smoke, DataTable v2 doğrulaması, audit retrofit, event/job altyapısı, production provider entegrasyonları.
- **2026-05-15:** Üçüncü tur kod-kanıtı doğrulaması (kalan 12 modül için). **6 modülde drift düzeltildi**:
  - **MOD-0046** Tenant Core UI: %70 → **%80** — Frontend full set (Index/Create/Details/Security + 5 JS dosyası, RESX) master-plan altında değerlendirilmişti
  - **MOD-0044** Tenant Manager Backend: %75 → **%82** — 10 lifecycle command (BulkDelete, CreateAdmin, DeleteAdmin, Invite, Reactivate, Register, Suspend, Update) tamamı kod kanıtlı
  - **PSS-010** Platform Admin Security: %45 → **%60** — AuthService'te `MfaChallenge`, `MfaChallengeService`, `PasswordPolicyService`, `IMfaChallengeRepository`, `VerifyMfa`/`ResendMfa` commands mevcut
  - **MOD-0032** API Gateway: %50 → **%65** — `ocelot.json` 60 downstream route + custom `GatewayJwtAuthenticationHandler` + `TenantResolutionMiddleware`
  - **PSS-008** Module Details Assignment: %70 → **%65** — backend tam (Handlers + Queries + 117 satır Contracts + Controller) ama frontend ayrı view yok (ModuleCatalog/Details içinde embedded tab)
  - **MOD-0041** Logging/Monitoring: %35 → **%20** — sadece `Microsoft.Extensions.Logging` (basic), Serilog/OpenTelemetry yok
  - **Uyumlu (değişmedi)**: PSS-004 (85), PSS-005 (92), PSS-006 (92), PSS-007 (88), MOD-0297 (80), MOD-0298 (87)
- **2026-05-15:** İkinci tur kod-kanıtı doğrulaması. **MOD-0012**: master-plan %70 → gerçek **%85** (`Diten.BuildingBlocks.Security.Secrets` 10+ dosya `ISecretsProvider`, `JwtSecretRotationResolver`, `SecretRequirementValidator`, `SecretRedactor` + Tests projesi — production-ready). **MOD-0014**: master-plan %20 → gerçek **%0** (`*ModuleBoundary*`/`*CapabilityGroup*`/`*ModuleDefinition*` HİÇ kod yok, pack-only). **MOD-0002**: master-plan %70 → gerçek **%80** (18+ Application DTO/service + Controller 72 satır + Frontend Index/Details/_ConsumersDataTable/_ConsumersFilter/_IndexL10n + RESX en/tr — Compact DataTable shell tam). **MOD-0043**: master-plan %100 → gerçek **%75** (`Diten.AuditService` ve `Diten.MdmService` service klasörleri yok; pack/audit "done" iddiası ile repo state drift'li). Ders: pack `status: done` ≠ kod kanıtı; AC her maddesi için repo'da fiili dosya görmek zorunlu.
  - **MOD-0046-QG** Tenant Quota Governance UI (approved, 30%) — MOD-0046 ailesinden ayrı UI pack
  - Tenant Management ailesi (MOD-0043/44/46) artık 3 ayrı pack: **MOD-0043** Foundation (done), **MOD-0044** Manager Backend (in-progress), **MOD-0046** Core UI (in-progress)

---

## 11. KAYNAK DOKÜMANLAR

- `docs/platform/module-catalog/api.md`
- `docs/platform/module-catalog/user-manual.md`
- `execution/domains/platform-shared-services/module-packs/PSS-005-tenant-module-catalog.md`
- `execution/domains/platform-shared-services/module-packs/PSS-006-tenant-subscription-plan-catalog.md`
- `execution/domains/platform-shared-services/module-packs/PSS-007-subscription-feature-management.md`
- `execution/domains/platform-shared-services/module-packs/MOD-0298-tenant-module-entitlements-module-pack.md`
- `execution/domains/platform-shared-services/module-packs/MOD-0297-tenant-subscription-management.md`
- `execution/domains/platform-shared-services/module-packs/PSS-004-tenant-login-security-settings.md`
- `execution/domains/platform-shared-services/module-packs/PSS-008-module-details-assignment-inspection.md`
