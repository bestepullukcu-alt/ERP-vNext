---
id: MOD-0027-FU03
name: Notification Event Catalog & Template Binding
domain: platform-shared-services
service: Diten.Platform
shell: platform-admin
golden_reference: custom-admin-register
entity_base: BaseEntity
status: ready-for-dev
parent: MOD-0027
owner: ali.tufanoglu
branch: feature/pss/mod-0027-fu03-notification-event-catalog
started: 2026-07-08
target: TBD
form_field_count: 0
---

# MOD-0027-FU03 - Notification Event Catalog & Template Binding

> **Identity (DCP-002 — GATE PASSED, 2026-07-08):** MOD-0027-FU03, Blueprint `MOD-0027` (Notification Service) parent'ının kanonik bir FU'sudur. **Registry satırı EKLENDİ** (`module-id-registry.md`: `MOD-0027-FU03 | Follow-up | draft | parent MOD-0027`). Preflight çalıştırıldı ve **PASS**:
> `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0027-FU03 --name "Notification Event Catalog & Template Binding" --parent MOD-0027` → **`OK  MOD-0027-FU03: proven against Blueprint/registry.`** (exit 0).
> **DCP-002 kimlik kapısı GEÇİLDİ.** BuildingBlocks cross-service manifest genişletme onayı **integration-agent tarafından PASS** (2026-07-08) — açık hard blocker kalmadı. **Status: `ready-for-dev`.** Tek kalan madde CONDITIONAL gateway teyidi (implementasyon başında; bkz. §15, §20).

## 1. Module Summary
- **Purpose:** MOD-0027 Notification Service içinde, **sistem event'leri ile notification template key'leri arasındaki resmi, platform-owned sözleşmeyi** tanımlayan **Notification Event Catalog** altyapısını kurar. "Hangi modül hangi bildirim event'ini üretir, o event varsayılan hangi template key'e bağlanır, hangi kanaldan gider, hangi değişkenleri zorunlu kılar, tenant override edebilir mi?" sorularının kanonik cevabını tek yerde tutar.
- **Primary outcome:** (1) Producer modüller (ör. MOD-0023 Workflow) bildirim event'lerini **module manifest** üzerinden deklare eder; (2) Notification Event Catalog bu deklarasyonları okur/senkronize eder, **Module Catalog / ModulePages / Permission Catalog'a karşı doğrular**; (3) Template create/edit akışı ileride **rastgele key yazmak yerine** aktif **event/template slot** listesinden beslenir; (4) Producer'lar `eventCode` ile notification queue eder ve binding aktif değilse dispatch **oluşmaz**.
- **Scope note:** Bu pack **contract-first / catalog-foundation**'dır. Yeni event tipi/entity yaratır (`NotificationEventDefinition`), manifest sync + read contract + template-slot endpoint sağlar ve **read-only Platform Admin katalog görünümü** ekler. Producer flow migration'ı, InApp/SignalR, tenant self-service override UI ve SMS/WhatsApp **kapsam DIŞIdır** (bkz. §2 Out-of-scope).
- **Ilişki:** MOD-0027 (parent, `approved`) veri modeli ve dispatch pipeline'ı; **MOD-0027-FU02** (`Bitti %100`, live smoke PASS 2026-07-08 — bkz. [smoke audit](../../../../docs/audits/pss-mod-0027-fu02-notification-template-ui-smoke-2026-07-08.md)) template/settings/dispatch yönetimi. FU03, FU02'nin `tenant.invite.email` gibi template'lerini **event'lere bağlar** ve template UI'ın gelecekteki "event slot" davranışının sözleşmesini kurar.

### 1.1 Motivasyon (kod incelemesi, 2026-07-08)
- FU02 template'leri **serbest `templateKey` metni** ile oluşturuluyor; hangi event'in hangi key'i beklediği kodda dağınık (ör. `TenantCreatedV1NotificationMapper.TemplateKey = "tenant.invite.email"` sabiti Application içinde gömülü). Event↔template ilişkisi için **kanonik bir katalog yok**.
- Producer entegrasyonu (ör. `AdminUserInvitationService` → `QueueEmailNotificationCommand`, MOD-0027-FU02 refactoru) `templateKey` + `Variables` sözleşmesini elle taşıyor; **required variables** sözleşmesi merkezi değil.
- Module self-registration altyapısı **zaten mevcut**: `Diten.BuildingBlocks.ModuleRegistration.Abstractions.ModuleManifestDocument` (ModuleCode, Pages[], Permissions/Actions) modüller tarafından `IModuleManifestProvider.GetManifest()` ile Platform module-catalog'a push ediliyor. FU03, bu manifest'e `notificationEvents` deklarasyonu ekleyerek **aynı self-registration seam'ini** kullanır — yeni bir paralel altyapı icat etmez.

### 1.2 Kabul edilen MVP kararları (final — 2026-07-08 kullanıcı onayı, bağlayıcı)

| Karar | Final MVP kararı | Durum |
|---|---|---|
| Entity sayısı | **Tek entity: `NotificationEventDefinition`.** Binding (eventCode→defaultTemplateKey+channel+requiredVars) definition üzerinde yaşar; ayrı `NotificationEventBinding`/`NotificationTemplateBinding` **MVP'ye eklenmez**. Çok-kanallı binding matrisi gerçek ihtiyaç olursa **FU04+ follow-up**. | **Kabul (final)** |
| UI kapsamı | **Read-only Platform Admin katalog UI BU pack'te.** Route `/Platform/NotificationEvents`; yalnızca **list / detail / sync-from-manifest / validation issues**. **Create/Edit/_Form ÜRETİLMEZ**; event'ler manuel oluşturulmaz (manifest-driven). Pattern: `custom-admin-register` = **read-only/list-detail** (NotificationDispatches precedent'i). | **Kabul (final)** |
| Manifest genişletmesi | **`ModuleManifestDocument`'a additive + opsiyonel `NotificationEvents` listesi (default boş → mevcut manifest'ler kırılmaz).** Bu **cross-cutting BuildingBlocks** değişikliğidir; repo scope (§5) ve protected paths (§6) çok net sınırlandırılır. Alternatif `INotificationEventManifestProvider` **reddedilen alternatif / follow-up notu** olarak kalır (§7.1). | **Kabul (final)** |
| Permission | **Yeni literaller: `platform.notifications.events.read` + `platform.notifications.events.manage`.** Mevcut `platform.notifications.read`/`configure` **fallback OLARAK KULLANILMAZ**. Alias-map + rol seed beklentisi acceptance/test'e eklenir (§14, §16-G). Template-slot erişim modeli §14'te açık. | **Kabul (final)** |
| EventCode formatı | **Canonical, stable, immutable, lowercase dotted**: `{domain}.{aggregate}.{event}` (ör. `workflow.task.assigned`, `tenant.user.invited`, `document.approval.requested`). Serbest metin gibi ele alınmaz. **Rename YOK** — değişiklik = deprecate + yeni EventCode. | **Kabul (final)** |

