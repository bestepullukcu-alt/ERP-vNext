# WP-0023-FX — Workflow güvenlik / doğruluk düzeltmesi — kanıt (2026-10-07)

> **Durum:** ajan sonucu, CT kabulü bekliyor (K13: ajan PASS ≠ CT ACCEPTED).
> **Dal:** `feature/sce/mod-0023-fx` (taban `ab1a901d3`), yalıtılmış worktree `C:\Users\Ali\erp-vnext-wt-0023fx`.
> **Prompt:** CT-MVP1-0023-FX v1. **Sahip kararı (2026-10-07):** MOD-0023'ü biz düzeltiyoruz (MOD-0175 CT-Q2).
> **Kapsam:** üç kusur — (1) dört göz (SoD) kimlik uyuşmazlığı, (2) doğrudan API'de aktör taklidi, (3) çalışma
> (runtime) anahtarlarının kapsamı. Web, gateway, CRM kodu, sözleşmeler dokunulmadı.

## 1. Kusurlar — önce kırmızı / sonra yeşil

Kırmızı koşu düzeltme öncesi kodla yapıldı (testler yazıldı, ürün kodu değişmeden koşuldu). Aktör testi o anda
2 parametreli controller kurucusuyla ve neden kodunu metin olarak bekleyerek derlendi; düzeltmeden sonra yalnız kurucu
çağrısı (3. parametre `ICurrentUserContext`) ve sabit adı güncellendi.

| # | Kusur | Kırmızı test (düzeltme öncesi sonuç) | Düzeltme | Sonra |
|---|---|---|---|---|
| 1 | Başlatan, kendi akışını onaylayabiliyor (tenant kullanıcısı) | `WorkflowCrossServiceTests.Fx_a_tenant_user_who_started_the_workflow_cannot_approve_it` → **Expected 409, Actual 200**; `Fx_an_instance_started_before_the_fix_still_blocks_its_starter_through_the_start_log` → 200; `Fx_assignment_and_SoD_compare_user_ids_as_ids_not_as_text_casing` → başarısız | `WorkflowInstance.StartedByUserId` (`services/Diten.Platform/src/Diten.Platform.Domain/Entities/Workflow/WorkflowInstance.cs:39`), başlatırken JWT `sub` yazılır (`…/Handlers/CommandHandlers/StartWorkflowInstanceHandler.cs:166`); SoD `IsStarterAsync` ile kimlik karşılaştırır (`…/WorkflowTaskTransitionSupport.cs:118,338-355`); kimlik eşitliği `WorkflowPrincipal.Same` (`…/Features/Workflow/WorkflowPrincipal.cs`) | 4/4 yeşil |
| 2 | Doğrudan API'de gövdedeki `ActorId` ile başkası adına işlem | `WorkflowActorBindingTests.A_body_actor_that_is_not_the_caller_is_refused_with_403_and_nothing_is_sent` (5 aksiyon) → **controller kurbanın kimliğiyle komut gönderdi** (Moq strict: `DelegateWorkflowTaskCommand { ActorId = c2… }`); `An_empty_body_actor_acts_as_the_caller` ve normalleştirme testleri → başarısız | `WorkflowActorBinding.Bind` (`…/Features/Workflow/WorkflowActorBinding.cs:26`), beş uçta da çağrılır (`services/Diten.Platform/src/Diten.Platform.API/Controllers/WorkflowDefinitionsController.cs:204,222,240,258,276`); yeni neden kodu `WORKFLOW_ACTOR_MISMATCH` (`…/WorkflowModels.cs:49`); `ActorId` istek kayıtlarında opsiyonel (`string?`) | 16/16 yeşil |
| 3 | Çalışma anahtarları PlatformAdmin; QA rolüne verilemiyor | `TenantRouteScopeCorrectionTests.Fx_the_workflow_runtime_keys_are_planned_PlatformAdmin_to_Tenant_and_no_design_key_is` → **Actual: []**; `TenantRouteScopeCorrectionMongoTests.Fx_a_tenant_role_can_be_granted_the_workflow_runtime_keys_but_not_the_design_keys` → **403 "This permission cannot be assigned to a tenant role."** | Workflow manifesti çalışma anahtarlarını `/Platform` sayfalarından çıkarıp `ApiPermissions` (Tenant) olarak bildirir (`…/SelfRegistration/WorkflowManifestProvider.cs:156-169`); AuthService izin listesi 7 anahtarla genişledi, yeni tür `TenantApiPermission` (`services/Diten.AuthService/src/Diten.AuthService.Domain/Authorization/TenantRouteScopeCorrections.cs:57,88-94,148`); seed literalleri Tenant (`…/Persistence/Seed/DataSeeder.cs:813-819`); seed kapsam uzlaştırması izin listesindeki anahtarı kendisi indirmez (`DataSeeder.cs:900`); her düzeltilen satır `authAuditLogs`'a `permission_scope_corrected` (`DataSeeder.cs:203,999`) | 2/2 + 2 yeni çapraz kontrol yeşil |

