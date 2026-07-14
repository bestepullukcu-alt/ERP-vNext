---
id: MOD-0027-FU03A
name: Notification Event SourceType & PlatformSeed Bridge
domain: platform-shared-services
service: Diten.Platform
shell: none
golden_reference: custom-integration
entity_base: NotificationEventDefinition
status: completed
parent: MOD-0027
owner: ali.tufanoglu
branch: feature/pss/mod-0027-fu03a-notification-event-sourcetype-platformseed
started: 2026-07-08
target: TBD
form_field_count: 0
---

# MOD-0027-FU03A - Notification Event SourceType & PlatformSeed Bridge

> **Identity (DCP-002 — GATE PASSED, 2026-07-08):** `MOD-0027-FU03A`, Blueprint `MOD-0027` (Notification Service) parent'ının kanonik bir FU/bridge'idir. Preflight çalıştırıldı ve **PASS**:
> `py .antigravity/scripts/verify_module_id.py . --check-id MOD-0027-FU03A --name "Notification Event SourceType & PlatformSeed Bridge" --parent MOD-0027` → **`OK  MOD-0027-FU03A: proven against Blueprint/registry.`** (exit 0).
> **Registry satırı EKLENDİ** (`module-id-registry.md`: `MOD-0027-FU03A | Follow-up | completed | parent MOD-0027`). **DCP-002 kimlik kapısı GEÇİLDİ.** Owner review + 3 PARTIAL bulgusu RESOLVED. **Implemented + closed out 2026-07-08 (PASS-with-note)** — tests **1148/0**, fleet live-boot-clean smoke, seed no-op; bkz. [FU03A smoke audit](../../../../docs/audits/pss-mod-0027-fu03a-notification-event-sourcetype-platformseed-bridge-smoke-2026-07-08.md). **Status: `completed`.**

## 0. Bridge'in gerekçesi (neden ayrı pack — BAĞLAYICI)
MOD-0027-FU04A (Tenant Management Notification Event Opt-in), Platform Admin **fixed-page** tenant event'lerini Module Catalog'a **eklemeden** Notification Event Catalog'da göstermek istiyor. Bunun için FU03 Event Catalog altyapısının **manifest-only** olmaktan çıkıp **kaynak-tipli (source-typed)** hale gelmesi gerekiyor. Bu değişiklik:
- **FU03 (tamamlanmış, 1139/1139 test, live smoke PASS) surface'ini** genişletir → bitmiş bir FU'nun entity + validation dalına **sessiz additive** olarak sızmamalı; kendi izlenebilir delta'sını hak eder.
- **Tenant event'lerinden bağımsız ve yeniden kullanılabilir**tir: `NotificationEventSourceType` enum'unda `SystemSeed = 2` zaten var → foundation, "3 tenant event"in ötesinde gelecekteki system-seed event'ler için de tasarlanır.
- Ayrı merge edilince **temiz regresyon kapısı** verir: "FU03 1139 test yeşil + `SourceType=Manifest` backward-compat kanıtlı + clobber-guard yeşil" kapısı, tenant event'leri surface'e dokunmadan **ÖNCE** geçilir.

**Bu bridge tenant event içeriği EKLEMEZ.** Yalnızca generic foundation sağlar; tenant event seed içeriği FU04A'da kalır (bkz. §19).

Referans: architecture-ba-reviewer sign-off (2026-07-08) — Karar **PARTIAL → B (bridge önerilir)**; FU03 owner sign-off additive kontrat için **VERİLDİ**.