### No-shell kuralı (bağlayıcı)
Manifest sync, event list, event detail ve template-slot backend contract'ları çalışır olmadan operasyonel görünen UI/aksiyon YAZILMAZ. **Fake event row, fake sync button, fake active slot, fake validation sonucu YASAKTIR.** Read-only görünüm, controlled empty/error state ve gerçek disabled aksiyon kabul edilir.

### Contract blocker kuralı (bağlayıcı)
Gerekli Module Catalog read contract, ModulePages/pageCode descriptor seam'i, Permission Catalog lookup, manifest format genişletmesi, route/gateway seam veya MOD-0027 notification backend contract eksik/uyumsuz çıkarsa **placeholder UI ÜRETİLMEZ**; eksik kontrat implementation report'ta açık **blocker/follow-up** olarak raporlanır.

## 2. Ownership and Boundaries
### In-scope
- **NotificationEventDefinition** entity (platform-owned system contract; TenantId null / global record — FU02 platform-default stratejisiyle aynı).
- Application: event catalog command/query/handler/validator/model dosyaları (create-via-sync, read, update, archive, template-contract, active-template-slots).
- **Manifest sync**: `ModuleManifestDocument.NotificationEvents` deklarasyonlarını okuyup katalogla senkronize etme + doğrulama (OwnerModuleId / TargetPageCode / RequiredPermissionKey / DefaultTemplateKey format).
- API: §5'teki event catalog read/admin endpoint'leri (mevcut `NotificationsController` veya ayrı `NotificationEventsController` — §10 kararı).
- **Read-only Platform Admin katalog UI** (`/Platform/NotificationEvents`): list + detail + sync aksiyonu + validation issues paneli (create/edit YOK).
- Template UI için **read-only template-slot** endpoint sözleşmesi (FU02 template create/edit'in ileride tüketmesi için — tüketim FU03'te değil).
- Yeni permission literalleri (§14) + alias/seed (onaylanırsa).

### Out-of-scope
- TenantShell **bell (çan) dropdown**, **InApp UserNotification** persistence, **SignalR** / real-time transport → MOD-0027-FU04+.
- **SMS/WhatsApp** provider/adaptör → MOD-0263.
- **Email provider** değişikliği (SMTP/SendGrid davranışı) — MOD-0027 core.
- **Tenant self-service template override UI** → gelecek FU (bu pack yalnızca `CanTenantOverride` policy'sini ve slot sözleşmesini tanımlar; tenant UI'ı yapmaz).
- **Workflow engine redesign** (MOD-0023 internals).
- **Module Catalog / Domain Management / ModulePages ownership** — FU03 yalnızca **read contract tüketir ve doğrular**, sahiplik değişmez.
- **Permission Catalog ownership** — yalnızca lookup/doğrulama.
- **MOD-0285 navigation/menu ownership**.
- **Producer flow migration (SERTLEŞTİRİLDİ):** Bu pack **mevcut invite / workflow / campaign producer flow'larını MİGRATE ETMEZ.** Ne `AdminUserInvitationService`, ne `TenantCreatedV1NotificationMapper`, ne de herhangi bir producer'ın gerçek queue çağrısı bu pack'te değiştirilmez. Yalnızca **event catalog + template slot + manifest sync sözleşmesi** kurulur; producer'ların bu sözleşmeye geçişi ayrı follow-up'tır (§20).
- **Business module implementation migration** — genel iş modülü kod taşımaları kapsam dışı.

### Ownership rule
- FU03, **event↔template binding sözleşmesini** ve katalog kayıtlarını sahiplenir. Module/page/permission **üretmez**, yalnızca referans verir ve doğrular.
- MOD-0027 dispatch orkestrasyonu, resolver, entity'leri **değişmez** (event catalog yalnızca "hangi template" kararını besler; queue/dispatch davranışı FU02'deki gibi kalır).
- Event visibility/binding **backend authorization'ın yerine geçmez** — producer'ın owning module'ünde `[HasPermission]` fail-closed korunur.

## 3. Owned Objects
### Yeni entity (öneri: TEK)
**`NotificationEventDefinition`** (`Diten.Platform.Domain`, `BaseEntity`; platform/global record, TenantId null):

| Alan | Tip | Açıklama |
|---|---|---|
| Id | Guid | PK |
| EventCode | string (lowercase dotted, max 200, **unique**) | Canonical stable event kimliği (ör. `workflow.task.assigned`). Immutable. |
| DisplayNameKey | string? | L10n resource key |
| FallbackDisplayName | string | L10n yoksa gösterilecek ad |
| Description | string? (max 1000) | |
| OwnerDomain | string | Producer domain (Module Catalog/Domain Management ile doğrulanır) |
| OwnerModuleId | string (ModuleCode) | Producer module — **Module Catalog'da var mı doğrulanır** |
| OwnerService | string | Producer servis |
| Channel | enum `NotificationChannelCode` (MVP: Email; InApp future) | Event'in hedef kanalı |
| DefaultTemplateKey | string (lowercase dotted, max 160) | Event→template binding; FU02 template key formatıyla uyumlu |
| RequiredVariables | list `TemplateVariableDefinition` (name, type, required) | **FU02 template variable contract ile aynı şekil** (preview/save validation uyumu) |
| OptionalVariables | list `TemplateVariableDefinition` | |
| CanTenantOverride | bool | Tenant, bu event'in template'ini override edebilir mi (tenant UI ileride yalnızca `true` olanları görür) |
| UsageType | enum `SystemEvent` / `ManualSelection` | Sistem event'i (producer otomatik) vs. manuel seçilen (custom/business) |
| IsSystemEvent | bool | `UsageType==SystemEvent` türevi/aynası (sorgu kolaylığı) |
| TargetPageCode | string? | ModulePages `PageCode` referansı — **ModulePages descriptor'da var mı doğrulanır** |
| TargetRouteDescriptorId | Guid? | ModulePage descriptor Id referansı (opsiyonel, pageCode ile birlikte) |
| RequiredPermissionKey | string? | Producer'ın event'i tetiklemek için gerektirdiği permission — **Permission Catalog'da var mı doğrulanır** |
| DefaultSeverity | enum (Info/Success/Warning/Critical — öneri) | InApp/gelecek görsel şiddet |
| LinkPolicy | enum/string (öneri: None/TargetPage/CustomUrl) | Bildirimin bağlanacağı hedef politikası |
| Status | enum `Draft` / `Active` / `Deprecated` / `Archived` | Event lifecycle (§ lifecycle) |
| ManifestSource | string? | Hangi manifest/modülden geldiği |
| ManifestVersion | string? | `ModuleManifestDocument.ModuleVersion` |
| LastSyncedAt | DateTimeOffset? | Son manifest sync zamanı |
| IsDeleted / DeletedAt | bool / DateTimeOffset? | Soft delete (zorunlu) |
| CreatedAt / CreatedBy | | audit |
| UpdatedAt / UpdatedBy | | audit |