## 2. Tasarım kararları ve gerekçeler

- **SoD — backfill yerine başlatma günlüğü.** Eski örneklerde starter kimliği zaten `workflow_transition_logs` Start
  satırında (sıra 1) duruyor: `ActorId = _currentUserContext.UserId` (`StartWorkflowInstanceHandler.cs:216`, eski
  `:214`). `StartedByUserId` boşsa SoD bu satırı okur — veri göçü yok, eski açık görevler korunur, idempotent. `StartedBy`
  (e-posta / ad) görüntü alanı olarak kalır ve yine kontrol edilir (adı/e-postası olmayan başlatanlar kimlikle
  yazılıyordu).
- **Kimlik karşılaştırması.** İki değer GUID ise GUID olarak, değilse büyük/küçük harf duyarsız. Bağlanan aktör her zaman
  küçük harf "D" GUID olduğundan, şablonda `user:5F00…` gibi büyük harfle yazılmış atamalar da artık doğru eşleşir
  (önceki ordinal karşılaştırma bunları reddederdi). Aynı kural temsil (delegate), bilgi isteme ve kendine temsil
  kontrolünde de uygulanır (`WorkflowTaskTransitionSupport.cs:372,405,489`).
- **Aktör bağlama — API sınırında.** Ölçülen tüm in-process çağıranlar komutu sunucu tarafında çözülmüş kimlikle kurar:
  WorkCenter (`WorkflowApprovalWorkItemActionDispatcher.cs:67`, JWT kullanıcısı), `TaskApprovalService.cs:206-214` ve
  `TaskReviewService.cs:236-244` (`_currentUser.UserId`). Eskalasyon çalıştırması isteğinde aktör taşımaz (sistem).
  Tarayıcının ya da başka servisin aktörü belirleyebildiği tek yer HTTP gövdesiydi; bağlama oraya kondu. Kabul edilen
  gövde değerleri: boş (= ben), çağıranın kimliği (herhangi bir GUID yazımı), çağıranın kendi e-postası. Diğer her şey
  komut gönderilmeden 403. Sonuçta komuta ve günlüğe her zaman çağıranın kullanıcı kimliği gider.
- **Kapsam geçişi — "PlatformAdmin kazanır" gevşetilmedi.** Katalog eşitlemesindeki kural
  (`services/Diten.AuthService/src/Diten.AuthService.Api/Controllers/InternalPermissionsController.cs:146-152`)
  dokunulmadı. Mevcut, kapalı ve izin listeli BL-411 düzeltmesi (`TenantRouteScopeCorrections`) genişletildi: listeye
  yalnız 7 çalışma anahtarı eklendi; Gate 3 bu tür için "route yok + modül adı var" ister. Testler listeyi BL-411'in 2
  anahtarı + bu 7 anahtarla sabitler ve üretimdeki Workflow manifestinden her birinin Tenant API izni olduğunu, hiçbir
  Platform manifestinde sayfa/aksiyon olarak geçmediğini kanıtlar. Bir tasarım anahtarı listeye eklenirse kırmızı
  (sabotaj c).