## 1. Module Summary
- **Purpose:** FU03 `NotificationEventDefinition` modelini **kaynak-tipli** hale getirir (`Manifest` / `PlatformSeed` / `SystemSeed`), manifest event'leri ile seed event'lerinin **aynı koleksiyonda güvenle** yaşamasını sağlar, PlatformSeed event'ler için **Module Catalog/ModulePages validation bypass** dalını ekler ve **generic idempotent seed loader iskeletini** tanımlar.
- **Neden foundation:** FU04A (tenant) ve ileride her system-seed event, bu foundation'a bağımlıdır; foundation tek yerde, yeniden kullanılabilir kalmalı.
- **Scope note:** Bu pack **generic foundation / contract-bridge**'dir. **Hiçbir tenant event, hiçbir tenant template hizası, hiçbir runtime dispatch içermez.** Read-only katalog davranışı FU03'ten miras alınır; yeni sayfa yoktur.
- **İlişki:** MOD-0027 (parent) veri modeli; **MOD-0027-FU03** (Bitti, ready-for-dev) manifest-driven katalog; **MOD-0027-FU04A** (reserved, draft) bu bridge'e `depends-on`; **MOD-0027-FU04B** (follow-up) runtime eventCode→dispatch.

### 1.1 Kabul edilen kaynak modeli (final — bağlayıcı)
Notification Event Catalog artık iki kaynak tipini ayırır:
1. **Manifest** (FU03 mevcut): module manifest `notificationEvents` deklarasyonundan; Module Catalog/ModulePages/permission manifest-içi doğrulanır.
2. **PlatformSeed / SystemSeed** (bu bridge — YENİ): platform fixed/admin (veya system) flow'lardan; **Module Catalog doğrulaması YAPILMAZ**, permission varsa gerçek RBAC literal'i üzerinden, yoksa **policy-gated** (`RequiredPolicy`); hedef bir **TargetRoute** (module-catalog page değil).

## 2. Ownership and Boundaries
### In-scope
- `NotificationEventSourceType` enum (`Manifest=0`, `PlatformSeed=1`, `SystemSeed=2`).
- `NotificationEventDefinition` additive alanları (§4): `SourceType`, `OwnerArea`, `OwnerDisplayName`, `TargetRoute`, `ModuleCatalogRef` (nullable), `RequiredPolicy` (kanonik tek ad; `AccessPolicy` kullanılmaz).
- Manifest sync tarafı: `create`'te `SourceType=Manifest`; çift-yönlü clobber guard (§6).
- **Generic** PlatformSeed validation dalı: Module Catalog/ModulePages bypass; RBAC-veya-policy permission modeli (§5).
- **Generic** idempotent `NotificationEventSeed.EnsureSeededAsync` **iskeleti** (upsert + HARD reconcile + SOFT preserve) — *mekanizma, içerik değil* (§7).
- DTO/mapping/`/Platform/NotificationEvents` UI additive **read-only** `sourceType`/`ownerArea`/`targetRoute` (§8).
- Backward-compat + clobber + validation-bypass + FU03 1139 regresyon testleri (§17).

### Out-of-scope (bkz. §19 detay + §6 protected paths)
- **3 tenant event** (`tenant.user.invited` / `tenant.lifecycle.suspended` / `tenant.lifecycle.reactivated`) → **FU04A**.
- Tenant-specific seed **içeriği** + tenant template variable **hizası** → FU04A.
- Runtime eventCode→dispatch, `eventCode → defaultTemplateKey → QueueEmailNotificationCommand` adapter → **FU04B**.
- TenantManagementManifestProvider / IModuleManifestProvider / Module Catalog `tenant-management` / ModulePages `TENANTS` / PlatformNavigationCatalog / Gateway / tenant producer-template-consumer / Workflow-document event opt-in → **YASAK (§6)**.

