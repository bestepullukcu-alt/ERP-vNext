---
id: tenant-subscription-management
name: Tenant Subscription Management
domain: platform-shared-services
status: review
owner: platform-team
branch: feature/pss/tenant-subscription-management
started: 2026-05-11
target: 2026-05-25
---

# Platform Tenant Subscription Foundation

## 1. module-brief.md

### Module Summary
Bu modül, Tenant yaşam döngüsünde abonelik yönetimi için gerekli olan temel altyapıyı (foundation) sağlar. Sistemde tenant'ların sadece bir `PlanId` alanıyla statik olarak plana bağlanması yerine, `TenantSubscription` varlığı üzerinden aktif abonelik dönemlerini, deneme sürümlerini (trial), yenileme ve iptal süreçlerini yönetmeyi sağlar. ERP uygulamaları devreye girmeden önce, tenant'ın yetkilerinin (entitlement), kotalarının (quota) ve sisteme erişim durumunun kontrol edilebilmesi için gerçek ve dinamik bir abonelik kaydı oluşturulması zorunludur.

### Business Context
Tenant'ların kayıt anında bir SubscriptionPlan'a bağlanması tek başına yetersizdir. Sistemin aşağıdaki soruları anlık olarak yanıtlaması gerekir:
- Tenant'ın aktif bir deneme (trial) süreci var mı ve ne zaman bitiyor?
- Geçerli fatura veya kullanım dönemi (billing period) tarihleri nedir?
- Abonelik şu an aktif mi, iptal mi edildi, süresi mi doldu, yoksa askıya mı alındı?
Bu modül sayesinde her tenant dinamik bir yaşam döngüsüne sahip `TenantSubscription` kaydına kavuşur ve tenant detail ekranında (Commercial sekmesi altında) aktif statüsü yönetilebilir hale gelir.

### System of Record & Boundaries
- **Tenant Entity:** Tenant Management yetkisindedir.
- **SubscriptionPlan:** Sistemdeki kullanılabilir paket/katalog tanımıdır.
- **TenantSubscription:** Tenant'ın canlı abonelik instansıdır ve bu modülün System of Record (SoR) nesnesidir. Lifecycle (durum geçişleri) tamamen bu modül tarafından kontrol edilir.
- **ERP Integration:** İleride eklenecek ERP uygulamaları, abonelik durumunu değiştiremez. Yalnızca mevcut aktif abonelik statüsünü, kotaları ve yetkileri read-only olarak okuyup erişim kararı (entitlement enforcement) verecektir.

---

## 2. execution-pack.md

### Data Model (Entity Fields)

**Entity:** `TenantSubscription` (Base: `EntityBase`, tenant-owned)
- `Id` (Guid, PK)
- `TenantId` (Guid, FK -> Tenant)
- `PlanId` (Guid, FK -> SubscriptionPlan)
- `Status` (TenantSubscriptionStatus enum)
- `TrialStartDateUtc` (DateTime?)
- `TrialEndDateUtc` (DateTime?)
- `CurrentPeriodStartUtc` (DateTime?)
- `CurrentPeriodEndUtc` (DateTime?)
- `ActivatedAtUtc` (DateTime?)
- `RenewedAtUtc` (DateTime?)
- `CancelledAtUtc` (DateTime?)
- `ExpiredAtUtc` (DateTime?)
- `SuspendedAtUtc` (DateTime?)
- `CancelAtPeriodEnd` (bool, default: false)
- `CancellationReason` (string?)
- `Source` (string?)
- `RowVersion` (byte[] - Concurrency için)

**Enum:** `TenantSubscriptionStatus`
- `PendingProvisioning`
- `Trialing`
- `Active`
- `PastDue`
- `Cancelled`
- `Expired`
- `Suspended`

### Lifecycle State Transitions

Aşağıdaki tablo, `TenantSubscription` üzerindeki yasal durum geçişlerini (state machine) tanımlar:

| Current Status | Action | Next Status | Required Fields | Rule |
|---|---|---|---|---|
| None | Assign Plan / Start Trial | Trialing | PlanId, TrialStartDateUtc, TrialEndDateUtc | Tenant için aktif/trialing subscription yoksa |
| None | Assign Plan / Activate | Active | PlanId, CurrentPeriodStartUtc, CurrentPeriodEndUtc | Trial kullanılmadan direkt aktif başlatma |
| PendingProvisioning | Provision Completed | Trialing / Active | PlanId, period fields | Seçilen plan/trial kararına göre |
| Trialing | Activate | Active | CurrentPeriodStartUtc, CurrentPeriodEndUtc | Trial bittiğinde veya manuel aktivasyonda |
| Trialing | Cancel | Cancelled | CancellationReason | Trial iptal edilirse |
| Trialing | Expire | Expired | ExpiredAtUtc | TrialEndDateUtc geçmişse |
| Active | Renew | Active | CurrentPeriodStartUtc, CurrentPeriodEndUtc, RenewedAtUtc | Yeni dönem başlatır |
| Active | Cancel At Period End | Active | CancelAtPeriodEnd=true, CancellationReason | Period bitene kadar erişim devam eder |
| Active | Cancel Now | Cancelled | CancelledAtUtc, CancellationReason | Erişim hemen kapanır |
| Active | Expire | Expired | ExpiredAtUtc | CurrentPeriodEndUtc geçmişse ve renewal yoksa |
| Active | Suspend | Suspended | SuspendedAtUtc, reason | Geçici erişim kapatma |
| Suspended | Reactivate | Active | ActivatedAtUtc veya ReactivatedAtUtc | Önceki plan/period geçerliyse |
| Cancelled | Reactivate | Active | Explicit reactivation reason | Sadece business rule izin verirse |
| Expired | Reactivate / New Subscription | Active / Trialing | New period fields | Eski kayıt direkt güncellenmeyecekse yeni kayıt açma kuralı belirt |

**Illegal Transitions (Örnekler):**
- Cancelled -> Trialing (Blocked)
- Expired -> Trialing (Blocked unless new subscription is created)
- Suspended -> Trialing (Blocked)
- Active -> Trialing (Blocked)
- Duplicate Active/Trialing (Blocked)

### Command Contracts

- `CreateTenantSubscriptionCommand`
  - *Purpose:* Tenant registration sonrası veya manuel olarak ilk abonelik kaydını oluşturma.
  - *Required Fields:* `TenantId`, `PlanId`, opsiyonel trial alanları.
  - *Status Transition:* `None -> PendingProvisioning / Trialing / Active`.
  - *Validation:* Aynı anda var olan aktif abonelik kontrolü.

- `AssignPlanToTenantCommand`
  - *Purpose:* UI üzerinden tenant'a bir plan atayarak trial veya active statüsünde abonelik başlatma.
  - *Required Fields:* `TenantId`, `PlanId`, `IsTrial`.
  - *Status Transition:* `None -> Trialing / Active`.
  - *Validation:* Plan validasyonu ve duplicate active kontrolü.

- `StartTrialSubscriptionCommand`
  - *Purpose:* Seçili plan için açıkça deneme sürümü başlatma.
  - *Required Fields:* `TenantId`, `PlanId`, `TrialEndDateUtc`.
  - *Status Transition:* `None / PendingProvisioning -> Trialing`.
  - *Validation:* `TrialEndDateUtc > Now`.

- `ActivateTenantSubscriptionCommand`
  - *Purpose:* Trialing aboneliği aktif döneme geçirme.
  - *Required Fields:* `SubscriptionId`, `CurrentPeriodStartUtc`, `CurrentPeriodEndUtc`.
  - *Status Transition:* `Trialing -> Active`.
  - *Validation:* Mevcut statünün `Trialing` (veya uygun) olması.

- `RenewTenantSubscriptionCommand`
  - *Purpose:* Active aboneliği yeni döneme uzatma.
  - *Required Fields:* `SubscriptionId`, `NewPeriodEndUtc`.
  - *Status Transition:* `Active -> Active`.
  - *Validation:* Geçerli statünün `Active` olması ve `CancelAtPeriodEnd` bayrağına göre uygunluğu.

- `CancelTenantSubscriptionCommand`
  - *Purpose:* Aboneliği anında iptal etme veya dönem sonunda iptal olarak işaretleme.
  - *Required Fields:* `SubscriptionId`, `CancellationReason`, `CancelAtPeriodEnd`.
  - *Status Transition:* `Active / Trialing -> Cancelled` veya `Active -> Active` (`CancelAtPeriodEnd = true`).
  - *Validation:* Geçerli statünün uygun olması; `CancellationReason` zorunluluğu.

