---
id: MOD-0027-FU02
name: Notification Template Management UI
domain: platform-shared-services
service: Diten.Platform
shell: platform-admin
golden_reference: compact
entity_base: BaseEntity
status: ready-for-dev
owner: ali.tufanoglu
branch: feature/pss/mod-0027-fu02-notification-template-ui
started: 2026-07-07
target: 2026-07-21
form_field_count: 9
---

# MOD-0027-FU02 - Notification Template Management UI

> **Identity:** Canonical FU of Blueprint `MOD-0027` (Notification Service). Registry alias geçmişi: `NEW-003 → MOD-0027-FU02` (DCP-002). Preflight kanıtı: `verify_module_id.py --check-id MOD-0027-FU02 --name "Notification Template Management UI" --parent MOD-0027` → `OK`.

## 1. Module Summary
- **Purpose:** MOD-0027 Central Tenant Email / Notification Service'in mevcut backend API'lerini Platform Admin operatörleri için kullanılabilir hale getiren yönetim UI'ıdır.
- **Primary outcome:** Platform operatörü API çağrısı yazmadan (1) platform-default ve tenant-özel bildirim şablonlarını, (2) tenant e-posta ayarlarını, (3) dispatch (teslimat) kayıtlarını tarayıcıdan yönetebilir.
- **Scope note:** Bu pack **UI-first**'tür. Backend MOD-0027 pack'i ile büyük ölçüde tamamlanmıştır; bu pack yalnızca iki küçük eklemeye izin verir: template render-preview endpoint'i ve dispatch list query filtre genişletmesi (bkz. §3, §5).
- **Master-plan durumu:** `master-development-plan.md` FU02'yi `partial 35%` gösterir; kod doğrulaması (2026-07-07): backend template/settings/dispatch API'leri mevcut, **hiçbir Platform Admin view/controller yok**. %35 backend hazırlığını yansıtır.
- **Tenant self-service DEĞİLDİR:** Tenant admin'in kendi şablon/ayarlarını `_LayoutTenantShell` üzerinden yönetmesi gelecek **MOD-0027-FU05** pack'idir (MOD-0027 pack §11 kararı). Bu pack yalnızca PlatformActor içindir. InApp kanalı/çan/polling/SignalR da **FU03/FU04** kapsamındadır; bu pack'te yer almaz.

### Accepted MVP decisions (final — 2026-07-07 kullanıcı onayı, bağlayıcı)

| Karar | MVP kararı | Durum |
|---|---|---|
| HTML şablon editörü | Düz **monospace `textarea`**. WYSIWYG/zengin editör **kapsam DIŞI**. | Kabul (final) |
| Render preview | Sunucu taraflı render: yeni endpoint `POST /api/platform/notifications/templates/render-preview` — kayıt YAPMADAN, gönderilen **unsaved** subject/body şablon içeriğini + sample variables değerlerini render eder. İzin: `platform.notifications.templates.read`. UI sonucu **sandboxed iframe** içinde gösterir. | Kabul (final) |
| Dispatch gövde gösterimi | Details tam e-posta gövdesini ve **Bcc**'yi ASLA göstermez. Yalnızca: metadata, redacted error, correlation id, sanitized variables + (yalnızca backend'de zaten güvenli şekilde mevcutsa) truncated/sanitized preview. | Kabul (final) |
| Enum lookup'ları | Yeni Platform lookup key'leri **PSS-011 pipeline** üzerinden eklenir (bkz. §13.1): `notification-channels`, `messaging-providers`, `notification-template-statuses`, `notification-fallback-policies`. JS içinde hardcoded fallback listesi YASAK. | Kabul (final) |
| Kapsam kesinleştirmeleri | Tenant self-service UI → gelecek **FU05** pack'i. InApp kanalı, çan (bell) dropdown, polling, SignalR → **FU03/FU04** pack'leri. Bu pack'te YOK. | Kabul (final) |

### Factual doğrulama (kod incelemesi, 2026-07-07)
- `GetNotificationDispatchListQuery` mevcut imzası `(TenantId, Page, PageSize)` — **filtre yok**; §3'teki filtre genişletmesi gerçek ihtiyaçtır.
- `locales` lookup key'i mevcut (`Features/Lookups/LookupModels.cs` → `Locales = "locales"`, `GetLocaleLookupHandler`) — ek iş gerekmez.
- Gateway `/api/platform/notifications/{everything}` route'u `POST` dahil tüm metodları kapsıyor (ocelot.json doğrulandı) — render-preview için gateway değişikliği **gereksiz**. Kod incelemesi aksini gösterirse gateway'e dokunulmaz; ayrı integration-agent follow-up maddesi açılır (bkz. §20).
- Permission literal'leri controller'da mevcut ve `PermissionAliasMap.cs`'te alias'lı; `actor_type=platform_admin` tüm permission'ları otomatik geçtiği için UI, platform admin için seed beklemeden çalışır. Rol-bazlı kısıtlı aktör testi için seed doğrulaması implementation başında yapılır (blocker değil).

### No-shell kuralı (bağlayıcı)
Arkasındaki save/load/update davranışı tamamlanmadan operasyonel görünen UI, buton, action veya lifecycle akışı YAZILMAZ. Disabled section, controlled empty state ve gerçek read-only görünüm kabul edilir. **Fake save, fake preview, fake archive, fake cancel veya placeholder lifecycle action YASAKTIR.**

### Contract blocker kuralı (bağlayıcı)
Gerekli backend endpoint, DTO, lookup key, proxy route, permission seed veya mevcut MOD-0027 kontratı eksik çıkarsa placeholder UI OLUŞTURULMAZ; eksik kontrat implementation report'ta açıkça blocker/follow-up olarak raporlanır.