- **Seed literalleri.** Seed kapsam uzlaştırması seed değerini iki yönde de dayattığı için
  (`DataSeeder.cs` `ReconcilePermissionScopesAsync`), literaller PlatformAdmin kalsaydı her açılışta kapsam geri
  yükselirdi. Literaller Tenant yapıldı; uzlaştırma izin listesindeki anahtarı Tenant'a **kendisi** indirmez, bu hareketi
  yalnız denetimli düzeltme yapar. Yeni veritabanında satırlar doğrudan Tenant oluşur (indirme yok, denetim yok).
- **Varsayılan yetkiler değişmedi (ölçüldü).** `ModulePermissionResolver` workflow modülünde kapsama bakmadan Admin'e
  tüm workflow anahtarlarını, Viewer'a okuma aksiyonlarını verir (`ModulePermissionResolver.cs:46-47,82-88`) — değişmedi.
  `DefaultRolePermissionTemplate`: Admin genişliği `AdminModules = {access-governance, legal-entity, crm-account,
  crm-contact}` (`DefaultRolePermissionTemplate.cs:36`), `workflow` yok; Viewer yalnız `read` aksiyonu — çalışma
  anahtarlarının aksiyonları `start/view/approve/…`. Yani seed rollerine yeni yetki gelmez; değişen tek şey tenant
  yöneticisinin bu anahtarları bir role **atayabilmesi**.

## 3. İzin listesi (anahtar · eski kapsam · yeni kapsam)

| Anahtar | Eski | Yeni | Kayıt |
|---|---|---|---|
| `platform.workflow.instances.start` | PlatformAdmin | **Tenant** | Workflow `ApiPermissions` |
| `platform.workflow.instances.view` | PlatformAdmin | **Tenant** | Workflow `ApiPermissions` |
| `platform.workflow.tasks.approve` | PlatformAdmin | **Tenant** | Workflow `ApiPermissions` |
| `platform.workflow.tasks.reject` | PlatformAdmin | **Tenant** | Workflow `ApiPermissions` |
| `platform.workflow.tasks.delegate` | PlatformAdmin | **Tenant** | Workflow `ApiPermissions` |
| `platform.workflow.tasks.request-info` | PlatformAdmin | **Tenant** | Workflow `ApiPermissions` |
| `platform.workflow.tasks.cancel` | PlatformAdmin | **Tenant** | Workflow `ApiPermissions` |
| `platform.workflow.definitions.view / manage / publish` | PlatformAdmin | PlatformAdmin | `/Platform/Workflow` sayfaları |
| `platform.workflow.escalations.manage / run`, `transitions.evaluate` | PlatformAdmin | PlatformAdmin | `/Platform/Workflow` sayfaları |

Manifest değişikliği: Definitions sayfasındaki `START_INSTANCE` aksiyonu ve Tasks sayfasındaki 5 görev aksiyonu
kaldırıldı; Instances ve Tasks (tasarımcı altındaki yönetim sayfaları) `definitions.view` ile korunur. Web düğmeleri
manifestten değil `ViewData` bayraklarından yönetildiği için (`frontend/Diten.Web/Controllers/WorkflowController.cs:248-257`)
ekranlar değişmedi. `permission-scope-baseline.csv` içinde 7 satır `1 → 0` (bilinçli değişiklik).

## 4. Test sayıları (önce / sonra)