> **Ikinci entity kararı:** `NotificationEventBinding` / `NotificationTemplateBinding` **ÖNERİLMİYOR** (MVP). Tek default binding + tek channel definition üzerinde tutulur; entity şişmesi yapılmaz. Çok-kanallı veya çok-template binding matrisi gerçek ihtiyaç olduğunda FU04+ ayrı binding entity'si ekler (gerekçelendirilerek).

### Backend eklemeleri (mevcut `Features/Notifications` içine — FU02 düzeni korunur)
- Queries: `GetNotificationEventListQuery`, `GetNotificationEventByCodeQuery`, `GetNotificationEventTemplateContractQuery`, `GetActiveTemplateSlotsQuery`.
- Commands: `SyncNotificationEventsFromManifestCommand`, `UpdateNotificationEventCommand`, `ArchiveNotificationEventCommand`.
- Handlers (`Handlers/QueryHandlers`, `Handlers/CommandHandlers`), Validators, DTO'lar (mevcut `NotificationContracts.cs`'e eklenir — yeni Models dosyası açılmaz).
- Manifest okuma/mapping servisi: `NotificationEventManifestSyncService` (Application; ModuleManifestDocument.NotificationEvents → definition mapping + validation).

### Manifest genişletmesi (cross-cutting — §7.1)
- `Diten.BuildingBlocks.ModuleRegistration.Abstractions.ModuleManifestDocument`'a additive opsiyonel `IReadOnlyList<ModuleManifestNotificationEvent> NotificationEvents = []` + `ModuleManifestNotificationEvent` record (§ Manifest bağlantısı alanları).

### Permissions (final — yeni literaller, §14)
```text
platform.notifications.events.read      (list / detail / template-contract / active-slots)
platform.notifications.events.manage    (sync-from-manifest / update / archive)
```
Mevcut `platform.notifications.read` / `configure` **fallback olarak KULLANILMAZ**. Yeni literaller `PermissionAliasMap.cs`'e alias'lı ve PlatformActor rollerine seed edilir (§16-G kabul kriteri). **Template-slot erişim modeli** (§14): NotificationEvents UI → `events.read/manage`; `active-template-slots` read endpoint'i template create/edit için **`templates.read` VEYA `templates.create/update`** sahibi aktör tarafından da okunabilir olmalı.

## 3.1 Manifest bağlantısı (Module Catalog / Manifest ile entegrasyon — açık tasarım)
Module manifest (`ModuleManifestDocument`) mevcut alanlarına ek olarak `notificationEvents` deklare edebilir. `ModuleManifestNotificationEvent` alanları:

| Alan | Doğrulama kaynağı |
|---|---|
| eventCode | katalog içinde unique; format regex |
| displayNameKey / description | — |
| channel | `NotificationChannelCode` enum |
| defaultTemplateKey | template key format (FU02) |
| requiredVariables / optionalVariables | `TemplateVariableDefinition` şekli |
| targetPageCode | **ModulePages `PageCode`** (var mı?) |
| targetRouteDescriptorId | **ModulePage descriptor Id** (var mı?) |
| requiredPermissionKey | **Permission Catalog** (var mı?) |
| canTenantOverride | policy flag |
| usageType | `SystemEvent` / `ManualSelection` |
| severityDefault | — |
| linkPolicy | — |
| status | lifecycle |

**Notification Event Catalog sync şunu yapar:**
1. Manifest'teki her `notificationEvents` deklarasyonunu okur.
2. **OwnerModuleId** (ModuleCode) Module Catalog'da var mı doğrular.
3. **targetPageCode / targetRouteDescriptorId** ModulePages descriptor'da var mı doğrular.
4. **requiredPermissionKey** Permission Catalog'da var mı doğrular.
5. **defaultTemplateKey** formatını doğrular (varlık zorunlu değil — template sonradan oluşturulabilir; ama format geçerli olmalı).
6. `requiredVariables` contract'ını saklar (template preview/save validation ile aynı şekil).
7. Event/template binding kaydını yönetir (upsert by `eventCode`; HARD alanları reconcile eder, Status geçişlerini kurallı yapar).
8. Template UI'ın tüketmesi için read endpoint sağlar (`template-contract`, `active-template-slots`).

**Doğrulama başarısızsa:** event **Active yapılmaz** → `Draft` kalır + **validation issue** üretilir → template-slot listesine düşmez → producer bu event ile dispatch **oluşturamaz**. Sistem module/page/permission sahipliği **MOD-0027'ye taşınmaz** (yalnızca referans doğrulanır).

## 4. Entity Fields (form/DTO kontratı)
Bu pack **manuel create/edit formu İÇERMEZ** (`form_field_count: 0`) — event'ler manifest-driven'dır. Read-only DTO'lar entity alanlarını (§3) birebir yansıtır. `UpdateNotificationEventCommand` yalnızca **operator-owned SOFT alanları** (DisplayNameKey/FallbackDisplayName override, CanTenantOverride, Status geçişi, DefaultSeverity, LinkPolicy) düzenler; **HARD alanlar** (EventCode, OwnerModuleId, Channel, RequiredVariables) manifest sync tarafından reconcile edilir, elle değiştirilmez.

