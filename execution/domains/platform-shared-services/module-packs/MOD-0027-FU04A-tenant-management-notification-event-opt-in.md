---
id: MOD-0027-FU04A
name: Tenant Management Notification Event Opt-in
domain: platform-shared-services
service: Diten.Platform
shell: none
golden_reference: custom-platform-seed
entity_base: none
status: completed
parent: MOD-0027
depends_on: MOD-0027-FU03A
owner: ali.tufanoglu
branch: feature/pss/mod-0027-fu04a-tenant-notification-events
started: 2026-07-08
target: TBD
form_field_count: 0
---

# MOD-0027-FU04A - Tenant Management Notification Event Opt-in

> **Identity (DCP-002 — GATE PASSED, 2026-07-08):** `MOD-0027-FU04A`, Blueprint `MOD-0027` parent'ının FU'sudur. **Registry satırı EKLENDİ** (`module-id-registry.md`: `MOD-0027-FU04A | Follow-up | reserved | parent MOD-0027`). Preflight **PASS**: `verify_module_id.py --check-id MOD-0027-FU04A --name "Tenant Management Notification Event Opt-in" --parent MOD-0027` → `OK` (exit 0). **DCP-002 kimlik kapısı GEÇİLDİ.**
>
> **Dependency (BAĞLAYICI):** Bu pack, **`MOD-0027-FU03A` (Notification Event SourceType & PlatformSeed Bridge)** üzerine kuruludur. Bridge, generic SourceType/PlatformSeed foundation'ı (enum + additive entity alanları + validation dalı + seed loader iskeleti) sağlar. **FU04A yalnızca 3 tenant event'in seed içeriğini** implement eder; foundation'a dokunmaz. FU04A, FU03A **implement + merge + FU03 regression gate** geçilene kadar ready-for-dev OLAMAZ. (Bkz. §17.)

## 0. Mimari karar değişikliği (2026-07-08 — BAĞLAYICI)
**Önceki "TenantManagementManifestProvider (minimal manifest)" yaklaşımı ÜRÜN/MİMARİ KARARIYLA REDDEDİLDİ.** Integration-agent raporunda teknik olarak güvenli bulunmuştu, ancak ürün kararı gereği **kabul edilmedi**:
- **Platform Admin fixed-page'ler Module Catalog vatandaşı OLMAMALI.** Module Catalog **tenant business/application modülleri** içindir.
- Tenant Management fixed admin page'inin self-registration ile Module Catalog'a düşmesi **istenmiyor**.
- `PlatformNavigationCatalog` (fixed admin nav) ile Module Catalog **ayrımı korunmalı**.

**Yeni doğru yaklaşım:** Tenant event'leri `ModuleManifestDocument.NotificationEvents` üzerinden DEĞİL, MOD-0027 Notification Event Catalog içinde **platform-owned PlatformSeed/SystemSeed event** olarak tanımlanır. Böylece event'ler kataloğa girer ama **Module Catalog'a yeni modül eklenmez, IModuleManifestProvider eklenmez.**

## 1. Module Summary
- **Purpose:** `tenant.user.invited`, `tenant.lifecycle.suspended`, `tenant.lifecycle.reactivated` event'lerini `/Platform/NotificationEvents` kataloğunda görünür yapmak — **Module Catalog'a dokunmadan**, platform-seed kaynak modeliyle.
- **Neden:** Bu 3 event gerçekten email queue eden + mevcut template'i olan yegâne tenant event'leridir (producer inventory 2026-07-08). Ama owner'ları (MOD-0009 Tenant/Environment Management) **fixed-page**'dir ve Module Catalog'a girmemelidir.
- **Yeni kaynak modeli:** Notification Event Catalog iki kaynak tipi tanır:
  1. **Manifest** (FU03): module manifest `notificationEvents` deklarasyonundan; Module Catalog/ModulePages/permission manifest-içi doğrulanır.
  2. **PlatformSeed / SystemSeed** (FU04A — YENİ): platform fixed/admin flow'lardan; **Module Catalog doğrulaması YAPILMAZ**, permission **gerçek RBAC literal**'i üzerinden doğrulanır, hedef bir **TargetRoute** (module catalog page değil).
- **Scope note:** FU04A yalnızca **katalog görünürlüğü** (platform-seed). Runtime eventCode→dispatch **DAHİL DEĞİL** (FU04B follow-up). Workflow/document event'leri **DAHİL DEĞİL**.