### Ownership rule
- FU03A, Notification Event Catalog'un **kaynak-tipli foundation'ını** sahiplenir. Module/page/permission **üretmez**; seed **içeriği** sahiplenmez (o FU04A'nın). PlatformSeed event'ler için yalnızca **gerçek RBAC permission veya policy** ve **route** referansı verir.

## 3. Owned Objects
- **Enum (yeni):** `NotificationEventSourceType` (`Diten.Platform.Domain.Enums`).
- **Entity (genişletilen, additive):** `NotificationEventDefinition` (`Diten.Platform.Domain.Entities.Notifications`).
- **Service (yeni iskelet):** `NotificationEventSeed.EnsureSeededAsync(IMongoDatabase)` (`Diten.Platform.Infrastructure.Persistence.Configurations`) — generic upsert mekanizması, tenant içeriği YOK.
- **Service (genişletilen):** `NotificationEventManifestSyncService` — `create`'te `SourceType=Manifest` + clobber guard (davranışsal additive, mevcut manifest davranışı değişmez).
- **DTO/Mapping (genişletilen, additive):** `NotificationEventDefinitionDto` + `NotificationEventMappings` read-only alanlar.
- **View (genişletilen, opsiyonel):** `/Platform/NotificationEvents` read-only kolonlar.

## 4. Entity Fields (additive — nullable/default → geriye dönük uyumlu)

### 4.1 `NotificationEventSourceType` enum (BAĞLAYICI)
```
NotificationEventSourceType
{
    Manifest     = 0,   // FU03 mevcut event'ler — geriye dönük default (§4.3)
    PlatformSeed = 1,   // platform fixed/admin flow'lar (FU04A içeriği)
    SystemSeed   = 2    // ileriye dönük: system-owned seed
}
```
**`Manifest = 0` ZORUNLU:** Mongo şemasız `notification_event_definitions` koleksiyonunda mevcut dokümanlarda `SourceType` alanı YOK; eksik alan enum default `0`'a çözülür → tüm mevcut FU03 event'leri migration'sız `Manifest` sayılır.

### 4.2 Additive alan tablosu
| Alan | Tip | Default | Not |
|---|---|---|---|
| `SourceType` | `NotificationEventSourceType` | `Manifest` (=0) | Kaynak ayrımı; zorunlu 0 default |
| `OwnerArea` | `string?` | null | Fixed-page alanı işareti (ör. `PlatformAdmin`) |
| `OwnerDisplayName` | `string?` | null | İnsan-okur owner adı |
| `TargetRoute` | `string?` | null | Serbest fixed route. **`TargetRouteDescriptorId` (Guid) ile KARIŞTIRILMAZ — ayrı string alan.** |
| `ModuleCatalogRef` | `string?` | null | Module Catalog bağı yok → null |
| `RequiredPolicy` | `string?` | null | **Kanonik tek alan adı** (eski taslaktaki `AccessPolicy` ismi KULLANILMAZ). Policy-gated fixed-page için (ör. `PlatformActor`). |

**Alan adı kararı (BAĞLAYICI):** Policy alanının kanonik adı **`RequiredPolicy`**'dir. `AccessPolicy` ismi pack genelinde kullanılmaz (FU03'ün mevcut `RequiredPermissionKey` alanıyla paralel okunur: `Required*`).

### 4.2.1 `RequiredPermissionKey` ↔ `RequiredPolicy` ilişkisi (BAĞLAYICI)
- **`RequiredPermissionKey` doluysa:** event **permission-gated**'tir; §5 katman kuralına göre gerçek RBAC/permission literal üzerinden doğrulanır.
- **`RequiredPermissionKey = null` ise:** event **policy-gated**'tir; `RequiredPolicy` (ör. `PlatformActor`) taşınır ve seed anında permission reflection **gerekmez** (§5).
- İkisi aynı anda: `RequiredPermissionKey` varsa permission doğrulaması önceliklidir; `RequiredPolicy` erişim politikası olarak taşınmaya devam edebilir. Fixed platform admin surface'lerinde tipik model: `RequiredPermissionKey=null` + `RequiredPolicy=PlatformActor`.

### 4.3 Backward-compatibility (BAĞLAYICI)
- `SourceType` non-nullable enum, default `Manifest (0)`; yeni alanlar nullable → mevcut dokümanlarda null.
- **Migration YOK.** Mevcut `notification_event_definitions` dokümanları olduğu gibi deserialize olur; `null→empty` coalesce korunur.
- FU03 sync `create` yolu yeni event'lere `SourceType = Manifest` **açıkça** yazar (§6).