## 5. Repo Scope
- `execution/domains/platform-shared-services/module-packs/MOD-0027-FU03-notification-event-catalog-template-binding.md`
- `services/Diten.Platform/src/Diten.Platform.Domain/**` — **yalnızca** `NotificationEventDefinition` entity (+ enum'lar gerekiyorsa `Notifications` enum grubuna ek)
- `services/Diten.Platform/src/Diten.Platform.Application/Features/Notifications/**` — event catalog command/query/handler/validator/model + manifest sync servisi + DTO eklemeleri
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/**` — `NotificationEventDefinition` repository + (gerekiyorsa) `eventCode` unique index
- `services/Diten.Platform/src/Diten.Platform.API/Controllers/Platform/` — `NotificationsController` (events action'ları) **veya** yeni `NotificationEventsController` (§10 kararı)
- `services/Diten.Building.Blocks/src/Diten.BuildingBlocks.ModuleRegistration.Abstractions/**` — **yalnızca** `ModuleManifestDocument`'a additive `NotificationEvents` alanı + `ModuleManifestNotificationEvent` record (cross-cutting — §7.1, dikkatli)
- `frontend/Diten.Web/Controllers/Platform/NotificationEventsController.cs` (UI dahilse)
- `frontend/Diten.Web/Views/Platform/NotificationEvents/**` (UI dahilse — read-only/list-detail)
- `frontend/Diten.Web/wwwroot/assets/js/Platform/NotificationEvents/**` (UI dahilse)
- `frontend/Diten.Web/Resources/Views/Platform/NotificationEvents/**` (UI dahilse — en+tr)
- `frontend/Diten.Web/Navigation/PlatformNavigationCatalog.cs` (UI dahilse — permission-gated menü öğesi; FU02 precedent'i)
- `frontend/Diten.Web/Views/Shared/_LayoutPlatformAdmin.cshtml` — sidebar (data-driven catalog üzerinden)
- **Module Catalog / Domain Management / ModulePages / Permission Catalog** — **yalnızca read contract tüketilir, sahiplenilmez** (bu dosyalara **write YOK**)

**Cross-service sınırı (bağlayıcı):** Bu pack, **`Diten.Platform` servisi + `frontend/Diten.Web` (UI)** dışında **tek bir cross-service dosya değişikliği** yapar: `Diten.BuildingBlocks.ModuleRegistration.Abstractions.ModuleManifestDocument`'a additive/opsiyonel `NotificationEvents` alanı. Başka hiçbir servise (Auth/Mdm/EnterpriseStrategy/DevEnablement) ve başka hiçbir BuildingBlocks dosyasına dokunulmaz. Producer modüllerin manifest provider'ları (ör. `DocumentManagementManifestProvider`) bu pack'te **değiştirilmez** — `notificationEvents` deklarasyonunu producer'lar kendi pack'lerinde ekler.

## 6. Protected Paths
- `.antigravity/**`
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml` (FROZEN)
- `frontend/Diten.Web/Controllers/Archive/**`, `Views/Archive/**`
- `gateway/Diten.ApiGateway/**/ocelot.json` (mevcut `/api/platform/notifications/{everything}` yeterli; değilse integration-agent — §15)
- `services/Diten.AuthService/**`, `services/Diten.MdmService/**`, `services/Diten.EnterpriseStrategyService/**`, `services/Diten.DevEnablementService/**`
- **Module Catalog / Domain Management / ModulePages ownership dosyaları** — **WRITE YOK**, yalnızca read contract tüketilir. Notification Event Catalog **module/page/permission ÜRETMEZ**; yalnızca manifest referanslarını (ModuleCode/pageCode/permissionKey) **doğrular**.
- **Permission Catalog ownership dosyaları** — **WRITE YOK**, yalnızca lookup/doğrulama (FU03 kendi `platform.notifications.events.*` literallerini seed eder; başka modülün permission'ını üretmez/değiştirmez)
- **BuildingBlocks:** `ModuleManifestDocument` additive `NotificationEvents` alanı **DIŞINDA** hiçbir BuildingBlocks dosyasına yazılmaz
- **MOD-0285 navigation/menu management** alanları
- **MOD-0027-FU02** template/settings/dispatch UI davranışları (değişmez; FU03 yalnızca event↔template binding ekler)
- **TenantShell bell dropdown** (bu pack'te yok)
- **SignalR / real-time transport** dosyaları
- **SMS/WhatsApp / provider adapter** alanları
- **Workflow engine internals** (MOD-0023)
- MOD-0027 mevcut command/handler/dispatch davranışı (davranış değişikliği yasak)

## 7. Dependencies
- **MOD-0027 (parent):** notification veri modeli, dispatch pipeline, `NotificationChannelCode`, `TemplateVariableDefinition`, `platform.notifications.*` permission ailesi.
- **MOD-0027-FU02 (`Bitti`):** template key formatı, template variable contract, render-preview/save validation, NotificationDispatches read-only precedent'i (bu pack'in UI kalıbı).
- **Module self-registration (BuildingBlocks):** `ModuleManifestDocument` + `IModuleManifestProvider` (DevEnablement + DocumentManagement precedent'i). Manifest `notificationEvents` deklarasyonunun kaynağı.
- **Module Catalog / Domain Management:** ModuleCode/Domain doğrulama kaynağı (source of truth).
- **ModulePages:** `PageCode` / route descriptor doğrulama seam'i (`ModulePagesController`, `ModulePageDescriptorCommand`).
- **Permission Catalog / RBAC:** `requiredPermissionKey` doğrulama + FU03 kendi permission seed'i.
- **DataTable v2 + SweetAlert2 (MOD-0013)** — UI dahilse.

### 7.1 Cross-cutting karar: manifest genişletmesi (BuildingBlocks) — **integration-agent PASS (2026-07-08)**
`ModuleManifestDocument` **birden çok servis** tarafından tüketilen paylaşılan bir contract'tır. `NotificationEvents` alanı **additive + opsiyonel (default boş/null)** eklenerek geriye dönük uyum korunur.
- **Karar: additive field (final).** Ayrı `INotificationEventManifestProvider` alternatifi **reddedildi** — iki paralel manifest seam'i drift riski yaratır; event'ler aynı manifest'teki page/permission'a referans verdiği için tek manifest source-of-truth korunur.
- **Integration-agent değerlendirmesi (PASS):** 9 producer named-arg kullanıyor (source-compat); pozisyonel deconstruction yok; alıcı (`InternalModuleRegistrationController` → `RegisterModuleManifestCommand`) default System.Text.Json (`UnmappedMemberHandling.Disallow` yok) → bilinmeyen üye yok sayılır (new→old güvenli), eksik property → null (old→new, coalesce ile güvenli). Mevcut reconcile/prune akışı `NotificationEvents`'e dokunmaz. **Cross-service hard blocker KAPANDI** (guardrail'ler §16-B'de zorunlu).
- **Rollout:** unknown-ignored + missing→empty olduğundan BuildingBlocks sürüm yayılımı **sıra bağımsız non-breaking**.

## 8. Runtime Constraints
- `NotificationEventDefinition` **platform/global** kayıttır (TenantId null) — MOD-0027 platform-default stratejisiyle aynı. **Tenant event oluşturamaz.**
- `EventCode` **stable ve unique**; oluşturulduktan sonra immutable (rename → deprecate + yeni code).
- Request/DTO payload'ları tenant taşımaz; event catalog PlatformActor + ilgili permission ile yönetilir.
- API yanıtları `Response<T>` envelope; proxy controller envelope'u değiştirmeden geçirir; browser same-origin `/Platform/NotificationEvents/api/...` (5001) → Gateway (5000) → Platform (5057); `5000/5057`'ye doğrudan gitmez.
- `Deprecated`/`Archived` event **yeni template-slot listesine düşmez**; mevcut dispatch history bozulmaz (geçmiş dispatch kayıtları eventCode/templateKey'i olduğu gibi korur).
- Binding **Active değilse** producer notification dispatch **yapmamalıdır** (producer entegrasyonu bu kuralı okur; enforcement producer tarafında + queue handler doğrulamasında).
- Loglara/console'a manifest ham secret'i, tam gövde, alıcı dökümü yazılmaz (FU02 kuralı devam).

## 9. Layout & Shell Contract
- `shell: platform-admin`.
- UI dahilse: TÜM `.cshtml` dosyalarında AÇIKÇA `Layout = "_LayoutPlatformAdmin";`.
- View klasörü: `Views/Platform/NotificationEvents/`; route `/Platform/NotificationEvents`.
- `_ViewStart.cshtml` değişmez; `Areas/` KULLANILMAZ.
- Canlı referans: `Views/Platform/NotificationDispatches/` (FU02 read-only/list-detail örneği).

## 10. Backend File Convention
Golden Reference backend kalıbı **mevcut** `Features/Notifications` düzenine ek olarak (FU02 gibi; yeni feature klasörü açılmaz):
```text
Features/Notifications/
├── Queries/ (GetNotificationEventList / ByCode / TemplateContract / ActiveTemplateSlots) — YENİ
├── Commands/ (SyncNotificationEventsFromManifest / UpdateNotificationEvent / ArchiveNotificationEvent) — YENİ
├── Handlers/QueryHandlers/ + Handlers/CommandHandlers/ — YENİ
├── Validators/ — YENİ
├── Services/ NotificationEventManifestSyncService — YENİ
└── (NotificationContracts.cs'e DTO eklemeleri)
```
Naming: `{Verb}{Object}Handler` / `{Verb}{Object}Validator` — `Command/Query/Request` suffix YASAK. Tek dosya tek public tip.

**Controller kararı:** Event catalog endpoint'leri mevcut `NotificationsController`'a eklenebilir **veya** okunabilirlik için ayrı `NotificationEventsController` (`[Route("api/platform/notifications/events")]`, `[Authorize(Policy="PlatformActor")]`) açılabilir. **Öneri: ayrı `NotificationEventsController`** (NotificationsController zaten büyük; ayrım net). Her iki yol da mevcut gateway `{everything}` route'u altında kalır.

## 11. Frontend File Contract (UI bu pack'e dahilse — read-only/list-detail)
```text
Views/Platform/NotificationEvents/
├── Index.cshtml           (Layout AÇIKÇA; _Filter → _DataTable → Scripts/_IndexL10n)
├── _Filter.cshtml         (owner module, channel, status, canTenantOverride, usageType)
├── _DataTable.cshtml      (data-dt-standard="v2" + #skeleton-loader)
├── Details.cshtml         (event contract: required variables, default template key, target page, required permission, validation issues)
├── _IndexL10n.cshtml
└── NotificationEventsIndex.cs (marker)

wwwroot/assets/js/Platform/NotificationEvents/ : index.js + index.l10n.js (+ details.js)
Resources/Views/Platform/NotificationEvents/ : NotificationEventsIndex.{en|tr}.resx
```
- **Read-only/list-detail alt kümesi** (NotificationDispatches precedent'i): `Create.cshtml`/`Edit.cshtml`/`_Form.cshtml` **ÜRETİLMEZ** (event'ler manuel oluşturulmaz). BulkActionBar YOK.
- Tek yazma-benzeri aksiyonlar: **Sync from manifest** (`POST /events/sync-from-manifest`, `events.manage`) + **Archive** (geçerli durumda) + **Update** (SOFT alanlar) — hepsi gerçek backend davranışına bağlı.
- Dropdown/filtre değerleri lookup/proxy'den beslenir; JS'te hardcoded fallback YOK. `window.L10n` PascalCase köprüsü ZORUNLU. DataTable v2 + skeleton loader.
- **Verifier notu:** compact değil; NotificationDispatches ile aynı read-only verifier yaklaşımı (Index-seviyesi kontrat; Create/Edit/_Form yokluğu kasıtlı karar olarak raporlanır — verifier'ı geçirmek için sahte sayfa üretmek YASAK).

**UI kapsam kararı (açık):** UI **BU pack'e dahildir** (read-only katalog gözlemlenebilirliği; sync/list/detail/validation gerçek backend'e bağlı). Eğer kullanıcı incelemesinde kapsam daraltılmak istenirse, UI ayrı bir FU'ya bırakılabilir ve bu pack **backend contract + catalog foundation** olarak sınırlandırılır — bu durumda §5/§11 UI satırları kaldırılır ve `golden_reference` alanı kaldırılır. Karar ready-for-dev öncesi netleştirilir.

## 12. Validation Rules
| Alan | Kural | Sync davranışı |
|---|---|---|
| EventCode | lowercase dotted, max 200, unique | duplicate/geçersiz → event Draft + issue |
| DefaultTemplateKey | template key formatı (FU02) | geçersiz format → issue; template yokluğu **issue değil** (sonradan oluşturulabilir) |
| OwnerModuleId | Module Catalog'da mevcut | yoksa → Active olmaz + issue |
| TargetPageCode / RouteDescriptorId | ModulePages descriptor'da mevcut | yoksa → issue (Active engellenir) |
| RequiredPermissionKey | Permission Catalog'da mevcut | yoksa → issue (Active engellenir) |
| RequiredVariables | name alfanümerik/dot/underscore; type enum; **FU02 preview/save ile uyumlu** | uyumsuz → issue |
| Status geçişi | Draft→Active→Deprecated→Archived (geri geçiş kuralı) | geçersiz geçiş → 409 |
| CanTenantOverride | bool | — |

## 13. Failure Path to Verify
1. **Invalid owner module:** manifest event'i **Module Catalog'da olmayan** `OwnerModuleId` referanslar → event **Active yapılmaz** → `Draft` + validation issue → template-slot listesine düşmez → producer dispatch oluşturamaz. Sahiplik MOD-0027'ye **taşınmaz**.
2. **Invalid pageCode / routeDescriptor:** `TargetPageCode`/`TargetRouteDescriptorId` **ModulePages descriptor'da yok** → validation issue → Active engellenir.
3. **Invalid requiredPermissionKey:** `RequiredPermissionKey` **Permission Catalog'da yok** → validation issue → Active engellenir.
4. **Duplicate EventCode:** aynı `EventCode` ikinci deklarasyon → reddedilir + issue; katalog **tek kanonik** kayıt tutar (uniqueness guard).
5. **Invalid DefaultTemplateKey format:** template key formatı bozuk → issue; sistem template'inde binding'den gelmeyen **rastgele key engellenir** (`UsageType=ManualSelection` custom template'ler ayrı sınıflandırılır). Template'in henüz var olmaması issue **değildir** (sonradan oluşturulabilir).
6. **Deprecated/Archived event slot listesine düşmez:** yeni `active-template-slots` listesinde **görünmez**; mevcut **dispatch history bozulmaz**; yeni producer entegrasyonunda kullanılmamalı (uyarı).
7. **Unauthorized actor:** `events.read` yoksa katalog açılmaz (menü gizli + direct URL 403); `events.manage` yoksa sync/archive/update aksiyonu render edilmez + direct çağrı 403.
8. **Manifest sync partial failure:** geçerli event'ler senkronize olur, geçersizler issue listesine düşer; sync **atomik-değil-ama-raporlu** (her event tek tek synced/updated/issue sonucu döner) — kısmi başarı tüm sync'i düşürmez.
9. **Tenant event creation attempt rejected:** tenant aktörü (veya tenant-scoped istek) event oluşturmaya/yönetmeye çalışırsa **fail-closed reddedilir** (403/policy); event catalog yalnızca PlatformActor + `events.manage`.
10. **CanTenantOverride=false event:** tenant override listesinde (ileride) **görünmez** (contract seviyesinde enforced).
11. **Binding aktif değilken producer dispatch denemesi:** event binding `Active` değilse queue handler dispatch **oluşturmaz** (controlled fail).

## 14. Authorization Convention
- Policy: `[Authorize(Policy = "PlatformActor")]` (backend mevcut).
- **Permission literalleri (final, yeni):** `platform.notifications.events.read` (list/detail/template-contract/active-slots), `platform.notifications.events.manage` (sync/update/archive). Gerekçe: event catalog, template/dispatch'ten ayrı bir admin yüzeyi; ayrı literal en az ayrıcalık ilkesine uyar. Mevcut `read`/`configure` **fallback olarak kullanılmaz.**
- `PermissionAliasMap.cs`'e alias + PlatformActor rol seed'i eklenir (FU02 precedent'i). `actor_type=platform_admin` otomatik geçer; rol-bazlı kısıtlı aktör testi implementation başında.
- **Erişim modeli (açık — final):**
  - **Platform Admin NotificationEvents UI** (list/detail/sync/validation/archive): `platform.notifications.events.read` (okuma), `platform.notifications.events.manage` (sync/update/archive).
  - **Template create/edit slot listesi** (`active-template-slots` / `template-contract` read): **`platform.notifications.templates.read` VEYA `platform.notifications.templates.create/update`** sahibi aktör bu read endpoint'ini çağırabilmeli — böylece template UI, event catalog manage iznine gerek olmadan aktif slot'ları okur. (Uygulama: slot read endpoint'i `events.read` **veya** `templates.read/create/update` kabul eder; seed buna göre.)
- **Tenant admin event catalog yönetemez.** Tenant override UI (ileride) yalnızca `CanTenantOverride=true` slot'ları görür.
- **Producer integration:** `eventCode` ile queue ederken backend authorization **owning module'de kalır** (event catalog authz'nin yerine geçmez).

## 15. Gateway / API Routing Decision
- **Karar (öneri): Gateway değişikliği GEREKSİZ.** Mevcut `ocelot.json` `/api/platform/notifications` + `/api/platform/notifications/{everything}` route çifti (FU02'de doğrulandı) `/events/*` ve `/template-slots` yollarını `{everything}` altında kapsar.
- Implementasyonda kapsamadığı **kanıtlanırsa** bu pack gateway'e **DOKUNMAZ**; ayrı **integration-agent** follow-up task'ı açılır (explicit upstream/downstream çifti, OPTIONS dahil).
- Browser her zaman Diten.Web same-origin proxy üzerinden gider; `5000/5057`'ye doğrudan istek YOK.

## 16. Acceptance Criteria
Gruplar sırayla (A→H); her grup no-shell'e tabi. **A ve B tamamlanmadan C→H'ye geçilmez** (kimlik ve contract seam'i önce).

### A. Registry / DCP preflight (governance — ÖNCE) — **TAMAMLANDI (2026-07-08)**
- [x] `verify_module_id.py --check-id MOD-0027-FU03 --name "Notification Event Catalog & Template Binding" --parent MOD-0027` → **`OK  MOD-0027-FU03: proven against Blueprint/registry.`** (exit 0).
- [x] `module-id-registry.md`'ye MOD-0027-FU03 satırı eklendi (Follow-up, parent MOD-0027, status draft, governance reservation).
- [ ] Status yalnızca **tüm** ready-for-dev ön koşulları kapanınca `ready-for-dev`'e alınabilir — A kapandı, ancak BuildingBlocks cross-service onayı hâlâ açık (bkz. §18/§20).

### B. Manifest contract extension (cross-cutting) — **integration-agent PASS; guardrail'ler ZORUNLU**
- [ ] `ModuleManifestDocument`'a additive/opsiyonel `NotificationEvents` eklendi; **mevcut manifest'ler kırılmadı** (geriye dönük uyum testi).
- [ ] `ModuleManifestNotificationEvent` record §3.1 alanlarını taşır (tolerant/default'lu).
- [ ] Bu genişletme dışında **başka cross-service dosya değişmedi** (Auth/Mdm/ESBP/DevEnablement + diğer BuildingBlocks'a write yok).

**Zorunlu integration guardrail'leri (integration-agent PASS koşulu — hepsi acceptance):**
- [ ] `NotificationEvents` record'un **EN SONUNA**, **nullable + default** ile eklendi: `IReadOnlyList<ModuleManifestNotificationEvent>? NotificationEvents = null`. Mevcut parametre **sırası/tipleri değişmedi**; `Icon`/`IsBaseline` **kaldırılmadı/değiştirilmedi**.
- [ ] Tüm tüketiciler **null→empty coalesce** yapar: `?? Array.Empty<ModuleManifestNotificationEvent>()` (Pages gibi non-null varsayılmaz).
- [ ] Alıcıda **default System.Text.Json davranışı korundu**; `UnmappedMemberHandling.Disallow` **eklenmedi**.
- [ ] `ModuleManifestNotificationEvent` alanları **tolerant/default'lu** tasarlandı.
- [ ] **`NotificationEvents` içermeyen eski JSON deserialize edilince exception YOK** (backward-compat deserialization testi).
- [ ] **Mevcut 9 manifest provider DEĞİŞTİRİLMEDİ**; producer manifest provider'ları bu pack'te `notificationEvents` eklemek için **güncellenmedi** (opt-in, ayrı pack).
- [ ] BuildingBlocks tarafında **yalnızca `ModuleManifestDocument.cs`** (+ aynı contract alanında `ModuleManifestNotificationEvent` record) değişti; **başka BuildingBlocks dosyasına dokunulmadı**.
- [ ] **Existing module self-registration reconcile/prune flow kırılmadı** (regresyon testleri yeşil).

### C. NotificationEventDefinition persistence + uniqueness
- [ ] `NotificationEventDefinition` entity + repository çalışır; soft delete (`IsDeleted/DeletedAt`).
- [ ] **`EventCode` unique** (index + write-path guard); duplicate reddedilir.
- [ ] `EventCode` **immutable** (update HARD alanı değiştiremez; rename yok → deprecate + yeni code).

### D. Manifest sync + validation
- [ ] `SyncNotificationEventsFromManifestCommand` manifest `notificationEvents`'i okur/senkronize eder; her event için sonuç (synced/updated/issue) döner (partial-failure raporlu, §13).
- [ ] Sync `OwnerModuleId` / `TargetPageCode` / `RequiredPermissionKey`'i **Module Catalog / ModulePages / Permission Catalog'a karşı doğrular**; geçersiz referans event'i **Active yapmaz** → `Draft` + validation issue.
- [ ] `DefaultTemplateKey` format doğrulaması (FU02); template yokluğu issue değil, format geçersizliği issue.

### E. Active template slot query
- [ ] `GetActiveTemplateSlotsQuery` yalnızca **Active + geçerli** event'leri döner; **Deprecated/Archived düşmez.**
- [ ] `GetNotificationEventTemplateContractQuery` event'in required/optional variables + default template key contract'ını **FU02 template validation ile aynı şekilde** döner.

### F. Read-only Platform Admin UI
- [ ] `/Platform/NotificationEvents` read-only DataTable v2 **gerçek API'den** beslenir (fake event YOK); filtreler: owner module / channel / status / canTenantOverride / usageType.
- [ ] Details: event contract + required variables + default template key + target page + required permission + **validation issues** (raw stack trace YOK, controlled mesaj); controlled empty/error/loading state.
- [ ] "Sync from manifest" aksiyonu gerçek sync çalıştırır; sonuç/issue paneli görünür. **Create/Edit/_Form ÜRETİLMEMİŞ** (read-only kanıtı).
- [ ] Same-origin: browser network'te yalnızca `/Platform/NotificationEvents/api/...`; `5000/5057`'ye doğrudan istek YOK.

### G. Permission / seed / alias
- [ ] `platform.notifications.events.read` + `events.manage` literalleri `PermissionAliasMap.cs`'te alias'lı ve PlatformActor rollerine **seed** edildi (mevcut `read`/`configure` fallback KULLANILMADI).
- [ ] İzinsiz aktörde: NotificationEvents menü/aksiyon gizli + direct URL **403**; `events.manage` olmadan sync/archive/update render edilmez.
- [ ] `active-template-slots` read endpoint'i `events.read` **VEYA** `templates.read/create/update` sahibi aktör tarafından okunabilir (template UI uyumu — §14).
- [ ] **Tenant aktörü event catalog yönetemez / event oluşturamaz** (fail-closed).

### H. Build / test / verifier / smoke
- [ ] `dotnet build Platform.API` + (UI dahil) `dotnet build Diten.Web` → 0 hata.
- [ ] §17 unit/integration testleri PASS; mevcut MOD-0027 + FU02 testleri **regresyonsuz**.
- [ ] (UI) NotificationDispatches read-only verifier yaklaşımı uygulanır ve raporlanır; RESX en+tr parity; `index.l10n.js` PascalCase köprüsü.
- [ ] (UI) Browser smoke §17 sırasıyla PASS.
- [ ] `module-implementation-status.md` FU03 satırı aynı PR'da eklenir/güncellenir.

## 17. Test Expectations
### Build
- `dotnet build services/Diten.Platform/src/Diten.Platform.API/Diten.Platform.API.csproj -c Debug`
- `dotnet build frontend/Diten.Web/Diten.Web.csproj -c Debug` (UI dahilse)

### Unit / integration
- `NotificationEventDefinition` repository tests (CRUD-by-sync, soft delete).
- **EventCode uniqueness** tests.
- Manifest sync **parser/mapper** tests (ModuleManifestDocument.NotificationEvents → definition).
- **Invalid owner module / pageCode / permissionKey** → Active olmaz + issue tests.
- **Active template slot query** tests (Deprecated/Archived exclusion).
- **CanTenantOverride policy** tests.
- **Deprecated/Archived exclusion** tests.
- `Response<T>` envelope tests.
- Permission/authorization tests (events.read/manage; tenant reddi; producer authz owning module'de).
- Mevcut MOD-0027 + FU02 testleri regresyonsuz.

### Verifier / statik (UI dahilse)
- `verify_datatable_page.py --area Platform --module NotificationEvents` → read-only yaklaşımıyla Index-seviyesi kontrat; NotificationDispatches ile aynı documented sınır (compact create/edit beklenmez; sahte sayfa YASAK).
- RESX en/tr parity.

### Browser smoke (UI dahilse — same-origin proxy üzerinden)
open `/Platform/NotificationEvents` → sync from manifest → list → detail (contract + required variables) → validation issue görünümü → active template-slot list → unauthorized actor URL/action check → same-origin network check.

## 18. Ready-for-dev Checklist
**Kabul edilen kararlar (2026-07-08 — karar kesinleşti; implementasyon "done" değil):**
- [x] Entity kararı **kabul edildi**: tek `NotificationEventDefinition` (binding entity yok; FU04+ follow-up).
- [x] UI kapsam kararı **kabul edildi**: read-only Platform Admin katalog UI bu pack'te (`/Platform/NotificationEvents`; list/detail/sync/validation; Create/Edit/_Form yok).
- [x] Manifest genişletme kararı **kabul edildi**: `ModuleManifestDocument` additive/opsiyonel `NotificationEvents` (default boş); alternatif provider reddedildi (§7.1).
- [x] Permission kararı **kabul edildi**: yeni `events.read`/`events.manage` (fallback yok) + template-slot erişim modeli (§14).
- [x] EventCode kararı **kabul edildi**: canonical/stable/immutable `{domain}.{aggregate}.{event}`.
- [x] Golden reference/verifier yaklaşımı (read-only/list-detail) netleştirildi (§11, §17).
- [x] Failure path 11 senaryo yazıldı (§13).

**Ready-for-dev ön koşulları — HEPSİ KAPANDI (2026-07-08):**
- [x] **DCP-002 preflight PASS + registry satırı (KAPANDI):** `MOD-0027-FU03` registry'ye eklendi; preflight `OK` (exit 0). DCP-002 kimlik kapısı geçildi.
- [x] **Manifest genişletmesi cross-service onayı (RESOLVED — integration-agent PASS):** `ModuleManifestDocument` additive/opsiyonel `NotificationEvents` geriye dönük uyumlu; guardrail'ler §16-B'de zorunlu acceptance olarak sabitlendi. **Hard blocker kapandı.**
- [x] **MVP kararları final** (§1.2).
- [ ] **CONDITIONAL (blocker DEĞİL) — Gateway `{everything}` kapsama teyidi**: implementasyon başında teyit edilir; kapsamıyorsa integration-agent route follow-up. Ready-for-dev'i **bloklamaz**.

> **Status kararı (2026-07-08):** Registry/DCP-002 kapısı geçti, BuildingBlocks cross-service onayı **integration-agent PASS**, MVP kararları final. **Açık hard blocker kalmadı** → status **`ready-for-dev`**. Tek kalan madde gateway CONDITIONAL implementation-start check'idir (blocker değil). Geliştirme `@orchestrator` ile başlayabilir.

## 19. Implementation Notes
- FU03, FU02'nin serbest `templateKey` yaklaşımını **event slot** disiplinine bağlar; FU02 template create/edit'in ileride `active-template-slots`'u tüketmesi ayrı bir küçük FU02-uyum işi olabilir (bu pack yalnızca **read contract**'ı sağlar, FU02 UI'ını değiştirmez).
- `TenantCreatedV1NotificationMapper.TemplateKey = "tenant.invite.email"` gibi gömülü sabitler, ileride event catalog'dan **eventCode → defaultTemplateKey** çözümüyle değiştirilebilir (producer migration — **bu pack'te DEĞİL**).
- `.resx` değişiklikleri hot-reload edilmez; smoke öncesi restart (yerel fleet notu).
- Dispatch listesinde iki DateTimeOffset alanının birlikte sort'u Mongo "parallel arrays" 500'üne yol açabilir (repo bilinen vaka) — yeni sorgu tasarımında tek alan/in-memory sort.

## 20. Follow-up Items / Open Blockers / Assumptions

### Blocker durumu — **AÇIK HARD BLOCKER YOK (2026-07-08)**
- [x] **RESOLVED (governance):** MOD-0027-FU03 `module-id-registry.md`'ye eklendi; `verify_module_id.py ... --check-id MOD-0027-FU03 --parent MOD-0027` → **`OK` (exit 0)**. DCP-002 kimlik kapısı geçildi.
- [x] **RESOLVED by integration-agent PASS:** Manifest genişletmesi (`ModuleManifestDocument.NotificationEvents` additive/opsiyonel) integration açısından **geriye dönük uyumlu** bulundu; cross-service hard blocker **kapandı**. Guardrail'ler §16-B'de zorunlu acceptance. Ayrı `INotificationEventManifestProvider` alternatifi **reddedildi** (drift riski).
- [ ] **CONDITIONAL (blocker DEĞİL):** Gateway `{everything}` `/events/*` + `/template-slots`'u kapsamıyorsa integration-agent route follow-up (explicit route, OPTIONS dahil). **Implementasyon başında** teyit edilir; ready-for-dev'i bloklamaz.

> **Sonuç:** Ready-for-dev'i bloklayan **açık hard blocker kalmadı**. Yalnızca gateway CONDITIONAL implementation-start check'i açık (route zaten `{everything}` altında mevcut kabul ediliyor — §15).

### Kabul edilmiş kararlar (RESOLVED — §1.2)
- [x] Entity: tek `NotificationEventDefinition` (binding entity yok).
- [x] UI: read-only Platform Admin katalog bu pack'te (`/Platform/NotificationEvents`).
- [x] Manifest: additive `NotificationEvents`; alternatif `INotificationEventManifestProvider` **reddedildi** (yalnızca not olarak §7.1'de kalır).
- [x] Permission: yeni `events.read`/`events.manage`, fallback yok; template-slot erişim modeli §14.
- [x] EventCode: canonical/stable/immutable `{domain}.{aggregate}.{event}`.

### Assumptions
- [ ] **ASSUMPTION:** ModulePages descriptor'ı (`PageCode`) ve Permission Catalog lookup'ı, sync doğrulaması için **okunabilir/sorgulanabilir** read contract sunar (`ModulePagesController` + permission lookup mevcut — doğrulandı; read-API seam'i implementasyon başında teyit edilir).
- [ ] **ASSUMPTION:** Persistence `eventCode` unique index ihtiyacı implementasyon sırasında gerekçelendirilerek ele alınır.

### Implementation closeout (2026-07-08)
- **Increment 1-4 tamamlandı (PASS-with-note):** BuildingBlocks manifest extension + Domain entity/enums → Application/API + persistence + permissions + sync/validation (1139/1139 test) → read-only `/Platform/NotificationEvents` UI + proxy + nav + RESX → live smoke. Bkz. [FU03 smoke audit](../../../../docs/audits/pss-mod-0027-fu03-notification-event-catalog-smoke-2026-07-08.md).
- **Empty-catalog notu:** Producer'lar `notificationEvents` deklare etmediği için sync `eventsDeclared:0` döner (controlled empty; bug değil). Populated golden-flow için producer opt-in gerekir (aşağıda).

### Follow-up (kapsam DIŞI — bu pack migrate etmez)
- [ ] **Producer opt-in (golden-flow tamamlama):** Workflow (`workflow.task.assigned`) / invite / campaign gibi producer manifest'lerine `notificationEvents` deklarasyonu eklenmesi — her producer'ın kendi pack'inde, ayrı follow-up. Katalog ancak bu opt-in'lerle dolar.
- [ ] **Per-event persisted validation issues:** Details'te olay-bazlı issue göstermek için entity'ye alan (şu an issues Index sync-result panel'de).
- [ ] **Producer flow migration:** mevcut invite / workflow / campaign producer flow'larının event catalog + slot sözleşmesine geçişi (ör. `AdminUserInvitationService`, `TenantCreatedV1NotificationMapper`) — **bu pack'te DEĞİL**.
- [ ] **FU02 uyum:** template create/edit'in `active-template-slots`'u tüketmesi (event slot seçimli template oluşturma) — bu pack yalnızca read contract'ı sağlar, FU02 UI'ını değiştirmez.
- [ ] **FU04+:** InApp kanalı + UserNotification + bell dropdown + SignalR; çok-kanallı `NotificationEventBinding` matrisi; tenant self-service override UI (`CanTenantOverride=true` slot tüketimi).

## Output Contract
Implementation final report şunları içermelidir: module status (PASS/PARTIAL/FAIL/BLOCKED); changed files; DCP-002 preflight + registry kanıtı; entity/manifest/permission kararlarının uygulanmış hali; manifest sync + validation kanıtı (geçerli/geçersiz referans); active template-slot kanıtı; permission seed/alias doğrulaması; (UI dahilse) verifier + RESX parity + browser smoke; same-origin proxy kanıtı; Module Catalog/ModulePages/Permission Catalog ownership'e dokunulmadığının kanıtı; Protected Paths ihlali yok; açık blocker/assumption; next recommended step.

**Integration guardrail kanıtı (ZORUNLU — §16-B):** Implementation report, BuildingBlocks manifest genişletmesi için şunları açıkça kanıtlamalıdır: (1) `NotificationEvents` record sonuna nullable+default eklendi, mevcut param sırası/tipleri ve `Icon`/`IsBaseline` değişmedi; (2) tüketiciler null→empty coalesce yapıyor; (3) alıcıda default STJ korundu (`UnmappedMemberHandling.Disallow` yok); (4) `NotificationEvents` içermeyen eski JSON exception'sız deserialize oluyor (backward-compat testi geçti); (5) mevcut 9 manifest provider + reconcile/prune flow regresyonsuz (test kanıtı); (6) yalnızca `ModuleManifestDocument.cs` (+ aynı dosyada `ModuleManifestNotificationEvent`) değişti, başka BuildingBlocks/servis dosyasına dokunulmadı.