### 1.1 Kesin yasaklar (bağlayıcı — ihlali FAIL)
- **TenantManagementManifestProvider oluşturma YASAK.**
- **IModuleManifestProvider ekleme YASAK.**
- **ModuleManifestDocument'a producer/provider ekleme YASAK.**
- **Module Catalog'a `tenant-management` modülü kaydetme YASAK.**
- **ModulePages'e `TENANTS` page kaydetme YASAK.**
- **PlatformNavigationCatalog değiştirme YASAK.**
- Mevcut Tenants nav/sayfa davranışını değiştirme YASAK.
- Mevcut tenant producer/template/consumer davranışını değiştirme YASAK (FU04A runtime içermez).

## 2. Ownership and Boundaries
### In-scope
- Notification Event Catalog'a **PlatformSeed/SystemSeed** kaynak tipi + 3 tenant event'inin **seed** olarak tanımlanması.
- (FU03-side, additive) `NotificationEventDefinition` için **SourceType / OwnerArea / TargetRoute** alanları + platform-seed **validation dalı** (Module Catalog yerine gerçek RBAC permission).
- Idempotent **NotificationEventSeed** (create-if-missing) — startup'ta veya seed-load ile.
- `/Platform/NotificationEvents` (FU03 UI) bu 3 event'i gösterir; `active-template-slots` döner.

### Out-of-scope
- Manifest provider / Module Catalog / ModulePages / nav (bkz. §1.1).
- Runtime eventCode dispatch → **FU04B**.
- Workflow/document/campaign event'leri.
- Tenant entity/CRUD/lifecycle davranışı (değişmez).

### Ownership rule
- FU04A, tenant event'lerinin **platform-seed tanımını** sahiplenir. Notification Event Catalog module/page/permission **üretmez**; PlatformSeed event'ler için yalnızca **gerçek RBAC permission** ve **route** referansı verir.