## 5. Validation Rules (PlatformSeed dalı)
- **Manifest event (FU03 mevcut — DEĞİŞMEZ):** OwnerModule/TargetPageCode/RequiredPermissionKey **manifest içinde** doğrulanır.
- **PlatformSeed / SystemSeed event (YENİ):**
  - **Module Catalog doğrulaması YOK.**
  - **ModulePages / TargetPageCode doğrulaması YOK** (`TargetPageCode = null` serbest).
  - `TargetRoute` serbest fixed route olarak **taşınır** (doğrulanmaz).
  - `RequiredPermissionKey` **varsa** → gerçek permission/RBAC/alias literalinde var mı doğrulanır (`HasPermissionReflector` + `PermissionAliasResolver` dual-read); **yoksa** → `RequiredPolicy` (ör. `PlatformActor`) ile **policy-gated kabul edilir**.
  - `DefaultTemplateKey` format geçerli + referans template mevcut.
  - `EventCode` canonical/lowercase dotted / unique / immutable.
  - Geçersiz permission (verilmişse) / eksik template → **Draft + issue** (fail-controlled; Active olmaz).

### 5.1 Permission-validation KATMAN kuralı (BAĞLAYICI — mimari)
Seed loader **Infrastructure** katmanında (`IMongoDatabase`, startup — §7/§10) çalışır; `HasPermissionReflector` + `PermissionAliasResolver` **Platform.API** assembly'sine bağlıdır. Bu iki gerçeği birbirine karıştırmak cross-layer bağımlılık yaratır.
- **`Infrastructure → Platform.API` bağımlılığı KESİN YASAK.** Infrastructure seed loader, API assembly reflection'ı (`HasPermissionReflector` vb.) **çağırmaz**.
- **`RequiredPermissionKey = null` + `RequiredPolicy` (policy-gated) seed'lerde** permission reflection **GEREKMEZ** → seed doğrudan geçerli/Active olabilir (bu bridge'in ve FU04A içeriğinin varsayılan yolu).
- **`RequiredPermissionKey` DOLU (permission-gated) seed'lerde** permission-literal doğrulaması **API-side** yapılır. İki kabul edilebilir desen:
  1. Seed, permission-taşıyan kaydı **`Draft`** yazar; **API-side validation/activation pass** (API assembly reflection erişilebilir) literal'i doğrulayıp geçerliyse **`Active`**'e terfi ettirir; geçersizse `Draft` + issue.
  2. Doğrulama tamamen **API-side** bir sync/validation adımında yürütülür; Infrastructure seed yalnızca kaydı oluşturur.
- **Generic gelecekteki SystemSeed** event'leri permission-literal taşıyorsa, validation katmanı **açıkça API-side** olmalıdır (Infrastructure'da değil).
- Not: Bu kural, FU04A §6.1'deki "katman gerilimi" notunun **foundation sahibi bridge'e taşınmış kanonik hâlidir**; foundation burada tanımlandığı için kural burada yaşar.

## 6. Protected Paths & Sync Guardrails
### 6.1 Değiştirilmeyecek (Protected Paths — ihlali FAIL)
- `IModuleManifestProvider` implementasyonları, `ModuleManifestDocument` (yeni provider/producer eklenmez).
- Module Catalog (`tenant-management` veya herhangi bir modül yazılmaz), ModulePages (`TENANTS` yazılmaz).
- `PlatformNavigationCatalog` (fixed admin nav).
- Gateway (`ocelot.json`).
- Tenant producer/template/consumer davranışı.
- FU03 manifest event davranışı (Manifest dalı birebir korunur).

### 6.2 Clobber guardrail'leri (BAĞLAYICI)
İki popülasyon aynı koleksiyonda `EventCode` ile ayrışır:
- **Manifest sync**, `SourceType == PlatformSeed`/`SystemSeed` kaydı **update ETMEZ** → skip + controlled issue.
- **Seed loader**, `SourceType == Manifest` kaydı **update ETMEZ** → skip + controlled issue.
- **Cross-source EventCode collision** → controlled issue (sahip kaynak korunur; clobber yok).
- Sync `create` yolu yeni event'lere `SourceType = Manifest` yazar.

