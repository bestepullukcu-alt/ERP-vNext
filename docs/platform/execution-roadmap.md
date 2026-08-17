# ERP-vNext — Execution Roadmap (2 Developer Paralel)

> **Bu döküman `docs/platform/master-plan.md` companion'ıdır:**
> - **master-plan.md** = "ne yapılacak" (modül başına AC, what's done, %100 için kalanlar)
> - **execution-roadmap.md** = "kim / hangi sırayla / hangi paralelizasyonla" (2-developer faz planı)
>
> **Tarih:** 2026-05-18
> **Versiyon:** 1.0
> **Hedef ekip:** 2 developer (Dev1 backend-infra, Dev2 auth-domain-frontend)

---

## Context

ERP-vNext projesinin objektif durum analizi ve sıralı yapılacaklar zinciri. `docs/platform/master-plan.md` (2451 satır, 2026-05-17 son reconciliation) ve canlı kod karşılaştırması temel alınarak hazırlandı.

### Doğrulanmış Bulgular (kod kanıtıyla)

1. **Master-plan güvenilir.** 5 kritik iddianın spot-check'i kod kanıtıyla doğrulandı:
   - MOD-0018 scaffold (`TenantModuleAuthorizationHandler` + `TenantModuleRequirement` mevcut, `[RequiresModule]` attribute YOK)
   - MOD-0009 tek event (`TenantActivatedV1.cs` tek başına)
   - MOD-0035 RabbitMQ adapter (`MassTransitRabbitMqEventPublisher` + `OutboxPublisherWorker` mevcut)
   - MOD-0027 sadece ad-hoc invite servisleri (generic `INotificationService` YOK)
   - MOD-0014 sıfır kod

2. **MOD-0041 master-plan'in iddia ettiğinden iyi durumda.** Plan %50 diyor ama gerçekte Serilog + Seq sink + JsonFormatter kod olarak hazır (`Program.cs:29,40,44`), sadece `appsettings.json`'da `Observability.Seq.Enabled: false` ve URL boş. OpenTelemetry de aynı şekilde `OtlpExporterEnabled: false` ama infra var. Seq enable etmek bir günlük config işi → gerçek skor ~%65.

3. **Tenant kullanım tarafı master-plan kapsamı dışı.** Tenant User/Role/Profile/Dashboard için ne pack ne master-plan kaydı var. Pack hazırlığı gerekli.

4. **AuthService backend tenant UI'sına hazır.** `Diten.AuthService/Features/{Users,Roles,Permissions}` tam CRUD scaffolding var. Tenant UI'ları sıfırdan değil, mevcut AuthService API üzerine inşa edilecek.

5. **Wave 1 foundation tam olmadan tenant tarafı açılamaz.** Kritik blokerler: MOD-0018 RBAC (%20), MOD-0027 Notification (%0), MOD-0263 Messaging (%0), MOD-0009 Lifecycle Events (%55, 6/7 event eksik), MOD-0035 Event Bus (%78, live broker validation pending).

---

## Kavram Netleştirmesi

### RBAC (`MOD-0018`) ne yapar?
Bir tenant kullanıcısı bir endpoint çağırdığında sistem **2 soru** sormalı:

1. **"Kullanıcının rolü bu işlemi yapabilir mi?"** → `[HasPermission("CRM.Customer.Create")]` (zaten çalışıyor)
2. **"Kullanıcının tenant'ı bu modülü satın aldı mı?"** → `[RequiresModule("CRM")]` (**YOK** — MOD-0018'in işi)

Şu an sadece 1. soru sorulduğu için, yanlışlıkla yetki verilen bir kullanıcı tenant'ının satın almadığı modüle erişebilir. MOD-0018 = bu ikinci kapıyı her endpoint'in önüne koymak. **Hem platform admin hem tenant ERP endpoint'leri** için geçerli; asıl kritik faydası tenant tarafında.

### MOD-0008 — MOD-0018'in alt görevi
"Tenant'a hangi modüller atanabilir?" sorusu şu an 3 controller'da 3 farklı ad-hoc query ile cevaplanıyor. MOD-0008 = bunu tek bir `IPlatformCatalogContract.GetAssignableModulesAsync()` interface'inde merkezleştirmek. MOD-0018 `[RequiresModule]` çalışırken bu interface'i zorunlu çağıracak → **MOD-0018 ile birlikte aynı sprint'te bitirilir, ayrı iş paketi değildir**.

### Messaging Provider (MOD-0263) ≠ Event Bus (MOD-0035)
- **Event Bus** = servisler arası **iç** mesaj (TenantCreated event → Audit + Notification servislerine). RabbitMQ + MassTransit.
- **Messaging Provider** = **dış dünyaya** mail/SMS gönderme. SMTP/SendGrid/Twilio adapter.

Notification flow: Command → Event Bus üzerinden event → Notification Service consume → Template render → **Messaging Provider** mail gönderir.

### PSS-008 — embedded zaten yapılmış, ayrı sayfa gereksiz
Pack "ayrı `/Platform/ModuleAssignments/*` sayfası" düşünmüş; ama mevcut embedded tab (`Views/Platform/ModuleCatalog/Details.cshtml` içinde) **yeterli**. Eksik bir şey yok, sadece pack scope farkı. Plan'da scope-confirm gerekli (yeni sayfa AÇMAYACAĞIZ kararı).

### PSS-010 Active Sessions — MVP'de değil
"Login olmuş tüm cihazları göster + remote logout" özelliği. SOC2 veya laptop kaybı senaryosu için lüks. **Wave 2/3'e ertelendi**, critical path'ten çıkarıldı.

### MOD-0033 Quota Dashboard — kritik değil
Backend (quota consume/release) çalışıyor. Dashboard = tüketim raporu görselleştirmesi. Tenant tarafı için **şart değil**, müşteri görseli istediğinde Wave 2/3'te yapılır.

### MOD-0032 Gateway Hardening — production'a çıkış kapısı
Şu an Ocelot çalışıyor ama: rate limit yok, circuit breaker yok, frontend'de direct-service fallback hâlâ var (güvenlik delik), API versioning (`/api/v1/...`) yok. Production'a çıkmadan kapatılmalı.

### MOD-0041 Logging/Monitoring — master-plan eskiyi söylüyor
Kontrol edildi (2026-05-18):
- `services/Diten.Platform/src/Diten.Platform.API/Program.cs:29,40,44` → `UseSerilog`, `WriteTo.Console(JsonFormatter)`, `WriteTo.Seq(...)` **kod olarak hazır**
- `appsettings.json` `Observability.Seq.Enabled: false`, `Seq.Url: ""` (devre dışı)
- OpenTelemetry: `Tracing.OtlpExporterEnabled: false` (devre dışı, "until collector infrastructure is configured")
- Health checks, correlation, metrics endpoint'leri konfigüre

**Gerçek skor ~%65**, master-plan'in %50 iddiası eski. Seq container kurulup config flag'i `true` yapılırsa anında çalışır. Bu, tek günlük config işi → FAZ A başında halledilebilir.

### MOD-0014 Module Boundary Registry — MVP'de atla
Pack var, kod sıfır. "Aynı modülün iki yere kaydedilmesini önleyen registry" — şu an manuel review ile idare ediliyor. ERP domain'leri bootstrap edilince anlam kazanır. **Plan'dan çıkarıldı**.

---

## Mevcut Durumun Özeti

### ✅ Hazır (%85+)
`MOD-0026` Job Scheduler (90), `MOD-0043` Tenant Foundation (80), `MOD-0044` Tenant Manager (85), `MOD-0046` Tenant Core UI (82), `PSS-005` Module Catalog (93), `PSS-006` Subscription Plan (96), `PSS-007` Subscription Feature (90), `PSS-009` Platform Admin Profile (89), `PSS-011` Lookups (93), `MOD-0298` Entitlements (90), `MOD-0021` Audit (98), `MOD-0012` Secrets (85), `NEW-002` Platform Admins (95), `MOD-0002` Interface Registry (80).

### 🟡 Yarım (kritik bağımlılık)
- `MOD-0018` RBAC (%20) — **FAZ A blocker**
- `MOD-0035` Event Bus (%78) — RabbitMQ live + 6 event emit eksik
- `MOD-0009` Tenant Lifecycle Events (%55) — 6/7 event emit eksik
- `MOD-0041` Logging (~%65 gerçek) — Seq config tek günde çözülür
- `MOD-0297` Subscription Lifecycle (%82) — job'lar Wave 2
- `PSS-004` Tenant Login Security (%86) — IP whitelist runtime test eksik

### 🟡 Yarım (öncelik düşük → Wave 2/3)
- `PSS-008` Module Assignment Inspection (%65) — embedded tab yeterli, ayrı sayfa AÇILMAYACAK
- `PSS-010` Platform Admin MFA + Active Sessions (%60) — MVP dışı
- `MOD-0033` Quota Dashboard (%78) — dashboard MVP dışı
- `MOD-0033-FU01` Quota Governance UI (%60)
- `MOD-0046+` Tenant Core UI Extensions (%60)
- `MOD-0032` API Gateway Hardening (%50) — production öncesi

### 🔴 Sıfır (kritik)
- `MOD-0027` Notification Service — FAZ A blocker
- `MOD-0263` Messaging Provider — FAZ A blocker

### 🔴 Sıfır (MVP dışı, ertelendi)
- `MOD-0014` Module Boundary Registry — plan dışı
- `MOD-0299` Billing
- `MOD-0287` User Notification Prefs
- `MOD-0034` Webhook Delivery
- `MOD-0042` Alerting
- `MOD-0265` SIEM
- `MOD-0038/39` Event Taxonomy + Schema Governance
- `MOD-0003` Data Contract Registry

### 🔴 Pack BİLE Yok (tenant kullanım tarafı — master-plan kapsam dışı)
Tenant User Management, Tenant Role Management, Tenant User-Role Assignment, Tenant My Profile/Settings, Tenant Dashboard, Tenant My Subscription/Entitlements, Tenant Notification Preferences, ERP Domain Bootstrap (MDM/CRM/HR — `Diten.MdmService` yok).

---

## Sıralı Yapılacaklar — 4 Faz (2 Developer Paralel)

> **Rol ataması:**
> - **Dev1 (Backend Infra/Eventing/Ops)** — Logging, Messaging, Notification, Event Bus, Tenant lifecycle events, Job runtime, Gateway hardening.
> - **Dev2 (Auth/Domain/Frontend)** — RBAC, Catalog contract, tenant UI pack'leri, Audit retrofit, ERP domain bootstrap.
>
> İkisi de aynı repo üzerinde çalışır; PR review için karşılıklı reviewer. Daily standup ile bağımlılık noktalarını sync eder.

---

### FAZ A — Wave 1 Foundation Kapanışı

**Süre:** 2 dev paralel ile **2-3 hafta** (tek dev tahminin yarısı).
**Sync noktaları:** Hafta 1 sonu, Hafta 2 sonu (MOD-0018 RBAC, MOD-0035 cache invalidation event'ine bağımlı).

#### Dev1 — Sıra (sequential)

1. **MOD-0041 Seq + OpenTelemetry enable** (~1 gün)
   - `appsettings.json` → `Observability.Seq.Enabled: true`, URL set
   - Seq container kurulumu (docker compose veya local install)
   - Smoke: Platform API isteği → Seq'te JSON log görüldü
   - Dosya: `services/Diten.Platform/src/Diten.Platform.API/appsettings.json`

2. **MOD-0263 Messaging Provider** (3-4 gün)
   - `IMessagingProvider` contract
   - `SmtpEmailProvider` (MailKit) + `FakeProvider` (dev)
   - Yeni klasör: `services/Diten.Platform.Infrastructure/Messaging/`
   - Konfig: `Messaging:DefaultEmailProvider`

3. **MOD-0027 Notification Service** (5-7 gün)
   - `INotificationService.SendAsync(templateKey, recipient, variables)`
   - `NotificationTemplate` + `NotificationDispatch` entities
   - DotLiquid template engine + throttling + retry
   - Mevcut ad-hoc servisleri migrate:
     - `services/Diten.Platform/.../Services/AdminUserInvitationService.cs`
     - `services/Diten.Platform/.../Services/PlatformAdministratorInvitationEmailService.cs`
     - `services/Diten.Platform/.../Services/EmailTemplates/*.cs`
   - İlk template'lar: `platform.admin.invite`, `tenant.invite.email`, `tenant.welcome`, `tenant.password.reset`, `tenant.otp.code`

4. **MOD-0035 Event Bus live broker** (2-3 gün)
   - Local RabbitMQ container kur
   - `RabbitMqEventingIntegrationTests` live runner PASS
   - `OutboxPublisherWorker.cs` production hardening

5. **MOD-0009 Tenant Lifecycle Events** (3-4 gün)
   - 6 eksik event contract: `TenantCreatedV1`, `TenantSuspendedV1`, `TenantReactivatedV1`, `TenantCancelledV1`, `TenantProvisioningCompletedV1`, `TenantProvisioningFailedV1`
   - Eklenecek: `services/Diten.Platform.Contracts/Events/*V1.cs`
   - Tenant lifecycle handler'larına `IEventBus.PublishAsync(...)` çağrısı ekle
   - Referans: mevcut `TenantActivatedV1Consumer.cs`

#### Dev2 — Sıra (sequential)

1. **PSS-004 IP whitelist runtime test + audit hookup** (2 gün)
   - AuthService login akışında IP check integration test
   - `TenantLoginSettings` değişiklikleri MOD-0021 audit'e bağla

2. **MOD-0008 Catalog Contract stabilize** (2 gün)
   - `IPlatformCatalogContract.GetAssignableModulesAsync()` public interface
   - 3 mevcut ad-hoc query'yi bu interface'e yönlendir

3. **MOD-0018 RBAC + Enforcement** (10-14 gün — FAZ A'nın en uzun parçası)
   - `[RequiresModule("CODE")]` ve `[RequiresFeature("FEATURE_X")]` attribute'ları
   - `IEntitlementChecker` contract (batch + deny reason + cache)
   - Mevcut iskelet üzerine: `services/Diten.Platform.Common/.../Authorization/TenantModuleAuthorizationHandler.cs`
   - En az 3 controller'a uygulama + integration test
   - **Dev1'e bağımlılık:** MOD-0035 cache invalidation event (Dev1'in 4. adımı bittiğinde Dev2 cache layer'ı bağlar)
   - **Dev1'e bağımlılık:** MOD-0021 audit deny → MOD-0021 zaten %98 hazır

4. **NEW-002-FU1 audit retrofit** (1-2 gün, MOD-0018 paralel)
   - 8 admin command'a `IAuditableCommand` + `IAuditMetadataProvider` ekle

#### Sync noktası — Hafta 2 sonu
- Dev1 MOD-0035 live broker PASS → Dev2 MOD-0018 cache invalidation event'i bağlayabilir.
- Dev1 MOD-0027 ready → Dev2 yeni tenant create event'inde tenant invite email tetiklenir (Dev2 testte kullanır).

**FAZ A çıkışı:** Tenant invite email gerçekten gidiyor, tenant lifecycle event-driven izleniyor, herhangi bir controller `[RequiresModule]` ile gate'leniyor, Seq'te merkezi log var.

---

### FAZ B — Tenant Kullanım Tarafı Pack Hazırlığı

**Süre:** 1 hafta (2 dev paralel). **FAZ A ile paralel başlatılabilir** (Hafta 2-3'te dev'ler %20-30 zamanlarını ayırır).

Orchestrator Demir Kural #2: kod yazımı yalnız `approved`/`ready-for-dev` pack ile başlar.

#### Dev1 paylaşımı (2 pack):
- **TEN-005** Tenant Dashboard / Landing — `golden_reference: none`
- **TEN-006** Tenant My Subscription & Entitlements (read-only) — `golden_reference: none`

#### Dev2 paylaşımı (4 pack):
- **TEN-001** Tenant User Management UI — `golden_reference: slim` (AuthService Users üzerine)
- **TEN-002** Tenant Role Management UI — `golden_reference: compact` (AuthService Roles + Permissions üzerine)
- **TEN-003** Tenant User-Role Assignment — sub-tab, ayrı pack veya TEN-001'e absorb
- **TEN-004** Tenant My Profile & Settings — `golden_reference: none` (AuthService Users.UpdateUser)

**Her pack'te zorunlu:**
- `golden_reference`, `form_field_count`, `shell: tenant`, `service`, `entity_base`
- L10n: tenant = 7 dil (en, fr, es, zh, ar, ru, tr)
- API: same-origin proxy
- Template: `.antigravity/rules/module-pack-standard.md`
- UI pattern: `frontend/Diten.Web/Views/DevEnablement/GoldenReference{Slim,Compact}/`

**Dev'ler birbirinin pack'ini review eder** (Demir Kural #2 garantisi).

---

### FAZ C — Tenant Kullanım Tarafı Implementation

**Süre:** 2 dev paralel ile **3-4 hafta** (tek dev tahminin 60-70%'i).
FAZ A bitiminden ve FAZ B pack'leri onaylandıktan sonra.

#### Dev1 — sıra:
1. **TEN-005 Tenant Dashboard** (1 hafta) — Login sonrası landing route çözülür, permission-driven menu wiring.
2. **TEN-006 Tenant My Subscription** (1 hafta) — PSS-006 + MOD-0298 read-only proxy.
3. **FAZ D'ye geçer** (MOD-0297/0033 background job'ları, Gateway hardening).

#### Dev2 — sıra (bağımlı, sequential):
1. **TEN-001 Tenant User Management** (1-1.5 hafta) — Slim DataTable + invite flow + AuthService Users CRUD.
2. **TEN-002 Tenant Role Management** (1-1.5 hafta) — Compact DataTable + permission matrix + AuthService Roles CRUD.
3. **TEN-003 User-Role Assignment** (3-5 gün) — TEN-001 + TEN-002 birleşimi.
4. **TEN-004 Tenant My Profile/Settings** (3-5 gün) — Self-service, PSS-009 pattern.

#### Sync:
- Dev2 TEN-001 bittikten sonra Dev1 TEN-005'te user listesini gösterir (cross-pack consume).
- Her tenant pack için bitmeden önce Dev1 review eder (paired review).

**Her UI'da uygulanacak Demir Kurallar:**
- Inline `_Filter` (offcanvas yasak), ColReorder, SaveView via `personalizationClient`
- HttpOnly cookie + same-origin proxy (browser JS `Authorization: Bearer` üretmez)
- L10n bridge: `_IndexL10n.cshtml` + `index.l10n.js` (camelCase → PascalCase)
- Required field markers (kırmızı yıldız + tracker)
- Quality Gate: `python3 .antigravity/scripts/verify_datatable_page.py . --area Tenant --module {ModuleName} --reference slim|compact` PASS

---

### FAZ D — Production Hardening + ERP Domain Bootstrap

**Süre:** Paralel başlar, FAZ C bitiminden önce. 2 dev paralel ile **3-4 hafta**.

#### Dev1 (Infra/Ops odaklı):
1. **MOD-0297-FU1** — Trial expiry, renewal, PastDue auto-suspend job'ları (MOD-0026 üzerine, business logic)
2. **MOD-0033-FU1** — Quota period reset job + 80% warning + breach notification (Dashboard hâlâ MVP dışı)
3. **MOD-0032 Gateway Hardening** — rate limit, circuit breaker, frontend direct-service fallback kaldırma, `/api/v1/` versioning
4. **MOD-0041-FU** — OpenTelemetry OTLP exporter aktif, Prometheus scrape canlı, Grafana data source bağlı
5. **PLATFORM-TEST-1** — Browser smoke (Playwright veya Cypress)

#### Dev2 (Data/Domain odaklı):
1. **MOD-0298-FU1** — Plan değişikliği → entitlement cache invalidation consumer (MOD-0035 üzerine)
2. **MOD-0021-FU-Partner** — partner_admin audit scope (per-tenant filter, partner-scoped redaction)
3. **PLATFORM-TEST-2** — DataTable v2 contract doğrulaması tüm Platform tablolarına
4. **ERP MDM Domain Bootstrap** (FAZ C tamamen bitince):
   - `Diten.MdmService` 5-katmanlı proje iskeleti
   - `execution/domains/master-data-management/` domain config + module packs
   - İlk modüller: Product, Organization, Party, Location
   - Cross-domain contract: MDM → Platform `IEntitlementChecker` kullanır
5. Sonraki domain'ler için pack hazırlığı: CRM, HR, Finance, Inventory

#### Ertelenen (Wave 3+, MVP dışı):
- `PSS-010` Active sessions ekranı (SOC2 hedefi geldiğinde)
- `MOD-0033` Quota dashboard görsel (müşteri talebi geldiğinde)
- `PSS-008` ayrı Module Assignment sayfası (embedded yeterli, AÇILMAYACAK)
- `MOD-0014` Module Boundary Registry (ERP domain'leri çoğaldığında)
- `MOD-0299` Billing
- `MOD-0287/0034/0042/0265/0038/0039/0003` (Wave 3 stratejik)

---

## Toplam Süre Tahmini (2 Developer)

| Faz | Tek Dev | 2 Dev Paralel |
|---|---|---|
| FAZ A | 3-5 hafta | **2-3 hafta** |
| FAZ B | 1-2 hafta (A ile paralel) | **1 hafta** (A ile paralel) |
| FAZ C | 4-6 hafta | **3-4 hafta** |
| FAZ D | 3-5 hafta | **3-4 hafta** |
| **Toplam** | ~12-18 hafta | **~8-11 hafta** (~2-2.5 ay) |

ERP domain (MDM) Faz D'nin sonunda çalışır durumda olur. Tenant kullanım tarafı (login + user/role/permission akışı) Faz C sonunda canlı.

---

## Tenant Tarafı — Platform %100 Bitmeden Başlanabilir mi?

**Kısmi cevap.** Bağımlılık matrisi:

| Tenant iş | Tamamen bekleyen | Paralel başlayabilen |
|---|---|---|
| TEN-001 User Mgmt UI | MOD-0018 | Pack hazırlığı (FAZ B) |
| TEN-002 Role Mgmt UI | MOD-0018 | Pack hazırlığı |
| TEN-004 Profile | yok | Hemen FAZ B'de pack |
| TEN-005 Dashboard | yok | Hemen FAZ B'de pack |
| TEN-006 Subscription view | yok | Hemen FAZ B'de pack |
| ERP MDM/CRM domain | MOD-0018 + TEN-001/002/003 | Domain bootstrap pack paralel |

Yani Platform %100 olması şart değil; **bağımlı parçalar bitince** o tenant modülüne başlayabilirsin. Bu yüzden FAZ B paralel açılır.

---

## Kritik Dosya Yolları

**Master kaynak:**
- `docs/platform/master-plan.md` — tek truth (modül başına `What's done` + `%100 için kalanlar`)
- `docs/platform/master-plan.md` §9.1/9.2/9.3 — wave bazlı status tablosu
- `execution/domains/platform-shared-services/module-packs/*.md` — pack metadata (29 dosya)

**Pack & UI rules:**
- `.antigravity/rules/module-pack-standard.md`
- `.antigravity/rules/frontend-datatable-template.md`
- `.antigravity/rules/frontend-js-standard.md`
- `.antigravity/rules/handler-design.md`
- `.antigravity/workflows/quality-gate-datatable.md`
- `.antigravity/agents/orchestrator.md` (Demir Kurallar)

**FAZ A için backend:**
- `services/Diten.Platform.Common/src/Diten.Platform.Common/Authorization/` — RBAC scaffold
- `services/Diten.Platform.Contracts/Events/` — yeni 6 lifecycle event buraya
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Eventing/` — RabbitMQ adapter
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Services/` — invite servisleri migrate
- `services/Diten.Platform/src/Diten.Platform.API/Program.cs:29,40,44` — Serilog/Seq
- `services/Diten.Platform/src/Diten.Platform.API/appsettings.json` — `Observability.Seq.Enabled`

**FAZ C için backend (zaten var, üstüne UI):**
- `services/Diten.AuthService/src/Diten.AuthService.Application/Features/Users/` (7 dosya)
- `services/Diten.AuthService/src/Diten.AuthService.Application/Features/Roles/` (8 dosya)
- `services/Diten.AuthService/src/Diten.AuthService.Application/Features/Permissions/` (4 dosya)

**Frontend altyapı:**
- `frontend/Diten.Web/Views/Shared/_LayoutTenantShell.cshtml` (hazır)
- `frontend/Diten.Web/wwwroot/assets/js/Shared/personalizationClient.js`
- `frontend/Diten.Web/Views/DevEnablement/GoldenReference{Slim,Compact}/` — pattern

---

## Doğrulama (Her Faz Sonu)

**FAZ A bittiğinde:**
- Seq UI'sında bir Platform API isteğinin JSON log'u görüldü
- `dotnet test services/Diten.Platform/tests/Diten.Platform.Application.Tests/Authorization/` PASS
- Test mail gerçek SMTP üzerinden gönderildi
- `dotnet test services/Diten.Platform/tests/Diten.Platform.Eventing.Tests/` — 7/7 lifecycle event emit
- Live RabbitMQ broker bağlı, `RabbitMqEventingIntegrationTests` PASS
- Master-plan §9.1 yüzdeleri güncellendi + reconciliation notu eklendi

**FAZ B bittiğinde:**
- 6 yeni pack `module-packs/` altında `status: approved`/`ready-for-dev`
- Her pack'te `golden_reference`, `form_field_count`, AC checklist (≥8 madde) dolu

**FAZ C bittiğinde — her tenant pack için:**
- `python3 .antigravity/scripts/verify_datatable_page.py . --area Tenant --module {ModuleName} --reference slim|compact` PASS
- Browser smoke: login → tenant shell → CRUD happy path manuel doğrulandı
- L10n: 7 dil `.resx` dolu
- Quality Gate Datatable checklist tamam

**FAZ D bittiğinde:**
- Master-plan §9.4 follow-up'ı tüm 🔴 maddeler 🟢/🟡
- `dotnet build` warning sıfır
- `ocelot.json` smoke (`curl :5000`) tüm endpoint'ler PASS
- `Diten.MdmService` derleniyor, ilk MDM modülü `Product` end-to-end çalışıyor

---

## Karar Notu

Plan **objektif gözleme dayalı**, kullanıcı tercihlerini içermez. Sıra (FAZ A → B paralel → C → D) **bağımlılık zincirinden** çıkar:

- Tenant kullanıcısı invite alamadan login olamaz → MOD-0027 + MOD-0263 gerekli
- Tenant kullanıcısı modül erişimini check etmek için → MOD-0018 + MOD-0008 gerekli
- Tenant lifecycle event-driven izlenmek için → MOD-0009 + MOD-0035 gerekli
- Tenant UI pack'leri kod yazımı için → Demir Kural #2 gereği önce approved pack
- ERP domain modülleri tenant user/role akışı çalışmadan kullanılamaz → FAZ C sonrasına

**2 Developer paralelizasyonu:**
- Dev1 backend infra/eventing/ops + 2 tenant pack
- Dev2 auth/RBAC + 4 tenant pack + ERP domain bootstrap
- Sync noktaları her hafta sonu; daily standup ile bağımlılık doğrulanır
- Cross-PR review (Demir Kural #2 garantisi)

**Risk noktaları:**
- Dev2'nin MOD-0018'i Dev1'in MOD-0035 cache event'ine bağımlı → Dev1 MOD-0035'i hafta 2 sonuna yetiştirmezse Dev2 cache-less MVP ile devam eder
- Dev1 MOD-0027 → Dev2 TEN-001 (invite flow) bağımlılığı → Dev1 yetişemezse Dev2 FakeProvider ile mock'lar, prod'a çıkmadan değiştirir
- Tek RabbitMQ + Seq container infra'sı kurulum süresi 2-3 saat → Dev1 başlamadan halletmeli

**Alternatif yollar (kullanıcı isterse):**
- Platform Dashboard: FAZ A öncesi yapılabilir ama widget'lar yarım foundation'a dayanır; tam veri ancak FAZ D sonrası gelir.
- Tenant tarafına önce başlama: Demir Kural #2 + MOD-0018 bağımlılığı nedeniyle teknik olarak bloklanır.

---

## Değişiklik Logu

- **2026-05-18:** İlk versiyon. master-plan.md companion'ı olarak yazıldı. 2-developer paralelizasyon, 4 faz, 8-11 hafta tahmin. Kullanıcı kavram netleştirme sorularına (RBAC, MOD-0008, Messaging vs Event Bus, PSS-008/010, MOD-0033/0032/0041/0014) inline cevaplar eklendi.