## 3. KARAR 1 — Kaynak modeli (BAĞLAYICI)
| Seçenek | Durum |
|---|---|
| **A — Full TenantManagementManifestProvider** | **REJECTED** (ürün mimarisi: fixed-page Module Catalog'a girmemeli) |
| **B — Minimal manifest (tek page + permission)** | **REJECTED due to product architecture** (yine Module Catalog'a modül/page kaydeder) |
| **B2 — Page-less manifest fallback** | **REJECTED** (yine IModuleManifestProvider + bare catalog modül girişi yaratır) |
| **C — PlatformSeed/SystemSeed NotificationEventDefinition (ÖNERİ/KABUL)** | **ACCEPTED** — manifest yok, Module Catalog yok; event doğrudan katalogda platform-seed olarak yaşar |

**Kabul edilen yaklaşım (C):** Event'ler, FU03 `NotificationEventDefinition` kayıtları olarak **PlatformSeed** kaynak tipiyle **seed** edilir. Manifest sync yolu (FU03) bu event'lere dokunmaz; ayrı bir **seed loader** (idempotent) bunları oluşturur/günceller. Validation: Module Catalog/ModulePages **doğrulanmaz**; `RequiredPermissionKey` **gerçek RBAC/permission literal** üzerinden doğrulanır; hedef `TargetRoute` (module-catalog page değil).

## 4. Yeni kaynak modeli — alan sözleşmesi (FU03-side additive — ACCEPTED)
Integration-impact raporu (2026-07-08) `NotificationEventDefinition`'a nullable/default additive alanlar eklemenin **güvenli ve regresyonsuz** olduğunu doğruladı. Kabul edilen alan sözleşmesi:

### 4.1 `NotificationEventSourceType` enum (ACCEPTED)
```
NotificationEventSourceType
{
    Manifest    = 0,   // FU03 mevcut event'ler — geriye dönük default (bkz. §4.3)
    PlatformSeed = 1,  // bu pack: platform fixed/admin flow'lar
    SystemSeed  = 2    // ileriye dönük ayrılmış (system-owned seed)
}
```
**`Manifest = 0` ZORUNLU** (integration bulgusu): Mongo şemasız koleksiyonda mevcut dokümanlarda `SourceType` alanı yoktur; eksik alan enum default `0`'a çözülür. `Manifest`'in `0` olması, tüm mevcut FU03 event'lerinin migration'sız `Manifest` kaynağına düşmesini garanti eder.

### 4.2 Additive alanlar (nullable/default → geriye dönük uyumlu)
| Alan | Tip | PlatformSeed davranışı |
|---|---|---|
| **SourceType** | `NotificationEventSourceType` | `PlatformSeed`. Default `Manifest` (=0). |
| **OwnerArea** | `string?` | `PlatformAdmin` (fixed-page alanı işareti) |
| **OwnerDisplayName** | `string?` | `Tenant / Environment Management` |
| **TargetRoute** | `string?` | `/Platform/Tenants` (fixed admin route). **`TargetRouteDescriptorId` (Guid) ile KARIŞTIRILMAZ — ayrı serbest string alan.** |
| **ModuleCatalogRef** | `string?` | **null** (Module Catalog'a bağlı değil) |
| **RequiredPolicy** (`AccessPolicy`) | `string?` | `PlatformActor` (fixed-page policy gate — bkz. §4.4) |
| **RequiredPermissionKey** | `string?` | **null** (gerçek enforce edilen literal yoksa — bkz. §4.4) |
| **TargetPageCode** | `string?` | **null** (Module Catalog page yok — mevcut FU03 alanı) |
| **ManifestSource** | `string?` | `notification-event-seed` (sentinel; manifest ModuleCode değil) |
| **OwnerModuleId** | `string` | `MOD-0009` (canonical Tenant/Environment Management) |

### 4.3 Backward-compatibility notu (BAĞLAYICI)
- `SourceType` non-nullable enum, default `Manifest (0)`. Yeni eklenen `OwnerArea`/`OwnerDisplayName`/`TargetRoute`/`ModuleCatalogRef`/`RequiredPolicy` **nullable**; mevcut dokümanlarda null kalır.
- FU03 sync `create` yolu, yeni event'lere `SourceType = Manifest` **açıkça** yazmalıdır (additive tek satır; §5.x sync guardrail).
- Migration yok; mevcut `notification_event_definitions` dokümanları olduğu gibi deserialize olur.

### 4.4 Permission/Policy kararı (RESOLVED — 2026-07-08 kullanıcı kararı)
`/Platform/Tenants` fixed-page'i besleyen `Admin/TenantsController` **`[Authorize(Policy = "PlatformActor")]`** ile korunuyor; `[HasPermission("platform.tenants.read")]` gibi bir base tenant-read literal'i Platform.API'de **enforce edilmiyor** (yalnızca `platform.tenants.quotas.*` ve `platform.tenants.commercial.subscription.*` alt-controller'larda var). Karar:
- **`platform.tenants.read` UYDURULMAYACAK.**
- PlatformSeed event'lerinde `RequiredPermissionKey` **null olabilir**; bu durumda `RequiredPolicy = PlatformActor` taşınır ve event **policy-gated** kabul edilir.
- Gerçek, enforce edilen bir `platform.tenants.*` literal'i varsa (ör. quotas/commercial), o kullanılabilir ve permission-gated doğrulanır.

### 4.5 PlatformSeed validation dalı (FU03 sync/seed ayrımı)
- Manifest event: OwnerModule/TargetPageCode/RequiredPermissionKey **manifest içinde** doğrulanır (FU03 mevcut — değişmez).
- **PlatformSeed event:**
  - **Module Catalog doğrulaması YAPILMAZ.**
  - **ModulePages / TargetPageCode doğrulaması YAPILMAZ** (`TargetPageCode = null`).
  - `TargetRoute` serbest fixed route olarak **taşınır**, doğrulanmaz.
  - `RequiredPermissionKey` **varsa** gerçek permission/RBAC literalinde var mı diye doğrulanır (`HasPermissionReflector` + `PermissionAliasResolver` dual-read); **yoksa** `RequiredPolicy = PlatformActor` ile policy-gated kabul edilir.
  - `DefaultTemplateKey` format + (bu 3'ünde) seeded template mevcut; `EventCode` canonical/unique/immutable.
  - Geçersiz permission (varsa) / eksik template → Draft + issue.

## 5. Event tanımları (PlatformSeed — ACCEPTED scope)
Kabul edilen 3 event (hepsi PlatformSeed, policy-gated). Ortak alanlar: `SourceType = PlatformSeed`, `OwnerModuleId = MOD-0009`, `OwnerArea = PlatformAdmin`, `OwnerDisplayName = Tenant / Environment Management`, `TargetRoute = /Platform/Tenants`, `TargetPageCode = null`, `ModuleCatalogRef = null`, `RequiredPolicy = PlatformActor`, `RequiredPermissionKey = null` (gerçek enforce edilen literal yoksa — §4.4), `Channel = Email`, `CanTenantOverride = true`, `UsageType = SystemEvent`. Runtime eventCode→dispatch adapter **DAHİL DEĞİL → FU04B**.

### 5.1 `tenant.user.invited` — Ready-to-implement (PlatformSeed)
| Alan | Değer |
|---|---|
| SourceType / OwnerArea | PlatformSeed / PlatformAdmin |
| OwnerModuleId / OwnerDisplayName | MOD-0009 / Tenant / Environment Management |
| Channel · DefaultTemplateKey | Email · `tenant.invite.email` ✅ (seeded) |
| RequiredVariables | **TenantDisplayName** |
| OptionalVariables | RecipientName, Email, LoginUrl, **TemporaryPassword (secret — §12 masking)** |
| TargetRoute · TargetPageCode | `/Platform/Tenants` · **null** |
| RequiredPolicy · RequiredPermissionKey | **PlatformActor** · **null** (policy-gated; §4.4 — `platform.tenants.read` uydurulmaz) |
| CanTenantOverride · UsageType · Severity · LinkPolicy | true · SystemEvent · Info · CustomUrl (LoginUrl varsa) / None |
| Producer flow · Template | **VAR** (`AdminUserInvitationService` → `QueueEmailNotificationCommand("tenant.invite.email")`) · **VAR** |
| Risk | Değişken hizası: producer `RecipientName/Email/TemporaryPassword/LoginUrl` sağlıyor; required yalnızca TenantDisplayName tutmak güvenli. |

### 5.2 `tenant.lifecycle.suspended` — Ready-to-implement (PlatformSeed)
Email · `tenant.suspended.email` ✅ · RequiredVariables **TenantDisplayName, Reason, SuspendedAtUtc** · Severity **Warning** · LinkPolicy None · TargetRoute `/Platform/Tenants` · RequiredPolicy **PlatformActor** · RequiredPermissionKey **null** · Producer `TenantLifecycleNotificationConsumer/TenantSuspendedV1` **VAR** · Template **VAR**.

### 5.3 `tenant.lifecycle.reactivated` — Ready-to-implement (PlatformSeed)
Email · `tenant.reactivated.email` ✅ · RequiredVariables **TenantDisplayName, ReactivatedAtUtc** · Severity **Success** · LinkPolicy None · TargetRoute `/Platform/Tenants` · RequiredPolicy **PlatformActor** · RequiredPermissionKey **null** · Producer `TenantReactivatedV1` **VAR** · Template **VAR**.

## 6. Repo Scope (öneri — implementation'da)
- `execution/domains/.../MOD-0027-FU04A-...md`
- **FU03-side additive (bu pack'in scope'u):**
  - `NotificationEventDefinition` entity additive alanları (nullable/default → geriye dönük uyumlu; mevcut event'ler `Manifest`):
    `SourceType` (enum, `Manifest=0` default), `OwnerArea`, `OwnerDisplayName`, `TargetRoute`, `ModuleCatalogRef` (nullable), `RequiredPolicy`/`AccessPolicy`.
  - Yeni `NotificationEventSourceType` enum (`Manifest=0`, `PlatformSeed=1`, `SystemSeed=2`).
  - Yeni **NotificationEventSeed.EnsureSeededAsync** (idempotent upsert; `NotificationTemplateSeed` kalıbı; startup) — 3 tenant event'i PlatformSeed olarak seed eder (§6.1).
  - PlatformSeed validation dalı: Module Catalog/ModulePages doğrulaması **atlanır**; `RequiredPermissionKey` varsa RBAC/permission doğrulanır, yoksa `RequiredPolicy=PlatformActor` policy-gated (§4.5).
  - Sync `create` yolu yeni event'lere `SourceType = Manifest` yazar; iki-yönlü clobber guardrail (§6.2).
  - DTO/mapping/view'a additive read-only `sourceType`/`ownerArea`/`targetRoute` alanları (§13-16).
- **Değiştirilmeyecek (Protected Paths):** IModuleManifestProvider'lar, ModuleManifestDocument, Module Catalog/ModulePages, PlatformNavigationCatalog, tenant producer/template/consumer, Gateway (ocelot).

### 6.1 Seed loader kararı (DECISION — RESOLVED: Seçenek A)
- **`NotificationEventSeed.EnsureSeededAsync(IMongoDatabase)`** — startup, `Diten.Platform.Infrastructure/DependencyInjection.cs` seed bloğunda **`NotificationTemplateSeed.EnsureSeededAsync`'in HEMEN ARDINDAN** çağrılır. Böylece referans template'ler (`tenant.invite.email` / `tenant.suspended.email` / `tenant.reactivated.email`) seed anında garanti mevcuttur.
- **Sync-from-manifest içine KARIŞTIRILMAZ.** `NotificationEventManifestSyncService` provider-driven kalır; platform-seed onun dışındadır (sahte provider/manifest üretmek yasak — reddedilen manifest-provider yaklaşımını geri getirir).
- **Idempotent upsert (§8):** yoksa create (`SourceType=PlatformSeed`); varsa **HARD alanları** (Channel, DefaultTemplateKey, Required/OptionalVariables, TargetRoute, RequiredPolicy/RequiredPermissionKey) reconcile eder; **SOFT operator alanlarını** (operator'ın değiştirdiği Status/Archived/Deprecated, display override'ları) korur.
- **Seçenek B (manuel admin seed-load)** tamamlayıcı olarak açık bırakılır (fresh DB'de tek yol olmamalı); **Seçenek C (sync içine dahil)** REDDEDİLDİ.
- **Katman notu:** RBAC permission doğrulaması Platform.**API** reflection'ına muhtaç; Infrastructure startup seed'i bunu yapamaz. Bu 3 event `RequiredPermissionKey=null` + `RequiredPolicy=PlatformActor` olduğundan seed anında permission doğrulaması gerekmez → doğrudan Active seed edilebilir. (İleride permission-gated PlatformSeed event gelirse, seed `Draft` yazıp API-tarafı activation pass'i `Active`'e terfi ettirmeli — follow-up.)

### 6.2 Clobber guardrail'leri (BAĞLAYICI)
İki popülasyon aynı koleksiyonda `EventCode` ile ayrışır; defence-in-depth:
- **Manifest sync**, `SourceType == PlatformSeed` olan bir kaydı **update ETMEZ** → skip + controlled issue.
- **Platform seed**, `SourceType == Manifest` olan bir kaydı **update ETMEZ** → skip + controlled issue.
- Cross-source **EventCode collision** → controlled issue (kayıt clobber edilmez; hangi kaynağın sahibi olduğu korunur).

## 7. KARAR 2 — Runtime integration (BAĞLAYICI)
- **FU04A runtime eventCode adapter İÇERMEZ.** Mevcut tenant producer'ları templateKey ile çalışmaya **devam eder** (`defaultTemplateKey` = kullanılan key → davranış aynı).
- **"EventCode üzerinden dispatch hazır" iddiası YAPILMAZ.**
- `eventCode → active event → defaultTemplateKey → QueueEmailNotificationCommand` adapter → **ayrı FU04B follow-up**.

## 8. Runtime Constraints
- Event kayıtları platform/global (FU03). Tenant event oluşturamaz.
- PlatformSeed idempotent: aynı EventCode ikinci seed → **update** (immutable EventCode; HARD reconcile / SOFT operator korunur).
- Secret (TemporaryPassword) dispatch preview'da FU02/FU03 masking ile redakte; RequiredVariables'a required konmaz.
- Module Catalog/ModulePages/permission catalog ownership **değişmez**; permission yalnızca **okunur/doğrulanır** (yeni permission yazılmaz).

## 9. Acceptance Criteria
- [ ] **Module Catalog'a yeni `tenant-management` modülü EKLENMEZ** (kanıt: catalog'da yok).
- [ ] **IModuleManifestProvider EKLENMEZ**; ModulePages'e `TENANTS` yazılmaz; PlatformNavigationCatalog değişmez.
- [ ] `NotificationEventDefinition` PlatformSeed kaynak modeli (SourceType `Manifest=0` default + OwnerArea/OwnerDisplayName/TargetRoute/ModuleCatalogRef/RequiredPolicy) additive eklendi; mevcut Manifest event'leri + FU03 sync **regresyonsuz**; `SourceType`'sız eski doküman `Manifest`'e çözülür.
- [ ] 3 tenant event PlatformSeed olarak seed edilir; EventCode canonical/lowercase dotted; DefaultTemplateKey **seeded** key ile uyumlu; RequiredVariables **producer+template** ile uyumlu; `RequiredPermissionKey=null` + `RequiredPolicy=PlatformActor` (policy-gated).
- [ ] `/Platform/NotificationEvents` 3 tenant event'i gösterir; policy-gated PlatformSeed **Active/valid** (permission verilmiş ama RBAC'ta yoksa → Draft+issue).
- [ ] `active-template-slots` bu 3 event'i döner.
- [ ] **Clobber guardrail:** sync `SourceType=PlatformSeed` kaydı update etmez; seed `SourceType=Manifest` kaydı update etmez (skip+issue).
- [ ] **Existing tenant mail flow bozulmaz** (producer templateKey ile çalışır).
- [ ] Existing FU02/FU03 testleri **regresyonsuz**; `dotnet build Platform.API` 0 hata.
- [ ] **No fake event · No Module Catalog side-effect.**
- [ ] Runtime eventCode dispatch DAHİL DEĞİL → açık follow-up (FU04B); "tam gönderim hazır" iddiası yok.

## 10. Test Expectations
- `dotnet build Platform.API` (fleet kilidi → temp-output); 0 hata.
- **Backward-compat / enum:**
  - `SourceType` alanı olmayan eski doküman deserialize → `SourceType == Manifest`.
  - `NotificationEventSourceType.Manifest == 0` (enum ordinal assert).
- **Seed:**
  - PlatformSeed seed 3 event yaratır (`tenant.user.invited`, `tenant.lifecycle.suspended`, `tenant.lifecycle.reactivated`); EventCode canonical/lowercase dotted; DefaultTemplateKey seeded key ile uyumlu.
  - Seed idempotent: ikinci seed duplicate yaratmaz, update eder.
- **Clobber guardrail:**
  - PlatformSeed seed, `SourceType=Manifest` event'i clobber etmez (skip + issue).
  - Manifest sync, `SourceType=PlatformSeed` event'i clobber etmez (skip + issue).
- **Validation:**
  - PlatformSeed, Module Catalog / ModulePages doğrulaması **çağırmaz** (mock/spy ile doğrulanır).
  - `RequiredPermissionKey = null` + `RequiredPolicy = PlatformActor` → **accepted** (policy-gated; event Active olabilir).
  - `RequiredPermissionKey` **varsa** ve RBAC'ta yoksa → Draft + issue.
- **Slot:** `active-template-slots` 3 tenant event'i döner (Active olduklarında).
- **FU03 regresyon:** mevcut Manifest event'leri (SourceType default Manifest) + sync regresyonsuz; `null→empty` coalesce korunur; FU02/FU03 (1139) PASS.
- (Öneri) sync `create` yolunun `SourceType = Manifest` yazdığı assert edilir.

## 11. Validation Rules
- **PlatformSeed event:**
  - `EventCode` canonical/lowercase dotted / unique / immutable.
  - `DefaultTemplateKey` format geçerli + seeded template mevcut.
  - **Module Catalog doğrulaması YOK; ModulePages doğrulaması YOK; `TargetPageCode = null`.**
  - `TargetRoute` serbest fixed route olarak taşınır (doğrulanmaz).
  - `RequiredPermissionKey` **varsa** → gerçek permission/RBAC literalinde var mı doğrulanır (`HasPermissionReflector` + `PermissionAliasResolver` dual-read); yoksa → `RequiredPolicy = PlatformActor` ile **policy-gated kabul edilir** (§4.4).
  - Geçersiz permission (varsa) / eksik template → Draft + issue.
- **SourceType:** enum `Manifest=0` default; mevcut dokümanlar migration'sız `Manifest`.
- Manifest event (FU03) davranışı **değişmez**.

## 12. Failure Paths to Verify
- **RequiredPermissionKey verilmiş ama RBAC'ta yoksa** → seed event **Draft + issue** (fail-controlled; Active olmaz). (`RequiredPermissionKey = null` + `RequiredPolicy=PlatformActor` ise policy-gated, geçerli.)
- **Template key bulunamazsa** → controlled issue.
- **PlatformSeed event duplicate** → update/idempotent (yeni kayıt yok).
- **Manifest sync bir `SourceType=PlatformSeed` kaydına denk gelirse** → skip + controlled issue (clobber yok).
- **Platform seed bir `SourceType=Manifest` kaydına denk gelirse** → skip + controlled issue (clobber yok).
- **Cross-source EventCode collision** → controlled issue (sahip kaynak korunur).
- **Module Catalog'a yanlışlıkla `tenant-management` modülü yazılırsa → FAIL** (bu pack'in ihlali).
- **TenantManagementManifestProvider / IModuleManifestProvider eklenirse → FAIL.**
- **TemporaryPassword/secret** UI/detail/dispatch preview'da **sızmaz** (masking doğrulanır).
- Producer action rollback → notification gönderilmez (mevcut davranış; değişmez).

## 13-16. (Layout/Frontend/Gateway)
- **shell: none — yeni sayfa YOK.** Mevcut `/Platform/NotificationEvents` (FU03) UI **bozulmaz**; PlatformSeed event'leri kaynak-agnostik listeye/slot'a otomatik girer.
- **Additive read-only UI (opsiyonel):** DTO (`NotificationEventDefinitionDto`) + `NotificationEventMappings` + view'a read-only `sourceType` / `ownerArea` / `targetRoute` alanları eklenebilir. Bunlar eklenmezse UI çalışmaya devam eder (yeni kolonları göstermez).
- **PlatformNavigationCatalog değişmez.**
- **Gateway (ocelot) değişikliği YOK** — yeni endpoint yok; mevcut FU03 notification-events route'ları yeterli.

## 17. Ready-for-dev Checklist / Open Blockers / TBD
### Çözülen kararlar (RESOLVED)
- [x] **Permission/policy kararı (RESOLVED — 2026-07-08):** PlatformSeed fixed-page event'leri `RequiredPolicy = PlatformActor` kullanır; `RequiredPermissionKey` opsiyonel/null'dır, ANCAK gerçek enforce edilen bir literal varsa o kullanılır. `platform.tenants.read` **uydurulmaz** (Platform.API'de enforce edilmiyor — `Admin/TenantsController` `[Authorize(Policy="PlatformActor")]`). Validation: permission varsa RBAC'tan doğrulanır, yoksa policy-gated kabul edilir (§4.4).
- [x] **DECISION — Seed tetikleme (RESOLVED):** startup `NotificationEventSeed.EnsureSeededAsync` (idempotent upsert), `NotificationTemplateSeed` sonrası; sync-from-manifest'e karıştırılmaz (§6.1).
- [x] **SourceType modeli (RESOLVED):** `NotificationEventSourceType { Manifest=0, PlatformSeed=1, SystemSeed=2 }`; `Manifest=0` backward-compat için zorunlu (§4.1). **Bu foundation'ın SAHİBİ artık `MOD-0027-FU03A` bridge'idir** (FU04A içinde değil).
- [x] **BLOCKER (governance) — FU04A registry reservation + DCP-002 preflight (RESOLVED — 2026-07-08):** Registry satırı EKLENDİ (`MOD-0027-FU04A | Follow-up | reserved | parent MOD-0027`); preflight PASS (exit 0).
- [x] **BLOCKER (scope onayı) — FU03 owner sign-off (RESOLVED via FU03A bridge path — 2026-07-08):** architecture-ba-reviewer sign-off VERİLDİ; FU03-side additive foundation (entity/enum/validation/sync-guard/seed-iskelet) **`MOD-0027-FU03A` bridge pack'ine** taşındı. FU04A artık FU03 kontratına dokunmaz. **Kalan bağımlılık: FU03A implementation + merge + FU03 regression gate** (aşağıda).

### Dependency (RESOLVED)
- [x] **FU03A bridge implement + merge + regression gate (RESOLVED — 2026-07-08):** `MOD-0027-FU03A` (SourceType/PlatformSeed foundation) **implement edildi + closed out (PASS-with-note)**; tests 1148/0, fleet live-boot-clean smoke, seed no-op. Bkz. [FU03A smoke audit](../../../../docs/audits/pss-mod-0027-fu03a-notification-event-sourcetype-platformseed-bridge-smoke-2026-07-08.md). Foundation (enum/entity/validation/sync-guard/seed-iskelet) hazır; FU04A artık yalnızca tenant seed içeriği ekler.

### Durum (CLOSED)
- **İmplement + closed out 2026-07-08 (PASS-with-note).** 3 tenant event PlatformSeed olarak eklendi; **canlı Active seed** (Mongo doğrulandı, tam 3, duplicate yok); tests 1153/0. Bkz. [FU04A smoke audit](../../../../docs/audits/pss-mod-0027-fu04a-tenant-management-notification-event-opt-in-smoke-2026-07-08.md). Note: authenticated API JSON list/slots fetch canlı yapılmadı (Mongo persist + unit test telafi). **Status: `completed`.**
- Açık blocker: **YOK.**

### İmplementasyon scope (BAĞLAYICI — sadece tenant seed içeriği)
FU04A **YALNIZCA** `NotificationEventSeedCatalog.PlatformSeedDefinitions` listesine 3 tenant event `NotificationEventSeedDefinition`'ı ekler (foundation FU03A'dan hazır). Her 3 event için ortak alanlar:
| Alan | Değer |
|---|---|
| `SourceType` | `PlatformSeed` |
| `OwnerModuleId` | `MOD-0009` |
| `OwnerArea` | `PlatformAdmin` |
| `OwnerDisplayName` | `Tenant / Environment Management` |
| `TargetRoute` | `/Platform/Tenants` |
| `RequiredPolicy` | `PlatformActor` |
| `RequiredPermissionKey` | **null** (policy-gated; `platform.tenants.read` uydurulmaz) |
| `Channel` | `Email` · `CanTenantOverride` true · `UsageType` SystemEvent |

Event → mevcut template (FU02 seeded) eşlemesi:
| EventCode | DefaultTemplateKey | Severity · RequiredVariables |
|---|---|---|
| `tenant.user.invited` | `tenant.invite.email` | Info · TenantDisplayName |
| `tenant.lifecycle.suspended` | `tenant.suspended.email` | Warning · TenantDisplayName, Reason, SuspendedAtUtc |
| `tenant.lifecycle.reactivated` | `tenant.reactivated.email` | Success · TenantDisplayName, ReactivatedAtUtc |

**FU04A DOKUNMAZ:** entity/enum/validation/sync-guard/seed-iskelet (FU03A) · runtime eventCode dispatch / `QueueEmailNotificationCommand` resolver (FU04B) · TenantManagementManifestProvider · IModuleManifestProvider · Module Catalog / ModulePages / PlatformNavigationCatalog / Gateway · Workflow/document event opt-in.

### TBD (blocker değil, implementasyonda netleşir)
- [ ] `tenant.user.invited` RequiredVariables'ın producer (`AdminUserInvitationService`) ile birebir hizası.
- [ ] Enforce edilen gerçek bir `platform.tenants.*` literal seçilirse (opsiyonel), null yerine o taşınır.

### Follow-up (FU04A sonrası — hepsi AÇIK)
- [ ] **FU04B:** Runtime eventCode → active event → `defaultTemplateKey` → `QueueEmailNotificationCommand` dispatch adapter.
- [ ] **Tenant producer migration:** tenant producer'ları hard-coded templateKey'den eventCode dispatch'e taşı (FU04B'ye bağlı).
- [ ] **Manifest-driven Workflow/document/import event opt-in** (her producer'ın kendi pack'inde `notificationEvents` deklarasyonu; inventory §B2).
- [ ] **FU04D — producer runtime wiring:** FU04B sonrası uçtan uca eventCode-driven gönderim.
- [ ] **Authenticated API/UI smoke:** PlatformActor token ile list/slots JSON + görsel `/Platform/NotificationEvents` teyidi (closeout kuyruğu).

### FU04A daralan scope (bridge sonrası — BAĞLAYICI)
FU03A foundation'ı sağladığından, **FU04A YALNIZCA** şunları implement eder:
- 3 tenant event'in **PlatformSeed seed içeriği** (`tenant.user.invited`, `tenant.lifecycle.suspended`, `tenant.lifecycle.reactivated`).
- Tenant-specific **template hizası** + `RequiredVariables` producer alignment.
- `NotificationEventSeed.EnsureSeededAsync` **çağrısının** tenant içerikle startup wiring'i + acceptance/smoke.
FU04A **foundation'a (entity/enum/validation/sync-guard/seed-iskelet) DOKUNMAZ** — bunlar FU03A'dan gelir.

### Kalan adımlar
1. ~~FU03A bridge: implement + closeout~~ **DONE (PASS-with-note, 2026-07-08).**
2. FU04A implement: 3 tenant event seed definition + FU03 regresyon + smoke → closeout.

- [x] Status **`ready-for-dev`** (2026-07-08) — FU03A dependency RESOLVED; implementasyon başlatılabilir.

## 18. Output Contract
Implementation report: status; changed files (yalnızca FU03-side additive alanlar + seed + validation dalı — **manifest provider YOK, Module Catalog YOK**); DCP preflight + registry; SourceType/PlatformSeed desteği kanıtı; 3 event seed + Active + slot kanıtı; **Module Catalog'a tenant-management yazılmadığı kanıtı** (catalog'da yok); IModuleManifestProvider eklenmediği kanıtı; RBAC permission doğrulama kanıtı; secret masking kanıtı; FU02/FU03 regresyon kanıtı; protected paths ihlali yok; runtime-integration'ın FU04B'ye bırakıldığı; next step.