## 7. Generic Seed Loader (iskelet — içerik YOK)
- **`NotificationEventSeed.EnsureSeededAsync(IMongoDatabase)`** — startup, `Diten.Platform.Infrastructure/DependencyInjection.cs` seed bloğunda **`NotificationTemplateSeed.EnsureSeededAsync`'in HEMEN ARDINDAN** (böylece referans template'ler garanti mevcut). Bu bridge yalnızca **mekanizmayı** kurar; **seed edilecek event listesi boş/generic** (tenant içeriği FU04A ekler).
- **Sync-from-manifest içine KARIŞTIRILMAZ.** `INotificationEventManifestSyncService` provider-driven kalır; seed onun DIŞINDA.
- **Idempotent upsert:** yoksa create (kaynak tipi çağıran tarafından verilir); varsa **HARD alanları** (Channel, DefaultTemplateKey, Required/OptionalVariables, TargetRoute, RequiredPolicy/RequiredPermissionKey) reconcile eder; **SOFT operator alanlarını** (operator'ın değiştirdiği Status/Archived/Deprecated, display override'ları) korur.
- **Clobber guard:** `SourceType=Manifest` kaydına dokunmaz; duplicate/cross-source collision → controlled issue.

## 8. UI Etkisi
- **shell: none — yeni sayfa YOK.** Mevcut `/Platform/NotificationEvents` (FU03) UI **bozulmaz**; seed event'ler kaynak-agnostik listeye/slot'a otomatik girer.
- **Additive read-only:** DTO (`NotificationEventDefinitionDto`) + `NotificationEventMappings` + view'a read-only `sourceType` / `ownerArea` / `targetRoute`. Eklenmezse UI çalışmaya devam eder.
- **PlatformNavigationCatalog değişmez.** **Gateway değişmez** (yeni endpoint yok).
- `active-template-slots` **kaynak-agnostik** kalır (`ListActiveAsync` yalnızca `Status==Active`; endpoint değişmez).

## 9. Runtime Constraints
- Event kayıtları platform/global (FU03). Tenant event oluşturamaz.
- Seed idempotent: aynı EventCode ikinci seed → update (immutable EventCode; HARD reconcile / SOFT preserve).
- Module Catalog/ModulePages/permission catalog ownership değişmez; permission yalnızca okunur/doğrulanır (yeni permission yazılmaz).
- **Runtime eventCode→dispatch YOK** (FU04B).