## 2. Ownership and Boundaries
### In-scope
- Platform Admin shell'de üç ekran grubu: **NotificationTemplates** (birincil, compact DataTable), **NotificationSettings** (tenant messaging ayarları), **NotificationDispatches** (salt-okunur izleme + cancel).
- Diten.Web same-origin proxy controller'ları (proxy-profile; HttpOnly cookie → Gateway Bearer).
- `_LayoutPlatformAdmin` sidebar'ına "Notifications" menü grubu.
- RESX **en + tr** (Platform tarafı kuralı) + `window.L10n` köprüsü.
- Backend'e sınırlı ekleme: render-preview endpoint + dispatch list filtreleri (mevcut `Features/Notifications` içinde, yeni feature klasörü YOK).
- Yeni Platform lookup key'lerinin PSS-011 pipeline'ına eklenmesi.

### Out-of-scope
- Tenant self-service şablon/ayar UI'ı (`_LayoutTenantShell`) — ayrı gelecek pack.
- InApp bildirim kanalı, çan (bell) entegrasyonu, SignalR — MOD-0027-FU03+ olarak ayrı pack.
- SMS/WhatsApp kanalları, gerçek dış sağlayıcı adaptörleri (SendGrid/Twilio) — MOD-0263.
- Yeni entity, yeni collection, domain model değişikliği.
- Yeni permission literal'i üretmek — mevcut `platform.notifications.*` seti aynen kullanılır.
- E-posta queue etme UI'ı (test mail gönderimi hariç — bkz. Follow-up; MVP'de yok).
- Ocelot route değişikliği (mevcut route'lar yeterli — bkz. §15).
- Mevcut davet/invitation e-posta akışlarının migrasyonu.

### Ownership rule
- Bu pack UI + proxy + sınırlı Application eklemesi sahiplenir.
- MOD-0027 core orkestrasyon/entity/resolver sahipliği değişmez; Domain ve Persistence katmanlarına dokunulmaz (index eklemesi hariç — dispatch filtreleri gerektirirse gerekçeli).
- Şablon çözümleme kuralı (tenant-özel → platform-default → FallbackPolicy) backend'de kalır; UI yalnızca gösterir.

## 3. Owned Objects
### Yeni entity
- **YOK.** Mevcut MOD-0027 entity'leri (`NotificationTemplate`, `TenantMessagingSettings`, `NotificationDispatch`) kullanılır. `entity_base: BaseEntity` mevcut entity'lerin tabanını belgeler; bu pack yeni kayıt tipi yaratmaz.

### Backend eklemeleri (mevcut `Features/Notifications` içine)
- Query: `RenderNotificationTemplatePreviewQuery` (sealed record) — kayıt YAPMADAN, request'te gönderilen **unsaved** subject/body şablon içeriğini ve sample variables değerlerini render eder (Create/Edit formundaki henüz kaydedilmemiş içerik için).
- Handler: `RenderNotificationTemplatePreviewHandler` (`Handlers/QueryHandlers/`).
- Validator: `RenderNotificationTemplatePreviewValidator`.
- Mevcut `GetNotificationDispatchListQuery`'ye filtre parametreleri (status, dateFrom, dateTo, templateKey, targetTenantId) — **doğrulandı: mevcut imza `(TenantId, Page, PageSize)` filtresizdir, genişletme gereklidir.** Mevcut çağıran davranışı geriye dönük korunur (tüm yeni parametreler optional).
- DTO eklemeleri mevcut `NotificationContracts.cs` / models dosyasına eklenir (yeni Models dosyası açılmaz — MOD-0027'nin mevcut dosya düzeni korunur).

### API endpoint (yeni)
- `POST /api/platform/notifications/templates/render-preview` — `[HasPermission("platform.notifications.templates.read")]`

### API endpoints (mevcut — UI tüketicisi)
- Templates: `GET/POST /templates`, `GET /templates/{templateKey}`, `PUT /templates/{id}`, `POST /templates/{id}/archive`, tenant-özel varyantları (`/tenant-settings/{tenantId}/templates...`)
- Settings: `GET/PUT/DELETE /tenant-settings/{tenantId}`, `GET /tenant-settings/{tenantId}/resolved`
- Dispatches: `GET /dispatches`, `GET /dispatches/{id}`, `POST /dispatches/{id}/cancel`
- (Base path: `/api/platform/notifications`)

### Frontend (Diten.Web)
- Proxy controller'lar (`Controllers/Platform/`):
  - `NotificationTemplatesController` — route `/Platform/NotificationTemplates`, proxy action'ları `/Platform/NotificationTemplates/api/...`
  - `NotificationSettingsController` — route `/Platform/NotificationSettings`
  - `NotificationDispatchesController` — route `/Platform/NotificationDispatches`
- View klasörleri: `Views/Platform/NotificationTemplates/`, `Views/Platform/NotificationSettings/`, `Views/Platform/NotificationDispatches/`
- JS: `wwwroot/assets/js/Platform/{Module}/index.js` + `index.l10n.js`
- RESX: `Resources/Views/Platform/{Module}/{Module}Index.{en|tr}.resx`
- Sidebar: `_LayoutPlatformAdmin` "Notifications" grubu (3 menü öğesi, permission-gated)

### Permissions (mevcut — yeni literal YOK)
```text
platform.notifications.read
platform.notifications.configure
platform.notifications.templates.read
platform.notifications.templates.create
platform.notifications.templates.update
platform.notifications.templates.archive
platform.notifications.dispatches.read
platform.notifications.dispatches.queue   (cancel/sent/failed geçişleri bu literal'i kullanır — koddan doğrulandı)
```

## 4. Entity Fields
Yeni entity yok. UI form kontratı mevcut entity alanlarına birebir bağlanır (kaynak: MOD-0027 pack §4 + koddaki entity'ler):

### NotificationTemplate create/edit formu (birincil form — 9 kullanıcı alanı → compact)
| # | Form alanı | Tip | UI kontrol |
|---|---|---|---|
| 1 | TemplateKey | string, lowercase dotted, max 160 | text input (create'te yazılabilir, edit'te readonly) |
| 2 | Locale | string (BCP-47) | select — `/api/lookups/locales` |
| 3 | Channel | enum (MVP: Email) | select — `notification-channels` lookup |
| 4 | SubjectTemplate | string, max 300 | text input |
| 5 | BodyHtmlTemplate | string, max 100000 | textarea (monospace) + preview |
| 6 | BodyTextTemplate | string?, max 100000 | textarea |
| 7 | Variables | liste (name, type, required) | satır ekle/sil alt formu |
| 8 | Status | enum (Draft/Active/Archived) | select — `notification-template-statuses` lookup (Archive ayrı aksiyon) |
| 9 | SemanticVersion | string?, max 40 | text input (`Version` adı YASAK — rezerve) |

Scope seçimi (platform-default vs tenant-özel) form alanı değil sayfa bağlamıdır: template listesi "Platform Defaults" ve "Tenant Overrides" sekmeleriyle ayrılır; tenant-özel işlemler hedef tenant seçildikten sonra `{tenantId}` route'lu endpoint'lere gider.

### TenantMessagingSettings formu (ikincil form — 11 alan, ayrı sayfa)
ProviderCode (select — `messaging-providers` lookup), SenderEmail, SenderName, ReplyToEmail, Host*, Port*, UseSsl*, ApiBaseUrl*, CredentialSecretRef (asla ham şifre; input açıklaması "MOD-0012 secret referansı"), IsEnabled, FallbackPolicy (select — `notification-fallback-policies` lookup). (* = SMTP seçilince görünür/zorunlu — koşullu alanlar.)

### NotificationDispatch (salt-okunur)
Liste kolonları: QueuedAt, Status, TemplateKey, Channel, ProviderCode, alıcı sayısı, RetryCount, ErrorCode. Details: + Locale, Subject, SentAt/FailedAt, NextRetryAt, redacted ErrorMessage, CorrelationId, VariablesJson (sanitized). **Bcc ve tam gövde gösterilmez.**

## 5. Repo Scope
- `execution/domains/platform-shared-services/module-packs/MOD-0027-FU02-notification-template-management-ui.md`
- `services/Diten.Platform/src/Diten.Platform.Application/Features/Notifications/**` — SADECE: render-preview query/handler/validator + dispatch list filtre genişletmesi + ilgili DTO eklemeleri
- `services/Diten.Platform/src/Diten.Platform.API/Controllers/Platform/NotificationsController.cs` — SADECE render-preview action eklemesi
- PSS-011 lookup pipeline dosyaları — SADECE yeni lookup key'lerinin eklenmesi (`notification-channels`, `messaging-providers`, `notification-template-statuses`, `notification-fallback-policies`)
- `frontend/Diten.Web/Controllers/Platform/NotificationTemplatesController.cs` (+ `NotificationSettingsController.cs`, `NotificationDispatchesController.cs`)
- `frontend/Diten.Web/Views/Platform/NotificationTemplates/**`, `.../NotificationSettings/**`, `.../NotificationDispatches/**`
- `frontend/Diten.Web/wwwroot/assets/js/Platform/NotificationTemplates/**`, `.../NotificationSettings/**`, `.../NotificationDispatches/**`
- `frontend/Diten.Web/Resources/Views/Platform/NotificationTemplates/**`, `.../NotificationSettings/**`, `.../NotificationDispatches/**`
- `frontend/Diten.Web/Views/Shared/_LayoutPlatformAdmin.cshtml` — SADECE sidebar menü grubu eklemesi

## 6. Protected Paths
- `.antigravity/**`
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml` (FROZEN)
- `frontend/Diten.Web/Controllers/Archive/**`, `Views/Archive/**`
- `gateway/Diten.ApiGateway/**/ocelot.json` (mevcut notification route'ları yeterli; değişiklik gerekirse integration-agent)
- `services/Diten.Platform/src/Diten.Platform.Domain/**` (yeni entity/alan YOK)
- `services/Diten.Platform/src/Diten.Platform.Persistence/**` (dispatch filtre index'i gerekirse ayrı gerekçeli istisna — implementation report'ta belgelenir)
- `services/Diten.AuthService/**`, `services/Diten.MdmService/**`, `services/Diten.EnterpriseStrategyService/**`, `services/Diten.DevEnablementService/**`
- MOD-0027 mevcut command/handler'ları (davranış değişikliği yasak; yalnızca §5'teki eklemeler)
- MOD-0026 scheduler, MOD-0035 event bus, MOD-0012 secret internals

## 7. Dependencies
- **MOD-0027 (parent):** Tüm veri modeli, API'ler, resolver, izin literalleri. Backend `in-progress %82` — UI'nin tükettiği endpoint'ler mevcut ve testli.
- **PSS-011 Lookups:** `locales` key'i + bu pack'in ekleyeceği 4 yeni notification enum key'i. UI hiçbir dropdown'ı hardcode etmez.
- **Tenant listesi:** Hedef tenant seçici mevcut Platform tenants list API'sini (Tenants modülü proxy kalıbı) tüketir; yeni endpoint açılmaz.
- **RBAC / permission seed:** `platform.notifications.*` literal'lerinin PlatformActor rollerine seed edildiği implementation başında doğrulanır (`PermissionAliasMap.cs`'te alias'lar mevcut; seed eksikse ilk iş olarak tamamlanır ve report'a yazılır).
- **DataTable v2 + BulkActionBar + SweetAlert2 (MOD-0013)** shared bileşenleri.
- **Gateway:** `/api/platform/notifications` + `{everything}` route'ları ocelot.json'da mevcut (satır ~766-794).

## 8. Runtime Constraints
- Kayıtlar tenant-aware `BaseEntity`'dir; platform-default şablonlar MOD-0027'nin kabul ettiği explicit platform/global record stratejisiyle yaşar. UI bu ayrımı "Platform Defaults / Tenant Overrides" olarak gösterir, yeni saklama stratejisi icat etmez.
- Request/DTO payload'ları **asla `TenantId` taşımaz**; hedef tenant yalnızca `{tenantId}` route segmenti ile ve yalnızca PlatformActor + ilgili izinle belirtilir (MOD-0027 tenant targeting contract).
- Browser JS servis portu `5057`'ye ve Gateway `5000`'e doğrudan gitmez; **proxy-profile** kullanılır (same-origin `/Platform/{Module}/api/...`, HttpOnly cookie server-side Bearer'a çevrilir).
- API yanıtları `Response<T>` envelope; proxy controller'lar envelope'u değiştirmeden geçirir.
- `CredentialSecretRef` alanı dışında secret-benzeri değer girişine UI izin vermez; form ham şifre/API key alanı içermez.
- Soft delete `IsDeleted/DeletedAt`; arşivlenen şablonlar listede "Archived" filtresiyle görünür, normal listede görünmez.
- Concurrency: update işlemleri mevcut concurrency kontratını kullanır; 409'da UI "reload required" akışı gösterir.
- Loglara/console'a tam e-posta gövdesi, alıcı dökümü, secret yazılmaz (UI dahil — `console.log` ile payload dump yasak).

## 9. Layout & Shell Contract
- `shell: platform-admin`.
- Üç modülün TÜM `.cshtml` dosyalarında AÇIKÇA: `Layout = "_LayoutPlatformAdmin";`
- View klasörleri: `Views/Platform/NotificationTemplates/`, `Views/Platform/NotificationSettings/`, `Views/Platform/NotificationDispatches/`
- Frontend route'lar: `/Platform/NotificationTemplates`, `/Platform/NotificationSettings`, `/Platform/NotificationDispatches`
- `_ViewStart.cshtml` değiştirilmez. `Areas/` klasörü KULLANILMAZ (VIEW-001).
- Canlı referans: `Views/Platform/Tenants/` (Platform Admin compact örneği).

## 10. Backend File Convention
Golden Reference Compact backend kalıbı, **mevcut** `Features/Notifications` düzenine ek olarak uygulanır (yeni feature klasörü açılmaz — MOD-0027'nin yerleşik düzeni korunur; bu, pack-üstü otorite kuralı gereği belgelenmiş bir sapmadır):

```text
services/Diten.Platform/src/Diten.Platform.Application/Features/Notifications/
├── Queries/
│   └── RenderNotificationTemplatePreviewQuery.cs      (sealed record — YENİ)
├── Handlers/QueryHandlers/
│   └── RenderNotificationTemplatePreviewHandler.cs    (sealed class, suffix YOK — YENİ)
├── Validators/
│   └── RenderNotificationTemplatePreviewValidator.cs  (YENİ)
└── (mevcut dosyalar — dokunulmaz, GetNotificationDispatchListQuery filtre eklemesi hariç)
```

Naming: `{Verb}{Module}Handler` / `{Verb}{Module}Validator` — `Command/Query/Request` suffix YASAK. Tek dosya tek public tip.

## 11. Frontend File Contract
### NotificationTemplates (birincil — Compact tam set)
```text
Views/Platform/NotificationTemplates/
├── Index.cshtml                 (Layout AÇIKÇA; ① _Filter → ② _BulkActionBar → ③ _DataTable → Scripts/_IndexL10n)
├── Create.cshtml                (sayfa kabuk + _Form)
├── Edit.cshtml                  (sayfa kabuk + _Form)
├── Details.cshtml               (ayrı detay sayfası + render preview paneli)
├── _Form.cshtml                 (ortak form partial; Variables satır editörü dahil)
├── _Filter.cshtml               (status, locale, channel, scope: default/override)
├── _DataTable.cshtml            (data-dt-standard="v2" + #skeleton-loader)
├── _IndexL10n.cshtml
└── NotificationTemplatesIndex.cs (marker class)

wwwroot/assets/js/Platform/NotificationTemplates/
├── index.js
└── index.l10n.js                (camelCase→PascalCase köprüsü ZORUNLU — bilinen L10n tuzağı)

Resources/Views/Platform/NotificationTemplates/
└── NotificationTemplatesIndex.{en|tr}.resx
```
**Compact'ta YASAK:** `_CreateEditOffcanvas.cshtml`, `_DetailsQuickView.cshtml`.

### NotificationSettings (Compact set — Create/Edit/Details/_Form + Index listesi "yapılandırılmış tenant'lar")
Aynı Compact dosya seti `Views/Platform/NotificationSettings/` altında. Index DataTable'ı ayar kaydı olan tenant'ları listeler; Create akışı hedef tenant seçici ile başlar; Details "resolved settings" (fallback sonucu) panelini içerir. Bulk delete YOK (ayar silme tekil + onaylı).

### NotificationDispatches (read-only/list-detail alt kümesi — golden reference kararı)
```text
Views/Platform/NotificationDispatches/
├── Index.cshtml, _Filter.cshtml, _DataTable.cshtml, _IndexL10n.cshtml
├── Details.cshtml               (metadata + redacted hata + koşullu Cancel aksiyonu)
└── NotificationDispatchesIndex.cs
```
- **Read-only izleme modülüdür:** `Create.cshtml`, `Edit.cshtml`, `_Form.cshtml` ÜRETİLMEZ; BulkActionBar YOK; hiçbir create/update formu yok.
- Tek yazma aksiyonu: dispatch **geçerli durumda ise** Cancel (`POST /dispatches/{id}/cancel`, `platform.notifications.dispatches.queue`). Geçersiz durumda buton render edilmez veya disabled + tooltip gösterilir (no-shell kuralı: fake cancel yasak).
- Bu alt küme kararı verifier yaklaşımıyla birlikte §17'de dokümante edilir.

### Golden reference eşlemesi (ekran bazında, kesin)
| Modül | Karar |
|---|---|
| NotificationTemplates | `compact` (9 alan) — tam Compact seti |
| NotificationSettings | `compact` (11 alan) — tam Compact seti (bulk delete hariç) |
| NotificationDispatches | read-only/list-detail alt kümesi — Create/Edit/_Form zorunlu DEĞİL ve üretilmez |

Frontmatter `golden_reference: compact` birincil modül (NotificationTemplates) üzerinden verilmiştir.

## 12. Validation Rules
Sunucu tarafı kurallar MOD-0027 pack §14'te tanımlı ve implementte mevcut; UI bunları yansıtır (client-side pre-check + sunucu hatasını field-level gösterme):

| Field | Required | Format/Rule | UI pre-check | Sunucu |
|---|---|---|---|---|
| TemplateKey | Evet | lowercase dotted, max 160 | regex + lowercase normalize | duplicate → 409 |
| Locale | Evet | lookup'tan seçim | select zorunlu | desteklenen locale |
| Channel | Evet | MVP: Email | select zorunlu | enum |
| SubjectTemplate | Evet | max 300, trim | maxlength | değişken doğrulama |
| BodyHtmlTemplate | Koşullu | text body yoksa zorunlu; max 100000 | ikisinden biri dolu | unsafe template reddi |
| BodyTextTemplate | Koşullu | HTML yoksa zorunlu | ikisinden biri dolu | değişken doğrulama |
| Variables | Evet | name: alfanümerik/dot/underscore; type enum; required flag | satır editörü boş isim engeli | eksik tanım → 400 |
| Status | Evet | enum | select | geçiş kuralları |
| SemanticVersion | Hayır | max 40 | maxlength | `Version` adı yasak |
| SenderEmail (settings) | Evet | email, max 256, lowercase | email input | format |
| Host/Port (settings) | Koşullu | SMTP'de zorunlu; port 1-65535 | ProviderCode=Smtp iken görünür+zorunlu | koşullu doğrulama |
| CredentialSecretRef | Koşullu | max 512, secret referansı | ham şifre paterni reddi (uyarı) | raw secret reddi |
| Preview variables | Evet (preview'da) | şablonun required değişkenleri | eksikleri işaretle | 400 |

## 13. Failure Path to Verify
- **Duplicate TemplateKey (aynı scope+Locale+Channel):** 409 + field-level hata + kayıt oluşmaz + reload sonrası temiz state.
- **Missing required template variable (preview/save):** 400 + validator mesajı + save/preview engellenir.
- **Concurrency conflict (template/settings update):** 409 + "data changed, reload required" UI'ı + sessiz overwrite YOK.
- **Unauthorized actor:** menü öğesi gizli + doğrudan URL'de 403 sayfa/aksiyon disabled; `templates.read` olmadan liste açılmaz.
- **Cross-tenant erişim:** başka tenant'ın kaydına doğrudan id ile erişim 404; UI hedef tenant bağlamı dışına link üretmez.
- **Raw secret girişi (settings):** payload'da şifre/API-key benzeri değer `CredentialSecretRef` dışında reddedilir (400) + UI ham şifre alanı sunmaz.
- **Dispatch cancel — geçersiz durum geçişi:** `Sent` dispatch cancel edilemez → 400/409 controlled fail + SweetAlert hata.
- **Lookup endpoint erişilemez:** dropdown boş + retry uyarısı; hardcoded fallback listesi YOK (bilinçli davranış).

## 14. Authorization Convention
- Policy: `[Authorize(Policy = "PlatformActor")]` (backend mevcut) + proxy controller'lar Platform Admin auth kalıbını izler.
- Permission format: `platform.{resource}.{action}` (PKS-001) — §3'teki mevcut liste; **yeni literal üretilmez**.
- UI gating eşlemesi:
  - Templates menü/list: `templates.read`; Create: `templates.create`; Edit: `templates.update`; Archive: `templates.archive`; Preview: `templates.read`
  - Settings menü/list/detail: `read`; Create/Edit/Delete: `configure`
  - Dispatches menü/list/detail: `dispatches.read`; Cancel: `dispatches.queue`
- `actor_type=platform_admin` tüm permission'ları otomatik geçer (mevcut kural).
- Partner admin bu pack'te kapsam dışı.

## 15. Gateway / API Routing Decision
- **Karar: Gateway değişikliği GEREKSİZ.**
- `ocelot.json`'da `/api/platform/notifications` ve `/api/platform/notifications/{everything}` explicit route çiftleri mevcut (≈ satır 766-794); render-preview yeni path'i `{everything}` kapsamındadır.
- Frontend proxy-profile kullanır: browser → `/Platform/{Module}/api/...` (5001, same-origin) → Gateway 5000 → Platform 5057. Browser JS 5000/5057'ye doğrudan gitmez.
- `gateway/Diten.ApiGateway/**/ocelot.json` protected; öngörülemeyen bir route ihtiyacı çıkarsa integration-agent task'ı açılır.

## 13.1 Platform Lookup & Reference Data Decision
| İhtiyaç | Karar |
|---|---|
| Locale seçimi | Mevcut `/api/lookups/locales` tüketilir (key mevcut değilse PSS-011 pipeline'ına eklenmesi bu pack'in test gate'idir) |
| Channel / Provider / TemplateStatus / FallbackPolicy | **Yeni Platform-owned lookup key'leri:** `notification-channels`, `messaging-providers`, `notification-template-statuses`, `notification-fallback-policies` — PSS-011 pipeline'ına eklenir, `LookupOptionDto` (`code`,`name`,`value`) kontratıyla döner |
| Hedef tenant seçici | Mevcut Platform tenants list API'si (Tenants proxy kalıbı) — yeni lookup key açılmaz |
| MDM/reference sınırı | Bu key'ler Platform system vocabulary'sidir; ERP business reference DEĞİLDİR — MDM'e taşınmaz |
- Tüm dropdown'lar `Response<T>.data` unwrap eder; hardcoded fallback YASAK.

## 16. Acceptance Criteria

Kriterler implementasyon sırasına göre gruplanmıştır (A → B → C → D). Bir grup tamamlanmadan sonrakine geçilmez; her grup no-shell kuralına tabidir (arkasında çalışan backend davranışı olmayan aksiyon UI'da render edilmez).

### A. NotificationTemplates golden flow
- [ ] Backend ön koşulu: `POST /templates/render-preview` endpoint'i + `RenderNotificationTemplatePreviewQuery/Handler/Validator` çalışır durumda; unsaved içerik + sample variables ile subject/HTML döner.
- [ ] `Views/Platform/NotificationTemplates/` altındaki TÜM `.cshtml` dosyalarında `Layout = "_LayoutPlatformAdmin"` AÇIKÇA yazılı.
- [ ] Index DataTable v2 (`data-dt-standard="v2"` + `#skeleton-loader`) platform-default şablonları listeler; "Tenant Overrides" görünümü hedef tenant seçimiyle tenant şablonlarını listeler.
- [ ] Create/Edit ayrı sayfalar + ortak `_Form.cshtml`; offcanvas KULLANILMAZ (Compact kuralı).
- [ ] Şablon create → 201 + listede görünür; duplicate key → 409 + field-level hata.
- [ ] Variables satır editörü ile eklenen değişkenler kaydedilir ve Details'te görünür.
- [ ] Preview butonu monospace textarea içeriğini render-preview'a gönderir; sonuç **sandboxed iframe**'de gösterilir; eksik required değişken 400 + UI işaretleme.
- [ ] Archive aksiyonu SweetAlert onayı ile `POST /templates/{id}/archive` çağırır; arşivli şablon normal listeden düşer.

### B. NotificationSettings golden flow
- [ ] `Views/Platform/NotificationSettings/` altındaki TÜM `.cshtml` dosyalarında `Layout = "_LayoutPlatformAdmin"` AÇIKÇA yazılı.
- [ ] Hedef tenant için ayar oluşturma/güncelleme/silme uçtan uca çalışır (`GET/PUT/DELETE /tenant-settings/{tenantId}`).
- [ ] `ProviderCode=Smtp` seçilince Host/Port/UseSsl görünür ve zorunlu olur (koşullu form davranışı).
- [ ] "Resolved" paneli `GET /tenant-settings/{tenantId}/resolved` sonucunu gösterir; tenant ayarı silinince platform default'un devreye girdiği panelde görünür.
- [ ] Formda ham şifre alanı yoktur; `CredentialSecretRef` dışı secret girişi sunucuda reddedilir ve UI hatayı field-level gösterir.

### C. NotificationDispatches read-only/cancel flow
- [ ] Backend ön koşulu: `GetNotificationDispatchListQuery` filtre genişletmesi (status/dateFrom/dateTo/templateKey/targetTenantId, tümü optional, geriye dönük uyumlu) çalışır durumda.
- [ ] `Views/Platform/NotificationDispatches/` altındaki TÜM `.cshtml` dosyalarında `Layout = "_LayoutPlatformAdmin"` AÇIKÇA yazılı.
- [ ] Filtreli liste + Details çalışır; Details YALNIZCA metadata, redacted error, correlation id, sanitized variables (+ backend'de güvenli mevcutsa truncated preview) gösterir; **tam gövde ve Bcc ASLA gösterilmez**.
- [ ] Create/Edit/_Form/BulkActionBar ÜRETİLMEMİŞTİR (read-only kararının kanıtı).
- [ ] Cancel yalnızca geçerli durumdaki dispatch'te aktiftir; geçersiz durumda buton render edilmez veya disabled'dır; geçersiz geçiş denemesi controlled 400/409 + SweetAlert hata gösterir.

### D. Integration / security / l10n verification
- [ ] 4 yeni lookup key'i (`notification-channels`, `messaging-providers`, `notification-template-statuses`, `notification-fallback-policies`) `/api/lookups/{key}` üzerinden `LookupOptionDto` (`code`,`name`,`value`) döner; unauthorized çağrı korumalıdır.
- [ ] Tüm dropdown'lar lookup/proxy'den beslenir; JS içinde hardcoded seçenek listesi YOK; `Response<T>.data` unwrap edilir.
- [ ] İzinsiz kullanıcıda: Notifications menü öğeleri görünmez, doğrudan URL 403 davranışı, aksiyon butonları render edilmez (§14 eşlemesi).
- [ ] Browser network'te yalnızca same-origin `/Platform/...` istekleri görünür; `5000`/`5057` portlarına doğrudan istek YOK.
- [ ] RESX en+tr parite PASS (üç modül); `index.l10n.js` PascalCase köprüsü çalışır (toast'ta `(undefined: corrId)` YOK).
- [ ] `verify_datatable_page.py --area Platform --module NotificationTemplates --reference compact` PASS ve `--module NotificationSettings --reference compact` PASS; NotificationDispatches için §17'deki read-only verifier yaklaşımı uygulanır ve raporlanır.
- [ ] Mevcut MOD-0027 backend testleri regresyonsuz PASS.
- [ ] `module-implementation-status.md` FU02 satırı aynı PR'da güncellenir.

## 17. Test Expectations
### Build
- `dotnet build services/Diten.Platform/src/Diten.Platform.API/Diten.Platform.API.csproj -c Debug`
- `dotnet build frontend/Diten.Web/Diten.Web.csproj -c Debug`

### Unit / integration (backend eklemeleri)
- Render-preview: tüm required değişkenlerle başarılı; eksik değişkende 400; unsafe template reddi; response'ta secret sızıntısı yok.
- Dispatch list filtreleri: status/tarih/templateKey/tenant kombinasyonları doğru sonuç döner.
- Yeni lookup key'leri `LookupOptionDto` kontratıyla döner.
- Mevcut MOD-0027 testleri REGRESYONSUZ geçer.

### Verifier / statik
- `python3 .antigravity/scripts/verify_datatable_page.py . --area Platform --module NotificationTemplates --reference compact` → PASS zorunlu.
- `python3 .antigravity/scripts/verify_datatable_page.py . --area Platform --module NotificationSettings --reference compact` → PASS zorunlu.
- **NotificationDispatches read-only verifier yaklaşımı (dokümante karar):** modül tam Compact seti içermediği için compact referansıyla verifier PASS beklenmez. Uygulanacak doğrulama: (1) verifier'ın Index-seviyesi kontratı (Filter/DataTable v2 marker/IndexL10n/skeleton) manuel checklist veya verifier'ın desteklediği en yakın modla doğrulanır; (2) `Create.cshtml`/`Edit.cshtml`/`_Form.cshtml` dosyalarının **yokluğu** kasıtlı karar olarak implementation report'a yazılır; (3) verifier read-only/list-detail modu desteklemiyorsa bu sınır follow-up maddesi olarak raporlanır — verifier'ı geçirmek için sahte Create/Edit sayfası üretmek YASAKTIR (no-shell kuralı).
- RESX en/tr parity kontrolü (üç modül).

### Browser smoke (fake provider ile — YALNIZCA Diten.Web same-origin proxy üzerinden)
1. PlatformActor ile giriş → Notifications menüsü görünür.
2. Template create (en + tr iki locale) → listede görünür → unsaved içerikle preview (sandboxed iframe) başarılı.
3. Hedef tenant'a SMTP ayarı oluştur → Resolved panelinde tenant-özel ayar görünür; ayarı sil → Resolved panelinde platform default görünür.
4. Test dispatch kaydı (mevcut queue API'si ile hazırlanmış) → Dispatches listesinde filtrelenir → Details açılır (tam gövde/Bcc YOK) → uygun durumda Cancel çalışır; uygun olmayan durumda Cancel sunulmaz.
5. İzinleri kısılmış test kullanıcısı → menü gizli + URL 403.
6. Browser network sekmesinde TÜM istekler same-origin (`5001` origin, `/Platform/...`) olmalı; `localhost:5000` veya `localhost:5057`'ye doğrudan istek varsa smoke FAIL.
7. Console/network'te secret, tam gövde, alıcı dökümü YOK.

## 18. Ready-for-dev Checklist
- [x] Golden Reference Compact pack'i (DEV-0001) ve canlı kodu referans alındı; `Views/Platform/Tenants/` Platform Admin örneği incelendi (2026-07-07 hazırlık oturumu).
- [x] Frontmatter tüm zorunlu alanlar dolu (service, shell, golden_reference, entity_base, form_field_count).
- [x] DCP-002 preflight PASS (`MOD-0027-FU02`, parent `MOD-0027`).
- [x] Layout & Shell Contract'ta `_LayoutPlatformAdmin` açıkça yazılı.
- [x] Backend File Convention mevcut Features/Notifications düzenine ek olarak tanımlı; yeni feature klasörü yok.
- [x] Frontend File Contract: Templates/Settings Compact tam set; Dispatches read-only/list-detail alt kümesi açıkça tanımlı.
- [x] Validation Rules her form alanı için yazılı.
- [x] Failure Path 8 senaryo (duplicate, missing preview variable, unauthorized, cross-tenant, raw secret, concurrency, invalid cancel state, lookup unavailable).
- [x] Authorization Convention mevcut literal listesi + UI gating eşlemesi ile yazılı; yeni literal yok.
- [x] Gateway kararı açık: değişiklik gereksiz (`{everything}` route POST dahil doğrulandı).
- [x] Platform Lookup Decision yazılı (mevcut `locales` key kodda doğrulandı + 4 yeni key + tenant seçici kararı).
- [x] MVP kararları (§1) kullanıcı tarafından KABUL EDİLDİ (2026-07-07): monospace textarea (WYSIWYG kapsam dışı), render-preview endpoint'i, dispatch gövde/Bcc gizliliği, PSS-011 lookup stratejisi, FU03/FU04/FU05 kapsam ayrımı.
- [x] No-shell kuralı ve Contract blocker kuralı pack'e bağlayıcı olarak eklendi.
- [x] Status `ready-for-dev` yapıldı (2026-07-07); implementasyon `@orchestrator` ile başlayabilir.

## 19. Implementation Notes
- Bu pack MOD-0027 pack'inin "Batch 3 - Deferred UI" maddesini ve registry'deki NEW-003 → MOD-0027-FU02 kanonikleştirmesini gerçekleştirir.
- Master-plan FU02 satırı `partial %35` — backend hazırlığı; UI sıfırdan başlar. İş bitiminde `module-implementation-status.md` ve master-plan reconciliation notu güncellenir.
- `index.l10n.js` köprüsünde camelCase→PascalCase dönüşümü atlanırsa `window.L10n` anahtarları undefined kalır (repo'da bilinen tuzak) — Test Expectations'taki smoke bu yüzden toast metnini doğrular.
- `.resx` değişiklikleri hot-reload edilmez; smoke öncesi ilgili servis/frontend tam restart gerektirir (yerel fleet notu).
- Dispatch listesinde iki DateTimeOffset alanının birlikte sort edilmesi Mongo "parallel arrays" 500'üne yol açabilir (repo'da bilinen vaka) — tek alan sort veya in-memory sort tercih edilir.
- Tenant self-service UI (gelecek pack) bu UI'nin `_Form.cshtml` kalıbını kopyalayabilir; ancak route/permission modeli tamamen farklı olacaktır (tenant-context, `Modules.NotificationSettings.*`).

## 20. Follow-up Items

> **Delivery status:** FU02 implementasyonu tamam ve **canlı smoke PASS (2026-07-08)** — bkz. [smoke audit](../../../../docs/audits/pss-mod-0027-fu02-notification-template-ui-smoke-2026-07-08.md). Aşağıdaki ilk üç madde smoke sırasında N/A kalan, **düşük riskli ve opsiyonel** (bloke etmeyen) doğrulama adımlarıdır; compensating evidence mevcuttur.

- [ ] OPSİYONEL (smoke N/A) — **Visual browser confirmation:** bağlı bir tarayıcı ile üç ekranda DataTable doldurma, preview iframe boyama, SweetAlert onayları, DevTools Network (yalnızca `:5001`) ve Console (0 error) görsel teyidi.
- [ ] OPSİYONEL (smoke N/A) — **Restricted actor 403 live seed test:** `platform.notifications.*` içermeyen bir platform kullanıcısı seed edilip menü gizleme + direct-URL 403 canlı doğrulaması (backend `[HasPermission]` fail-closed zaten alias map + policy ile doğrulandı).
- [ ] OPSİYONEL (smoke N/A) — **Queued dispatch cancel live success:** Fake/deferred provider enable edilip Queued dispatch üretilerek koşullu Cancel butonu + HTTP 200 başarı canlı doğrulaması (invalid-state 409 zaten canlı kanıtlandı; Queued→Cancelled unit-test kaplı).
- [ ] MOD-0027-FU03: InApp kanalı + `UserNotification` + çan (bell) dropdown + polling pack'i (bu pack'in kapsamı DIŞI).
- [ ] MOD-0027-FU04: SignalR canlı iletim (hub Diten.Web'de, MOD-0035 event bus consumer) (bu pack'in kapsamı DIŞI).
- [ ] MOD-0027-FU05: Tenant self-service notification settings/template override UI (`_LayoutTenantShell`, 7 dil, tenant-context permission modeli) (bu pack'in kapsamı DIŞI; DCP-002 kimlik preflight'ı pack açılırken çalıştırılır).
- [ ] KOŞULLU — Gateway: implementasyon sırasında `/api/platform/notifications/{everything}` route'unun render-preview'ı kapsamadığı kanıtlanırsa bu pack gateway'e DOKUNMAZ; ayrı integration-agent task'ı açılır (explicit upstream/downstream çifti, OPTIONS dahil).
- [ ] KOŞULLU — Persistence: dispatch filtreleri için index ihtiyacı kanıtlanırsa gerekçeli istisna olarak ayrı rapor/follow-up maddesiyle ele alınır; bu pack Persistence'a doğrudan yazmaz.
- [ ] KOŞULLU — DataTable verifier read-only/list-detail modu desteklemiyorsa verifier geliştirme önerisi ayrı follow-up olarak raporlanır.
- [ ] "Send test email" aksiyonu (settings ekranından fake/SMTP provider ile) — MVP dışı, ayrı küçük ekleme.
- [ ] MOD-0263 gerçek sağlayıcı adaptörleri sonrası settings ekranına "Validate connection" aksiyonu.
- [ ] Delivery Analytics rapor görünümü (başarı oranı, kanal hacmi) — Excel Blueprint'in üçüncü Soft Page'inin olgun hali.

## Output Contract
Implementation final report şunları içermelidir:
- Module status: PASS / PARTIAL / FAIL / BLOCKED
- Changed files listesi
- Permission seed doğrulama kanıtı (`platform.notifications.*` rollere bağlı)
- Verifier + RESX parity çıktıları
- Browser smoke kanıtları (§17 sıra 1-6)
- Lookup key'lerinin `LookupOptionDto` kontrat kanıtı
- Proxy-profile kanıtı (network'te yalnızca same-origin istekler)
- Boundary check (Protected Paths ihlali yok)
- Open blockers / assumptions
- Next recommended step