---

## Özet karar tablosu
| Karar | Sonuç |
|---|---|
| Manifest provider (A/B/B2) | **REJECTED** (ürün mimarisi: fixed-page Module Catalog'a girmez) |
| Kabul edilen yaklaşım | **PlatformSeed/SystemSeed NotificationEventDefinition** (Module Catalog'suz katalog görünürlüğü) |
| SourceType enum | **ACCEPTED** `Manifest=0` / `PlatformSeed=1` / `SystemSeed=2` (Manifest=0 backward-compat zorunlu) |
| FU03-side dokunuş | Additive: SourceType/OwnerArea/OwnerDisplayName/TargetRoute/ModuleCatalogRef/RequiredPolicy + seed loader + PlatformSeed validation dalı + clobber guardrail |
| Permission/policy | **RESOLVED** — `RequiredPolicy=PlatformActor`, `RequiredPermissionKey=null` (gerçek enforce literal yoksa; `platform.tenants.read` uydurulmaz) |
| Seed loader | **RESOLVED** — startup `NotificationEventSeed.EnsureSeededAsync` (idempotent), `NotificationTemplateSeed` sonrası; sync'e karıştırılmaz |
| Runtime eventCode dispatch | **FU04B** (ayrı); FU04A "tam gönderim hazır" demez |
| Event'ler | 3'ü Ready-to-implement (PlatformSeed; policy-gated) |
| Workflow/document | Kapsam DIŞI |
| Açık blocker'lar | (1) FU04A registry/DCP-002 preflight, (2) FU03 owner sign-off veya bridge |
| Status | **draft** (2 blocker açık; permission/policy + seed + SourceType RESOLVED) |