## 10. Backend File Convention
- Enum: `services/Diten.Platform/src/Diten.Platform.Domain/Enums/NotificationEventSourceType.cs` (yeni).
- Entity additive: `.../Domain/Entities/Notifications/NotificationEventDefinition.cs`.
- Sync guardrail: `.../Application/Features/Notifications/Services/NotificationEventManifestSyncService.cs`.
- Seed iskelet: `.../Infrastructure/Persistence/Configurations/NotificationEventSeed.cs` (yeni; `NotificationTemplateSeed` kalıbı).
- DTO/Mapping: `.../Application/Features/Notifications/NotificationEventContracts.cs` + `NotificationEventMappings.cs`.
- Startup wiring: `.../Infrastructure/DependencyInjection.cs` (seed sırası).
- **Yeni CQRS command/query iskeleti gerekmez** (bridge; read/slot contract FU03'ten miras).

## 11. Frontend File Contract
- Yeni view seti YOK. Opsiyonel additive read-only kolonlar mevcut `/Platform/NotificationEvents` view'ında.

## 12. Authorization Convention
- Yeni permission literal **üretilmez**. PlatformSeed event `RequiredPolicy` (ör. `PlatformActor`) veya `RequiredPermissionKey` (gerçek, enforce edilen literal) taşır. Katalog UI'ın kendi permission'ı FU03'ten miras (`platform.notifications.events.read/manage`).

## 13. Gateway / API Routing Decision
- **Gateway değişikliği YOK.** Yeni endpoint yok; mevcut FU03 notification-events route'ları yeterli. integration-agent task'ı **gerekmez**.

## 14. Failure Paths to Verify
- `SourceType`'sız eski doküman → `Manifest` (deserialize; exception yok).
- Manifest sync `SourceType=PlatformSeed`/`SystemSeed` kaydına denk gelirse → skip + issue (clobber yok).
- Seed `SourceType=Manifest` kaydına denk gelirse → skip + issue (clobber yok).
- Cross-source EventCode collision → controlled issue.
- `RequiredPermissionKey` verilmiş ama RBAC'ta yoksa → Draft + issue. (`null` + `RequiredPolicy=PlatformActor` → geçerli/policy-gated.)
- Template key bulunamazsa → controlled issue.
- Module Catalog'a yanlışlıkla modül / ModulePages'e page yazılırsa → **FAIL**.
- IModuleManifestProvider eklenirse → **FAIL**.

## 15. Acceptance Criteria
- [ ] `NotificationEventSourceType` enum eklendi; **`Manifest == 0`** (ordinal test).
- [ ] `NotificationEventDefinition` additive alanları (SourceType/OwnerArea/OwnerDisplayName/TargetRoute/ModuleCatalogRef/RequiredPolicy) nullable/default; **migration yok**; `SourceType`'sız eski doküman `Manifest`'e çözülür.
- [ ] **Bridge, generic foundation'ı tenant event içeriğinden AÇIKÇA ayırır** (tenant event / tenant template hizası bu pack'te YOK).
- [ ] FU03 manifest-driven davranış **değişmez** (Manifest dalı birebir korunur); sync `create` `SourceType=Manifest` yazar.
- [ ] Clobber guard: sync `SourceType=PlatformSeed/SystemSeed` kaydı update etmez; seed `SourceType=Manifest` kaydı update etmez (skip+issue); cross-source collision → issue.
- [ ] PlatformSeed validation: Module Catalog/ModulePages doğrulaması **çağrılmaz**; `RequiredPermissionKey=null` + `RequiredPolicy=PlatformActor` **kabul edilir**; permission verilmiş+geçersiz → Draft+issue.
- [ ] Generic `NotificationEventSeed.EnsureSeededAsync` iskeleti eklendi; `NotificationTemplateSeed` sonrası; sync'e karıştırılmaz; idempotent; HARD reconcile + SOFT preserve.
- [ ] `active-template-slots` **kaynak-agnostik** kalır; mevcut `/Platform/NotificationEvents` UI **bozulmaz**.
- [ ] **No Module Catalog side-effect · No PlatformNavigationCatalog side-effect · No runtime producer adapter · No Gateway change.**
- [ ] **FU04A bu bridge'e depends-on olarak bağlanabilir ve yalnızca tenant event seed içeriğini implement eder** (§19).
- [ ] Backward-compat explicit; existing FU02/FU03 (1139) **regresyonsuz**; `dotnet build Platform.API` 0 hata.