- `ExpireTenantSubscriptionCommand`
  - *Purpose:* Süresi dolmuş aboneliği kapatma (genellikle background job üzerinden çağrılır).
  - *Required Fields:* `SubscriptionId`.
  - *Status Transition:* `Active / Trialing -> Expired`.
  - *Validation:* Sürenin geçmiş olması.

- `SuspendTenantSubscriptionCommand`
  - *Purpose:* İhlal veya ödeme sorunu nedeniyle geçici erişim kısıtlaması.
  - *Required Fields:* `SubscriptionId`, `Reason`.
  - *Status Transition:* `Active -> Suspended`.
  - *Validation:* Geçerli statünün `Active` olması; neden (reason) zorunluluğu.

- `ReactivateTenantSubscriptionCommand`
  - *Purpose:* Suspended bir aboneliği tekrar aktive etme.
  - *Required Fields:* `SubscriptionId`.
  - *Status Transition:* `Suspended -> Active`.
  - *Validation:* Geçerli statünün `Suspended` olması.

### Query Contracts

- `GetTenantCommercialSubscriptionQuery` / `GetTenantActiveSubscriptionQuery`
  - *Purpose:* UI'ın Commercial tabında göstermek üzere tenant'ın tekil geçerli aboneliğini döner.
  - *Response Shape:* DTO (Plan detayları, Status, Tarihler).

- `GetTenantSubscriptionHistoryQuery`
  - *Purpose:* History tablosu için tenant'ın tüm abonelik geçmişi hareketlerini listeler.
  - *Response Shape:* Paginated List DTO (Tarih, Action, Reason, Actor).

- `GetTenantSubscriptionDetailQuery`
  - *Purpose:* Tek bir aboneliğin tüm ID ve tarihçesini detaylı okur.
  - *Response Shape:* Detailed DTO.

- `HasTenantActiveSubscriptionQuery`
  - *Purpose:* ERP uygulamaları veya middleware interceptor'lar tarafından "erişim var mı?" kontrolü için hızlı okuma.
  - *Response Shape:* Boolean (veya mini DTO: `IsActive`, `PlanId`).

- `GetTenantSubscriptionEntitlementSnapshotQuery`
  - *Purpose:* ERP uygulamasının kotaları ve yetkileri kontrol edebilmesi için snapshot döner.
  - *Response Shape:* Entitlement Dictionary/DTO.

### Permission Keys

Önerilen permission key'ler:

- `platform.tenants.commercial.subscription.view`
  - Plan / Subscription tabını ve read-only subscription bilgisini görme.
- `platform.tenants.commercial.subscription.assign`
  - Tenant’a plan atama veya trial başlatma.
- `platform.tenants.commercial.subscription.activate`
  - Trialing/PendingProvisioning subscription’ı active yapma.
- `platform.tenants.commercial.subscription.renew`
  - Active subscription için yeni period başlatma.
- `platform.tenants.commercial.subscription.cancel`
  - Active/Trialing subscription iptal etme.
- `platform.tenants.commercial.subscription.suspend`
  - Active subscription askıya alma.
- `platform.tenants.commercial.subscription.expire`
  - Expire işlemini manuel/system-admin seviyesinde çalıştırma.
- `platform.tenants.commercial.subscription.reactivate`
  - Suspended subscription’ı yeniden active yapma.
- `platform.tenants.commercial.subscription.history.view`
  - Subscription history tablosunu görme.

*Kurallar:*
- View permission yoksa tab tamamen gizlenebilir veya permission denied state gösterilebilir.
- Manage/lifecycle permission yoksa action butonları enabled görünmemeli.
- Doğrudan API çağrısında backend yine 403 dönmeli.
- UI visibility hiçbir zaman backend authorization yerine geçmemeli.

### API Surface (Commercial Focus)

UI'ın tüketeceği ana endpoint'ler Commercial root bazlı olacak şekilde tanımlanmıştır:

- `GET /api/platform/tenants/{tenantId}/commercial/subscription`
- `GET /api/platform/tenants/{tenantId}/commercial/subscription/history`
- `POST /api/platform/tenants/{tenantId}/commercial/subscription`
- `POST /api/platform/tenants/{tenantId}/commercial/subscription/{subscriptionId}/activate`
- `POST /api/platform/tenants/{tenantId}/commercial/subscription/{subscriptionId}/cancel`
- `POST /api/platform/tenants/{tenantId}/commercial/subscription/{subscriptionId}/renew`
- `POST /api/platform/tenants/{tenantId}/commercial/subscription/{subscriptionId}/expire`
- `POST /api/platform/tenants/{tenantId}/commercial/subscription/{subscriptionId}/suspend`
- `POST /api/platform/tenants/{tenantId}/commercial/subscription/{subscriptionId}/reactivate`