| Suite | Önce (taban `ab1a901d3`) | Sonra | Not |
|---|---|---|---|
| Platform (filtre: Workflow · ModuleRegistration · ModulePages · WorkAggregation) | 432/432 | **454/454** | +22 yeni (SoD 4, aktör 16 vaka, manifest 2); tam suite koşulmadı (BRD testleri 27017'de DB açıp siliyor — prompt kuralı) |
| AuthService (tamamı, test-owned mongod `DITEN_ITEST_MONGOD_BIN_DIR`) | 1019/1019 | **1023/1023** | +4 yeni; ilk taban koşusunda mongod yolu verilmediği için 143 test "mongod bulunamadı" ile kırmızıydı, yol verilince 1019/1019 |
| CrmService | 2109 + 5 atlanan | **2109 + 5 atlanan** | CRM kodu değişmedi |
| tests/architecture | 18/18 | **18/18** | |
| Derleme uyarıları | Platform 87 | Platform 87 | yeni uyarı yok; AuthService çıktısındaki tek uyarı dokunulmamış `UserListQueryTests.cs:80` (artımlı tabanda görünmemişti) |

## 5. Sabotaj (K3)

| # | Sabotaj | Kırmızı olan | Geri alınca |
|---|---|---|---|
| a | SoD karşılaştırması eski haline (`instance.StartedBy` ile ordinal) | 3/4: `Fx_a_tenant_user_who_started…`, `Fx_an_instance_started_before_the_fix…`, `Fx_assignment_and_SoD_compare…` | dosya birebir aynı (`cmp`), yeşil |
| b | Aktör bağlama kontrolü kaldırıldı (gövde aktörü kabul) | 9/16: 5 aksiyonun hepsinde taklit testi + 4 normalleştirme vakası | birebir aynı, yeşil |
| c | İzin listesine `platform.workflow.definitions.publish` eklendi | 3: planlayıcı testi, "tam liste" testi, üretim manifesti çapraz kontrolü | birebir aynı, yeşil |

Sabotaj koşuları bin klasörlerini sabotajlı kodla derlediği için runtime probe'dan önce iki API temiz kodla yeniden
derlendi (0 hata, 0 uyarı).

## 6. Runtime probe (kendi portları, izole veritabanı) — 34/34

- **Ortam:** probe kendi `mongod`'unu açtı (EphemeralMongo, tek düğümlü replica set, rastgele port; 27017 değil),
  AuthService `127.0.0.1:15056`, Platform `127.0.0.1:15057` (`--urls`), veritabanları `fx_probe_auth`,
  `fx_probe_platform`, `fx_probe_jobs`. JWT ve iç API anahtarları rastgele üretildi (depodaki dev secret kullanılmadı;
  token'ları yalnız AuthService imzaladı). Sahibin filosuna gidebilecek her adres (MDM, uzak iş kalemi sağlayıcısı, Seq)
  ölü porta, olay taşıyıcısı InMemory'ye çevrildi. Sonunda iki süreç ve probe `mongod`'u kapatıldı; geride yalnız
  sahibin `mongod` servisi kaldı.
- **Fikstür:** izole veritabanında üç seed kullanıcısına probe'un ürettiği test parolasının BCrypt özeti yazıldı;
  tenant yöneticisi olarak bir seed kullanıcısına (charlie) tenant'ın seed'li SuperAdmin rol satırı klonlandı (seed'li
  `admin@diten.com` satırını Platform'un yönetici senkronu açılışta yeniden yazıyor — ölçüldü). Yetki ve rol işlemleri
  gerçek API'lerle yapıldı.
- **Sonuç:**

| Kontrol | Sonuç |
|---|---|
| 7 çalışma anahtarı katalogda `Scope=0 (Tenant)`; 6 tasarım anahtarı `Scope=1 (PlatformAdmin)` | 13 PASS |
| Tenant yöneticisi QA rolü oluşturur; 7 çalışma anahtarını atar | 201, 7 × 204 |
| `definitions.publish` / `definitions.manage` QA rolüne | 403 "This permission cannot be assigned to a tenant role." |
| QA rolü A ve B'ye | 204 × 2 |
| Yönetici şablon oluşturur / yayınlar | 201 / 200 |
| A (QA rolü) akış başlatır, aday A; **A kendi akışını onaylar** | **409 `SOD_VIOLATION`** |
| A akış başlatır, aday B; **A, gövdede B'nin kimliğiyle onaylar** | **403 `WORKFLOW_ACTOR_MISMATCH`** |
| B kendi görevini onaylar | 200, örnek `Completed` |
| Örnek kaydında `StartedBy` = A'nın e-postası, `StartedByUserId` = A'nın kimliği | PASS |
| Probe 27017'yi hiç kullanmadı | PASS |

Probe kaynağı: `docs/records/audits/2026-10/mod-0023-fx-scripts-2026-10-07.md`.

## 7. CRM etkisi

- CRM akış başlatır ve iptal eder; onay vermez (`WP-CL-BE-3-workflow-cross-service.md`).
- İptal gövdesinde `actorId` = CRM `IActorContext.ActorName`, bu da önce `sub` claim'idir
  (`services/Diten.CrmService/src/Diten.CrmService.Infrastructure/HttpActorContext.cs`) → oturumdaki kullanıcının
  kimliği; bağlama ile uyumlu. CRM kodu değişmedi, CRM testleri aynı (2109 + 5 atlanan).
- CRM'in yeni başlattığı örnekler `StartedByUserId` taşır; SoD artık CRM claim onaylarında da çalışır (kusur 1 CRM'i de
  etkiliyordu).

## 8. AuthService çakışma riski (WP-0022-E1 ile paralel)

Bu WP AuthService'te yalnız `Domain/Authorization/TenantRouteScopeCorrections.cs`, `Persistence/Seed/DataSeeder.cs`
(workflow seed satırları, kapsam uzlaştırmasında tek koşul, düzeltmeye denetim parametresi) ve iki test dosyasına
dokundu. E1'in alanı `Features/ESignReauth/**`, `TenantAuthController.cs`, yeni varlık/depo dosyaları. Ortak dosya
olasılığı: `DataSeeder.cs` (E1 yeni koleksiyon indeksleri için seed'e dokunursa). Birleştirmede satır çakışması
beklenmez; farklı bölgeler.

## 9. Bilinen sınırlar ve takip

- **FU-0023-FX-WEB (Web, bu WP'de korunan yol):** `/Platform/Workflow` altındaki Tasks ve Definitions ekranlarında
  "Actor Id" serbest metin alanı var ve boş bırakılamıyor (`frontend/Diten.Web/Views/Platform/Workflow/Tasks.cshtml:46-47`,
  `Index.cshtml:328`; `wwwroot/assets/js/Platform/Workflow/tasks.js:405-413`, `index.js:1028-1036`). Düzeltmeden
  sonra kullanıcı yalnız kendi kimliğini ya da e-postasını yazabilir, başkasınınki 403 olur. Önerilen: alanı kaldırıp
  boş gönder (sunucu oturumdaki kullanıcıyı kullanır). Asıl onay yüzeyi WorkCenter ve o etkilenmedi.
- **FU-0023-FX-SOD-SWITCH:** "başlatan onaylayamaz" kuralı şablon bazında kapatılamaz (MOD-0175 CT-Q13 için bilgi).
- **FU-0023-FX-PLATFORM-ADMIN-SYNC (gözlem):** Platform açılışta seed'li `admin@diten.com` satırını yeniden yazıyor
  (probe'da ölçüldü); geliştirme fikstürü kurarken dikkat.
- Katalog eşitlemesinin anahtarın `Module` alanını sahip olmayan manifeste taşıması konusu (FU-0174-CAT-02) açık;
  bu WP onu çözmez, çalışma anahtarları zaten yalnız sahibi Workflow manifestinde bildiriliyor.