## 16. Guardrail Listesi (bağlayıcı)
1. `NotificationEventSourceType.Manifest == 0` (ordinal test ile kilitli).
2. Additive alanlar nullable/default; **migration gerekmez**.
3. Manifest sync ve PlatformSeed seed **birbirini clobber etmez** (çift-yönlü skip+issue).
4. Seed loader `INotificationEventManifestSyncService` **DIŞINDA**; hiçbir `IModuleManifestProvider` eklenmez.
5. `TargetRoute` (string) ≠ `TargetRouteDescriptorId` (Guid) — ayrı alan.
6. `RequiredPermissionKey` **uydurulmaz**; fixed platform admin surface için `RequiredPolicy` modeli serbest.
7. `active-template-slots` kaynak-agnostik kalır.
8. Mevcut FU03 davranışı değişmez; FU03 test suite (1139) geçer.
9. Bu bridge **tenant event içeriği eklemez** (FU04A'ya kalır).
10. Runtime eventCode→dispatch **eklenmez** (FU04B).

## 17. Test Expectations
- `dotnet build Platform.API` (fleet kilidi → temp-output); 0 hata.
- `SourceType` alanı olmayan eski doküman deserialize → `SourceType == Manifest`.
- `NotificationEventSourceType.Manifest == 0` (enum ordinal assert).
- Sync `create` yolu `SourceType = Manifest` yazar (assert).
- Sync, `SourceType=PlatformSeed` event'i clobber etmez (skip + issue).
- Seed (generic), `SourceType=Manifest` event'i clobber etmez (skip + issue).
- **Generic seed idempotency (explicit — test-only fixture seed ile):**
  - 1. run → kayıt oluşturur (`SourceType` fixture'a göre PlatformSeed/SystemSeed).
  - Operator SOFT alanı değiştirir (ör. `Status`, display override).
  - 2. run → **HARD alanları reconcile eder** (Channel/DefaultTemplateKey/Required/OptionalVariables/TargetRoute/RequiredPolicy/RequiredPermissionKey), **operator SOFT alanları korunur**, **duplicate yaratmaz**.
  - Aynı run → `SourceType=Manifest` kaydını **clobber etmez** (skip + issue).
- PlatformSeed validation, Module Catalog / ModulePages doğrulaması **çağırmaz** (mock/spy).
- `RequiredPermissionKey = null` + `RequiredPolicy = PlatformActor` → accepted (Active olabilir; seed-side permission reflection çağrılmaz — §5.1).
- Permission-gated seed (`RequiredPermissionKey` dolu) → seed **Draft** yazar; API-side pass literal geçerliyse **Active**'e terfi, geçersizse Draft + issue (§5.1 katman kuralı; Infrastructure API reflection çağırmaz).
- `active-template-slots` kaynak-agnostik (Manifest + seed Active event'leri birlikte döner).
- Existing FU02/FU03 (1139) regression **PASS**; `null→empty` coalesce korunur.

> **Test fixture kuralı (BAĞLAYICI):** Bridge seed testleri **gerçek tenant event içeriği KULLANMAZ**; **test-only fixture seed** (ör. `test.fixture.event`) kullanılır. `tenant.user.invited` / `tenant.lifecycle.suspended` / `tenant.lifecycle.reactivated` bu bridge'in testlerinde **yer almaz** — bunlar **FU04A scope'udur**.

## 18. Ready-for-dev Checklist / Kalan governance adımları
### Çözülen (RESOLVED)
- [x] **DCP-002 preflight PASS** (2026-07-08): `MOD-0027-FU03A: proven against Blueprint/registry.` (exit 0).
- [x] **Registry reservation (RESOLVED — 2026-07-08):** `module-id-registry.md`'ye `MOD-0027-FU03A | Follow-up | reserved | parent MOD-0027` satırı EKLENDİ.
- [x] **FU03 owner sign-off** — additive kontrat için VERİLDİ (architecture-ba-reviewer, 2026-07-08; aynı owner ali.tufanoglu).
- [x] SourceType modeli + `Manifest=0` backward-compat kararı (§4).
- [x] Permission/policy modeli: `RequiredPolicy=PlatformActor` + `RequiredPermissionKey=null` (fixed-page) — kabul.
- [x] **Owner review PARTIAL bulguları (RESOLVED — 2026-07-08):**
  - [x] **#1 Alan adı ikiliği:** `AccessPolicy` kaldırıldı; kanonik tek ad **`RequiredPolicy`** (§4.2) + `RequiredPermissionKey↔RequiredPolicy` ilişkisi (§4.2.1).
  - [x] **#2 Permission-validation katman kuralı:** §5.1 eklendi — `Infrastructure → Platform.API` reflection **YASAK**; permission-gated seed API-side (Draft→Active) doğrulanır; policy-gated seed reflection gerektirmez.
  - [x] **#3 Generic seed idempotency test coverage:** §17'ye explicit idempotency testi (HARD reconcile + SOFT preserve + no-duplicate + no-clobber) + **test-only fixture** kuralı eklendi.

- [x] **Owner pack review onayı (RESOLVED — 2026-07-08):** 3 PARTIAL bulgusu kapandı; mimari/precision blocker yok. Status `draft` → **`ready-for-dev`**.

### Açık governance adımları
- **Yok.** Tüm governance/DCP/registry/owner-review kapıları geçildi. FU03A **implementasyona hazır**.

### Implementation gate (teslim kapısı — kod tarafında)
- [ ] **FU03 1139 regresyon** yeşil (mevcut Manifest davranışı + sync regresyonsuz; `null→empty` coalesce korunur).
- [ ] **Bridge testleri** yeşil (§17: `Manifest=0` ordinal, sync `create` `SourceType=Manifest`, çift-yönlü clobber, generic seed idempotency/fixture, validation bypass, `active-template-slots` source-agnostic).
- [ ] `dotnet build Platform.API` 0 hata.
- [ ] FU04A pack'inde FU03A `depends-on` referansı + FU04A §17 "FU03 owner sign-off" blocker'ının **RESOLVED (via FU03A bridge)** işaretlenmesi.

### Kalan adımlar (sıra)
1. FU03A registry reservation satırı.
2. FU03A owner review → `ready-for-dev`.
3. FU03A implement + **FU03 1139 regresyon kapısı** (yeşil).
4. (Kapı geçince) FU04A → tenant seed içeriği + `ready-for-dev`.

- [ ] Status **draft** — bu geçişte ready-for-dev YAPILMADI.

## 19. FU04A'nın bridge sonrası daralan scope'u
Bu bridge merge/ready olduktan sonra **FU04A yalnızca şunları** sahiplenir:
- 3 tenant event'in **PlatformSeed seed içeriği** (`tenant.user.invited`, `tenant.lifecycle.suspended`, `tenant.lifecycle.reactivated`).
- Tenant-specific **template hizası** + `RequiredVariables` producer alignment.
- `NotificationEventSeed.EnsureSeededAsync` **çağrısının** tenant içerikle startup wiring'i.
- FU04A acceptance/smoke.

FU04A **foundation'a dokunmaz** (entity/enum/validation/sync-guard/seed-iskelet bridge'den gelir). Böylece FU04A "sadece içerik" pack'ine dönüşür.

## 20. Follow-up Items
- **FU04A:** tenant event seed içeriği (bu bridge'e depends-on).
- **FU04B:** runtime `eventCode → defaultTemplateKey → QueueEmailNotificationCommand` adapter.
- İleride SystemSeed event'leri (non-tenant) — aynı foundation'ı yeniden kullanır.

---

## Özet karar tablosu
| Konu | Sonuç |
|---|---|
| Bridge gerekçesi | Bitmiş FU03 surface'inin genişlemesi + yeniden kullanılabilir foundation (SystemSeed dahil) + temiz regresyon kapısı |
| SourceType enum | `Manifest=0` / `PlatformSeed=1` / `SystemSeed=2` (`Manifest=0` zorunlu) |
| Additive alanlar | SourceType/OwnerArea/OwnerDisplayName/TargetRoute/ModuleCatalogRef/RequiredPolicy (nullable/default) |
| Permission/policy | `RequiredPolicy=PlatformActor` + `RequiredPermissionKey=null` (fixed-page) — kabul; literal uydurulmaz |
| Seed loader | Generic `EnsureSeededAsync` iskeleti; sync DIŞINDA; içerik YOK |
| Tenant event içeriği | **FU04A** (bu bridge'de YOK) |
| Runtime dispatch | **FU04B** |
| DCP-002 | Preflight PASS (exit 0) |
| Açık governance | **Yok** (reservation + preflight + owner review + 3 PARTIAL bulgusu RESOLVED) |
| Status | **ready-for-dev** — implementasyona hazır; teslim kapısı: FU03 1139 regresyon + bridge testleri |