### UI Presentation & Action Behaviors

Kapsam, "Tenant Details" sayfası (`/Platform/Tenants/Details/{tenantId}`) altındaki **Commercial** sekmesine eklenecek bir **Plan / Subscription** alt sekmesi olarak tasarlanmıştır.

**Empty State:**
- Tenant için active/trialing subscription yoksa "No active subscription found" empty state gösterilir.
- Kullanıcı yetkisi varsa "Assign Plan" veya "Start Trial" aksiyonu gösterilir.
- Yetki yoksa read-only empty state olarak bırakılır.

**Plan Assignment:**
- Plan seçimi dropdown/lookup üzerinden yapılır.
- Plan seçilmeden `Submit` butonu disabled olur.
- Seçilen plan trial destekliyorsa "Start Trial" seçeneği aktifleşir. Desteklemiyorsa direkt "Activate" seçeneği görünür.
- İşlem sonrası UI, local state ile güncellenmez; `GET commercial subscription` endpoint'i yeniden çağrılarak refresh edilir.

**Actions:**
- **Activate:** Sadece Trialing veya PendingProvisioning için enabled.
- **Renew:** Sadece Active için enabled.
- **Cancel:** Active veya Trialing için enabled. `CancellationReason` zorunlu.
- **Expire:** Sadece yetkili admin veya sistem job'ları için enabled (normal UI'da read-only veya disabled).
- **Suspend:** Sadece Active için enabled. Reason (sebep) zorunlu.
- **Reactivate:** Suspended için enabled; Cancelled/Expired için business rule açık değilse disabled.
- **Not:** Backend contract eksikse hiçbir aksiyon butonu enabled görünmez.

**History:**
- History tablosu salt-okunur (read-only) olmalıdır.
- Sütunlar: `Status`, `Plan`, `Period Start`, `Period End`, `Changed At`, `Changed By`, `Reason`.

### Audit Events & Payloads

Aşağıdaki event'ler sistem üzerinden tetiklenir:
- `tenant_subscription.plan_assigned`
- `tenant_subscription.trial_started`
- `tenant_subscription.activated`
- `tenant_subscription.renewed`
- `tenant_subscription.cancelled`
- `tenant_subscription.expired`
- `tenant_subscription.suspended`
- `tenant_subscription.reactivation_requested` (veya `tenant_subscription.reactivated`)

**Audit Payload İçeriği:**
- `actorId`
- `tenantId`
- `subscriptionId`
- `oldStatus`
- `newStatus`
- `planId`
- `oldPlanId`
- `newPlanId`
- `reason`
- `correlationId`
- `timestampUtc`
- `source`
- `currentPeriodStartUtc`
- `currentPeriodEndUtc`

---

## 3. acceptance-criteria.md

### Runtime Success Criteria
- [ ] Platform Admin `/Platform/Tenants/Details/{tenantId}` sayfasını açabilir.
- [ ] Commercial tab altında `Plan / Subscription` alt tabı görünür.
- [ ] Alt tab açıldığında active/trialing subscription backend API üzerinden okunur.
- [ ] Tenant için active/trialing subscription yoksa controlled empty state görünür.
- [ ] Yetkili kullanıcı plan seçip Assign Plan / Start Trial / Activate akışını çalıştırabilir.
- [ ] Başarılı işlemden sonra UI local state yerine backend GET endpoint’ini yeniden çağırarak refresh olur.
- [ ] Sayfa reload edildiğinde Current Plan, Subscription Status, Trial Start/End ve Current Period Start/End bilgileri DB’den aynı şekilde tekrar görünür.
- [ ] Subscription history read-only table doğru kayıtları gösterir.

### Integrity Success Criteria
- [ ] TenantSubscription kaydı tenant'a ve seçilen plana doğru şekilde bağlanır.
- [ ] Aynı tenant için tek active/trialing subscription kuralı korunur.
- [ ] Trialing, Active, Suspended, Cancelled ve Expired statüleri lifecycle state machine’e göre yönetilir.
- [ ] Current period ve trial period alanları tutarlı saklanır.
- [ ] RowVersion concurrency kontrolü update akışlarında kullanılır.
- [ ] Tenant.PlanId operasyonel kaynak olmaktan çıkarılır veya sadece backward-compatible read-only legacy alan olarak tutulur.

### UX Success Criteria
- [ ] Loading, empty, error ve permission denied state’leri kullanıcıya net görünür.
- [ ] Status badge’leri anlaşılırdır: Trialing, Active, PastDue, Cancelled, Expired, Suspended.
- [ ] Action butonları sadece geçerli statüde ve yetkili kullanıcı için enabled olur.
- [ ] Cancel/Suspend gibi reason isteyen aksiyonlarda reason alanı zorunlu gösterilir.
- [ ] History table read-only çalışır.
- [ ] Raw exception, stack trace veya internal token UI’da gösterilmez.

### Runtime Failure Criteria
- [ ] Plan sistemde bulunamazsa (veya silinmişse), create/assign işlemi `controlled validation error` döner.
- [ ] Backend API sunucu hatası (500) verirse, UI `error state` gösterir; hiçbir durumda fake success ibaresi oluşmaz.
- [ ] Kullanıcının action yetkisi yoksa, aksiyon butonları gizlenir veya disabled görünür. Doğrudan API çağrısı denenirse `403 Controlled State` (Forbidden) döner.
- [ ] Tenant'ın aboneliği yoksa `empty state` görünür; eksik veri nedeniyle `null reference` patlaması veya bozuk UI render edilmez.

### Integrity Failure Criteria
- [ ] Aynı tenant için halihazırda var olan `Active` veya `Trialing` aboneliğin üzerine ikinci bir Active/Trialing abonelik eklenmesi veritabanı/domain seviyesinde engellenir.
- [ ] Tabloda belirtilen yasadışı geçiş denemeleri (Illegal transition: Cancelled -> Trialing vb.) `canonical validation error` fırlatarak işlemi bloke eder.
- [ ] `RowVersion` uyuşmazlığı durumlarında `controlled concurrency error` döner. Stale data veritabanını bozamaz.
- [ ] `Cancel` ve `Suspend` işlemleri, bir neden (reason) belirtilmeden tamamlanamaz.
- [ ] `Expire` işlemi standart UI kullanıcısı tarafından manuel olarak tetiklenemez (yetki engeli).
- [ ] İşlem başarısız olduğunda tenant veya subscription kayıtları yarım/tutarsız (corrupt) state'te bırakılamaz (Transactional integrity).

### UX Failure Criteria
- [ ] Backend validasyon mesajları (örneğin "Invalid date range"), raw exception veya stack trace göstermeden, doğrudan form alanlarında veya kontrollü toast mesajları olarak kullanıcıya yansır.
- [ ] Başarılı işlemlerden sonra lokal state manipüle edilmez; ekran her halükarda backend'den tekrar okuyarak kendini eşitler.
- [ ] Sayfa (veya tarayıcı sekmesi) yenilendiğinde hiçbir veri veya state kaybolmaz.
- [ ] Backend contract desteği olmayan veya geçersiz state'deki aksiyonlar `disabled` görünür ve üzerine gelindiğinde bir tooltip ile neden yapılamadığı anlatılır.

---

## 4. repo-scope.md

### Repo Scope (Commercial Context)
- `services/Diten.Platform/src/Diten.Platform.Domain/Tenants/Entities/TenantSubscription.cs`
- `services/Diten.Platform/src/Diten.Platform.Domain/Tenants/Enums/TenantSubscriptionStatus.cs`
- `services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Commercial/Subscriptions/**` (Command/Query/Handlers/Validators)
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Configurations/TenantSubscriptionConfiguration.cs`
- `services/Diten.Platform/src/Diten.Platform.API/Controllers/TenantCommercialSubscriptionsController.cs`
- `gateway/Diten.ApiGateway/ocelot.json` (Commercial subscription route eklemeleri)
- `frontend/Diten.Web/Areas/Platform/Views/Tenants/Details.cshtml` (Commercial tab altındaki sub-tab entegrasyonu)
- `frontend/Diten.Web/Areas/Platform/Views/Tenants/Commercial/_PlanSubscriptionTab.cshtml` (Yeni sub-tab partial view)
- `frontend/Diten.Web/Areas/Platform/Models/Tenants/TenantCommercialSubscriptionViewModel.cs` (Data binding modeli)
- `frontend/Diten.Web/Areas/Platform/Controllers/TenantsController.cs` (Get commercial subscription UI route'ları)

### Protected Paths
- `.antigravity/**`
- `frontend/Diten.Web/Controllers/Archive/**`
- `frontend/Diten.Web/Views/Archive/**`
- `services/Diten.MdmService/**`
- `services/Diten.EnterpriseStrategyService/**`

---

## 5. test-notes.md

### Test Expectations

- **UI Smoke Test:** Tenant Details sayfasına gidilir -> Commercial sekmesi açılır -> Plan / Subscription alt sekmesi açılır -> `Empty state` gözlemlenir -> Assign plan işlemi yapılır -> Sayfa yenilenir (Reload) -> Status'ün `Active` veya `Trialing` olarak kaldığı (persistence) doğrulanır.
- **UI Failure Smoke Test:** Backend kasti olarak 500 dönmeye zorlanır -> UI'ın çökmediği ve proper `error state` gösterdiği doğrulanır.
- **UI Permission Smoke Test:** Action yetkisi olmayan bir kullanıcı hesabı ile giriş yapılır -> Tüm aksiyon butonlarının disabled/hidden olduğu doğrulanır.
- **API Failure Tests:**
  - Mevcut olmayan (silinmiş/hatalı) bir plan atanmaya çalışılır (`plan not found`).
  - Active aboneliği varken yeni abonelik yaratılmaya çalışılır (`duplicate active blocked`).
  - `Cancelled` statüsündeki abonelik `Trialing` yapılmaya çalışılır (`illegal transition blocked`).
  - Stale `RowVersion` payload'u ile güncelleme atılır (`stale row version blocked`).
- **Audit Tests:** `plan_assigned`, `activated`, `cancelled`, `suspended` işlemleri tetiklenir ve üretilen event payload'larının (actorId, correlationId, newStatus vb.) eksiksiz loglandığı doğrulanır.
- **Transaction Test:** Create veya Update işlemi esnasında kasti bir hata fırlatılarak (örneğin event publish esnasında) veritabanında yarım/corrupt (partial) state kalmadığı (rollback) teyit edilir.

---

## 6. notes.md

### Implementation Notes
- Veritabanında `TenantId` ve `Status` alanlarını içeren Partial/Filtered Index kurularak "Aynı anda sadece bir Active veya Trialing kayıt bulunabilir" kuralı veritabanı seviyesinde katılaştırılacaktır.
- Mevcut `Tenant.PlanId` alanının işlevi değiştirilecek veya geçiş süresi boyunca senkron tutulacak bir geri uyumluluk (backward-compatibility) stratejisi oluşturulacaktır.

### Implementation Batches

Proje geliştirme sürecinde riskleri minimize etmek ve uçtan uca çalışabilir parçalar teslim etmek amacıyla şu adımlar (batches) izlenecektir:

- **Batch 01: Backend data foundation + active commercial subscription query**
  (Entity, Enum, Configuration, Migration ve `GET /api/.../commercial/subscription` yapısının kurulması.)
- **Batch 02: Commercial > Plan / Subscription read-only tab flow**
  (Frontend `_PlanSubscriptionTab.cshtml` arayüzünün, ViewModel'lerin, loading/empty statülerin bağlanması ve GET endpoint entegrasyonu.)
- **Batch 03: Assign Plan / Start Trial / Activate lifecycle actions**
  (Tenant'a yeni plan atama ve aboneliği aktif/trial başlatma komutlarının yazılması, API'a bağlanması ve UI'da işlevsel hale getirilmesi.)
- **Batch 04: Cancel / Renew / Suspend / Expire / Reactivate lifecycle actions**
  (Diğer tüm yaşam döngüsü komutlarının kodlanması, sebep (reason) validasyonları ve UI butonlarının state'e göre bağlanması.)
- **Batch 05: Audit, validation, concurrency, transaction, and test hardening**
  (Eksik kalan Audit event'lerin fırlatılması, Concurrency validasyonlarının test edilmesi, transaction bütünlüğü kontrolleri ve tüm Acceptance Criteria maddelerinin otomatik testleri.)

### Final Readiness Decision
Bu pack kodlamaya verilebilir durumdadır; ancak geliştirme batch'leri sırayla uygulanmalıdır. İlk batch sadece backend data foundation ve read-only active commercial subscription query içermelidir. UI actionları backend contract ve validation tamamlanmadan enabled yapılmamalıdır.
